using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel;

namespace AppGroup.ViewModels {
public partial class SettingsDialogViewModel : ObservableObject {
    private static readonly ResourceLoader _resourceLoader = new ResourceLoader();
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
        try {
            // 패키지 버전 가져오기 (Package.appxmanifest의 Version)
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            VersionText = string.Format(_resourceLoader.GetString("VersionFormat"), 
                packageVersion.Major, packageVersion.Minor, packageVersion.Build);
        }
        catch {
            // 패키지 버전을 가져올 수 없는 경우 어셈블리 버전 사용
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null) {
                VersionText = string.Format(_resourceLoader.GetString("VersionFormat"), version.Major, version.Minor, version.Build);
            }
            else {
                VersionText = _resourceLoader.GetString("VersionUnknown");
            }
        }
    }

    public async Task LoadCurrentSettingsAsync() {
        try {
            _settings = await SettingsHelper.LoadSettingsAsync();

            ShowSystemTrayIcon = _settings.ShowSystemTrayIcon;
            UseGrayscaleIcon = _settings.UseGrayscaleIcon;

            // 실제 시스템 시작 프로그램 상태 확인
            var startupState = await SettingsHelper.GetStartupStateAsync();
            Debug.WriteLine($"[Settings] Startup state from system: {startupState}");

            // 시스템 상태에 따라 UI 동기화
            switch (startupState) {
                case StartupTaskState.Enabled:
                    RunAtStartup = true;
                    IsStartupBlocked = false;
                    StartupStatusMessage = string.Empty;
                    break;

                case StartupTaskState.DisabledByUser:
                    RunAtStartup = false;
                    IsStartupBlocked = true;
                    StartupStatusMessage = _resourceLoader.GetString("StartupBlockedByUser");
                    break;

                case StartupTaskState.DisabledByPolicy:
                    RunAtStartup = false;
                    IsStartupBlocked = true;
                    StartupStatusMessage = _resourceLoader.GetString("StartupBlockedByPolicy");
                    break;

                case StartupTaskState.Disabled:
                default:
                    // 설정 파일 값 유지 (사용자가 설정한 값)
                    RunAtStartup = _settings.RunAtStartup;
                    IsStartupBlocked = false;
                    StartupStatusMessage = string.Empty;
                    break;
            }

            // 설정 파일과 실제 상태가 다르면 동기화
            if (_settings.RunAtStartup != RunAtStartup) {
                _settings.RunAtStartup = RunAtStartup;
                await SettingsHelper.SaveSettingsAsync(_settings);
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error loading settings in dialog: {ex.Message}");
            _settings = new SettingsHelper.AppSettings();
            ShowSystemTrayIcon = true;
            RunAtStartup = false;
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
                    Debug.WriteLine("[Settings] Attempting to enable startup...");
                    var result = await SettingsHelper.AddToStartupAsync();
                    Debug.WriteLine($"[Settings] AddToStartup result: {result}");

                    if (result == StartupTaskState.DisabledByUser) {
                        // 사용자가 Windows 설정에서 거부함
                        StartupStatusMessage = _resourceLoader.GetString("StartupBlockedByUser");
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
                        StartupStatusMessage = _resourceLoader.GetString("StartupBlockedByPolicy");
                        IsStartupBlocked = true;
                        RunAtStartup = false;

                        if (_settings != null) {
                            _settings.RunAtStartup = false;
                            await SettingsHelper.SaveSettingsAsync(_settings);
                        }
                        return false;
                    }
                    else if (result == StartupTaskState.Enabled) {
                        Debug.WriteLine("[Settings] Startup enabled successfully");
                        StartupStatusMessage = string.Empty;
                        IsStartupBlocked = false;
                        return true;
                    }
                    else if (result == StartupTaskState.Disabled) {
                        // 요청은 했지만 여전히 Disabled 상태 - 재시도 또는 사용자 알림
                        Debug.WriteLine("[Settings] Startup still disabled after request");
                        StartupStatusMessage = _resourceLoader.GetString("StartupRegistrationFailed");
                        IsStartupBlocked = true;
                        RunAtStartup = false;

                        if (_settings != null) {
                            _settings.RunAtStartup = false;
                            await SettingsHelper.SaveSettingsAsync(_settings);
                        }
                        return false;
                    }
                }
                else {
                    Debug.WriteLine("[Settings] Disabling startup...");
                    await SettingsHelper.RemoveFromStartupAsync();
                    StartupStatusMessage = string.Empty;
                    IsStartupBlocked = false;
                }

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error applying startup settings: {ex.GetType().Name} - {ex.Message}");
                var errorMessage = !string.IsNullOrWhiteSpace(ex.Message) 
                    ? ex.Message 
                    : ex.GetType().Name;
                StartupStatusMessage = string.Format(_resourceLoader.GetString("ErrorFormat"), errorMessage);
                IsStartupBlocked = true;
                return false;
            }
        }
    }
}
