using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;

namespace AppGroup.ViewModels {
    public partial class SettingsDialogViewModel : ObservableObject {
        private SettingsHelper.AppSettings? _settings;
        private bool _isLoading = true;
        private bool _isCheckingForUpdates;
        private string _updateReleaseUrl = string.Empty;
        private string _versionText = "";
        private string _updateStatusText = "";
        private bool _isUpdateInfoBarOpen;
        private string _updateInfoBarMessage = "";

        private bool _showSystemTrayIcon = true;
        private bool _runAtStartup = true;
        private bool _useGrayscaleIcon;
        private bool _checkForUpdatesOnStartup = true;
        private string _startupStatusMessage = string.Empty;
        private bool _isStartupBlocked;

        public bool IsLoading {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsCheckingForUpdates {
            get => _isCheckingForUpdates;
            set => SetProperty(ref _isCheckingForUpdates, value);
        }

        public string VersionText {
            get => _versionText;
            set => SetProperty(ref _versionText, value);
        }

        public string UpdateStatusText {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value);
        }

        public bool IsUpdateInfoBarOpen {
            get => _isUpdateInfoBarOpen;
            set => SetProperty(ref _isUpdateInfoBarOpen, value);
        }

        public string UpdateInfoBarMessage {
            get => _updateInfoBarMessage;
            set => SetProperty(ref _updateInfoBarMessage, value);
        }

        public bool ShowSystemTrayIcon {
            get => _showSystemTrayIcon;
            set => SetProperty(ref _showSystemTrayIcon, value);
        }

        public bool RunAtStartup {
            get => _runAtStartup;
            set => SetProperty(ref _runAtStartup, value);
        }

        public bool UseGrayscaleIcon {
            get => _useGrayscaleIcon;
            set => SetProperty(ref _useGrayscaleIcon, value);
        }

        public bool CheckForUpdatesOnStartup {
            get => _checkForUpdatesOnStartup;
            set => SetProperty(ref _checkForUpdatesOnStartup, value);
        }

        /// <summary>
        /// 시작 프로그램 상태 메시지 (사용자/정책에 의해 차단된 경우 표시)
        /// </summary>
        public string StartupStatusMessage {
            get => _startupStatusMessage;
            set => SetProperty(ref _startupStatusMessage, value);
        }

        /// <summary>
        /// 시작 프로그램이 사용자/정책에 의해 차단되었는지 여부
        /// </summary>
        public bool IsStartupBlocked {
            get => _isStartupBlocked;
            set {
                if (SetProperty(ref _isStartupBlocked, value)) {
                    OnPropertyChanged(nameof(StartupStatusVisibility));
                }
            }
        }

        /// <summary>
        /// 시작 프로그램 상태 메시지 표시 여부 (Visibility 바인딩용)
        /// </summary>
        public Visibility StartupStatusVisibility =>
            IsStartupBlocked ? Visibility.Visible : Visibility.Collapsed;

        public void InitializeVersionText() {
            VersionText = $"버전 {UpdateChecker.GetCurrentVersion()}";
        }

