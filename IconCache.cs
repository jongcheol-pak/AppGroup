
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
namespace AppGroup
{

    public static class IconCache {
        // LRU 캐시를 위한 구조: (캐시 키, (아이콘 경로, 마지막 접근 시간))
        private static readonly ConcurrentDictionary<string, (string path, DateTime lastAccess)> _iconCache
            = new ConcurrentDictionary<string, (string, DateTime)>();
        private static readonly string CacheFilePath = GetCacheFilePath();
        private static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

        // 캐시 크기 제한 (최대 500개 항목)
        private const int MAX_CACHE_SIZE = 500;
        // 캐시 정리 시 제거할 항목 비율 (20%)
        private const double CLEANUP_RATIO = 0.2;

        // 직접 캐시 접근을 위한 레거시 호환성
        public static ConcurrentDictionary<string, string> IconCacheData =>
            new ConcurrentDictionary<string, string>(_iconCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.path));

        static IconCache() {
            LoadIconCache();
        }

        private static string GetCacheFilePath() {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appGroupFolder = Path.Combine(folder, "AppGroup");
            Directory.CreateDirectory(appGroupFolder); 
            return Path.Combine(appGroupFolder, "icon_cache.json");
        }

        private static void LoadIconCache() {
            try {
                if (File.Exists(CacheFilePath)) {
                    string json = File.ReadAllText(CacheFilePath);
                    var cacheData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                    if (cacheData != null) {
                        foreach (var kvp in cacheData) {
                            if (!string.IsNullOrEmpty(kvp.Value) && File.Exists(kvp.Value)) {
                                // LRU: 로드 시점을 접근 시간으로 설정
                                _iconCache.TryAdd(kvp.Key, (kvp.Value, DateTime.Now));
                            }
                        }
                    }
                    Debug.WriteLine($"Cache loaded from {CacheFilePath}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to load cache: {ex.Message}");
            }
        }
       
        /// <summary>
        /// 이미지 파일을 BitmapImage로 로드합니다.
        /// DecodePixelWidth/Height를 설정하여 고품질 스케일링을 수행합니다.
        /// </summary>
        /// <param name="filePath">이미지 파일 경로</param>
        /// <param name="decodeSize">디코딩할 크기 (논리 픽셀). DPI 스케일링이 자동 적용됩니다.</param>
        public static async Task<BitmapImage> LoadImageFromPathAsync(string filePath, int decodeSize = 48) {
            BitmapImage bitmapImage = new BitmapImage();

            try {
                // DecodePixel 설정으로 고품질 스케일링 (SetSourceAsync 전에 설정해야 함)
                bitmapImage.DecodePixelWidth = decodeSize;
                bitmapImage.DecodePixelHeight = decodeSize;
                bitmapImage.DecodePixelType = DecodePixelType.Logical;  // DPI 자동 스케일링

                using var stream = File.OpenRead(filePath);
                using var randomAccessStream = stream.AsRandomAccessStream();
                await bitmapImage.SetSourceAsync(randomAccessStream);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to load image: {ex.Message}");
            }

            return bitmapImage;
        }
        public static async Task<string> GetIconPathAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return null;

            // Check for valid path - either file exists or it's a UWP app path
            bool isUwpApp = filePath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase);
            if (!isUwpApp && !File.Exists(filePath)) {
                Debug.WriteLine($"GetIconPathAsync: File does not exist: {filePath}");
                return null;
            }

            string cacheKey = ComputeFileCacheKey(filePath);

            // LRU: 캐시 히트 시 접근 시간 갱신
            if (_iconCache.TryGetValue(cacheKey, out var cachedEntry)) {
                // 캐시된 아이콘 파일이 실제로 존재하는지 확인
                if (File.Exists(cachedEntry.path)) {
                    // 접근 시간 갱신 (LRU)
                    _iconCache.TryUpdate(cacheKey, (cachedEntry.path, DateTime.Now), cachedEntry);
                    return cachedEntry.path;
                }
                // 캐시된 파일이 없으면 캐시에서 제거
                _iconCache.TryRemove(cacheKey, out _);
            }

            try {
                string outputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup",
                    "Icons"
                );
                Directory.CreateDirectory(outputDirectory);

                // Program Files 등 보호된 폴더의 경우 더 긴 타임아웃 사용
                bool isProtectedPath = filePath.Contains("Program Files", StringComparison.OrdinalIgnoreCase) ||
                                       filePath.Contains("Windows", StringComparison.OrdinalIgnoreCase);
                TimeSpan timeout = isProtectedPath ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(3);

                Debug.WriteLine($"GetIconPathAsync: Extracting icon for {filePath} (timeout: {timeout.TotalSeconds}s, size: 32x32)");

