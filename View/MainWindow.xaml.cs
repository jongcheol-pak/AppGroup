using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GroupItem = AppGroup.Models.GroupItem;
using StartMenuItem = AppGroup.Models.StartMenuItem;
using AppGroup.ViewModels;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup.View {
/// <summary>
/// 애플리케이션의 메인 윈도우 클래스입니다.
/// 그룹 목록 표시, 그룹 관리, 설정, 파일 감시 등의 주요 기능을 담당합니다.
/// </summary>
public sealed partial class MainWindow : WinUIEx.WindowEx, IDisposable {
// Private fields
// 열려있는 편집 윈도우들을 추적하기 위한 딕셔너리 (GroupId -> Window)
private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();
// 백업 및 복원 도우미
private BackupHelper _backupHelper;
// 메인 윈도우 뷰모델
private readonly MainWindowViewModel _viewModel;
// 설정 파일 변경 감시자
private FileSystemWatcher _fileWatcher;
// 로딩 동기화를 위한 락 객체
private readonly object _loadLock = new object();
private readonly IconHelper _iconHelper;
// 검색 필터링 디바운스 타이머
private DispatcherTimer debounceTimer;
private DispatcherTimer startMenuDebounceTimer;
private bool _disposed = false;

    /// <summary>
    /// MainWindow 생성자
    /// </summary>
    public MainWindow() {
        InitializeComponent();

        _backupHelper = new BackupHelper(this);

        _viewModel = new MainWindowViewModel();
        if (Content is FrameworkElement rootElement) {
            rootElement.DataContext = _viewModel;
        }
        _iconHelper = new IconHelper();

        // 윈도우 초기 설정
        this.CenterOnScreen();
        this.MinHeight = 600;
        this.MinWidth = 530;

        this.ExtendsContentIntoTitleBar = true;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

        this.AppWindow.SetIcon(iconPath);

        // 비동기로 그룹 목록 로드 시작
        _ = LoadGroupsAsync();

        // 비동기로 시작 메뉴 목록 로드 시작
        _ = LoadStartMenuItemsAsync();

        // 설정 파일 감시 설정
        SetupFileWatcher();

        ThemeHelper.UpdateTitleBarColors(this);
        debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        debounceTimer.Tick += FilterGroups;
        startMenuDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        startMenuDebounceTimer.Tick += FilterStartMenuItems;

        // 작업 표시줄 그룹화 ID 설정
        NativeMethods.SetCurrentProcessExplicitAppUserModelID("AppGroup.Main");
            
        this.AppWindow.Closing += AppWindow_Closing;
        SetWindowIcon();
    }


        private void SetWindowIcon() {
            try {
                // 윈도우 핸들 가져오기
                IntPtr hWnd = WindowNative.GetWindowHandle(this);

                // 내장 리소스에서 아이콘 로드 먼저 시도
                var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

                if (File.Exists(iconPath)) {
                    // Win32 API를 사용하여 아이콘 로드 및 설정
                    IntPtr hIcon = NativeMethods.LoadIcon(iconPath);
                    if (hIcon != IntPtr.Zero) {
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_SMALL, hIcon);
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_BIG, hIcon);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) {
            args.Cancel = true;
            try {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(this.Content.XamlRoot);
                foreach (var popup in popups) {
                    if (popup.Child is ContentDialog dialog) {
                        dialog.Hide();
                    }
                }
            }
            catch {
                // 폴백 - 일부 대화 상자는 팝업에 없을 수 있음
            }
            this.Hide();        // 창 숨기기
        }
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            debounceTimer.Stop();
            debounceTimer.Start();
        }

        private void FilterGroups(object sender, object e) {
            debounceTimer.Stop();
            _viewModel.SearchText = SearchTextBox.Text;
            _viewModel.ApplyFilter();
            UpdateGroupCountAndEmptyState();
        }

        private void UpdateGroupCountAndEmptyState() {
            var count = _viewModel.FilteredGroupItems.Count;
            _viewModel.GroupsCountText = count > 1
                ? count + "개 그룹"
                : count == 1
                ? "1개 그룹"
                : "";
            _viewModel.EmptyViewVisibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }




        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        
        /// <summary>
        /// JSON 파일 변경 시 그룹 아이템을 업데이트합니다.
        /// </summary>
        /// <param name="jsonFilePath">JSON 설정 파일 경로</param>
        public async Task UpdateGroupItemAsync(string jsonFilePath) {
            await _semaphore.WaitAsync();
            try {
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                // 딕셔너리의 각 항목 처리
                var tasks = groupDictionary.Select(async property => {
                    if (int.TryParse(property.Key, out int groupId)) {
                        var existingItem = _viewModel.GroupItems.FirstOrDefault(item => item.GroupId == groupId);
                        if (existingItem != null) {
                            // 기존 아이템 업데이트 Logic
                            string newGroupName = property.Value?["groupName"]?.GetValue<string>();
                            string newGroupIcon = property.Value?["groupIcon"]?.GetValue<string>();

                            existingItem.GroupName = newGroupName;
                            existingItem.GroupIcon = null; // 아이콘 갱신을 위해 null 할당
                            existingItem.GroupIcon = IconHelper.FindOrigIcon(newGroupIcon);

                            // 경로 및 아이콘 업데이트
                            var paths = property.Value?["path"]?.AsObject();
                            if (paths?.Count > 0) {
                                var iconTasks = paths
                                    .Where(p => p.Value != null)
                                    .Select(async path => {
                                        string filePath = path.Key;
                                        string tooltip = path.Value["tooltip"]?.GetValue<string>();
                                        string args = path.Value["args"]?.GetValue<string>();
                                        string customIcon = path.Value["icon"]?.GetValue<string>(); // 사용자 지정 아이콘

                                        existingItem.Tooltips[filePath] = tooltip;
                                        existingItem.Args[filePath] = args;
                                        existingItem.CustomIcons[filePath] = customIcon;

                                        // 사용자 지정 아이콘이 있으면 사용, 없으면 파일 자체 아이콘 추출
                                        if (!string.IsNullOrEmpty(customIcon) && File.Exists(customIcon)) {
                                            return customIcon;
                                        }
                                        else {
                                            string icon;
                                            if (Path.GetExtension(filePath).Equals(".url", StringComparison.OrdinalIgnoreCase)) {
                                                icon = await IconHelper.GetUrlFileIconAsync(filePath);
                                            }
                                            else {
                                                icon = await IconCache.GetIconPathAsync(filePath);
                                            }

                                            return icon;
                                        }
                                    })
                                    .ToList();

                                var iconPaths = await Task.WhenAll(iconTasks);
                                var validIconPaths = iconPaths.Where(p => !string.IsNullOrEmpty(p)).ToList();

                                // 최대 7개까지만 아이콘 표시
                                int maxIconsToShow = 7;
                                existingItem.PathIcons = validIconPaths.Take(maxIconsToShow).ToList();
                                existingItem.AdditionalIconsCount = Math.Max(0, validIconPaths.Count - maxIconsToShow);
                            }
                        }
                        else {
                            // 새 아이템 생성
                            var newItem = await CreateGroupItemAsync(groupId, property.Value);
                            _viewModel.GroupItems.Add(newItem);
                        }
                    }
                }).ToList();

                await Task.WhenAll(tasks);

                // 루프 외부에서 UI 업데이트 수행 (중복 업데이트 방지)
                _viewModel.ApplyFilter();
                UpdateGroupCountAndEmptyState();
            }
            finally {
                _semaphore.Release();
            }
        }
        private bool _isReordering = false;

        // 드래그 중인 파일 추적용 딕셔너리
        private readonly Dictionary<int, string> _tempDragFiles = new Dictionary<int, string>();

        /// <summary>
        /// 그룹 이름이 파일 경로에 사용하기에 안전한지 검증합니다.
        /// 경로 트래버설 공격(../, ..\) 및 잘못된 문자를 방지합니다.
        /// </summary>
        /// <param name="groupName">검증할 그룹 이름</param>
        /// <returns>안전하면 true, 위험하면 false</returns>
        private static bool IsValidGroupName(string groupName) {
            if (string.IsNullOrWhiteSpace(groupName)) {
                return false;
            }

            // 경로 트래버설 패턴 검사
            if (groupName.Contains("..") || groupName.Contains("/") || groupName.Contains("\\")) {
                System.Diagnostics.Debug.WriteLine($"Invalid group name detected (path traversal): {groupName}");
                return false;
            }

            // 파일명에 사용할 수 없는 문자 검사
            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (groupName.IndexOfAny(invalidChars) >= 0) {
                System.Diagnostics.Debug.WriteLine($"Invalid group name detected (invalid chars): {groupName}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 드래그 시작 시 호출됩니다. 내부 재정렬 및 외부로의 드래그를 처리합니다.
        /// </summary>
        private async void GroupListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e) {
            // 재정렬 중 파일 감시자가 간섭하지 않도록 플래그 설정
            _isReordering = true;

            // 드래그된 아이템 참조 저장
            if (e.Items.Count > 0 && e.Items[0] is GroupItem draggedItem) {
                e.Data.Properties.Add("DraggedGroupId", draggedItem.GroupId);

                // 내부 재정렬을 위한 기본 데이터 설정
                e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move | DataPackageOperation.Link;

                // 그룹 이름 보안 검증 (경로 트래버설 공격 방지)
                if (!IsValidGroupName(draggedItem.GroupName)) {
                    System.Diagnostics.Debug.WriteLine($"Drag cancelled: invalid group name '{draggedItem.GroupName}'");
                    return;
                }

                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                // 외부 드롭을 위한 바로가기 파일 준비
                string shortcutPath = Path.Combine(appDataPath, "Groups", draggedItem.GroupName, $"{draggedItem.GroupName}.lnk");
                string fullShortcutPath = Path.GetFullPath(shortcutPath);
                if (File.Exists(fullShortcutPath)) {
                    try {
                        // 임시 위치로 복사
                        string tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup", "DragTemp");
                        Directory.CreateDirectory(tempDir);
                        string tempShortcutPath = Path.Combine(tempDir, $"{draggedItem.GroupName}.lnk");

                        if (File.Exists(tempShortcutPath)) {
                            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            tempShortcutPath = Path.Combine(tempDir, $"{draggedItem.GroupName}_{timestamp}.lnk");
                        }

                        File.Copy(fullShortcutPath, tempShortcutPath, true);
                        _tempDragFiles[draggedItem.GroupId] = tempShortcutPath;

                        // 텍스트 데이터 즉시 설정 (재정렬을 중단하지 않음)
                        e.Data.SetText(fullShortcutPath);

                        // StorageItems에 SetDataProvider 사용 - 외부 대상이 요청할 때만 데이터 제공
                        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async (request) => {
                            var deferral = request.GetDeferral();
                            try {
                                var tempFolder = await StorageFolder.GetFolderFromPathAsync(tempDir);
                                var tempFile = await tempFolder.GetFileAsync(Path.GetFileName(tempShortcutPath));
                                request.SetData(new List<IStorageItem> { tempFile });
                                System.Diagnostics.Debug.WriteLine($"Provided storage items for external drop: {tempShortcutPath}");
                            }
                            catch (Exception ex) {
                                System.Diagnostics.Debug.WriteLine($"Error providing storage items: {ex.Message}");
                            }
                            finally {
                                deferral.Complete();
                            }
                        });

                        System.Diagnostics.Debug.WriteLine($"Prepared conditional drag data for: {tempShortcutPath}");
                    }
                    catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"Error preparing drag data: {ex.Message}");
                    }
                }
            }
        }

        private async void GroupListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            try {
                // 모든 드래그된 항목에 대한 임시 파일 정리
                foreach (var item in args.Items) {
                    if (item is GroupItem groupItem && _tempDragFiles.ContainsKey(groupItem.GroupId)) {
                        string tempFilePath = _tempDragFiles[groupItem.GroupId];
                        if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath)) {
                            try {
                                File.Delete(tempFilePath);
                                System.Diagnostics.Debug.WriteLine($"Cleaned up temp file: {tempFilePath}");
                            }
                            catch (Exception cleanupEx) {
                                System.Diagnostics.Debug.WriteLine($"Error cleaning up temp file: {cleanupEx.Message}");
                            }
                        }
                        _tempDragFiles.Remove(groupItem.GroupId);
                    }
                }

                // 재정렬 플래그 초기화
                _isReordering = false;

                if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move) {
                    // ListView의 현재 항목 순서 가져오기
                    var reorderedItems = new List<GroupItem>();
                    for (int i = 0; i < GroupListView.Items.Count; i++) {
                        if (GroupListView.Items[i] is GroupItem item) {
                            reorderedItems.Add(item);
                        }
                    }

                    // 새 순서로 JSON 파일 업데이트
                    await UpdateJsonWithNewOrderAsync(reorderedItems);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error during drag completion: {ex.Message}");
                // 올바른 상태를 복원하기 위해 그룹 다시 로드
                _ = LoadGroupsAsync();
            }
        }

        private async Task UpdateJsonWithNewOrderAsync(List<GroupItem> reorderedItems) {
            try {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();

                // 재귀 업데이트를 방지하기 위해 파일 감시자를 일시적으로 비활성화
                _fileWatcher.EnableRaisingEvents = false;

                // 현재 JSON 콘텐츠 읽기
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                // 정렬된 새 JSON 객체 생성
                var newJsonObject = new JsonObject();

                // 순서를 보존하기 위해 새 순차 ID 매핑 생성
                var orderMapping = new Dictionary<int, int>();
                for (int i = 0; i < reorderedItems.Count; i++) {
                    int newId = i + 1; // 1부터 시작
                    int oldId = reorderedItems[i].GroupId;
                    orderMapping[oldId] = newId;
                }

                // 새 순서와 ID로 JSON 다시 빌드
                for (int i = 0; i < reorderedItems.Count; i++) {
                    var item = reorderedItems[i];
                    int newId = i + 1;
                    string oldKey = item.GroupId.ToString();
                    string newKey = newId.ToString();

                    if (groupDictionary.ContainsKey(oldKey)) {
                        var groupData = groupDictionary[oldKey];
                        newJsonObject[newKey] = groupData?.DeepClone();
                    }
                }

                // 업데이트된 JSON을 파일에 다시 쓰기
                string updatedJsonContent = newJsonObject.ToJsonString(new JsonSerializerOptions {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(jsonFilePath, updatedJsonContent);

                // 새 ID와 일치하도록 ObservableCollection의 GroupId 속성 업데이트
                for (int i = 0; i < reorderedItems.Count; i++) {
                    reorderedItems[i].GroupId = i + 1;
                }

                // 파일 쓰기가 완료되도록 잠시 지연
                await Task.Delay(100);

                // 파일 감시자 다시 활성화
                _fileWatcher.EnableRaisingEvents = true;

                Debug.WriteLine("JSON file updated with new group order");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error updating JSON with new order: {ex.Message}");
                // 오류 발생 시 파일 감시자 다시 활성화
                _fileWatcher.EnableRaisingEvents = true;
                throw;
            }
        }

        /// <summary>
        /// 설정 파일 변경 감시자를 설정합니다.
        /// </summary>
        private void SetupFileWatcher() {
            string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
            string directoryPath = Path.GetDirectoryName(jsonFilePath);
            string fileName = Path.GetFileName(jsonFilePath);

            _fileWatcher = new FileSystemWatcher(directoryPath, fileName) {
                NotifyFilter = NotifyFilters.LastWrite
            };

            // 람다식 대신 명명된 메서드로 이벤트 핸들러 등록 (정확한 해제를 위해)
            _fileWatcher.Changed += OnFileWatcherChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// 설정 파일 변경 시 호출되는 이벤트 핸들러
        /// </summary>
        private void OnFileWatcherChanged(object sender, FileSystemEventArgs e) {
            // 재정렬 중에는 파일 감시자 업데이트를 스킵하여 충돌 방지
            if (_isReordering || _disposed) return;

            string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
            DispatcherQueue.TryEnqueue(async () => {
                if (!_disposed && !IsFileInUse(jsonFilePath)) {
                    await UpdateGroupItemAsync(jsonFilePath);
                }
            });
        }

        // 선택 사항: 현재 순서를 수동으로 저장하는 메서드 추가
        private async void SaveCurrentOrder() {
            var currentItems = _viewModel.GroupItems.ToList();
            await UpdateJsonWithNewOrderAsync(currentItems);
        }


        private bool IsFileInUse(string filePath) {
            try {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    fs.Close();
                }
                return false;
            }
            catch (IOException) {
                return true;
            }
        }

        private async void Reload(object sender, RoutedEventArgs e) {
            _ = LoadGroupsAsync();


        }


        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _loadCancellationSource = new CancellationTokenSource();




        private async Task<List<GroupItem>> ProcessGroupsInParallelAsync(
            JsonObject groupDictionary,
            CancellationToken cancellationToken) {
            var options = new ParallelOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var newGroupItems = new ConcurrentBag<GroupItem>();

            await Parallel.ForEachAsync(
                groupDictionary,
                options,
                async (property, token) => {
                    if (int.TryParse(property.Key, out int groupId)) {
                        try {
                            var groupItem = await CreateGroupItemAsync(groupId, property.Value);
                            newGroupItems.Add(groupItem);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error processing group {groupId}: {ex.Message}");
                        }
                    }
                });

            return newGroupItems
                 .OrderBy(g => g.GroupId)
                .ToList();
        }

        private async Task<List<GroupItem>> ProcessGroupsSequentiallyAsync(
            JsonObject groupDictionary,
            CancellationToken cancellationToken) {
            var newGroupItems = new List<GroupItem>();

            foreach (var property in groupDictionary) {
                cancellationToken.ThrowIfCancellationRequested();

                if (int.TryParse(property.Key, out int groupId)) {
                    try {
                        var groupItem = await CreateGroupItemAsync(groupId, property.Value);

                        newGroupItems.Add(groupItem);
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error processing group {groupId}: {ex.Message}");
                    }
                }
            }

            return newGroupItems
        .OrderBy(g => g.GroupId)
        .ToList();
        }

        private void HandleLoadingError(Exception ex) {
            Debug.WriteLine($"Critical error loading groups: {ex.Message}");

            DispatcherQueue.TryEnqueue(() => {
            });
        }
        public async Task LoadGroupsAsync() {
            if (!await _loadingSemaphore.WaitAsync(0)) {
                return;
            }

            try {
                using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_loadCancellationSource.Token);
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                DispatcherQueue.TryEnqueue(() =>
                {
                    _viewModel.GroupItems.Clear();
                });

                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath, cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                var processingStrategy = groupDictionary.Count >= 5
                    ? ProcessGroupsInParallelAsync(groupDictionary, cancellationTokenSource.Token)
                    : ProcessGroupsSequentiallyAsync(groupDictionary, cancellationTokenSource.Token);

                var updatedGroupItems = await processingStrategy
                    .ConfigureAwait(false);

                DispatcherQueue.TryEnqueue(async () =>
                {
                    _viewModel.GroupItems.Clear();
                    foreach (var item in updatedGroupItems) {
                        // Check if the item already exists in GroupItems
                        if (!_viewModel.GroupItems.Any(existingItem => existingItem.GroupId == item.GroupId)) {
                            _viewModel.GroupItems.Add(item);
                            // EmptyViewVisibility는 ViewModel에서 관리
                        }


                    }
                    _viewModel.ApplyFilter();
                    UpdateGroupCountAndEmptyState();


                });
            }
            catch (OperationCanceledException) {
                Debug.WriteLine("Group loading timed out.");
            }
            catch (Exception ex) {
                HandleLoadingError(ex);
            }
            finally {
                _loadingSemaphore.Release();
            }
        }



        private async Task<GroupItem> CreateGroupItemAsync(int groupId, JsonNode groupNode) {
            string groupName = groupNode?["groupName"]?.GetValue<string>();
            string groupIcon = IconHelper.FindOrigIcon(groupNode?["groupIcon"]?.GetValue<string>());

            var groupItem = new GroupItem {
                GroupId = groupId,
                GroupName = groupName,
                GroupIcon = groupIcon,
                PathIcons = new List<string>(),
                Tooltips = new Dictionary<string, string>(),
                Args = new Dictionary<string, string>(),
                CustomIcons = new Dictionary<string, string>() // 사용자 지정 아이콘 초기화
            };

            var paths = groupNode?["path"]?.AsObject();
            if (paths?.Count > 0) {
                string outputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup",
                    "Icons"
                );
                Directory.CreateDirectory(outputDirectory);

                var iconTasks = paths
                    .Where(p => p.Value != null)
                    .Select(async path => {
                        string filePath = path.Key;
                        string tooltip = path.Value["tooltip"]?.GetValue<string>();
                        string args = path.Value["args"]?.GetValue<string>();
                        string customIcon = path.Value["icon"]?.GetValue<string>(); // JSON에서 사용자 지정 아이콘 가져오기

                        groupItem.Tooltips[filePath] = tooltip;
                        groupItem.Args[filePath] = args;
                        groupItem.CustomIcons[filePath] = customIcon; // 사용자 지정 아이콘 저장

                        // 사용자 지정 아이콘을 사용할 수 있고 존재하는 경우 사용, 그렇지 않으면 캐시된 아이콘 사용
                        if (!string.IsNullOrEmpty(customIcon) && File.Exists(customIcon)) {
                            return customIcon;
                        }
                        else {
                            // 존재하지 않는 경우 아이콘 재생성 강제 수행
                            string cachedIconPath;
                            if (Path.GetExtension(filePath).Equals(".url", StringComparison.OrdinalIgnoreCase)) {
                                cachedIconPath = await IconHelper.GetUrlFileIconAsync(filePath);
                            }
                            else {
                                cachedIconPath = await IconCache.GetIconPathAsync(filePath);
                            }
                            // 아이콘이 실제로 생성되었는지 확인하기 위한 추가 검증
                            if (string.IsNullOrEmpty(cachedIconPath) || !File.Exists(cachedIconPath)) {
                                cachedIconPath = await ReGenerateIconAsync(filePath, outputDirectory);
                            }

                            return cachedIconPath;
                        }
                    })
                    .ToList();

                var iconPaths = await Task.WhenAll(iconTasks);
                var validIconPaths = iconPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();
 
                // 아이콘 7개로 제한
                int maxIconsToShow = 7;
                groupItem.PathIcons.AddRange(validIconPaths.Take(maxIconsToShow));
                groupItem.AdditionalIconsCount = Math.Max(0, validIconPaths.Count - maxIconsToShow);
            }

            return groupItem;
        }
        private async Task<string> ReGenerateIconAsync(string filePath, string outputDirectory) {
            try {
                // 아이콘 재생성 강제 수행
                var regeneratedIconPath = await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

                if (regeneratedIconPath != null && File.Exists(regeneratedIconPath)) {
                    // 캐시 키 계산 및 캐시 업데이트
                    string cacheKey = IconCache.ComputeFileCacheKey(filePath);
                    IconCache.IconCacheData.TryAdd(cacheKey, regeneratedIconPath);
                    IconCache.SaveIconCache();

                    return regeneratedIconPath;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Icon regeneration failed for {filePath}: {ex.Message}");
            }

            return null;
        }



        private async void ExportBackupButton_Click(object sender, RoutedEventArgs e) {
            await _backupHelper.ExportBackupAsync();
        }

        private async void ImportBackupButton_Click(object sender, RoutedEventArgs e) {
            await _backupHelper.ImportBackupAsync();
        }

        private void ForceTaskbarUpdate_Click(object sender, RoutedEventArgs e) {

             TaskbarManager.ForceTaskbarUpdateAsync();

        }

        private void AddGroup(object sender, RoutedEventArgs e) {
            int groupId = JsonConfigHelper.GetNextGroupId();
            AppPaths.SaveGroupIdToFile(groupId.ToString());
            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", groupId);
            editGroup.Activate();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is GroupItem selectedGroup) {
                AppPaths.SaveGroupIdToFile(selectedGroup.GroupId.ToString());
                EditGroupHelper editGroup = new EditGroupHelper("Edit Group", selectedGroup.GroupId);
                editGroup.Activate();
            }
        }
        private async void DeleteButton_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup) {
                ContentDialog deleteDialog = new ContentDialog {
                    Title = "삭제",
                    Content = $"\"{selectedGroup.GroupName}\" 그룹을 삭제하시겠습니까?",
                    PrimaryButtonText = "삭제",
                    CloseButtonText = "취소",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await deleteDialog.ShowAsync();
                if (result == ContentDialogResult.Primary) {
                    string filePath = JsonConfigHelper.GetDefaultConfigPath();
                    JsonConfigHelper.DeleteGroupFromJson(filePath, selectedGroup.GroupId);
                    await LoadGroupsAsync();
                }
            }
        }

        // MainWindow.xaml.cs의 MainWindow 클래스에 이 메서드 추가

        private async void SettingsButton_Click(object sender, RoutedEventArgs e) {
            try {
                SettingsDialog settingsDialog = new SettingsDialog {
                    XamlRoot = this.Content.XamlRoot
                };

                await settingsDialog.ShowAsync();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error showing settings dialog: {ex.Message}");

                // Optional: Show an error message to the user
                ContentDialog errorDialog = new ContentDialog {
                    Title = "오류",
                    Content = "설정 창을 열지 못했습니다.",
                    CloseButtonText = "확인",
                    XamlRoot = this.Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }
        private async void DuplicateButton_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup) {
                string filePath = JsonConfigHelper.GetDefaultConfigPath();
                JsonConfigHelper.DuplicateGroupInJson(filePath, selectedGroup.GroupId);
                await LoadGroupsAsync();
            }
        }
        private void OpenLocationButton_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup) {
                JsonConfigHelper.OpenGroupFolder(selectedGroup.GroupId);
            }
        }




        private void GroupListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (GroupListView.SelectedItem is GroupItem selectedGroup) {
                AppPaths.SaveGroupIdToFile(selectedGroup.GroupId.ToString());
                EditGroupHelper editGroup = new EditGroupHelper("Edit Group", selectedGroup.GroupId);
                editGroup.Activate();
            }
        }

        /// <summary>
        /// 비관리 리소스를 해제하고 관리 리소스를 삭제합니다.
        /// </summary>
        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                // FileSystemWatcher 정리 - 이벤트 핸들러 명시적 해제
                if (_fileWatcher != null) {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Changed -= OnFileWatcherChanged;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }

                // SemaphoreSlim 인스턴스 정리
                _semaphore?.Dispose();
                _loadingSemaphore?.Dispose();

                // CancellationTokenSource 정리
                if (_loadCancellationSource != null && !_loadCancellationSource.IsCancellationRequested) {
                    _loadCancellationSource.Cancel();
                }
                _loadCancellationSource?.Dispose();

                // 디바운스 타이머 정리
                if (debounceTimer != null) {
                    debounceTimer.Stop();
                    debounceTimer.Tick -= FilterGroups;
                    debounceTimer = null;
                }

                if (startMenuDebounceTimer != null) {
                    startMenuDebounceTimer.Stop();
                    startMenuDebounceTimer.Tick -= FilterStartMenuItems;
                    startMenuDebounceTimer = null;
                }

                // 편집 윈도우 딕셔너리 정리
                _openEditWindows?.Clear();
            }

            _disposed = true;
        }

        ~MainWindow() {
            Dispose(disposing: false);
        }

        #region 시작 메뉴 관련 메서드

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();
                if (tag == "Taskbar")
                {
                    if (TaskbarContent != null) TaskbarContent.Visibility = Visibility.Visible;
                    if (StartMenuContent != null) StartMenuContent.Visibility = Visibility.Collapsed;
                    if (SettingsContent != null) SettingsContent.Visibility = Visibility.Collapsed;
                }
                else if (tag == "StartMenu")
                {
                    if (TaskbarContent != null) TaskbarContent.Visibility = Visibility.Collapsed;
                    if (StartMenuContent != null) StartMenuContent.Visibility = Visibility.Visible;
                    if (SettingsContent != null) SettingsContent.Visibility = Visibility.Collapsed;
                }
                else if (tag == "Settings")
                {
                    if (TaskbarContent != null) TaskbarContent.Visibility = Visibility.Collapsed;
                    if (StartMenuContent != null) StartMenuContent.Visibility = Visibility.Collapsed;
                    if (SettingsContent != null) SettingsContent.Visibility = Visibility.Visible;
                    _ = LoadSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 시작 메뉴 설정 버튼 클릭 이벤트 핸들러
        /// </summary>
        private async void StartMenuSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new StartMenuSettingsDialog
                {
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"시작 메뉴 설정 다이얼로그 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 메뉴 검색 텍스트 변경 이벤트 핸들러
        /// </summary>
        private void StartMenuSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            startMenuDebounceTimer.Stop();
            startMenuDebounceTimer.Start();
        }

        /// <summary>
        /// 시작 메뉴 필터링 메서드
        /// </summary>
        private void FilterStartMenuItems(object sender, object e)
        {
            startMenuDebounceTimer.Stop();
            _viewModel.StartMenuSearchText = StartMenuSearchTextBox.Text;
            _viewModel.ApplyStartMenuFilter();
            UpdateStartMenuCountAndEmptyState();
        }

        /// <summary>
        /// 시작 메뉴 항목 수와 빈 상태 표시를 업데이트합니다
        /// </summary>
        private void UpdateStartMenuCountAndEmptyState()
        {
            var count = _viewModel.FilteredStartMenuItems.Count;
            _viewModel.StartMenuCountText = count > 1
                ? count + "개 폴더"
                : count == 1
                ? "1개 폴더"
                : "";
            _viewModel.StartMenuEmptyViewVisibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 시작 메뉴 폴더 목록을 로드합니다
        /// </summary>
        private async Task LoadStartMenuItemsAsync()
        {
            try
            {
                var folders = await JsonConfigHelper.LoadStartMenuFoldersAsync();

                DispatcherQueue.TryEnqueue(() =>
                {
                    _viewModel.StartMenuItems.Clear();
                    foreach (var folder in folders)
                    {
                        _viewModel.StartMenuItems.Add(folder);
                    }
                    _viewModel.ApplyStartMenuFilter();
                    UpdateStartMenuCountAndEmptyState();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"시작 메뉴 폴더 로드 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 메뉴 Grid 드래그 오버 이벤트 핸들러
        /// </summary>
        private void StartMenuGrid_DragOver(object sender, DragEventArgs e)
        {
            // StorageItems 포함 여부만 동기적으로 확인하고, 폴더 검증은 Drop에서 수행
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        /// <summary>
        /// 시작 메뉴 Grid 드롭 이벤트 핸들러
        /// </summary>
        private async void StartMenuGrid_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count > 0)
                    {
                        foreach (var item in items)
                        {
                            if (item is StorageFolder folder)
                            {
                                // 폴더인 경우 JSON에 추가
                                JsonConfigHelper.AddStartMenuFolder(folder.Path, folder.Name);
                            }
                        }

                        // 목록 다시 로드
                        await LoadStartMenuItemsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"드롭 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 시작 메뉴 폴더 위치 열기 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void StartMenuOpenLocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is StartMenuItem selectedFolder)
            {
                if (Directory.Exists(selectedFolder.FolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = selectedFolder.FolderPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Debug.WriteLine($"폴더가 존재하지 않습니다: {selectedFolder.FolderPath}");
                }
            }
        }

        /// <summary>
        /// 시작 메뉴 폴더 삭제 버튼 클릭 이벤트 핸들러
        /// </summary>
        private async void StartMenuDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is StartMenuItem selectedFolder)
            {
                ContentDialog deleteDialog = new ContentDialog
                {
                    Title = "삭제",
                    Content = $"\"{selectedFolder.FolderName}\" 폴더를 삭제하시겠습니까?",
                    PrimaryButtonText = "삭제",
                    CloseButtonText = "취소",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await deleteDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    JsonConfigHelper.DeleteStartMenuFolder(selectedFolder.FolderId);
                    await LoadStartMenuItemsAsync();
                }
            }
        }

        #region 시작 메뉴 폴더 수정 다이얼로그

        // 현재 수정 중인 폴더 정보
        private StartMenuItem _editingFolder;
        private string _selectedFolderIconPath;

        /// <summary>
        /// 시작 메뉴 폴더 수정 버튼 클릭 이벤트 핸들러
        /// </summary>
        private async void EditStartMenuButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("=== EditStartMenuButton_Click 시작 ===");
            try
            {
                Debug.WriteLine($"sender 타입: {sender?.GetType().Name}");
                
                if (sender is Button button)
                {
                    Debug.WriteLine($"DataContext 타입: {button.DataContext?.GetType().Name}");
                    
                    if (button.DataContext is StartMenuItem selectedFolder)
                    {
                        Debug.WriteLine($"선택된 폴더: {selectedFolder.FolderName}");
                        
                        _editingFolder = selectedFolder;
                        _selectedFolderIconPath = selectedFolder.FolderIcon;

                        // 다이얼로그 UI 초기화
                        Debug.WriteLine("UI 초기화 중...");
                        FolderNameTextBox.Text = selectedFolder.FolderName;
                        FolderPathTextBlock.Text = selectedFolder.FolderPath;

                        // 아이콘 로드
                        Debug.WriteLine($"아이콘 로드 중: {selectedFolder.FolderIcon}");
                        LoadFolderIconPreview(selectedFolder.FolderIcon);

                        // 다이얼로그 표시
                        Debug.WriteLine("다이얼로그 표시 중...");
                        EditStartMenuDialog.XamlRoot = this.Content.XamlRoot;
                        var result = await EditStartMenuDialog.ShowAsync();
                        Debug.WriteLine($"다이얼로그 결과: {result}");
                    }
                    else
                    {
                        Debug.WriteLine("DataContext가 StartMenuItem이 아닙니다!");
                    }
                }
                else
                {
                    Debug.WriteLine("sender가 Button이 아닙니다!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EditStartMenuButton_Click 오류: {ex.Message}");
                Debug.WriteLine($"스택: {ex.StackTrace}");
            }
            Debug.WriteLine("=== EditStartMenuButton_Click 종료 ===");
        }

        /// <summary>
        /// 폴더 아이콘 미리보기를 로드합니다.
        /// </summary>
        private void LoadFolderIconPreview(string iconPath)
        {
            try
            {
                if (string.IsNullOrEmpty(iconPath) || iconPath.StartsWith("/Assets"))
                {
                    FolderIconPreview.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
                }
                else if (File.Exists(iconPath))
                {
                    FolderIconPreview.Source = new BitmapImage(new Uri(iconPath));
                }
                else
                {
                    FolderIconPreview.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"아이콘 로드 오류: {ex.Message}");
                FolderIconPreview.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
            }
        }

        /// <summary>
        /// 폴더 아이콘 변경 버튼 클릭 - 아이콘 선택 다이얼로그 표시
        /// </summary>
        private async void BrowseFolderIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 열려 있는 EditStartMenuDialog를 먼저 숨김
                EditStartMenuDialog.Hide();
                
                // 약간의 지연 후 아이콘 다이얼로그 표시 (다이얼로그 전환을 위해)
                await Task.Delay(100);

                // 아이콘 선택 옵션 패널 표시, 리소스 그리드 숨김
                FolderIconSelectionOptionsPanel.Visibility = Visibility.Visible;
                FolderResourceIconGridView.Visibility = Visibility.Collapsed;

                FolderIconDialog.XamlRoot = this.Content.XamlRoot;
                await FolderIconDialog.ShowAsync();
                
                // 참고: FolderIconDialog가 닫힌 후의 처리는 각 핸들러에서 수행
                // - CloseFolderIconDialog (X 버튼)
                // - FolderRegularIconClick (일반 아이콘 선택)
                // - FolderResourceIcon_SelectionChanged (리소스 아이콘 선택)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"아이콘 다이얼로그 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 아이콘 선택 다이얼로그 닫기 (X 버튼 클릭)
        /// </summary>
        private async void CloseFolderIconDialog(object sender, RoutedEventArgs e)
        {
            FolderIconDialog.Hide();
            
            // EditStartMenuDialog 다시 표시
            await Task.Delay(100);
            EditStartMenuDialog.XamlRoot = this.Content.XamlRoot;
            await EditStartMenuDialog.ShowAsync();
        }

        /// <summary>
        /// 일반 아이콘 선택 (파일 탐색기)
        /// </summary>
        private async void FolderRegularIconClick(object sender, RoutedEventArgs e)
        {
            try
            {
                FolderIconDialog.Hide();
                
                // 약간의 지연 후 파일 탐색기 표시
                await Task.Delay(100);

                var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
                var hwnd = WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                openPicker.FileTypeFilter.Add(".png");
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".ico");

                var file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    _selectedFolderIconPath = file.Path;
                    FolderIconPreview.Source = new BitmapImage(new Uri(file.Path));
                }
                
                // EditStartMenuDialog 다시 표시
                await Task.Delay(100);
                EditStartMenuDialog.XamlRoot = this.Content.XamlRoot;
                await EditStartMenuDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"일반 아이콘 선택 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 리소스 아이콘 선택 버튼 클릭
        /// </summary>
        private void FolderResourceIconClick(object sender, RoutedEventArgs e)
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
                        FolderIconSelectionOptionsPanel.Visibility = Visibility.Collapsed;
                        FolderResourceIconGridView.Visibility = Visibility.Visible;
                        FolderResourceIconGridView.ItemsSource = iconFiles;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"리소스 아이콘 로드 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 리소스 아이콘 선택 완료
        /// </summary>
        private async void FolderResourceIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderResourceIconGridView.SelectedItem is string iconPath)
            {
                try
                {
                    _selectedFolderIconPath = iconPath;
                    FolderIconPreview.Source = new BitmapImage(new Uri(iconPath));

                    // UI 초기화 및 다이얼로그 닫기
                    FolderResourceIconGridView.SelectedItem = null;
                    FolderResourceIconGridView.Visibility = Visibility.Collapsed;
                    FolderIconSelectionOptionsPanel.Visibility = Visibility.Visible;
                    FolderIconDialog.Hide();
                    
                    // EditStartMenuDialog 다시 표시
                    await Task.Delay(100);
                    EditStartMenuDialog.XamlRoot = this.Content.XamlRoot;
                    await EditStartMenuDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"리소스 아이콘 선택 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 폴더 아이콘 초기화 버튼 클릭
        /// </summary>
        private void ResetFolderIcon_Click(object sender, RoutedEventArgs e)
        {
            _selectedFolderIconPath = "/Assets/icon/folder_3.png";
            FolderIconPreview.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
        }

        /// <summary>
        /// 폴더 수정 저장 버튼 클릭
        /// </summary>
        private async void SaveFolderEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newName = FolderNameTextBox.Text.Trim();
                if (string.IsNullOrEmpty(newName))
                {
                    return;
                }

                if (_editingFolder != null)
                {
                    JsonConfigHelper.UpdateStartMenuFolder(_editingFolder.FolderId, newName, _editingFolder.FolderPath, _selectedFolderIconPath);
                    await LoadStartMenuItemsAsync();
                }

                EditStartMenuDialog.Hide();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 저장 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더 수정 다이얼로그 닫기
        /// </summary>
        private void CloseEditStartMenuDialog(object sender, RoutedEventArgs e)
        {
            EditStartMenuDialog.Hide();
        }

        #endregion

        #endregion

        #region 설정 관련 메서드

        private SettingsDialogViewModel _settingsViewModel;
        private bool _isSettingsLoading = false;

        /// <summary>
        /// 설정 탭이 선택될 때 설정을 비동기로 로드합니다.
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            if (_isSettingsLoading) return;

            try
            {
                _isSettingsLoading = true;

                // ViewModel 초기화 (최초 한 번만)
                if (_settingsViewModel == null)
                {
                    _settingsViewModel = new SettingsDialogViewModel();
                    _settingsViewModel.InitializeVersionText();

                    // 이벤트 핸들러 등록 (최초 한 번만)
                    SettingsStartupToggle.Toggled += SettingsStartupToggle_Toggled;
                    SettingsSystemTrayToggle.Toggled += SettingsSystemTrayToggle_Toggled;
                }

                // 설정 콘텐츠의 DataContext 설정
                if (SettingsContent != null)
                {
                    SettingsContent.DataContext = _settingsViewModel;
                }

                // 설정 로드
                await _settingsViewModel.LoadCurrentSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"설정 로드 오류: {ex.Message}");
            }
            finally
            {
                _isSettingsLoading = false;
            }
        }

        /// <summary>
        /// 시작 시 실행 토글 변경 이벤트 핸들러
        /// </summary>
        private async void SettingsStartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSettingsLoading || _settingsViewModel == null) return;

            try
            {
                await _settingsViewModel.SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"시작 프로그램 설정 저장 오류: {ex.Message}");
                _isSettingsLoading = true;
                SettingsStartupToggle.IsOn = !SettingsStartupToggle.IsOn;
                _isSettingsLoading = false;
            }
        }

        /// <summary>
        /// 시스템 트레이 아이콘 토글 변경 이벤트 핸들러
        /// </summary>
        private async void SettingsSystemTrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSettingsLoading || _settingsViewModel == null) return;

            try
            {
                await _settingsViewModel.SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"시스템 트레이 설정 저장 오류: {ex.Message}");
                _isSettingsLoading = true;
                SettingsSystemTrayToggle.IsOn = !SettingsSystemTrayToggle.IsOn;
                _isSettingsLoading = false;
            }
        }

        #endregion
    }
}
