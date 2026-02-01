using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using IWshRuntimeLibrary;
using Windows.ApplicationModel.DataTransfer;
using File = System.IO.File;
using System.Text.RegularExpressions;
using WinUIEx.Messaging;
using System.Drawing;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;

using AppGroup.Models;
using AppGroup.ViewModels;


namespace AppGroup.View {
public sealed partial class EditGroupWindow : WinUIEx.WindowEx, IDisposable {
    private bool _disposed = false;
    public int GroupId { get; private set; }
    private string selectedIconPath = string.Empty;
    private string selectedFilePath = string.Empty;
    private readonly EditGroupViewModel _viewModel;
    private bool regularIcon = true;
    private string? lastSelectedItem;
    private string? copiedImagePath;
    private string tempIcon;
    private string? groupName;
    private FileSystemWatcher fileWatcher;
    private string groupIdFilePath;
    private int? lastGroupId = null;
    private ExeFileModel CurrentItem { get; set; }
    private string originalItemIconPath = null;
    private bool _isDialogRepositioning = false;

    // 폴더/웹 편집 모드 플래그
    private bool _isEditingFolderWebItem = false;
    private ExeFileModel _editingFolderWebItem = null;

    private const int DEFAULT_LABEL_SIZE = 12;
    private const string DEFAULT_LABEL_POSITION = "Bottom";

    public EditGroupWindow(int groupId)
    {

            this.InitializeComponent();

            GroupId = groupId;

            this.CenterOnScreen();

            var iconPath = Path.Combine(AppContext.BaseDirectory, "EditGroup.ico");
            this.AppWindow.SetIcon(iconPath);
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _viewModel = new EditGroupViewModel();
            if (Content is FrameworkElement rootElement) {
                rootElement.DataContext = _viewModel;
            }
            ExeListView.ItemsSource = _viewModel.ExeFiles;

            MinHeight = 600;
            MinWidth = 530;
            ExtendsContentIntoTitleBar = true;


            ThemeHelper.UpdateTitleBarColors(this);
            _ = LoadGroupDataAsync(GroupId);
            Closed += MainWindow_Closed;
            this.AppWindow.Closing += AppWindow_Closing;

            _viewModel.ApplicationCountText = "항목";
            NativeMethods.SetCurrentProcessExplicitAppUserModelID("AppGroup.EditGroup");
            Activated += EditGroupWindow_Activated;
            //this.AppWindow.Changed += AppWindow_Changed;

            this.SizeChanged += EditGroupWindow_SizeChanged;
        }


        private async void EditGroupWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {

            await HideAllDialogsAsync();
            //if (EditItemDialog.Visibility == Visibility.Visible && !_isDialogRepositioning) {
            //    _isDialogRepositioning = true;
            //    System.Diagnostics.Debug.WriteLine("Dialog was visible, hiding and reshowing...");

            //    try {
            //        EditItemDialog.Hide();
            //        EditItemDialog.XamlRoot = this.Content.XamlRoot;
            //        //await Task.Delay(100);
            //        //_= EditItemDialog.ShowAsync();
            //    }
            //    finally {
            //        _isDialogRepositioning = false;
            //    }
            //}
        }

        private async void EditGroupWindow_Activated(object sender, WindowActivatedEventArgs e)
        {

            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                // GC.Collect() 제거 - 불필요한 성능 저하 유발
            }

            if (e.WindowActivationState == WindowActivationState.CodeActivated)
            {
                // 비교할 현재 그룹 ID 저장
                int previousGroupId = GroupId;
                int newGroupId = -1; // 기본값

                // 창이 활성화될 때마다 파일에서 그룹 필터 읽기

                try
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string appFolderPath = Path.Combine(appDataPath, "AppGroup");
                    string filePath = Path.Combine(appFolderPath, "lastEdit");

                    if (File.Exists(filePath))
                    {
                        string fileGroupIdText = File.ReadAllText(filePath).Trim();
                        if (!string.IsNullOrEmpty(fileGroupIdText) && int.TryParse(fileGroupIdText, out int fileGroupId))
                        {
                            newGroupId = fileGroupId;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading group name from file: {ex.Message}");
                }

                // 그룹 ID 업데이트 및 변경된 경우에만 데이터 로드
                GroupId = newGroupId;
                if (GroupId != previousGroupId)
                {
                    await LoadGroupDataAsync(-1);
                    await LoadGroupDataAsync(GroupId);
                    Debug.WriteLine($"GroupId changed from {previousGroupId} to {GroupId}, data reloaded");
                }
                else
                {
                    Debug.WriteLine($"GroupId unchanged ({GroupId}), skipping data reload");
                }
            }
        }

        private async void ShowActivationDialog(string id)
        {
            try
            {
                var dialog = new ContentDialog()
                {
                    Title = "창 활성화됨",
                    Content = "그룹 ID: " + id,
                    CloseButtonText = "확인",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing dialog: {ex.Message}");
            }
        }

        private async void OnGroupIdFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (File.Exists(groupIdFilePath))
                {
                    using (FileStream stream = new FileStream(groupIdFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string newGroupIdText = await reader.ReadToEndAsync();

                        if (int.TryParse(newGroupIdText, out int newGroupId))
                        {
                            if (lastGroupId != newGroupId)
                            {
                                lastGroupId = newGroupId;
                                GroupId = newGroupId;

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    _viewModel.ExeFiles.Clear();
                                    LoadGroupDataAsync(GroupId);

                                });


                            }
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.WriteLine("File read error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unexpected error: " + ex.Message);
            }
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            GroupId = -1;
            await HideAllDialogsAsync();
            this.Hide();        // 창 숨기기
        }

        private async Task HideAllDialogsAsync()
        {
            var dialogs = FindVisualChildren<ContentDialog>(this.Content);

            foreach (var dialog in dialogs)
            {
                if (dialog.Visibility == Visibility.Visible)
                {
                    dialog.Hide();
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);

                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                // FileSystemWatcher가 존재하면 해제
                if (fileWatcher != null)
                {
                    fileWatcher.EnableRaisingEvents = false;
                    fileWatcher.Dispose();
                    fileWatcher = null;
                }

                // 임시 그룹 ID 파일이 존재하면 삭제
                if (!string.IsNullOrEmpty(groupIdFilePath) && File.Exists(groupIdFilePath))
                {
                    File.Delete(groupIdFilePath);
                }

                // 임시 아이콘 폴더 정리
                if (!string.IsNullOrEmpty(tempIcon))
                {
                    string tempFolder = Path.GetDirectoryName(tempIcon);
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }

                // GC를 돕기 위해 이미지 참조 지우기
                foreach (var exeFile in _viewModel.ExeFiles)
                {
                    exeFile.Icon = null;
                }
                _viewModel.ExeFiles.Clear();

                // 이벤트 핸들러 제거
                Activated -= EditGroupWindow_Activated;
                SizeChanged -= EditGroupWindow_SizeChanged;
                Closed -= MainWindow_Closed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup error in MainWindow_Closed: {ex.Message}");
            }
        }


        private async void ExeListView_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drag Enter Error: {ex.Message}");
            }
        }

        private async void ExeListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();

                    foreach (var item in items)
                    {
                        if (item is StorageFile file &&
                            (file.FileType.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                             file.FileType.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                             file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase)))
                        {

                            string icon;

                            if (file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase))
                            {
                                icon = await IconHelper.GetUrlFileIconAsync(file.Path);
                            }
                            else
                            {
                                icon = await IconCache.GetIconPathAsync(file.Path);
                            }

