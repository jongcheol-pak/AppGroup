using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using AppGroup.ViewModels;

namespace AppGroup.View {
    public sealed partial class SettingsDialog : ContentDialog {
        private readonly SettingsDialogViewModel _viewModel;

        public SettingsDialog() {
            InitializeComponent();
            _viewModel = new SettingsDialogViewModel();
            DataContext = _viewModel;
            _viewModel.InitializeVersionText();
            _viewModel.UpdateStatusText = "클릭하여 새 버전 확인";
            Loaded += SettingsDialog_Loaded;
            Unloaded += SettingsDialog_Unloaded;
        }

        /// <summary>
        /// 다이얼로그 언로드 시 이벤트 핸들러를 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        private void SettingsDialog_Unloaded(object sender, RoutedEventArgs e) {
            try {
                // Toggle 이벤트 핸들러 해제
                SystemTrayToggle.Toggled -= SystemTrayToggle_Toggled;
                StartupToggle.Toggled -= StartupToggle_Toggled;
                GrayscaleIconToggle.Toggled -= GrayScaleToggle_Toggled;
                UpdateCheckToggle.Toggled -= UpdateCheckToggle_Toggled;

                // Loaded/Unloaded 이벤트 핸들러 해제
                Loaded -= SettingsDialog_Loaded;
                Unloaded -= SettingsDialog_Unloaded;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in SettingsDialog_Unloaded: {ex.Message}");
            }
        }

        private async void SettingsDialog_Loaded(object sender, RoutedEventArgs e) {
            try {
                _viewModel.IsLoading = true;
                await _viewModel.LoadCurrentSettingsAsync();

                // Wire up toggle events after loading to prevent firing during init
                SystemTrayToggle.Toggled += SystemTrayToggle_Toggled;
                StartupToggle.Toggled += StartupToggle_Toggled;
                GrayscaleIconToggle.Toggled += GrayScaleToggle_Toggled;
                UpdateCheckToggle.Toggled += UpdateCheckToggle_Toggled;

                _viewModel.IsLoading = false;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in SettingsDialog_Loaded: {ex.Message}");
                _viewModel.IsLoading = false;
            }
        }

        private async void SystemTrayToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_viewModel.IsLoading) return;

            try {
                await _viewModel.SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving system tray settings: {ex.Message}");
                _viewModel.IsLoading = true;
                SystemTrayToggle.IsOn = !SystemTrayToggle.IsOn;
                _viewModel.IsLoading = false;
            }
        }

        private async void StartupToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_viewModel.IsLoading) return;

            try {
                await _viewModel.SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving startup settings: {ex.Message}");
                _viewModel.IsLoading = true;
                StartupToggle.IsOn = !StartupToggle.IsOn;
                _viewModel.IsLoading = false;
            }
        }

        private async void GrayScaleToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_viewModel.IsLoading) return;

            try {
                await _viewModel.SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving grayscale settings: {ex.Message}");
                _viewModel.IsLoading = true;
                GrayscaleIconToggle.IsOn = !GrayscaleIconToggle.IsOn;
                _viewModel.IsLoading = false;
            }
        }

        private async void UpdateCheckToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_viewModel.IsLoading) return;

            try {
                await _viewModel.SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving update check settings: {ex.Message}");
                _viewModel.IsLoading = true;
                UpdateCheckToggle.IsOn = !UpdateCheckToggle.IsOn;
                _viewModel.IsLoading = false;
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e) {
            if (_viewModel.IsCheckingForUpdates) return;

            CheckUpdateButton.IsEnabled = false;
            await _viewModel.CheckForUpdatesAsync();
            CheckUpdateButton.IsEnabled = true;
        }

        private void DownloadUpdate_Click(object sender, RoutedEventArgs e) {
            _viewModel.OpenUpdateReleasePage();
        }

        private void CloseDialog(object sender, RoutedEventArgs e) {
            try {
                Hide();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error closing dialog: {ex.Message}");
                try {
                    if (XamlRoot?.Content is FrameworkElement) {
                        // 시각 트리에서 제거 실패 시를 위한 예외 처리
                    }
                }
                catch (Exception ex2) {
                    Debug.WriteLine($"Error in alternative close method: {ex2.Message}");
                }
            }
        }
    }
}
