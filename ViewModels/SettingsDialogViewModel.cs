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
    private string _versionText = "";

    private bool _showSystemTrayIcon = true;
    private bool _runAtStartup = true;
    private bool _useGrayscaleIcon;
    private string _startupStatusMessage = string.Empty;
    private bool _isStartupBlocked;

    public bool IsLoading {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string VersionText {
        get => _versionText;
        set => SetProperty(ref _versionText, value);
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
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        if (version != null) {
            VersionText = $"버전 {version.Major}.{version.Minor}.{version.Build}";
        }
        else {
            VersionText = "버전 Unknown";
        }
    }

    public async Task LoadCurrentSettingsAsync() {
        try {
            _settings = await SettingsHelper.LoadSettingsAsync();

            ShowSystemTrayIcon = _settings.ShowSystemTrayIcon;
            RunAtStartup = _settings.RunAtStartup;
            UseGrayscaleIcon = _settings.UseGrayscaleIcon;

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
        }
    }

    public async Task SaveSettingsAsync() {
        try {
            _settings ??= new SettingsHelper.AppSettings();

            _settings.ShowSystemTrayIcon = ShowSystemTrayIcon;
            _settings.RunAtStartup = RunAtStartup;
            _settings.UseGrayscaleIcon = UseGrayscaleIcon;

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
