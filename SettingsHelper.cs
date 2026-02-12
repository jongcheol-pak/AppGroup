using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace AppGroup
{
    public class SettingsHelper
    {
        private const string STARTUP_TASK_ID = "AppGroupStartupTask";
        private static readonly string SettingsPath = Path.Combine(
            AppPaths.AppDataFolder,
            "settings.json"
        );

        /// <summary>
        /// 앱이 패키지 컨텍스트에서 실행 중인지 확인
        /// </summary>
        private static bool IsPackagedApp()
        {
            try
            {
                // Package.Current에 접근 가능하면 패키지된 앱
                _ = Package.Current.Id;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public class AppSettings
        {
            public bool ShowSystemTrayIcon { get; set; } = true;
            public bool RunAtStartup { get; set; } = true;
            public bool UseGrayscaleIcon { get; set; } = false;

            // 언어 설정 (빈 문자열이면 OS 기본 언어 사용)
            public string Language { get; set; } = "";

            // 테마 설정 (빈 문자열이면 시스템 기본값, "Dark", "Light")
            public string Theme { get; set; } = "";

            // 시작 메뉴 설정
            public string TrayClickAction { get; set; } = "FolderList";
            public bool ShowFolderPath { get; set; } = true;
            public bool ShowFolderIcon { get; set; } = true;
            public bool ShowStartMenuPopup { get; set; } = true;
            public int FolderColumnCount { get; set; } = 1;
            public int SubfolderDepth { get; set; } = 2;
        }

        private static AppSettings _currentSettings;

        public static async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string jsonContent = await File.ReadAllTextAsync(SettingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent) ?? new AppSettings();
                }
                else
                {
                    _currentSettings = new AppSettings();
                    await SaveSettingsAsync(_currentSettings);
                }

                // Apply startup setting if this is the first time loading
                await EnsureStartupSettingIsApplied();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                _currentSettings = new AppSettings();
                // Even if loading failed, try to apply default startup setting
                await EnsureStartupSettingIsApplied();
            }

            return _currentSettings;
        }

        /// <summary>
        /// 설정 파일의 시작 프로그램 설정과 실제 시스템 설정을 동기화
        /// </summary>
        private static async Task EnsureStartupSettingIsApplied()
        {
            try
            {
                // 패키지 컨텍스트 확인
                if (!IsPackagedApp())
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsHelper] Not running in packaged context, skipping startup sync");
                    return;
                }

                bool isEnabled = await IsInStartupAsync();

                if (_currentSettings.RunAtStartup && !isEnabled)
                {
                    // 설정에서는 시작 시 실행인데 실제로는 비활성화 - 활성화
                    await AddToStartupAsync();
                    System.Diagnostics.Debug.WriteLine("Applied default startup setting: Added to startup");
                }
                else if (!_currentSettings.RunAtStartup && isEnabled)
                {
                    // 설정에서는 시작 시 실행 안함인데 실제로는 활성화 - 비활성화
                    await RemoveFromStartupAsync();
                    System.Diagnostics.Debug.WriteLine("Applied startup setting: Removed from startup");
                }
                // 이미 일치하면 조치 불필요
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring startup setting is applied: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 프로그램에 등록 (비동기)
        /// </summary>
        public static async Task<StartupTaskState?> AddToStartupAsync()
        {
            try
            {
                // 패키지 컨텍스트 확인
                if (!IsPackagedApp())
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsHelper] Not running in packaged context, cannot add to startup");
                    return null;
                }

                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);

                switch (startupTask.State)
                {
                    case StartupTaskState.Disabled:
                        var result = await startupTask.RequestEnableAsync();
                        System.Diagnostics.Debug.WriteLine($"StartupTask enable result: {result}");
                        return result;

                    case StartupTaskState.DisabledByUser:
                        System.Diagnostics.Debug.WriteLine("StartupTask disabled by user - cannot enable from app");
                        return StartupTaskState.DisabledByUser;

                    case StartupTaskState.DisabledByPolicy:
                        System.Diagnostics.Debug.WriteLine("StartupTask disabled by policy");
                        return StartupTaskState.DisabledByPolicy;

                    case StartupTaskState.Enabled:
                        System.Diagnostics.Debug.WriteLine("StartupTask already enabled");
                        return StartupTaskState.Enabled;

                    default:
                        System.Diagnostics.Debug.WriteLine($"StartupTask unknown state: {startupTask.State}");
                        return startupTask.State;
                }
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"COMException adding to startup: 0x{comEx.HResult:X8} - {comEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding to startup: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 시작 프로그램에서 제거 (비동기)
        /// </summary>
        public static async Task RemoveFromStartupAsync()
        {
            try
            {
                // 패키지 컨텍스트 확인
                if (!IsPackagedApp())
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsHelper] Not running in packaged context, cannot remove from startup");
                    return;
                }

                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                startupTask.Disable();
                System.Diagnostics.Debug.WriteLine("StartupTask disabled");
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"COMException removing from startup: 0x{comEx.HResult:X8} - {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing from startup: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 프로그램 등록 여부 확인 (비동기)
        /// </summary>
        public static async Task<bool> IsInStartupAsync()
        {
            try
            {
                // 패키지 컨텍스트 확인
                if (!IsPackagedApp())
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsHelper] Not running in packaged context");
                    return false;
                }

                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                return startupTask.State == StartupTaskState.Enabled;
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"COMException checking startup status: 0x{comEx.HResult:X8}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking startup status: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 시작 프로그램 상태 조회 (비동기)
        /// </summary>
        public static async Task<StartupTaskState> GetStartupStateAsync()
        {
            try
            {
                // 패키지 컨텍스트 확인
                if (!IsPackagedApp())
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsHelper] Not running in packaged context, skipping StartupTask");
                    return StartupTaskState.Disabled;
                }

                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                System.Diagnostics.Debug.WriteLine($"[SettingsHelper] StartupTask state: {startupTask.State}");
                return startupTask.State;
            }
            catch (COMException comEx)
            {
                // 0x80073D54: 패키지가 없거나 StartupTask가 등록되지 않음
                System.Diagnostics.Debug.WriteLine($"COMException getting startup state: 0x{comEx.HResult:X8} - {comEx.Message}");
                return StartupTaskState.Disabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting startup state: {ex.Message}");
                return StartupTaskState.Disabled;
            }
        }

        #region 동기 메서드 (레거시 호환용)

        /// <summary>
        /// 시작 프로그램에 등록 (동기 - 레거시 호환용)
        /// </summary>
        public static void AddToStartup()
        {
            AddToStartupAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 시작 프로그램에서 제거 (동기 - 레거시 호환용)
        /// </summary>
        public static void RemoveFromStartup()
        {
            RemoveFromStartupAsync().GetAwaiter().GetResult();
        }

        #endregion

        public static async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonContent = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(SettingsPath, jsonContent);

                _currentSettings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static AppSettings GetCurrentSettings()
        {
            return _currentSettings ?? new AppSettings();
        }

        /// <summary>
        /// 저장된 언어 설정을 앱 시작 시 적용합니다.
        /// Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride를 설정합니다.
        /// </summary>
        public static void ApplyLanguageOverride()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string jsonContent = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.Language))
                    {
                        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = settings.Language;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"언어 설정 적용 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 저장된 테마 설정을 앱에 적용합니다.
        /// Application.Current.RequestedTheme을 설정합니다.
        /// </summary>
        public static void ApplyThemeOverride()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string jsonContent = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.Theme))
                    {
                        if (Enum.TryParse<Microsoft.UI.Xaml.ApplicationTheme>(settings.Theme, out var theme))
                        {
                            Microsoft.UI.Xaml.Application.Current.RequestedTheme = theme;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"테마 설정 적용 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 저장된 테마 설정 문자열을 반환합니다.
        /// 캐시된 설정이 있으면 파일 I/O 없이 반환합니다.
        /// </summary>
        public static string GetSavedTheme()
        {
            // 캐시된 설정이 있으면 파일 I/O 없이 반환
            if (_currentSettings != null)
            {
                return _currentSettings.Theme ?? "";
            }

            try
            {
                if (File.Exists(SettingsPath))
                {
                    string jsonContent = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
                    return settings?.Theme ?? "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"테마 설정 읽기 오류: {ex.Message}");
            }
            return "";
        }
    }
}
