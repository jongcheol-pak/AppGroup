using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace AppGroup {
    public class SettingsHelper {
        private const string STARTUP_TASK_ID = "AppGroupStartupTask";
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppGroup",
            "settings.json"
        );

        public class AppSettings {
            public bool ShowSystemTrayIcon { get; set; } = true;
            public bool RunAtStartup { get; set; } = true;
            public bool UseGrayscaleIcon { get; set; } = false;
        }

        private static AppSettings _currentSettings;

        public static async Task<AppSettings> LoadSettingsAsync() {
            try {
                if (File.Exists(SettingsPath)) {
                    string jsonContent = await File.ReadAllTextAsync(SettingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent) ?? new AppSettings();
                }
                else {
                    _currentSettings = new AppSettings();
                    await SaveSettingsAsync(_currentSettings);
                }

                // Apply startup setting if this is the first time loading
                await EnsureStartupSettingIsApplied();
            }
            catch (Exception ex) {
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
        private static async Task EnsureStartupSettingIsApplied() {
            try {
                bool isEnabled = await IsInStartupAsync();

                if (_currentSettings.RunAtStartup && !isEnabled) {
                    // 설정에서는 시작 시 실행인데 실제로는 비활성화 - 활성화
                    await AddToStartupAsync();
                    System.Diagnostics.Debug.WriteLine("Applied default startup setting: Added to startup");
                }
                else if (!_currentSettings.RunAtStartup && isEnabled) {
                    // 설정에서는 시작 시 실행 안함인데 실제로는 활성화 - 비활성화
                    await RemoveFromStartupAsync();
                    System.Diagnostics.Debug.WriteLine("Applied startup setting: Removed from startup");
                }
                // 이미 일치하면 조치 불필요
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error ensuring startup setting is applied: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 프로그램에 등록 (비동기)
        /// </summary>
        public static async Task<StartupTaskState?> AddToStartupAsync() {
            try {
                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);

                switch (startupTask.State) {
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
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error adding to startup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 시작 프로그램에서 제거 (비동기)
        /// </summary>
        public static async Task RemoveFromStartupAsync() {
            try {
                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                startupTask.Disable();
                System.Diagnostics.Debug.WriteLine("StartupTask disabled");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error removing from startup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 시작 프로그램 등록 여부 확인 (비동기)
        /// </summary>
        public static async Task<bool> IsInStartupAsync() {
            try {
                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                return startupTask.State == StartupTaskState.Enabled;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error checking startup status: {ex.Message}");
            }
            return false;
        }

        #region 동기 메서드 (레거시 호환용)

        /// <summary>
        /// 시작 프로그램에 등록 (동기 - 레거시 호환용)
        /// </summary>
        public static void AddToStartup() {
            AddToStartupAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 시작 프로그램에서 제거 (동기 - 레거시 호환용)
        /// </summary>
        public static void RemoveFromStartup() {
            RemoveFromStartupAsync().GetAwaiter().GetResult();
        }

        #endregion

        public static async Task SaveSettingsAsync(AppSettings settings) {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));

                var options = new JsonSerializerOptions {
                    WriteIndented = true
                };

                string jsonContent = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(SettingsPath, jsonContent);

                _currentSettings = settings;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static AppSettings GetCurrentSettings() {
            return _currentSettings ?? new AppSettings();
        }
    }
}
