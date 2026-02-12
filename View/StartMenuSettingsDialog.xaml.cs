using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

namespace AppGroup.View
{
    /// <summary>
    /// 시작 메뉴 설정을 위한 ContentDialog
    /// </summary>
    public sealed partial class StartMenuSettingsDialog : ContentDialog
    {
        private bool _isLoading = true;

        public StartMenuSettingsDialog()
        {
            InitializeComponent();

            // 저장된 테마 설정 적용 (ContentDialog는 부모 테마를 자동 상속하지 않음)
            string savedTheme = SettingsHelper.GetSavedTheme();
            if (!string.IsNullOrWhiteSpace(savedTheme))
            {
                RequestedTheme = savedTheme switch
                {
                    "Dark" => ElementTheme.Dark,
                    "Light" => ElementTheme.Light,
                    _ => ElementTheme.Default
                };
            }

            Loaded += StartMenuSettingsDialog_Loaded;
            Unloaded += StartMenuSettingsDialog_Unloaded;
        }

        /// <summary>
        /// 다이얼로그 로드 시 설정 값을 로드합니다.
        /// </summary>
        private async void StartMenuSettingsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                await LoadSettingsAsync();
                _isLoading = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartMenuSettingsDialog_Loaded 오류: {ex.Message}");
                _isLoading = false;
            }
        }

        /// <summary>
        /// 다이얼로그 언로드 시 이벤트 핸들러를 해제합니다.
        /// </summary>
        private void StartMenuSettingsDialog_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Loaded -= StartMenuSettingsDialog_Loaded;
                Unloaded -= StartMenuSettingsDialog_Unloaded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartMenuSettingsDialog_Unloaded 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 설정 값을 로드합니다.
        /// </summary>
        private async System.Threading.Tasks.Task LoadSettingsAsync()
        {
            try
            {
                var settings = await SettingsHelper.LoadSettingsAsync();

                // 트레이 아이콘 클릭 시 팝업 표시 설정
                ShowStartMenuPopupToggle.IsOn = settings.ShowStartMenuPopup;

                // 폴더 열 개수 설정 (1~5, 인덱스는 0~4)
                int columnIndex = Math.Max(0, Math.Min(4, settings.FolderColumnCount - 1));
                FolderColumnCountComboBox.SelectedIndex = columnIndex;

                // 하위 폴더 탐색 개수 설정 (1~5, 인덱스는 0~4)
                int depthIndex = Math.Max(0, Math.Min(4, settings.SubfolderDepth - 1));
                SubfolderDepthComboBox.SelectedIndex = depthIndex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSettingsAsync 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 설정 값을 저장합니다.
        /// </summary>
        private async System.Threading.Tasks.Task SaveSettingsAsync()
        {
            try
            {
                var settings = await SettingsHelper.LoadSettingsAsync();

                // 트레이 아이콘 클릭 시 팝업 표시 설정
                settings.ShowStartMenuPopup = ShowStartMenuPopupToggle.IsOn;

                // 폴더 열 개수 설정
                settings.FolderColumnCount = FolderColumnCountComboBox.SelectedIndex + 1;

                // 하위 폴더 탐색 개수 설정
                settings.SubfolderDepth = SubfolderDepthComboBox.SelectedIndex + 1;

                await SettingsHelper.SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveSettingsAsync 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 트레이 클릭 동작 변경 이벤트 핸들러
        /// </summary>
        private async void TrayClickActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// 폴더 경로 표시 토글 변경 이벤트 핸들러
        /// </summary>
        private async void ShowFolderPathToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// 폴더 아이콘 표시 토글 변경 이벤트 핸들러
        /// </summary>
        private async void ShowFolderIconToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// 폴더 열 개수 변경 이벤트 핸들러
        /// </summary>
        private async void FolderColumnCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// 시작 메뉴 팝업 표시 토글 변경 이벤트 핸들러
        /// </summary>
        private async void ShowStartMenuPopupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// 하위 폴더 탐색 개수 변경 이벤트 핸들러
        /// </summary>
        private async void SubfolderDepthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// 다이얼로그 닫기
        /// </summary>
        private void CloseDialog(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