        public async Task LoadCurrentSettingsAsync() {
            try {
                _settings = await SettingsHelper.LoadSettingsAsync();

                ShowSystemTrayIcon = _settings.ShowSystemTrayIcon;
                RunAtStartup = _settings.RunAtStartup;
                UseGrayscaleIcon = _settings.UseGrayscaleIcon;
                CheckForUpdatesOnStartup = _settings.CheckForUpdatesOnStartup;

                // 실제 시스템 시작 프로그램 상태와 동기화
                bool isEnabled = await SettingsHelper.IsInStartupAsync();
                if (RunAtStartup != isEnabled) {
                    RunAtStartup = isEnabled;
                    _settings.RunAtStartup = isEnabled;
                    await SettingsHelper.SaveSettingsAsync(_settings);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading settings in dialog: {ex.Message}");
                _settings = new SettingsHelper.AppSettings();
                ShowSystemTrayIcon = true;
                RunAtStartup = true;
                UseGrayscaleIcon = false;
                CheckForUpdatesOnStartup = true;
            }
        }

        public async Task SaveSettingsAsync() {
            try {
                _settings ??= new SettingsHelper.AppSettings();

                _settings.ShowSystemTrayIcon = ShowSystemTrayIcon;
                _settings.RunAtStartup = RunAtStartup;
                _settings.UseGrayscaleIcon = UseGrayscaleIcon;
                _settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;

                await SettingsHelper.SaveSettingsAsync(_settings);

                try {
                    ApplySystemTraySettings();
                    await ApplyStartupSettingsAsync();
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error applying settings: {ex.Message}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        public async Task CheckForUpdatesAsync() {
            if (IsCheckingForUpdates) {
                return;
            }

            IsCheckingForUpdates = true;
            UpdateStatusText = "업데이트 확인 중...";

            try {
                var updateInfo = await UpdateChecker.CheckForUpdatesAsync();

                if (!string.IsNullOrEmpty(updateInfo.ErrorMessage)) {
                    UpdateStatusText = updateInfo.ErrorMessage;
                }
                else if (updateInfo.UpdateAvailable) {
                    UpdateStatusText = $"v{updateInfo.LatestVersion} 사용 가능";
                    _updateReleaseUrl = updateInfo.ReleaseUrl;
                    UpdateInfoBarMessage = $"버전 {updateInfo.LatestVersion} 사용 가능 (현재 {updateInfo.CurrentVersion})";
                    IsUpdateInfoBarOpen = true;
                }
                else {
                    UpdateStatusText = $"최신 버전입니다! (v{updateInfo.CurrentVersion})";
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error checking for updates: {ex}");
                UpdateStatusText = "업데이트 확인 오류";
            }
            finally {
                IsCheckingForUpdates = false;
            }
        }

        public void OpenUpdateReleasePage() {
            if (!string.IsNullOrEmpty(_updateReleaseUrl)) {
                UpdateChecker.OpenReleasesPage(_updateReleaseUrl);
            }
        }

        private void ApplySystemTraySettings() {
            try {
                if (ShowSystemTrayIcon) {
                    if (Application.Current is App app) {
                        app.ShowSystemTray();
                    }
                }
                else {
                    if (Application.Current is App app) {
                        app.HideSystemTray();
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error applying system tray settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 프로그램 설정 적용 (비동기)
        /// </summary>
        /// <returns>설정 적용 성공 여부</returns>
        private async Task<bool> ApplyStartupSettingsAsync() {
            try {
                StartupStatusMessage = string.Empty;
                IsStartupBlocked = false;

                if (RunAtStartup) {
                    var result = await SettingsHelper.AddToStartupAsync();

                    if (result == StartupTaskState.DisabledByUser) {
                        // 사용자가 Windows 설정에서 거부함
                        StartupStatusMessage = "Windows 설정에서 차단됨. 설정 > 앱 > 시작프로그램에서 허용하세요.";
                        IsStartupBlocked = true;
                        RunAtStartup = false;

                        // 설정 파일도 업데이트
                        if (_settings != null) {
                            _settings.RunAtStartup = false;
                            await SettingsHelper.SaveSettingsAsync(_settings);
                        }
                        return false;
                    }
                    else if (result == StartupTaskState.DisabledByPolicy) {
                        // 그룹 정책에 의해 차단됨
                        StartupStatusMessage = "그룹 정책에 의해 차단됨";
                        IsStartupBlocked = true;
                        RunAtStartup = false;

                        if (_settings != null) {
                            _settings.RunAtStartup = false;
                            await SettingsHelper.SaveSettingsAsync(_settings);
                        }
                        return false;
                    }
                    else if (result == StartupTaskState.Enabled) {
                        StartupStatusMessage = string.Empty;
                        IsStartupBlocked = false;
                        return true;
                    }
                }
                else {
                    await SettingsHelper.RemoveFromStartupAsync();
                    StartupStatusMessage = string.Empty;
                    IsStartupBlocked = false;
                }

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error applying startup settings: {ex.Message}");
                return false;
            }
        }
    }
}