                var extractedIconPath = await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, timeout, size: 32);

                if (extractedIconPath != null && File.Exists(extractedIconPath)) {
                    // 캐시 크기 제한 확인 및 정리
                    if (_iconCache.Count >= MAX_CACHE_SIZE) {
                        await CleanupOldCacheEntriesAsync();
                    }
                    // LRU: 현재 시간과 함께 캐시에 추가
                    _iconCache.TryAdd(cacheKey, (extractedIconPath, DateTime.Now));
                    await SaveIconCacheAsync();
                    Debug.WriteLine($"GetIconPathAsync: Successfully extracted icon to {extractedIconPath}");
                    return extractedIconPath;
                }
                else {
                    Debug.WriteLine($"GetIconPathAsync: Failed to extract icon for {filePath}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Icon extraction failed for {filePath}: {ex.Message}");
            }

            return null;
        }
        public static void SaveIconCache() {
            // Fire and forget for backward compatibility
            _ = SaveIconCacheAsync();
        }

        public static async Task SaveIconCacheAsync() {
            if (!await _saveLock.WaitAsync(TimeSpan.FromSeconds(2))) {
                Debug.WriteLine("SaveIconCacheAsync: Skipped due to lock timeout (2초). Cache save may have failed or is taking too long.");
                return;
            }

            try {
                // LRU 구조에서 일반 Dictionary로 변환 (접근 시간 제외)
                var cacheSnapshot = new Dictionary<string, string>();
                foreach (var kvp in _iconCache) {
                    cacheSnapshot[kvp.Key] = kvp.Value.path;
                }

                string json = JsonSerializer.Serialize(cacheSnapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(CacheFilePath, json);
                Debug.WriteLine($"Cache saved to {CacheFilePath}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to save cache: {ex.Message}");
            }
            finally {
                _saveLock.Release();
            }
        }

        public static string ComputeFileCacheKey(string filePath) {
            if (string.IsNullOrEmpty(filePath)) {
                return string.Empty;
            }

            // shell:AppsFolder 경로 (UWP 앱)는 경로 자체를 키로 사용
            if (filePath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase)) {
                return filePath;
            }

            if (!File.Exists(filePath)) {
                return filePath;
            }

            var fileInfo = new FileInfo(filePath);
            return $"{filePath}_{fileInfo.LastWriteTimeUtc}_{fileInfo.Length}";
        }

        /// <summary>
        /// 캐시 크기 제한 초과 시 오래된 항목 정리 (LRU 전략)
        /// </summary>
        private static async Task CleanupOldCacheEntriesAsync() {
            try {
                int removeCount = (int)(_iconCache.Count * CLEANUP_RATIO);
                if (removeCount < 1) removeCount = 1;

                Debug.WriteLine($"IconCache: Cleaning up {removeCount} old entries (current size: {_iconCache.Count})");

                // 존재하지 않는 파일을 먼저 제거
                var keysToRemove = new List<string>();
                foreach (var kvp in _iconCache) {
                    if (!File.Exists(kvp.Value.path)) {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove) {
                    _iconCache.TryRemove(key, out _);
                }

                // 아직 더 제거해야 할 경우, LRU (가장 오래된 항목) 제거
                if (_iconCache.Count >= MAX_CACHE_SIZE) {
                    int remaining = removeCount - keysToRemove.Count;
                    if (remaining > 0) {
                        // LRU: 접근 시간 기준 정렬 후 가장 오래된 항목 제거
                        var oldestEntries = _iconCache
                            .OrderBy(kvp => kvp.Value.lastAccess)
                            .Take(remaining)
                            .ToList();

                        foreach (var entry in oldestEntries) {
                            _iconCache.TryRemove(entry.Key, out _);
                            Debug.WriteLine($"IconCache: Removed old entry (last access: {entry.Value.lastAccess})");
                        }
                    }
                }

                await SaveIconCacheAsync();
                Debug.WriteLine($"IconCache: Cleanup complete (new size: {_iconCache.Count})");
            }
            catch (Exception ex) {
                Debug.WriteLine($"IconCache cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// 존재하지 않는 아이콘 파일 참조를 캐시에서 제거
        /// </summary>
        public static async Task InvalidateMissingEntriesAsync() {
            try {
                var keysToRemove = new List<string>();
                foreach (var kvp in _iconCache) {
                    if (!File.Exists(kvp.Value.path)) {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                if (keysToRemove.Count > 0) {
                    foreach (var key in keysToRemove) {
                        _iconCache.TryRemove(key, out _);
                    }
                    await SaveIconCacheAsync();
                    Debug.WriteLine($"IconCache: Removed {keysToRemove.Count} invalid entries");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"IconCache invalidation error: {ex.Message}");
            }
        }

        /// <summary>
        /// 캐시 전체 초기화
        /// </summary>
        public static void ClearCache() {
            _iconCache.Clear();
            Debug.WriteLine("IconCache: Cache cleared");
        }

        /// <summary>
        /// 현재 캐시 크기 반환
        /// </summary>
        public static int Count => _iconCache.Count;
    }

}
