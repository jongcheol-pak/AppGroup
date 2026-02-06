using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Storage.Pickers;
using AppGroup.Models;
using File = System.IO.File;

namespace AppGroup.View {
    /// <summary>
    /// EditGroupWindow - FolderWeb 다이얼로그 관련 partial class
    /// 폴더/웹 항목 추가 및 편집 기능을 담당합니다.
    /// </summary>
    public sealed partial class EditGroupWindow {
        private static readonly ResourceLoader _folderWebResourceLoader = new ResourceLoader();

        #region FolderWeb Dialog Handlers

        // 폴더/웹 아이콘 경로
        private string selectedFolderWebIconPath = null;

        /// <summary>
        /// FolderWeb 다이얼로그 닫기
        /// </summary>
        private void CloseFolderWebDialog(object sender, RoutedEventArgs e)
        {
            // 편집 모드 초기화
            _isEditingFolderWebItem = false;
            _editingFolderWebItem = null;
            FolderWebDialog.Hide();
        }

        /// <summary>
        /// 폴더/웹 추가 버튼 클릭 - 다이얼로그 표시
        /// </summary>
        private async void FolderWebButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 편집 모드 초기화 (새 항목 추가 모드)
                _isEditingFolderWebItem = false;
                _editingFolderWebItem = null;

                // 다이얼로그 초기화
                FolderWebTypeComboBox.SelectedIndex = 0; // 기본: 폴더
                FolderWebNameTextBox.Text = "";
                FolderPathTextBox.Text = "";
                WebUrlTextBox.Text = "";
                selectedFolderWebIconPath = null;

                // 기본 아이콘 설정 (아이콘 파일이 없으면 null 처리)
                FolderWebIconPreview.Source = null;

                // 폴더 패널 표시, 웹 패널 숨김
                FolderPathPanel.Visibility = Visibility.Visible;
                WebUrlPanel.Visibility = Visibility.Collapsed;

                FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                await FolderWebDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing folder/web dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더/웹 타입 콤보박스 선택 변경
        /// </summary>
        private void FolderWebTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderWebTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();
                if (tag == "Folder")
                {
                    FolderPathPanel.Visibility = Visibility.Visible;
                    WebUrlPanel.Visibility = Visibility.Collapsed;

                    // 폴더 기본 아이콘 설정 (커스텀 아이콘이 없는 경우 null)
                    if (string.IsNullOrEmpty(selectedFolderWebIconPath))
                    {
                        FolderWebIconPreview.Source = null;
                    }
                }
                else if (tag == "Web")
                {
                    FolderPathPanel.Visibility = Visibility.Collapsed;
                    WebUrlPanel.Visibility = Visibility.Visible;

                    // 웹 기본 아이콘 설정 (커스텀 아이콘이 없는 경우 null)
                    if (string.IsNullOrEmpty(selectedFolderWebIconPath))
                    {
                        FolderWebIconPreview.Source = null;
                    }
                }
            }
        }

        /// <summary>
        /// 폴더 경로 찾아보기 버튼 클릭
        /// </summary>
        private async void BrowseFolderPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    FolderPathTextBox.Text = folder.Path;

                    // 폴더 이름 자동 설정 (비어있는 경우)
                    if (string.IsNullOrEmpty(FolderWebNameTextBox.Text))
                    {
                        FolderWebNameTextBox.Text = folder.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting folder: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더/웹 아이콘 찾아보기 버튼 클릭
        /// </summary>
        private async void BrowseFolderWebIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // FolderWebDialog를 먼저 숨김 (ContentDialog는 동시에 하나만 표시 가능)
                FolderWebDialog.Hide();
                await Task.Delay(50); // 다이얼로그가 완전히 닫힐 때까지 대기

                // 다이얼로그 상태 초기화
                if (FolderWebIconSelectionOptionsPanel != null && FolderWebResourceIconGridView != null)
                {
                    FolderWebIconSelectionOptionsPanel.Visibility = Visibility.Visible;
                    FolderWebResourceIconGridView.Visibility = Visibility.Collapsed;
                }

                FolderWebIconDialog.XamlRoot = this.Content.XamlRoot;
                await FolderWebIconDialog.ShowAsync();
                // 아이콘 선택 다이얼로그가 닫히면 각 버튼 핸들러에서 FolderWebDialog를 다시 표시함
                // (CloseFolderWebIconDialog, FolderWebRegularIconClick, FolderWebResourceIcon_SelectionChanged 등)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing icon dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더/웹 아이콘 선택 다이얼로그 닫기
        /// </summary>
        private async void CloseFolderWebIconDialog(object sender, RoutedEventArgs e)
        {
            FolderWebIconDialog.Hide();
            await Task.Delay(50);
            FolderWebDialog.XamlRoot = this.Content.XamlRoot;
            _ = FolderWebDialog.ShowAsync();
        }

        /// <summary>
        /// 폴더/웹 일반 아이콘 선택 (파일에서 선택)
        /// </summary>
        private async void FolderWebRegularIconClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // 아이콘 선택 다이얼로그 먼저 숨김
                FolderWebIconDialog.Hide();
                await Task.Delay(50);

                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".ico");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".exe");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    selectedFolderWebIconPath = file.Path;
                    BitmapImage bitmapImage = new BitmapImage();

                    if (file.FileType.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var iconPath = await IconCache.GetIconPathAsync(file.Path);
                        if (!string.IsNullOrEmpty(iconPath))
                        {
                            selectedFolderWebIconPath = iconPath;
                            using (var stream = File.OpenRead(iconPath))
                            {
                                await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                            }
                        }
                    }
                    else
                    {
                        using (var stream = await file.OpenReadAsync())
                        {
                            await bitmapImage.SetSourceAsync(stream);
                        }
                    }

                    FolderWebIconPreview.Source = bitmapImage;
                }

                // FolderWebDialog 다시 표시
                await Task.Delay(50);
                FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                _ = FolderWebDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting icon: {ex.Message}");
                // 에러 발생 시에도 FolderWebDialog 다시 표시
                await Task.Delay(50);
                FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                _ = FolderWebDialog.ShowAsync();
            }
        }

        /// <summary>
        /// 폴더/웹 리소스 아이콘 선택 (내장 아이콘에서 선택)
        /// </summary>
        private async void FolderWebResourceIconClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string iconFolderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon");
                if (Directory.Exists(iconFolderPath))
                {
                    var iconFiles = Directory.GetFiles(iconFolderPath)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (iconFiles.Count > 0)
                    {
                        FolderWebIconSelectionOptionsPanel.Visibility = Visibility.Collapsed;
                        FolderWebResourceIconGridView.Visibility = Visibility.Visible;
                        FolderWebResourceIconGridView.ItemsSource = iconFiles;
                    }
                    else
                    {
                        // 다이얼로그를 먼저 숨기고 에러 표시
                        FolderWebIconDialog.Hide();
                        await Task.Delay(50);
                        await ShowDialog(_folderWebResourceLoader.GetString("NotificationTitle"), _folderWebResourceLoader.GetString("NoResourceIconsAvailable"));
                        await Task.Delay(50);
                        FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                        _ = FolderWebDialog.ShowAsync();
                    }
                }
                else
                {
                    // 다이얼로그를 먼저 숨기고 에러 표시
                    FolderWebIconDialog.Hide();
                    await Task.Delay(50);
                    await ShowDialog(_folderWebResourceLoader.GetString("ErrorTitle"), string.Format(_folderWebResourceLoader.GetString("IconFolderNotFoundFormat"), iconFolderPath));
                    await Task.Delay(50);
                    FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                    _ = FolderWebDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                // 다이얼로그를 먼저 숨기고 에러 표시
                FolderWebIconDialog.Hide();
                await Task.Delay(50);
                await ShowDialog(_folderWebResourceLoader.GetString("ErrorTitle"), string.Format(_folderWebResourceLoader.GetString("IconLoadErrorFormat"), ex.Message));
                await Task.Delay(50);
                FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                _ = FolderWebDialog.ShowAsync();
            }
        }

        /// <summary>
        /// 폴더/웹 리소스 아이콘 그리드뷰 선택 변경
        /// </summary>
        private async void FolderWebResourceIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderWebResourceIconGridView.SelectedItem is string iconPath)
            {
                try
                {
                    selectedFolderWebIconPath = iconPath;
                    FolderWebIconPreview.Source = new BitmapImage(new Uri(iconPath));

                    // UI 초기화
                    FolderWebResourceIconGridView.SelectedItem = null;
                    FolderWebResourceIconGridView.Visibility = Visibility.Collapsed;
                    FolderWebIconSelectionOptionsPanel.Visibility = Visibility.Visible;

                    // 아이콘 선택 다이얼로그 숨기고 FolderWebDialog 다시 표시
                    FolderWebIconDialog.Hide();
                    await Task.Delay(50);
                    FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                    _ = FolderWebDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(_folderWebResourceLoader.GetString("ErrorTitle"), string.Format(_folderWebResourceLoader.GetString("IconSelectionErrorFormat"), ex.Message));
                }
            }
        }

        /// <summary>
        /// 폴더/웹 항목 저장 버튼 클릭
        /// </summary>
        private async void FolderWebSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = FolderWebNameTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    // 다이얼로그를 먼저 숨기고 에러 표시 후 다시 열기
                    FolderWebDialog.Hide();
                    await Task.Delay(50);
                    await ShowDialog(_folderWebResourceLoader.GetString("ErrorTitle"), _folderWebResourceLoader.GetString("EnterNameError"));
                    await Task.Delay(50);
                    FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                    _ = FolderWebDialog.ShowAsync();
                    return;
                }

                string selectedType = (FolderWebTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                string path = null;
                ItemType itemType = ItemType.App;

                if (selectedType == "Folder")
                {
                    path = FolderPathTextBox.Text?.Trim();
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    {
                        // 다이얼로그를 먼저 숨기고 에러 표시 후 다시 열기
                        FolderWebDialog.Hide();
                        await Task.Delay(50);
                        await ShowDialog(_folderWebResourceLoader.GetString("ErrorTitle"), _folderWebResourceLoader.GetString("SelectValidFolderPathError"));
                        await Task.Delay(50);
                        FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                        _ = FolderWebDialog.ShowAsync();
                        return;
                    }
                    itemType = ItemType.Folder;
                }
                else if (selectedType == "Web")
                {
                    path = WebUrlTextBox.Text?.Trim();
                    if (string.IsNullOrEmpty(path))
                    {
                        // 다이얼로그를 먼저 숨기고 에러 표시 후 다시 열기
                        FolderWebDialog.Hide();
                        await Task.Delay(50);
                        await ShowDialog(_folderWebResourceLoader.GetString("ErrorTitle"), _folderWebResourceLoader.GetString("EnterWebUrlError"));
                        await Task.Delay(50);
                        FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                        _ = FolderWebDialog.ShowAsync();
                        return;
                    }

                    // URL 검증 및 http/https 접두사 추가
                    if (!path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        path = "https://" + path;
                    }
                    itemType = ItemType.Web;
                }

                // 아이콘 경로 설정 (선택된 아이콘이 없으면 null - 기본 아이콘 없음)
                string iconPath = selectedFolderWebIconPath ?? "";

                // 편집 모드인 경우 기존 항목 업데이트
                if (_isEditingFolderWebItem && _editingFolderWebItem != null)
                {
                    _editingFolderWebItem.FileName = name;
                    _editingFolderWebItem.FilePath = path;
                    _editingFolderWebItem.Icon = iconPath;
                    _editingFolderWebItem.Tooltip = name;
                    _editingFolderWebItem.IconPath = iconPath;
                    _editingFolderWebItem.ItemType = itemType;

                    // UI 새로 고침 강제 수행
                    int index = _viewModel.ExeFiles.IndexOf(_editingFolderWebItem);
                    if (index >= 0)
                    {
                        _viewModel.ExeFiles.RemoveAt(index);
                        _viewModel.ExeFiles.Insert(index, _editingFolderWebItem);
                    }

                    // 편집 모드 해제
                    _isEditingFolderWebItem = false;
                    _editingFolderWebItem = null;

                    FolderWebDialog.Hide();
                    return;
                }

                // 새 항목 추가 모드
                _viewModel.ExeFiles.Add(new ExeFileModel
                {
                    FileName = name,
                    FilePath = path,
                    Icon = iconPath,
                    Tooltip = name,
                    Args = "",
                    IconPath = iconPath,
                    ItemType = itemType
                });

                // UI 업데이트
                ExeListView.ItemsSource = _viewModel.ExeFiles;
                lastSelectedItem = GroupColComboBox.SelectedItem as string;
                _viewModel.ApplicationCountText = ExeListView.Items.Count > 1
                    ? string.Format(_folderWebResourceLoader.GetString("ItemsCountFormat"), ExeListView.Items.Count)
                    : ExeListView.Items.Count == 1
                    ? _folderWebResourceLoader.GetString("OneItem")
                    : "";

                IconGridComboBox.Items.Clear();
                IconGridComboBox.Items.Add("2");
                IconGridComboBox.SelectedItem = "2";

                GroupColComboBox.Items.Clear();
                for (int i = 1; i <= _viewModel.ExeFiles.Count; i++)
                {
                    GroupColComboBox.Items.Add(i.ToString());
                }

                if (_viewModel.ExeFiles.Count > 3)
                {
                    GroupColComboBox.SelectedItem = lastSelectedItem ?? "3";
                }
                else
                {
                    GroupColComboBox.SelectedItem = _viewModel.ExeFiles.Count.ToString();
                }

                FolderWebDialog.Hide();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving folder/web item: {ex.Message}");
                // 다이얼로그를 먼저 숨기고 에러 표시
                FolderWebDialog.Hide();
                await Task.Delay(50);
                await ShowDialog(_folderWebResourceLoader.GetString("ErrorTitle"), string.Format(_folderWebResourceLoader.GetString("SaveErrorFormat"), ex.Message));
            }
            finally
            {
                // 편집 모드 해제
                _isEditingFolderWebItem = false;
                _editingFolderWebItem = null;
            }
        }

        #endregion
    }
}