                            _viewModel.ExeFiles.Add(new ExeFileModel
                            {
                                FileName = file.Name,
                                Icon = icon,
                                FilePath = file.Path,
                                IconPath = icon
                            });
                        }
                    }

                    // BrowseFiles 메서드의 기존 로직 재사용
                    ExeListView.ItemsSource = _viewModel.ExeFiles;
                    lastSelectedItem = GroupColComboBox.SelectedItem as string;
                    _viewModel.ApplicationCountText = ExeListView.Items.Count > 1
                        ? ExeListView.Items.Count.ToString() + "개 항목"
                        : ExeListView.Items.Count == 1
                        ? "1개 항목"
                        : "";
                    IconGridComboBox.Items.Clear();
                    if (_viewModel.ExeFiles.Count >= 9)
                    {
                        IconGridComboBox.Items.Add("2");
                        IconGridComboBox.Items.Add("3");
                        IconGridComboBox.SelectedItem = "3";
                    }
                    else
                    {
                        IconGridComboBox.Items.Add("2");
                        IconGridComboBox.SelectedItem = "2";
                    }

                    GroupColComboBox.Items.Clear();
                    for (int i = 1; i <= _viewModel.ExeFiles.Count; i++)
                    {
                        GroupColComboBox.Items.Add(i.ToString());
                    }
                    if (_viewModel.ExeFiles.Count > 3)
                    {
                        if (lastSelectedItem != null)
                        {
                            GroupColComboBox.SelectedItem = lastSelectedItem;

                        }
                        else
                        {
                            GroupColComboBox.SelectedItem = "3";

                        }
                    }
                    else
                    {
                        GroupColComboBox.SelectedItem = _viewModel.ExeFiles.Count.ToString();
                    }

                    if (!regularIcon)
                    {
                        IconGridComboBox.Visibility = Visibility.Visible;
                        if (CustomDialog.XamlRoot != null)
                        {
                            CustomDialog.Hide();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drop Error: {ex.Message}");
            }
        }


        private void GroupColComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupColComboBox.SelectedItem != null && GroupColComboBox.SelectedItem.ToString() == "1")
            {
                GroupHeader.IsEnabled = false;
                ExpanderHeader.Opacity = 0.5;
            }
            else
            {
                GroupHeader.IsEnabled = true;
                ExpanderHeader.Opacity = 1.0;

            }
        }

        private void ShowLabels_Toggled(object sender, RoutedEventArgs e)
        {
            if (ShowLabels.IsOn)
            {
                LabelSizePanel.Opacity = 1.0;
                LabelSizeComboBox.IsEnabled = true;

                LabelPositionPanel.Opacity = 1.0;
                LabelPositionComboBox.IsEnabled = true;
            }
            else
            {
                LabelSizePanel.Opacity = 0.5;
                LabelSizeComboBox.IsEnabled = false;
                LabelPositionPanel.Opacity = 0.5;
                LabelPositionComboBox.IsEnabled = false;
            }
        }

        private void GroupHeader_Toggled(object sender, RoutedEventArgs e)
        {
            if (GroupHeader.IsOn)
            {
                HeaderTextPanel.Opacity = 1.0;
                ShowGroupName.IsEnabled = true;
            }
            else
            {
                HeaderTextPanel.Opacity = 0.5;
                ShowGroupName.IsEnabled = false;
            }
        }

        private void InitializeLabelSizeComboBox()
        {
            LabelSizeComboBox.Items.Clear();
            int[] sizes = { 8, 9, 10, 11, 12, 14 };
            foreach (int size in sizes)
            {
                LabelSizeComboBox.Items.Add(size.ToString());
            }
            LabelSizeComboBox.SelectedItem = int.Parse(DEFAULT_LABEL_SIZE.ToString()); // Default
        }

        private void InitializeLabelPositionComboBox()
        {
            LabelPositionComboBox.Items.Clear();

            LabelPositionComboBox.Items.Add("Right");
            LabelPositionComboBox.Items.Add("Bottom");
            LabelPositionComboBox.SelectedItem = DEFAULT_LABEL_POSITION;
        }

        private async Task LoadGroupDataAsync(int groupId)
        {
            await Task.Run(async () =>
            {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                if (File.Exists(jsonFilePath))
                {
                    string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                    JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                    if (jsonObject.AsObject().TryGetPropertyValue(groupId.ToString(), out JsonNode groupNode))
                    {
                        groupName = groupNode["groupName"]?.GetValue<string>();
                        int groupCol = groupNode["groupCol"]?.GetValue<int>() ?? 0;
                        string groupIcon = IconHelper.FindOrigIcon(groupNode["groupIcon"]?.GetValue<string>());
                        bool groupHeader = groupNode["groupHeader"]?.GetValue<bool>() ?? false;
                        bool showGroupEdit = groupNode["showGroupEdit"]?.GetValue<bool>() ?? true;
                        bool showLabels = groupNode["showLabels"]?.GetValue<bool>() ?? false;
                        int labelSize = groupNode["labelSize"]?.GetValue<int>() ?? int.Parse(DEFAULT_LABEL_SIZE.ToString());
                        string labelPosition = groupNode["labelPosition"]?.GetValue<string>() ?? DEFAULT_LABEL_POSITION;
                        JsonObject paths = groupNode["path"]?.AsObject();

                        // 원본 파일 경로를 직접 사용 (temp 폴더 복사 불필요)
                        tempIcon = groupIcon;

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _viewModel.GroupHeaderIsOn = groupHeader;
                            _viewModel.ShowGroupEditIsOn = showGroupEdit;
                            if (!string.IsNullOrEmpty(groupName))
                            {
                                _viewModel.GroupName = groupName;
                            }

                            // 레이블 설정 초기화 및 설정
                            InitializeLabelSizeComboBox();
                            InitializeLabelPositionComboBox();
                            _viewModel.ShowLabelsIsOn = showLabels;
                            LabelSizeComboBox.SelectedItem = labelSize.ToString();
                            LabelPositionComboBox.SelectedItem = labelPosition.ToString();
                            // 레이블 크기 패널 상태 업데이트

                            if (_viewModel.ShowLabelsIsOn)
                            {
                                LabelSizePanel.Opacity = 1.0;
                                LabelSizeComboBox.IsEnabled = true;

                                LabelPositionPanel.Opacity = 1.0;
                                LabelPositionComboBox.IsEnabled = true;
                            }
                            else
                            {
                                LabelSizePanel.Opacity = 0.5;
                                LabelSizeComboBox.IsEnabled = false;
                                LabelPositionPanel.Opacity = 0.5;
                                LabelPositionComboBox.IsEnabled = false;
                            }
                            
                            // 헤더 텍스트 패널 상태 업데이트
                            if (_viewModel.GroupHeaderIsOn)
                            {
                                HeaderTextPanel.Opacity = 1.0;
                                ShowGroupName.IsEnabled = true;
                            }
                            else
                            {
                                HeaderTextPanel.Opacity = 0.5;
                                ShowGroupName.IsEnabled = false;
                            }
                        });

                        if (groupCol > 0)
                        {
                            if (paths != null)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    for (int i = 1; i <= paths.Count; i++)
                                    {
                                        GroupColComboBox.Items.Add(i.ToString());
                                    }
                                    GroupColComboBox.SelectedItem = groupCol.ToString();
                                });
                            }
                        }

                        if (!string.IsNullOrEmpty(groupIcon) && File.Exists(tempIcon))
                        {
                            // 파일 잠금을 완전히 방지하기 위해 파일을 먼저 메모리로 읽기
                            try
                            {
                                byte[] imageData = await File.ReadAllBytesAsync(tempIcon);

                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    selectedIconPath = tempIcon;
                                    // 파일 잠금을 방지하기 위해 메모리 스트림에서 이미지 로드
                                    BitmapImage bitmapImage = new BitmapImage();
                                    using (var memoryStream = new MemoryStream(imageData))
                                    {
                                        await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream());
                                    }
                                    IconPreviewImage.Source = bitmapImage;
                                    IconPreviewBorder.Visibility = Visibility.Visible;
                                    _viewModel.ApplicationCountText = paths.Count > 1
                                        ? paths.Count.ToString() + "개 항목"
                                        : paths.Count == 1
                                        ? "1개 항목"
                                        : "";
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to load group icon: {ex.Message}");
                                // 아이콘 로드 실패 시 기본 상태로 유지 (사용자가 새 아이콘 선택 가능)
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    selectedIconPath = string.Empty;
                                    IconPreviewBorder.Visibility = Visibility.Collapsed;
                                    _viewModel.ApplicationCountText = paths?.Count > 1
                                        ? paths.Count.ToString() + "개 항목"
                                        : paths?.Count == 1
                                        ? "1개 항목"
                                        : "";
                                });
                            }
                        }
                        else
                        {
                            // 아이콘 파일이 없는 경우 - 사용자가 새 아이콘 선택 가능
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                selectedIconPath = string.Empty;
                                IconPreviewBorder.Visibility = Visibility.Collapsed;
                                _viewModel.ApplicationCountText = paths?.Count > 1
                                    ? paths.Count.ToString() + "개 항목"
                                    : paths?.Count == 1
                                    ? "1개 항목"
                                    : "";
                            });
                        }

                        if (paths != null)
                        {
                            foreach (var path in paths)
                            {
                                string filePath = path.Key;
                                
                                // ItemType 확인 (폴더/웹 항목은 파일 존재 여부와 무관)
                                string itemTypeStr = path.Value["itemType"]?.GetValue<string>();
                                bool isFolder = itemTypeStr?.Equals("Folder", StringComparison.OrdinalIgnoreCase) == true;
                                bool isWeb = itemTypeStr?.Equals("Web", StringComparison.OrdinalIgnoreCase) == true;
                                
                                // 일반 파일, shell:AppsFolder 경로 (UWP 앱), 폴더 또는 웹 URL 확인
                                bool isValidPath = !string.IsNullOrEmpty(filePath) &&
                                    (File.Exists(filePath) || 
                                     filePath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase) ||
                                     isFolder && Directory.Exists(filePath) ||
                                     isWeb ||
                                     filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                     filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

                                if (isValidPath)
                                {
                                    Debug.WriteLine($"Icon : {filePath}");

                                    // 캐시에서 아이콘을 먼저 가져온 다음 저장된 아이콘 경로에서 가져오기 시도
                                    string icon = null;

                                    // JSON에 저장된 아이콘 경로가 있는지 확인
                                    if (path.Value.AsObject().TryGetPropertyValue("icon", out JsonNode? iconNode)
                                          && iconNode is not null
                                          && !string.IsNullOrEmpty(iconNode.GetValue<string>())
                                          && File.Exists(iconNode.GetValue<string>()))
                                    {
                                        icon = iconNode.GetValue<string>();
                                    }
                                    // UWP 앱 (shell:AppsFolder)의 경우 임시 바로가기를 생성하고 아이콘 추출
                                    else if (filePath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string aumid = filePath.Replace("shell:AppsFolder\\", "", StringComparison.OrdinalIgnoreCase);
                                        string tempDisplayName = path.Value["tooltip"]?.GetValue<string>() ?? aumid;
                                        icon = await GetAppIconFromShellAsync(aumid, tempDisplayName);
                                    }
                                    // 일반 exe 파일의 경우 아이콘 추출 시도
                                    else
                                    {
                                        icon = await IconCache.GetIconPathAsync(filePath);
                                    }

                                    await Task.Delay(10);

                                    // 표시 이름 가져오기 - 툴팁이 있으면 사용, 그렇지 않으면 경로에서 추출
                                    string displayName = path.Value["tooltip"]?.GetValue<string>();
                                    if (string.IsNullOrEmpty(displayName))
                                    {
                                        displayName = filePath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase)
                                            ? filePath.Substring(filePath.LastIndexOf('\\') + 1)
                                            : Path.GetFileName(filePath);
                                    }

                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        // ItemType 파싱 (기본값: App)
                                        ItemType itemType = ItemType.App;
                                        string itemTypeStr = path.Value["itemType"]?.GetValue<string>();
                                        if (!string.IsNullOrEmpty(itemTypeStr) && Enum.TryParse<ItemType>(itemTypeStr, true, out var parsedType))
                                        {
                                            itemType = parsedType;
                                        }

                                        _viewModel.ExeFiles.Add(new ExeFileModel
                                        {
                                            FileName = displayName,
                                            Icon = icon,
                                            FilePath = filePath,
                                            Tooltip = path.Value["tooltip"]?.GetValue<string>(),
                                            Args = path.Value["args"]?.GetValue<string>(),
                                            IconPath = icon,
                                            ItemType = itemType
                                        });
                                    });
                                }
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                IconGridComboBox.Items.Clear();
                                if (_viewModel.ExeFiles.Count >= 9)
                                {
                                    IconGridComboBox.Items.Add("2");
                                    IconGridComboBox.Items.Add("3");
                                    IconGridComboBox.SelectedItem = "3";
                                }
                                else
                                {
                                    IconGridComboBox.Items.Add("2");
                                    IconGridComboBox.SelectedItem = "2";
                                }
                                if (groupIcon.Contains("grid"))
                                {
                                    IconGridComboBox.SelectedItem = groupIcon.Contains("grid3") ? "3" : "2";
                                    regularIcon = false;
                                    IconGridComboBox.Visibility = Visibility.Visible;
                                }
                            });
                        }
                    }
                    else
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            groupName = "";
                            _viewModel.GroupHeaderIsOn = false;
                            _viewModel.GroupName = string.Empty;
                            GroupColComboBox.Items.Clear();
                            selectedIconPath = string.Empty;
                            IconPreviewImage.Source = new BitmapImage(new Uri("ms-appx:///default_preview.png"));

                            _viewModel.ApplicationCountText = string.Empty;
                            _viewModel.ExeFiles.Clear();
                            IconGridComboBox.Items.Clear();
                            IconGridComboBox.Visibility = Visibility.Collapsed;

                            // 새 그룹에 대한 레이블 설정 초기화
                            InitializeLabelSizeComboBox();
                            InitializeLabelPositionComboBox();

                            _viewModel.ShowLabelsIsOn = false;
                            LabelSizePanel.Opacity = 0.5;
                            LabelSizeComboBox.IsEnabled = false;
                            LabelPositionPanel.Opacity = 0.5;
                            LabelPositionComboBox.IsEnabled = false;

                        });
                    }
                }
                else
                {
                    // 구성 파일이 존재하지 않음 (새로 설치) - 기본값으로 초기화
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        groupName = "";
                        _viewModel.GroupHeaderIsOn = false;
                        _viewModel.GroupName = string.Empty;
                        GroupColComboBox.Items.Clear();
                        selectedIconPath = string.Empty;
                        IconPreviewImage.Source = new BitmapImage(new Uri("ms-appx:///default_preview.png"));

                        _viewModel.ApplicationCountText = string.Empty;
                        _viewModel.ExeFiles.Clear();
                        IconGridComboBox.Items.Clear();
                        IconGridComboBox.Visibility = Visibility.Collapsed;

                        // 새로 설치 시 새 그룹에 대한 레이블 설정 초기화
                        InitializeLabelSizeComboBox();
                        InitializeLabelPositionComboBox();

                        _viewModel.ShowLabelsIsOn = false;
                        LabelSizePanel.Opacity = 0.5;
                        LabelSizeComboBox.IsEnabled = false;
                    });
                }

                await Task.Run(() => Task.Delay(10));

                DispatcherQueue.TryEnqueue(() =>
                {
                    // 최종 UI 상태 설정
                    if (CustomDialog != null && CustomDialog.XamlRoot == null)
                    {
                        CustomDialog.XamlRoot = this.Content.XamlRoot;
                    }
                });
            });
        }
        private async void CreateGridIcon()
        {
            var selectedItem = IconGridComboBox.SelectedItem;
            int selectedGridSize = 2;
            if (selectedItem != null && int.TryParse(selectedItem.ToString(), out int selectedSize))
            {
                // 선택된 항목뿐만 아니라 그리드 크기까지의 모든 항목 사용
                var selectedItems = _viewModel.ExeFiles.Take(selectedSize * selectedSize).ToList();

                try
                {
                    IconHelper iconHelper = new IconHelper();
                    selectedIconPath = await iconHelper.CreateGridIconAsync(
                        selectedItems,
                        selectedSize,
                        IconPreviewImage,
                        IconPreviewBorder
                    );
                }
                catch (Exception ex)
                {
                    ShowErrorDialog("그리드 아이콘 생성 오류", ex.Message);
                    Debug.WriteLine($"Grid icon creation error: {ex.Message}");
                }
            }
            else
            {
                ShowErrorDialog("잘못된 그리드 크기", "콤보박스에서 유효한 그리드 크기를 선택하세요.");
            }
        }

        private void IconGridComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IconGridComboBox.SelectedItem != null && !regularIcon)
            {
                CreateGridIcon();
            }
        }

        private async void BrowseIconButton_Click(object sender, RoutedEventArgs e)
        {
            // 다이얼로그 상태 초기화
            if (IconSelectionOptionsPanel != null && ResourceIconGridView != null)
            {
                IconSelectionOptionsPanel.Visibility = Visibility.Visible;
                ResourceIconGridView.Visibility = Visibility.Collapsed;
            }

            ContentDialogResult result = await CustomDialog.ShowAsync();

        }

        private void CloseDialog(object sender, RoutedEventArgs e)
        {
            CustomDialog.Hide();
        }
        private void CloseEditDialog(object sender, RoutedEventArgs e)
        {
            EditItemDialog.Hide();
        }

        private void CloseCustomizeDialog(object sender, RoutedEventArgs e)
        {
            CustomizeDialog.Hide();
        }
        private void GridClick(object sender, RoutedEventArgs e)
        {
            if (ExeListView.Items.Count == 0)
            {
                regularIcon = false;
                BrowseFiles();
            }
            else
            {
                regularIcon = false;
                CreateGridIcon();
                IconGridComboBox.Visibility = Visibility.Visible;
                CustomDialog.Hide();
            }
        }

        private void ResourceClick(object sender, RoutedEventArgs e)
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
                        IconSelectionOptionsPanel.Visibility = Visibility.Collapsed;
                        ResourceIconGridView.Visibility = Visibility.Visible;
                        ResourceIconGridView.ItemsSource = iconFiles;
                    }
                    else
                    {
                        ShowErrorDialog("알림", "사용 가능한 리소스 아이콘이 없습니다.");
                    }
                }
                else
                {
                    ShowErrorDialog("오류", $"아이콘 폴더를 찾을 수 없습니다:\n{iconFolderPath}");
                }
            }
            catch (Exception ex)
            {
                ShowErrorDialog("오류", $"아이콘 로드 중 오류 발생: {ex.Message}");
            }
        }

        private void ResourceIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResourceIconGridView.SelectedItem is string iconPath)
            {
                try
                {
                    selectedIconPath = iconPath;
                    IconPreviewImage.Source = new BitmapImage(new Uri(iconPath));
                    IconPreviewBorder.Visibility = Visibility.Visible;

                    regularIcon = true;
                    IconGridComboBox.Visibility = Visibility.Collapsed;

                    // 다음 사용을 위해 UI 재설정
                    ResourceIconGridView.SelectedItem = null;
                    ResourceIconGridView.Visibility = Visibility.Collapsed;
                    IconSelectionOptionsPanel.Visibility = Visibility.Visible;
                    
                    if (CustomDialog.XamlRoot != null)
                    {
                        CustomDialog.Hide();
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorDialog("오류", $"아이콘 선택 중 오류 발생: {ex.Message}");
                }
            }
        }

        private void RegularClick(object sender, RoutedEventArgs e)
        {
            regularIcon = true;
            BrowseIcon();
        }

        private async void BrowseIcon()
        {
            try
            {
                FileOpenPicker openPicker = new FileOpenPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(openPicker, hwnd);
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".exe");
                openPicker.FileTypeFilter.Add(".url");
                openPicker.FileTypeFilter.Add(".png");
                openPicker.FileTypeFilter.Add(".ico");
                StorageFile file = await openPicker.PickSingleFileAsync();

                if (file != null)
                {
                    selectedIconPath = file.Path;
                    BitmapImage bitmapImage = new BitmapImage();
                    if (file.FileType == ".exe")
                    {
                        var iconPath = await IconCache.GetIconPathAsync(file.Path);
                        if (!string.IsNullOrEmpty(iconPath))
                        {
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

                    IconPreviewImage.Source = bitmapImage;
                    IconPreviewBorder.Visibility = Visibility.Visible;

                    if (CustomDialog.XamlRoot != null)
                    {
                        CustomDialog.Hide();
                        IconGridComboBox.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Error selecting icon", ex.Message);
            }
        }

        private async void BrowseFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            BrowseFiles();
        }

        private async void BrowseFiles()
        {
            var openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".exe");
            openPicker.FileTypeFilter.Add(".url");
            openPicker.FileTypeFilter.Add(".lnk");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hwnd);

            var files = await openPicker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;
            string icon;
            foreach (var file in files)
            {
                if (file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase))
                {
                    icon = await IconHelper.GetUrlFileIconAsync(file.Path);
                }
                else
                {
                    icon = await IconCache.GetIconPathAsync(file.Path);
                }
                _viewModel.ExeFiles.Add(new ExeFileModel { FileName = file.Name, Icon = icon, FilePath = file.Path, Tooltip = "", Args = "", IconPath = icon });
            }

            ExeListView.ItemsSource = _viewModel.ExeFiles;
            lastSelectedItem = GroupColComboBox.SelectedItem as string;
            _viewModel.ApplicationCountText = ExeListView.Items.Count > 1
                          ? ExeListView.Items.Count.ToString() + "개 항목"
                          : ExeListView.Items.Count == 1
                          ? "1개 항목"
                          : "";


            IconGridComboBox.Items.Clear();
            //if (ExeFiles.Count >= 9) {
            //    IconGridComboBox.Items.Add("2");
            //    IconGridComboBox.Items.Add("3");

            //    IconGridComboBox.SelectedItem = "3";
            //}
            //else {
            IconGridComboBox.Items.Add("2");
            IconGridComboBox.SelectedItem = "2";
            //}

            GroupColComboBox.Items.Clear();
            for (int i = 1; i <= _viewModel.ExeFiles.Count; i++)
            {
                GroupColComboBox.Items.Add(i.ToString());
            }

            if (_viewModel.ExeFiles.Count > 3)
            {
                if (lastSelectedItem != null)
                {
                    GroupColComboBox.SelectedItem = lastSelectedItem;

                }
                else
                {
                    GroupColComboBox.SelectedItem = "3";

                }
            }
            else
            {
                GroupColComboBox.SelectedItem = _viewModel.ExeFiles.Count.ToString();
            }

            if (!regularIcon)
            {
                IconGridComboBox.Visibility = Visibility.Visible;
                if (CustomDialog.XamlRoot != null)
                {
                    CustomDialog.Hide();
                }
            }
        }

        private void ExeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {


        }

        private void ExeListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Move && IconGridComboBox.SelectedItem != null && !regularIcon)
            {
                CreateGridIcon();
            }
        }


        private async void CustomizeDialog_Click(object sender, RoutedEventArgs e)
        {
            ContentDialogResult result = await CustomizeDialog.ShowAsync();

        }
        private async void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ExeFileModel item)
            {
                // 웹/폴더 항목인 경우 별도의 편집 다이얼로그 표시
                if (item.ItemType == ItemType.Folder || item.ItemType == ItemType.Web)
                {
                    await EditFolderWebItem(item);
                    return;
                }

                // 일반 앱 항목 편집
                CurrentItem = item;

                EditTitle.Text = item.FileName;
                TooltipTextBox.Text = item.Tooltip;
                ArgsTextBox.Text = item.Args;

                // 아이콘 저장을 위한 현재 그룹 경로 설정
                string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
                //string groupsFolder = Path.Combine(exeDirectory, "Groups");

                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                Directory.CreateDirectory(groupsFolder);

                string groupName = _viewModel.GroupName?.Trim() ?? string.Empty;
                string groupFolder = Path.Combine(groupsFolder, groupName);
                string uniqueFolderName = groupName;
                currentGroupPath = Path.Combine(groupFolder, uniqueFolderName);

                // 원본 아이콘 경로 저장 (exe에서 추출됨)
                originalItemIconPath = await IconCache.GetIconPathAsync(item.FilePath);
                
                // 추출 실패 시 item.Icon으로 폴백
                if (string.IsNullOrEmpty(originalItemIconPath) && !string.IsNullOrEmpty(item.Icon))
                {
                    originalItemIconPath = item.Icon;
                }
                
                // 사용 가능한 경우 기존 사용자 지정 아이콘 로드, 그렇지 않으면 원본 표시
                if (!string.IsNullOrEmpty(item.IconPath) && item.IconPath != originalItemIconPath && File.Exists(item.IconPath))
                {
                    selectedItemIconPath = item.IconPath;
                    ItemIconPreview.Source = new BitmapImage(new Uri(item.IconPath));
                }
                else if (!string.IsNullOrEmpty(originalItemIconPath) && File.Exists(originalItemIconPath))
                {
                    selectedItemIconPath = originalItemIconPath;
                    ItemIconPreview.Source = new BitmapImage(new Uri(originalItemIconPath));
                }
                else if (!string.IsNullOrEmpty(item.Icon) && File.Exists(item.Icon))
                {
                    // ListView에서 사용되는 item.Icon으로 직접 폴백
                    selectedItemIconPath = item.Icon;
                    ItemIconPreview.Source = new BitmapImage(new Uri(item.Icon));
                }
                else
                {
                    // 유효한 아이콘을 찾을 수 없는 경우 미리보기 지우기
                    ItemIconPreview.Source = null;
                }

                ContentDialogResult result = await EditItemDialog.ShowAsync();
            }
        }

        private async Task EditFolderWebItem(ExeFileModel item)
        {
            try
            {
                // 편집 모드 설정
                _isEditingFolderWebItem = true;
                _editingFolderWebItem = item;

                // 다이얼로그 초기화 - 기존 값으로 채우기
                if (item.ItemType == ItemType.Folder)
                {
                    FolderWebTypeComboBox.SelectedIndex = 0; // 폴더
                    FolderPathTextBox.Text = item.FilePath;
                    WebUrlTextBox.Text = "";
                    FolderPathPanel.Visibility = Visibility.Visible;
                    WebUrlPanel.Visibility = Visibility.Collapsed;
                }
                else // Web
                {
                    FolderWebTypeComboBox.SelectedIndex = 1; // 웹
                    WebUrlTextBox.Text = item.FilePath;
                    FolderPathTextBox.Text = "";
                    FolderPathPanel.Visibility = Visibility.Collapsed;
                    WebUrlPanel.Visibility = Visibility.Visible;
                }

                FolderWebNameTextBox.Text = item.FileName;
                selectedFolderWebIconPath = item.IconPath;

                // 아이콘 미리보기 설정
                if (!string.IsNullOrEmpty(item.Icon) && File.Exists(item.Icon))
                {
                    FolderWebIconPreview.Source = new BitmapImage(new Uri(item.Icon));
                }
                else
                {
                    FolderWebIconPreview.Source = null;
                }

                FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                await FolderWebDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error editing folder/web item: {ex.Message}");
                _isEditingFolderWebItem = false;
                _editingFolderWebItem = null;
            }
        }

        private void EditItemSave_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentItem != null)
            {
                // 모델 속성 업데이트
                CurrentItem.Tooltip = TooltipTextBox.Text;
                CurrentItem.Args = ArgsTextBox.Text;

                // 제공되고 원본과 다른 경우 아이콘 경로 저장
                if (!string.IsNullOrEmpty(selectedItemIconPath))
                {
                    if (selectedItemIconPath == originalItemIconPath)
                    {
                        // 원본으로 재설정 - 사용자 지정 아이콘 경로 지우기
                        CurrentItem.IconPath = null;
                        CurrentItem.Icon = originalItemIconPath;
                    }
                    else
                    {
                        // 사용자 지정 아이콘 선택됨
                        CurrentItem.IconPath = selectedItemIconPath;
                        CurrentItem.Icon = selectedItemIconPath;
                    }
                }

                // 항목이 변경되었음을 ListView에 알려 UI 새로 고침 강제 수행
                int index = _viewModel.ExeFiles.IndexOf(CurrentItem);
                if (index >= 0)
                {
                    _viewModel.ExeFiles.RemoveAt(index);
                    _viewModel.ExeFiles.Insert(index, CurrentItem);
                }

                // 그리드 아이콘을 사용하고 일반 아이콘을 사용하지 않는 경우, 그리드 아이콘 재생성
                if (!regularIcon && IconGridComboBox.SelectedItem != null)
                {
                    CreateGridIcon();
                }

                EditItemDialog.Hide();
            }
        }
        private async void ResetItemIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(originalItemIconPath) && File.Exists(originalItemIconPath))
                {
                    selectedItemIconPath = originalItemIconPath;
                    ItemIconPreview.Source = new BitmapImage(new Uri(originalItemIconPath));
                }
                else if (CurrentItem != null)
                {
                    // 폴백: 실행 파일에서 아이콘 다시 추출
                    string originalIcon = await IconCache.GetIconPathAsync(CurrentItem.FilePath);
                    if (!string.IsNullOrEmpty(originalIcon) && File.Exists(originalIcon))
                    {
                        selectedItemIconPath = originalIcon;
                        originalItemIconPath = originalIcon;
                        ItemIconPreview.Source = new BitmapImage(new Uri(originalIcon));
                    }
                    else if (!string.IsNullOrEmpty(CurrentItem.Icon) && File.Exists(CurrentItem.Icon))
                    {
                        // ListView의 아이콘을 폴백으로 사용
                        selectedItemIconPath = CurrentItem.Icon;
                        originalItemIconPath = CurrentItem.Icon;
                        ItemIconPreview.Source = new BitmapImage(new Uri(CurrentItem.Icon));
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog()
                {
                    Title = "오류",
                    Content = $"아이콘 초기화 실패: {ex.Message}",
                    CloseButtonText = "확인"
                };
                dialog.XamlRoot = this.Content.XamlRoot;
                await dialog.ShowAsync();
            }
        }
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ExeFileModel item)
            {
                _viewModel.ExeFiles.Remove(item);
            }

            ExeListView.ItemsSource = _viewModel.ExeFiles;
            IconGridComboBox.Items.Clear();
            _viewModel.ApplicationCountText = ExeListView.Items.Count > 0
      ? ExeListView.Items.Count.ToString() + "개 항목"
      : "항목";

            //if (ExeFiles.Count >= 9) {
            //    IconGridComboBox.Items.Add("2");
            //    IconGridComboBox.Items.Add("3");

            //    IconGridComboBox.SelectedItem = "3";
            //}
            //else {
            IconGridComboBox.Items.Add("2");
            IconGridComboBox.SelectedItem = "2";
            //}

            lastSelectedItem = GroupColComboBox.SelectedItem as string;
            GroupColComboBox.Items.Clear();

            for (int i = 1; i <= _viewModel.ExeFiles.Count; i++)
            {
                GroupColComboBox.Items.Add(i.ToString());
            }

            if (lastSelectedItem != null && int.TryParse(lastSelectedItem, out int lastSelectedIndex))
            {

                GroupColComboBox.SelectedItem = lastSelectedItem;
                if (lastSelectedIndex > _viewModel.ExeFiles.Count)
                {
                    GroupColComboBox.SelectedItem = _viewModel.ExeFiles.Count.ToString();
                }
            }



        }
        private void GroupNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 텍스트 상자를 클릭하면 InfoBar 표시

        }

        private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string newGroupName = textBox.Text;
                string oldGroupName = GetOldGroupName();
                Debug.WriteLine($"old: {oldGroupName}");
                Debug.WriteLine($"new: {newGroupName}");
                if (!string.IsNullOrEmpty(_viewModel.GroupName) &&
                    !string.IsNullOrEmpty(oldGroupName) &&
                    oldGroupName != newGroupName)
                {
                    RenameInfoBar.IsOpen = true;
                }
                else
                {
                    RenameInfoBar.IsOpen = false;
                }
            }
        }
        private async void CreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && !button.IsEnabled)
                return;

            if (button != null)
                button.IsEnabled = false;

            try
            {
                string newGroupName = _viewModel.GroupName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(newGroupName))
                {
                    await ShowDialog("오류", "그룹 이름을 입력하세요.");
                    return;
                }

                // 그룹 이름 보안 검증
                if (!IsValidGroupName(newGroupName))
                {
                    await ShowDialog("오류", "그룹 이름에 잘못된 문자가 포함되어 있습니다.");
                    return;
                }

                if (string.IsNullOrEmpty(selectedIconPath))
                {
                    await ShowDialog("오류", "아이콘을 선택하세요.");
                    return;
                }



                string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                Directory.CreateDirectory(groupsFolder);



                //string groupsFolder = Path.Combine(exeDirectory, "Groups");
                //Directory.CreateDirectory(groupsFolder);

                string oldGroupName = GetOldGroupName();
                string oldGroupFolder = Path.Combine(groupsFolder, oldGroupName);

                if (!string.IsNullOrEmpty(oldGroupName) && Directory.Exists(oldGroupFolder) && oldGroupName != newGroupName)
                {
                    Directory.Delete(oldGroupFolder, true);
                    await ShowDialog("알림", "그룹 이름을 변경하면 '작업 표시줄 강제 업데이트'를 하거나 작업 표시줄에 다시 고정해야 합니다.");
                }

                string groupFolder = Path.Combine(groupsFolder, newGroupName);
                Directory.CreateDirectory(groupFolder);

                string uniqueFolderName = newGroupName;
                string uniqueFolderPath = Path.Combine(groupFolder, uniqueFolderName);
                Directory.CreateDirectory(uniqueFolderPath);

                // selectedIconPath가 삭제될 폴더 내에 있을 수 있으므로 먼저 메모리로 읽어둠
                byte[] selectedIconData = null;
                if (File.Exists(selectedIconPath))
                {
                    selectedIconData = await File.ReadAllBytesAsync(selectedIconPath);
                }

                // 누적을 방지하기 위해 폴더 내의 오래된 아이콘 파일 삭제
                if (Directory.Exists(uniqueFolderPath))
                {
                    foreach (var oldFile in Directory.GetFiles(uniqueFolderPath, "*.ico"))
                    {
                        try { File.Delete(oldFile); } catch { }
                    }
                    foreach (var oldFile in Directory.GetFiles(uniqueFolderPath, "*.png"))
                    {
                        try { File.Delete(oldFile); } catch { }
                    }
                }

                File.SetAttributes(uniqueFolderPath, File.GetAttributes(uniqueFolderPath) | System.IO.FileAttributes.Hidden);
                string shortcutPath = Path.Combine(groupFolder, $"{newGroupName}.lnk");
                string targetPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(exeDirectory, "AppGroup.exe");

                // Windows 아이콘 캐시를 우회하기 위해 아이콘 파일 이름에 타임스탬프 추가
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string iconBaseName = $"{newGroupName}_{(regularIcon ? "regular" : (IconGridComboBox.SelectedItem?.ToString() == "3" ? "grid3" : "grid"))}_{timestamp}";
                string icoFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.ico");
                string copiedImagePath; // 변수 정의

                string originalImageExtension = Path.GetExtension(selectedIconPath);

                if (selectedIconData == null)
                {
                    await ShowDialog("오류", "아이콘 파일을 찾을 수 없습니다.");
                    return;
                }

                if (originalImageExtension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    // 이미 ICO인 경우 메모리에서 쓰기
                    await File.WriteAllBytesAsync(icoFilePath, selectedIconData);
                }
                else if (originalImageExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // EXE 파일의 경우 GetIconPathAsync를 사용하여 아이콘 추출 (PNG 반환)
                    string extractedPngPath = await IconCache.GetIconPathAsync(selectedIconPath);
                    if (!string.IsNullOrEmpty(extractedPngPath))
                    {
                        // 추출된 PNG를 대상 폴더에 저장
                        string pngFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.png");
                        File.Copy(extractedPngPath, pngFilePath, true);

                        // PNG를 ICO로 변환
                        bool iconSuccess = await IconHelper.ConvertToIco(pngFilePath, icoFilePath);
                        if (!iconSuccess)
                        {
                            await ShowDialog("오류", "PNG를 ICO 형식으로 변환하지 못했습니다.");
                            return;
                        }
                    }
                    else
                    {
                        await ShowDialog("오류", "EXE 파일에서 아이콘을 추출하지 못했습니다.");
                        return;
                    }
                }
                else
                {
                    // 다른 모든 이미지 유형 (PNG, JPG 등)의 경우 먼저 메모리에서 저장한 다음 ICO로 변환
                    string tempImagePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}{originalImageExtension}");
                    await File.WriteAllBytesAsync(tempImagePath, selectedIconData);

                    bool iconSuccess = await IconHelper.ConvertToIco(tempImagePath, icoFilePath);
                    if (!iconSuccess)
                    {
                        await ShowDialog("오류", "이미지를 ICO 형식으로 변환하지 못했습니다.");
                        return;
                    }
                }

                // 참조/향후 사용을 위해 원본 이미지 복사 (EXE 및 ICO 파일 제외)
                if (!originalImageExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !originalImageExtension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    // ICO가 아닌 이미지 유형은 위에서 이미 저장됨
                    copiedImagePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}{originalImageExtension}");
                    // 위 변환 단계에서 파일이 이미 존재함
                }
                else if (originalImageExtension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    // ICO 파일의 경우 향후 참조를 위해 PNG 복사본도 저장
                    copiedImagePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.png");
                    // ICO는 이미 저장됨, PNG 복사본은 선택적
                }

                IWshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = targetPath;
                shortcut.Arguments = $"\"{newGroupName}\"";
                shortcut.Description = $"{newGroupName} - AppGroup Shortcut";
                shortcut.IconLocation = icoFilePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();




                bool isPinned = await TaskbarManager.IsShortcutPinnedToTaskbar(oldGroupName ?? newGroupName);

                if (isPinned)
                {
                    await TaskbarManager.UpdateTaskbarShortcutIcon(oldGroupName ?? newGroupName, newGroupName, icoFilePath);

                    TaskbarManager.TryRefreshTaskbarWithoutRestartAsync();
                }

                bool groupHeader = GroupHeader.IsEnabled ? _viewModel.GroupHeaderIsOn : false;
                if (GroupColComboBox.SelectedItem != null && int.TryParse(GroupColComboBox.SelectedItem.ToString(), out int groupCol) && groupCol > 0)
                {
                    // When saving to JSON (ItemType 포함)
                    Dictionary<string, (string tooltip, string args, string icon, string itemType)> paths = _viewModel.ExeFiles.ToDictionary(
                        file => file.FilePath,
                        file => (file.Tooltip, file.Args, file.IconPath, file.ItemType.ToString())
                    );

                    // UI에서 레이블 설정 가져오기
                    bool showLabels = _viewModel.ShowLabelsIsOn;
                    int labelSize = LabelSizeComboBox.SelectedItem != null ? int.Parse(LabelSizeComboBox.SelectedItem.ToString()) : int.Parse(DEFAULT_LABEL_SIZE.ToString());
                    string? labelPosition = LabelPositionComboBox.SelectedItem != null ? LabelPositionComboBox.SelectedItem.ToString() : DEFAULT_LABEL_POSITION;
                    bool showGroupEdit = _viewModel.ShowGroupEditIsOn;

                    // 아이콘을 처리하도록 AddGroupToJson 메서드 시그니처 및 구현 업데이트
                    JsonConfigHelper.AddGroupToJson(JsonConfigHelper.GetDefaultConfigPath(), GroupId, newGroupName, groupHeader, showGroupEdit, icoFilePath, groupCol, showLabels, labelSize, labelPosition, paths);
                    ExpanderLabel.IsExpanded = false;
                    ExpanderHeader.IsExpanded = false;


                    // tempIcon이 temp 폴더에 있는 경우에만 삭제 (원본 파일 보호)
                    if (!string.IsNullOrEmpty(tempIcon) && tempIcon.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(tempIcon);
                            Console.WriteLine("TempIcon deleted successfully.");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"TempIcon delete error: {ex.Message}");
                        }
                    }

                    string[] oldFolders = Directory.GetDirectories(groupFolder);
                    foreach (string oldFolder in oldFolders)
                    {
                        if (oldFolder != uniqueFolderPath)
                        {
                            Directory.Delete(oldFolder, true);
                        }
                    }
                    IntPtr hWnd = NativeMethods.FindWindow(null, "App Group");
                    if (hWnd != IntPtr.Zero)
                    {

                        NativeMethods.SetForegroundWindow(hWnd);
                    }
                    GroupId = -1;

                    this.Hide();
                }
                else
                {
                    await ShowDialog("오류", "유효한 그룹 열 값을 선택하세요.");
                }
            }
            catch (Exception ex)
            {
                await ShowDialog("오류", $"오류가 발생했습니다: {ex.Message}");
            }
            finally
            {
                if (button != null)
                    button.IsEnabled = true;
            }
        }


        private string GetOldGroupName()
        {
            return groupName ?? "";
        }

        /// <summary>
        /// 그룹 이름이 유효한지 검증합니다 (경로 이동 공격 방지)
        /// </summary>
        /// <param name="groupName">검증할 그룹 이름</param>
        /// <returns>유효하면 true, 그렇지 않으면 false</returns>
        private static bool IsValidGroupName(string groupName) {
            if (string.IsNullOrWhiteSpace(groupName)) return false;

            // 경로 이동 공격 방지
            if (groupName.Contains("..") || groupName.Contains("/") || groupName.Contains("\\")) {
                return false;
            }

            // 파일명에 사용할 수 없는 문자 확인
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return !groupName.Any(c => invalidChars.Contains(c));
        }

        // 클래스에 다음 필드 추가
        private string selectedItemIconPath = null;
        private string currentGroupPath = null; // 그룹 폴더 경로 저장

        // 아이콘 찾아보기를 위한 메서드 추가
        private async void BrowseItemIcon_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".ico");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".exe");

            // 윈도우 핸들로 피커 초기화
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await ProcessSelectedIcon(file);
            }
        }

        private async Task ProcessSelectedIcon(StorageFile file)
        {
            try
            {
                // 그룹 디렉토리가 존재하는지 확인
                if (!string.IsNullOrEmpty(currentGroupPath) && !Directory.Exists(currentGroupPath))
                {
                    Directory.CreateDirectory(currentGroupPath);
                }

                selectedItemIconPath = file.Path;
                BitmapImage bitmapImage = new BitmapImage();

                if (file.FileType == ".exe")
                {
                    var iconPath = await IconCache.GetIconPathAsync(file.Path);
                    if (!string.IsNullOrEmpty(iconPath))
                    {
                        using (var stream = File.OpenRead(iconPath))
                        {
                            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                        }
                        selectedItemIconPath = iconPath;
                    }
                }
                else
                {
                    using (var stream = await file.OpenReadAsync())
                    {
                        await bitmapImage.SetSourceAsync(stream);
                    }
                }

                //IconPathTextBox.Text = Path.GetFileName(selectedItemIconPath);
                ItemIconPreview.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog()
                {
                    Title = "오류",
                    Content = $"아이콘 처리 실패: {ex.Message}",
                    CloseButtonText = "확인"
                };
                dialog.XamlRoot = this.Content.XamlRoot;
                await dialog.ShowAsync();
            }
        }
        private async Task<bool> ConfirmOverwrite(string path)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "덮어쓰기",
                Content = $"이 이름의 바로가기가 이미 있습니다. 바꾸시겠습니까?",
                PrimaryButtonText = "예",
                CloseButtonText = "아니오",
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        private async Task ShowDialog(string title, string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "확인",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void ShowErrorDialog(string title, string message)
        {
            await ShowDialog(title, message);
        }


        private void CloseAllAppsDialog(object sender, RoutedEventArgs e)
        {
            AllAppsDialog.Hide();
        }

        #region FolderWeb Dialog Handlers


        private string selectedFolderWebIconPath = null;

        private void CloseFolderWebDialog(object sender, RoutedEventArgs e)
        {
            // 편집 모드 초기화
            _isEditingFolderWebItem = false;
            _editingFolderWebItem = null;
            FolderWebDialog.Hide();
        }



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

        private async void CloseFolderWebIconDialog(object sender, RoutedEventArgs e)
        {
            FolderWebIconDialog.Hide();
            await Task.Delay(50);
            FolderWebDialog.XamlRoot = this.Content.XamlRoot;
            _ = FolderWebDialog.ShowAsync();
        }

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
                        await ShowDialog("알림", "사용 가능한 리소스 아이콘이 없습니다.");
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
                    await ShowDialog("오류", $"아이콘 폴더를 찾을 수 없습니다:\n{iconFolderPath}");
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
                await ShowDialog("오류", $"아이콘 로드 중 오류 발생: {ex.Message}");
                await Task.Delay(50);
                FolderWebDialog.XamlRoot = this.Content.XamlRoot;
                _ = FolderWebDialog.ShowAsync();
            }
        }

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
                    ShowErrorDialog("오류", $"아이콘 선택 중 오류 발생: {ex.Message}");
                }
            }
        }

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
                    await ShowDialog("오류", "이름을 입력하세요.");
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
                        await ShowDialog("오류", "유효한 폴더 경로를 선택하세요.");
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
                        await ShowDialog("오류", "웹 URL을 입력하세요.");
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
                    ? ExeListView.Items.Count.ToString() + "개 항목"
                    : ExeListView.Items.Count == 1
                    ? "1개 항목"
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
                await ShowDialog("오류", $"저장 중 오류가 발생했습니다: {ex.Message}");
            }
            finally
            {
                // 편집 모드 해제
                _isEditingFolderWebItem = false;
                _editingFolderWebItem = null;
            }
        }

        #endregion

        private async void AllAppsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AllAppsDialog.XamlRoot = this.Content.XamlRoot;
                AppsLoadingRing.IsActive = true;

                // 로딩 중 컨트롤 비활성화
                AllAppsListView.IsEnabled = false;
                AddSelectedAppsButton.IsEnabled = false;
                AppSearchTextBox.IsEnabled = false;

                _viewModel.InstalledApps.Clear();
                _viewModel.AllInstalledApps.Clear();
                AllAppsListView.ItemsSource = _viewModel.InstalledApps;
                AppSearchTextBox.Text = "";
                _viewModel.SelectedAppsCountText = "0개 선택됨";

                _ = AllAppsDialog.ShowAsync();

                var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // shell:AppsFolder를 통해 Windows 설치된 앱 목록 가져오기 (Win32 + UWP 모두 포함)
                var shellApps = GetAppsFromShellFolder();

                foreach (var appInfo in shellApps)
                {
                    try
                    {
                        // 중복 이름 건너뛰기
                        if (addedNames.Contains(appInfo.DisplayName)) continue;
                        addedNames.Add(appInfo.DisplayName);

                        string icon = null;

                        // exe 파일인 경우 아이콘 추출
                        if (!string.IsNullOrEmpty(appInfo.ExecutablePath) &&
                            appInfo.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(appInfo.ExecutablePath))
                        {
                            icon = await IconCache.GetIconPathAsync(appInfo.ExecutablePath);
                        }
                        // UWP 앱 또는 exe가 없는 경우 바로가기 생성 후 아이콘 추출
                        else if (!string.IsNullOrEmpty(appInfo.AppUserModelId))
                        {
                            icon = await GetAppIconFromShellAsync(appInfo.AppUserModelId, appInfo.DisplayName);
                        }

                        var app = new InstalledAppModel
                        {
                            DisplayName = appInfo.DisplayName,
                            ExecutablePath = appInfo.ExecutablePath ?? $"shell:AppsFolder\\{appInfo.AppUserModelId}",
                            Icon = icon,
                            IsSelected = false
                        };
                        app.SelectionChanged += (s, args) => UpdateSelectedAppsCount();
                        _viewModel.AllInstalledApps.Add(app);
                        _viewModel.InstalledApps.Add(app);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing app {appInfo.DisplayName}: {ex.Message}");
                    }
                }

                // 이름순 정렬
                var sorted = _viewModel.AllInstalledApps.OrderBy(a => a.DisplayName).ToList();
                _viewModel.AllInstalledApps.Clear();
                _viewModel.InstalledApps.Clear();
                foreach (var app in sorted)
                {
                    _viewModel.AllInstalledApps.Add(app);
                    _viewModel.InstalledApps.Add(app);
                }

                AppsLoadingRing.IsActive = false;

                // 로딩 완료 후 컨트롤 활성화
                AllAppsListView.IsEnabled = true;
                AddSelectedAppsButton.IsEnabled = true;
                AppSearchTextBox.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing installed apps: {ex.Message}");
                AppsLoadingRing.IsActive = false;

                // 에러 발생 시에도 컨트롤 활성화
                AllAppsListView.IsEnabled = true;
                AddSelectedAppsButton.IsEnabled = true;
                AppSearchTextBox.IsEnabled = true;
            }
        }

        // shell:AppsFolder에서 설치된 앱 목록 가져오기
        private List<(string DisplayName, string ExecutablePath, string AppUserModelId)> GetAppsFromShellFolder()
        {
            var apps = new List<(string DisplayName, string ExecutablePath, string AppUserModelId)>();

            try
            {
                // Shell.Application COM 객체를 통해 AppsFolder 접근
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return apps;

                dynamic shell = Activator.CreateInstance(shellType);
                if (shell == null) return apps;

                // shell:AppsFolder 네임스페이스 열기
                dynamic folder = shell.NameSpace("shell:AppsFolder");
                if (folder == null) return apps;

                foreach (dynamic item in folder.Items())
                {
                    try
                    {
                        string name = item.Name;
                        string path = item.Path; // AUMID 또는 exe 경로

                        if (string.IsNullOrEmpty(name)) continue;

                        // 시스템 항목 필터링
                        if (name.StartsWith("Microsoft.") &&
                            (name.Contains("Extension") || name.Contains("Client"))) continue;

                        string exePath = null;
                        string aumid = null;

                        // path가 exe 경로인지 AUMID인지 확인
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                            {
                                exePath = path;
                            }
                            else
                            {
                                // AUMID로 간주
                                aumid = path;

                                // AUMID에서 exe 경로 추출 시도 (Win32 앱의 경우)
                                exePath = TryGetExePathFromAumid(path);
                            }
                        }

                        apps.Add((name, exePath, aumid));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error enumerating shell item: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing shell:AppsFolder: {ex.Message}");
            }

            return apps;
        }

        // AUMID에서 exe 경로 추출 시도
        private string TryGetExePathFromAumid(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return null;

            try
            {
                // AUMID 형식: {PFN}!{AppId} 또는 경로 형식
                // 레지스트리에서 AUMID로 exe 경로 찾기
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"Software\Classes\ActivatableClasses\Package\{aumid.Split('!')[0]}\Server");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var exePath = subKey?.GetValue("ExePath") as string;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            return exePath;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        // UWP 앱 아이콘 추출
        private async Task<string> GetAppIconFromShellAsync(string aumid, string displayName)
        {
            try
            {
                // shell:AppsFolder 경로로 직접 IconCache 호출
                string shellPath = $"shell:AppsFolder\\{aumid}";
                var icon = await IconCache.GetIconPathAsync(shellPath);

                if (!string.IsNullOrEmpty(icon))
                {
                    Debug.WriteLine($"GetAppIconFromShellAsync: Got icon from IconCache for {displayName}");
                    return icon;
                }

                // 폴백: 임시 바로가기 생성하여 실제 아이콘 추출 (화살표 없이)
                string tempFolder = Path.Combine(Path.GetTempPath(), "AppGroup", "TempIcons");
                Directory.CreateDirectory(tempFolder);

                string tempLnkPath = Path.Combine(tempFolder, $"{SanitizeFileName(displayName)}.lnk");

                // 바로가기 생성
                IWshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(tempLnkPath);
                shortcut.TargetPath = shellPath;
                shortcut.Save();

                // 바로가기의 타겟에서 아이콘 추출 (화살표 없는 실제 아이콘)
                if (File.Exists(tempLnkPath))
                {
                    try
                    {
                        IWshShortcut savedShortcut = (IWshShortcut)wshShell.CreateShortcut(tempLnkPath);
                        string iconLocation = savedShortcut.IconLocation;
                        string targetPath = savedShortcut.TargetPath;

                        // 1. IconLocation에서 아이콘 파일 추출 시도 (exe, dll 등에서 추출)
                        if (!string.IsNullOrEmpty(iconLocation) && iconLocation != ",")
                        {
                            string[] iconInfo = iconLocation.Split(',');
                            string iconPath = iconInfo[0].Trim();

                            // IconLocation의 파일이 존재하면 해당 파일에서 아이콘 추출
                            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                            {
                                icon = await IconCache.GetIconPathAsync(iconPath);
                                if (!string.IsNullOrEmpty(icon))
                                {
                                    Debug.WriteLine($"GetAppIconFromShellAsync: Extracted from IconLocation: {iconPath}");
                                    try { File.Delete(tempLnkPath); } catch { }
                                    return icon;
                                }
                            }
                        }

                        // 2. 타겟 경로에서 직접 아이콘 추출 (화살표 없음)
                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            icon = await IconCache.GetIconPathAsync(targetPath);
                            if (!string.IsNullOrEmpty(icon))
                            {
                                Debug.WriteLine($"GetAppIconFromShellAsync: Extracted from target: {targetPath}");
                                try { File.Delete(tempLnkPath); } catch { }
                                return icon;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading shortcut properties: {ex.Message}");
                    }

                    // 3. 바로가기 파일 자체에서 아이콘 추출 (IconCache 사용 - 화살표 제거 로직 적용됨)
                    icon = await IconCache.GetIconPathAsync(tempLnkPath);

                    // 임시 파일 정리
                    try { File.Delete(tempLnkPath); } catch { }

                    if (!string.IsNullOrEmpty(icon))
                    {
                        Debug.WriteLine($"GetAppIconFromShellAsync: Extracted from shortcut file for {displayName}");
                        return icon;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting UWP app icon for {displayName}: {ex.Message}");
            }

            return null;
        }

        // 파일명에 사용할 수 없는 문자 제거
        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "app";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private string GetShortcutTarget(string shortcutPath)
        {
            try
            {
                IWshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                return shortcut.TargetPath;
            }
            catch
            {
                return null;
            }
        }

        private void AppSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = AppSearchTextBox.Text?.ToLower() ?? "";
            _viewModel.InstalledApps.Clear();

            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? _viewModel.AllInstalledApps
                : _viewModel.AllInstalledApps.Where(a => a.DisplayName.ToLower().Contains(searchText)).ToList();

            foreach (var app in filtered)
            {
                _viewModel.InstalledApps.Add(app);
            }

            UpdateSelectedAppsCount();
        }

        private void UpdateSelectedAppsCount()
        {
            var count = _viewModel.AllInstalledApps.Count(a => a.IsSelected);
            _viewModel.SelectedAppsCountText = $"{count}개 선택됨";
        }

        private async void AddSelectedApps_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = _viewModel.AllInstalledApps.Where(a => a.IsSelected).ToList();

            if (selectedApps.Count == 0)
            {
                return;
            }

            foreach (var app in selectedApps)
            {
                // 이미 추가되었는지 확인
                if (_viewModel.ExeFiles.Any(f => f.FilePath.Equals(app.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // 다시 추출하는 대신 InstalledAppModel에서 이미 추출된 아이콘 사용
                // ExecutablePath가 shell:AppsFolder\... 형식인 UWP 앱 처리
                var icon = app.Icon ?? await IconCache.GetIconPathAsync(app.ExecutablePath);
                _viewModel.ExeFiles.Add(new ExeFileModel
                {
                    FileName = string.IsNullOrEmpty(app.DisplayName) ? Path.GetFileName(app.ExecutablePath) : app.DisplayName,
                    Icon = icon,
                    FilePath = app.ExecutablePath,
                    Tooltip = app.DisplayName,
                    Args = "",
                    IconPath = icon
                });
            }

            ExeListView.ItemsSource = _viewModel.ExeFiles;
            lastSelectedItem = GroupColComboBox.SelectedItem as string;
            _viewModel.ApplicationCountText = ExeListView.Items.Count > 1
                ? ExeListView.Items.Count.ToString() + "개 항목"
                : ExeListView.Items.Count == 1
                ? "1개 항목"
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

            AllAppsDialog.Hide();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        }

        [ClassInterface(ClassInterfaceType.None)]
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        /// <summary>
        /// IDisposable 패턴 구현 - 리소스 해제
        /// </summary>
        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                // FileSystemWatcher 정리
                if (fileWatcher != null) {
                    fileWatcher.EnableRaisingEvents = false;
                    fileWatcher.Dispose();
                    fileWatcher = null;
                }

                // 임시 아이콘 폴더 정리 (temp 폴더에 있는 경우에만, 원본 폴더 보호)
                if (!string.IsNullOrEmpty(tempIcon) && tempIcon.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) {
                    try {
                        string tempFolder = Path.GetDirectoryName(tempIcon);
                        if (Directory.Exists(tempFolder)) {
                            Directory.Delete(tempFolder, true);
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Temp folder cleanup error: {ex.Message}");
                    }
                }

                // 이미지 참조 정리
                foreach (var exeFile in _viewModel.ExeFiles) {
                    exeFile.Icon = null;
                }
                _viewModel.ExeFiles.Clear();

                // 이벤트 핸들러 제거
                Activated -= EditGroupWindow_Activated;
                SizeChanged -= EditGroupWindow_SizeChanged;
                Closed -= MainWindow_Closed;
            }

            _disposed = true;
        }

        ~EditGroupWindow() {
            Dispose(disposing: false);
        }
    }
}
