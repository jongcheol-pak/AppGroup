using IWshRuntimeLibrary;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using WinUIEx;
using static AppGroup.WindowHelper;
using AppGroup.Models;
using AppGroup.ViewModels;
using File = System.IO.File;

namespace AppGroup.View
{
    public class PathData
    {
        public string Tooltip { get; set; }
        public string Args { get; set; }
        public string Icon { get; set; }
        public string ItemType { get; set; } = "App";
    }
    public class GroupData
    {
        public required string GroupIcon { get; set; }
        public required string GroupName { get; set; }
        public bool GroupHeader { get; set; }
        public bool ShowGroupEdit { get; set; } = true;
        public int GroupCol { get; set; }
        public int GroupId { get; set; }
        public bool ShowLabels { get; set; } = false;
        public int LabelSize { get; set; } = 12;
        public string LabelPosition { get; set; } = "Bottom";

        public Dictionary<string, PathData> Path { get; set; }  // Changed to Pascal case
    }

    public sealed partial class PopupWindow : Window, IDisposable
    {

        // Constants for UI elements
        private const int BUTTON_SIZE = 40;
        private const int BUTTON_SIZE_WITH_LABEL = 56;
        private const int BUTTON_HEIGHT_HORIZONTAL_LABEL = 40;  // Same as BUTTON_SIZE for consistent height
        private const int BUTTON_WIDTH_HORIZONTAL_LABEL = 180;
        private const int ICON_SIZE = 24;
        private const int BUTTON_MARGIN = 4;
        private const int DEFAULT_LABEL_SIZE = 12;
        private const string DEFAULT_LABEL_POSITION = "Bottom";

        // Add these constants to PopupWindow class

        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private NativeMethods.WndProcDelegate _newWndProc; // Keep reference to prevent GC


        // Static JSON options to prevent redundant creation
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Member variables
        private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();
        private readonly WindowHelper _windowHelper;
        private readonly PopupWindowViewModel _viewModel;
        private Dictionary<string, GroupData> _groups;
        private GridView _gridView;
        private PopupItem _clickedItem;
        private int _groupId;
        private string _groupFilter = null;
        private string _json = "";
        private bool _anyGroupDisplayed;
        private DataTemplate _itemTemplate;
        private DataTemplate _itemTemplateWithLabel;
        private DataTemplate _itemTemplateHorizontalLabel;
        private ItemsPanelTemplate _panelTemplate;
        private ItemsPanelTemplate _panelTemplateWithLabel;
        private ItemsPanelTemplate _panelTemplateHorizontalLabel;

        // Label settings for current group
        private bool _showLabels = false;
        private int _labelSize = DEFAULT_LABEL_SIZE;
        private string _labelPosition = DEFAULT_LABEL_POSITION;
        private int _currentColumns = 1;


        private string _originalIconPath;
        private string _iconWithBackgroundPath;
        private string iconGroup;
        // Add these fields to your class
        private static string _cachedAppFolderPath;
        private static string _cachedLastOpenPath;
        private UISettings _uiSettings; // Cache UISettings instance
        private bool _isUISettingsSubscribed = false;
        private readonly List<Task> _backgroundTasks = new List<Task>();
        private readonly List<Task> _iconLoadingTasks = new List<Task>();

        // 백그라운드 작업 취소를 위한 CancellationTokenSource
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private bool useFileMode = false;
        private bool _disposed = false;

        private NativeMethods.SubclassProc _subclassProc; // Keep reference to prevent GC
        private const int SUBCLASS_ID = 1;
        // Constructor
        public PopupWindow(string groupFilter = null)
        {
            InitializeComponent();

            _viewModel = new PopupWindowViewModel();
            if (Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
            }



            _groupFilter = groupFilter;
            this.Title = "Popup Window";

            // Setup window
            _windowHelper = new WindowHelper(this);

            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = true;
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = false;
            _windowHelper.IsAlwaysOnTop = true;

            this.Hide();


            // Initialize templates

            InitializeTemplates();

            SetWindowIcon();
            if (!useFileMode)
            {
                // Setup custom window procedure AFTER window is created
                _hwnd = WindowNative.GetWindowHandle(this);
                SubclassWindow();

            }
            //InitializeSystemTray();
            this.AppWindow.IsShownInSwitchers = false;

            // Load on activation
            this.Activated += Window_Activated;
        }
        private void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            // Update the MainGrid background color based on the current settings
            UpdateMainGridBackground(sender);
        }

        private void SubclassWindow()
        {
            try
            {
                // Create delegate and keep reference to prevent GC
                _subclassProc = new NativeMethods.SubclassProc(SubclassProc);

                // Use SetWindowSubclass instead of SetWindowLongPtr
                bool success = NativeMethods.SetWindowSubclass(
                    _hwnd,
                    _subclassProc,
                    SUBCLASS_ID,
                    IntPtr.Zero);

                if (success)
                {
                    Debug.WriteLine("Window subclassed successfully");
                }
                else
                {
                    Debug.WriteLine($"Failed to subclass window. Error: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to subclass window: {ex.Message}");
            }
        }

        // Subclass procedure
        private IntPtr SubclassProc(
     IntPtr hWnd,
     uint msg,
     IntPtr wParam,
     IntPtr lParam,
     IntPtr uIdSubclass,
     IntPtr dwRefData)
        {

            // Handle WM_COPYDATA for string messages
            if (msg == NativeMethods.WM_COPYDATA)
            {
                try
                {
                    NativeMethods.COPYDATASTRUCT cds = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(
                        lParam, typeof(NativeMethods.COPYDATASTRUCT));

                    // Check the dwData to identify message type
                    if (cds.dwData == (IntPtr)100)
                    { // Your custom identifier
                        string groupName = Marshal.PtrToStringUni(cds.lpData);
                        Debug.WriteLine($"Received WM_COPYDATA message with groupName: {groupName}");

                        // Update on UI thread
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                _groupFilter = groupName;
                                Debug.WriteLine($"Updated group filter to: {_groupFilter}");
                                LoadConfiguration();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error updating group: {ex.Message}");
                            }
                        });

                        return (IntPtr)1; // Message handled successfully
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in WM_COPYDATA handler: {ex.Message}");
                }
            }

            return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
        }
        private void UpdateMainGridBackground(UISettings uiSettings)
        {
            // Check if the accent color is being shown on Start and taskbar
            if (IsAccentColorOnStartTaskbarEnabled())
            {

                if (Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = ElementTheme.Dark;
                }
                // Get current app theme
                var appTheme = Application.Current.RequestedTheme;

                // Use SystemAccentColorDark2 for Light mode, Dark3 for Dark mode
                string accentResourceKey = appTheme == ApplicationTheme.Light
                    ? "SystemAccentColorDark2"
                    : "SystemAccentColorDark2";

                if (Application.Current.Resources.TryGetValue(accentResourceKey, out object accentColor))
                {
                    var acrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                    {
                        TintColor = (Windows.UI.Color)accentColor,
                        TintOpacity = 0.8,
                        FallbackColor = (Windows.UI.Color)accentColor
                    };
                    MainGrid.Background = acrylicBrush;
                }
            }
            else
            {
                MainGrid.Background = null;
            }
        }

        private bool IsAccentColorOnStartTaskbarEnabled()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key != null)
                {
                    object value = key.GetValue("ColorPrevalence");
                    if (value != null && (int)value == 1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void InitializeTemplates()
        {
            // Create item template once (without labels)
            // 이미지 비율 유지를 위해 Width/Height 대신 MaxWidth/MaxHeight 사용
            _itemTemplate = (DataTemplate)XamlReader.Load(
     $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid VerticalAlignment=""Center""
          HorizontalAlignment=""Center""
          Width=""{BUTTON_SIZE}""
          Height=""{BUTTON_SIZE}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <Image Source=""{{Binding Icon}}""
               MaxWidth=""{ICON_SIZE}""
               MaxHeight=""{ICON_SIZE}""
               Stretch=""Uniform""
               VerticalAlignment=""Center""
               HorizontalAlignment=""Center""
               Margin=""8"" />
    </Grid>
</DataTemplate>");

            // Create panel template once (without labels)
            const int EFFECTIVE_BUTTON_WIDTH = BUTTON_SIZE + (BUTTON_MARGIN * 2);
            _panelTemplate = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{EFFECTIVE_BUTTON_WIDTH}""
                              ItemHeight=""{EFFECTIVE_BUTTON_WIDTH}""
                              HorizontalAlignment=""Center""
                              VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");

            // Label templates will be created dynamically in CreateLabelTemplates() with the actual font size
        }

        // Create label templates with the specified font size
        private void CreateLabelTemplates(int fontSize)
        {
            // Create item template with labels
            // 이미지 비율 유지를 위해 Width/Height 대신 MaxWidth/MaxHeight 사용
            const int EFFECTIVE_BUTTON_WIDTH_WITH_LABEL = BUTTON_SIZE_WITH_LABEL + (BUTTON_MARGIN * 2);
            _itemTemplateWithLabel = (DataTemplate)XamlReader.Load(
     $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel VerticalAlignment=""Center""
          HorizontalAlignment=""Center""
          Width=""{BUTTON_SIZE_WITH_LABEL}""
          Height=""{BUTTON_SIZE_WITH_LABEL}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <Image Source=""{{Binding Icon}}""
               MaxWidth=""{ICON_SIZE}""
               MaxHeight=""{ICON_SIZE}""
               Stretch=""Uniform""
               HorizontalAlignment=""Center""
               Margin=""4,6,4,2"" />
        <TextBlock Text=""{{Binding ToolTip}}""
                   FontSize=""{fontSize}""
                   TextTrimming=""CharacterEllipsis""
                   TextAlignment=""Center""
                   HorizontalAlignment=""Center""
                   MaxWidth=""{BUTTON_SIZE_WITH_LABEL - 4}""
                   Opacity=""0.9"" />
    </StackPanel>
</DataTemplate>");

            // Create panel template with labels
            _panelTemplateWithLabel = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{EFFECTIVE_BUTTON_WIDTH_WITH_LABEL}""
                              ItemHeight=""{EFFECTIVE_BUTTON_WIDTH_WITH_LABEL}""
                              HorizontalAlignment=""Center""
                              VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");

            // Create item template with horizontal labels (for single column layout)
            // 이미지 비율 유지를 위해 Width/Height 대신 MaxWidth/MaxHeight 사용
            const int EFFECTIVE_BUTTON_HEIGHT_HORIZONTAL = BUTTON_HEIGHT_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2);
            _itemTemplateHorizontalLabel = (DataTemplate)XamlReader.Load(
     $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid Width=""{BUTTON_WIDTH_HORIZONTAL_LABEL}""
          Height=""{BUTTON_HEIGHT_HORIZONTAL_LABEL}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <StackPanel Orientation=""Horizontal""
              VerticalAlignment=""Center""
              HorizontalAlignment=""Left"">
            <Image Source=""{{Binding Icon}}""
                   MaxWidth=""{ICON_SIZE}""
                   MaxHeight=""{ICON_SIZE}""
                   Stretch=""Uniform""
                   VerticalAlignment=""Center""
                   Margin=""8,0,8,0"" />
            <TextBlock Text=""{{Binding ToolTip}}""
                       FontSize=""{fontSize}""
                       TextTrimming=""CharacterEllipsis""
                       VerticalAlignment=""Center""
                       MaxWidth=""{BUTTON_WIDTH_HORIZONTAL_LABEL - ICON_SIZE - 12}""
                       Opacity=""0.9"" />
        </StackPanel>
    </Grid>
</DataTemplate>");

            // Create panel template with horizontal labels
            _panelTemplateHorizontalLabel = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{BUTTON_WIDTH_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2)}""
                              ItemHeight=""{EFFECTIVE_BUTTON_HEIGHT_HORIZONTAL}""
                              HorizontalAlignment=""Left""
                              VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");
        }

        // Load configuration with better error handling and caching
        private void LoadConfiguration()
        {
            try
            {
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                _json = JsonConfigHelper.ReadJsonFromFile(configPath);
                _groups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);

                // Only initialize the window and create dynamic content if groups are loaded successfully
                if (_groups != null)
                {
                    InitializeWindow();
                    CreateDynamicContent();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading configuration: {ex.Message}");
                _json = GetDefaultJsonConfiguration();
                _groups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);
                InitializeWindow();
                CreateDynamicContent();
            }
        }

        private string GetDefaultJsonConfiguration()
        {
            return @"{
            ""Group1NameHere"": {
                ""groupCol"": 3,
                ""groupIcon"": ""test.png"",
                ""path"": [""C:\\Windows\\System32\\notepad.exe"", ""C:\\Windows\\System32\\calc.exe"", ""C:\\Windows\\System32\\mspaint.exe""]
            }
        }";
        }

        // Non-async window initialization for faster loading
        private void InitializeWindow()
        {

            int maxPathItems = 1;
            int maxColumns = 1;
            string groupIcon = "AppGroup.ico";
            bool groupHeader = false;

            // Reset label settings
            _showLabels = false;
            _labelSize = DEFAULT_LABEL_SIZE;
            _labelPosition = DEFAULT_LABEL_POSITION;
            _currentColumns = 1;

            // If we have a group filter, only consider that group
            if (!string.IsNullOrEmpty(_groupFilter) && _groups.Values.Any(g => g.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase)))
            {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                maxPathItems = filteredGroup.Value.Path.Count;
                maxColumns = filteredGroup.Value.GroupCol;
                groupHeader = filteredGroup.Value.GroupHeader;
                //groupIcon = filteredGroup.Value.groupIcon;
                iconGroup = filteredGroup.Value.GroupIcon;

                // Get label settings
                _showLabels = filteredGroup.Value.ShowLabels;
                _labelSize = filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE;
                _labelPosition = filteredGroup.Value.LabelPosition != null ? filteredGroup.Value.LabelPosition : DEFAULT_LABEL_POSITION;

                _currentColumns = maxColumns;


                // Create label templates with the actual font size from config
                if (_showLabels)
                {
                    CreateLabelTemplates(_labelSize);
                }

                if (!int.TryParse(filteredGroup.Key, out _groupId))
                {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }
            }
            else
            {
                foreach (var group in _groups.Values)
                {
                    maxPathItems = Math.Max(maxPathItems, group.Path.Count);
                    maxColumns = Math.Max(maxColumns, group.GroupCol);
                }
                _currentColumns = maxColumns;
            }

            // Determine if using horizontal labels (single column with labels)
            //bool useHorizontalLabels = _showLabels && _currentColumns == 1;
            bool useHorizontalLabels = _showLabels && _labelPosition == "Right";
            // Use appropriate button size based on label setting and layout
            int buttonWidth, buttonHeight;
            if (useHorizontalLabels)
            {
                buttonWidth = BUTTON_WIDTH_HORIZONTAL_LABEL;
                buttonHeight = BUTTON_HEIGHT_HORIZONTAL_LABEL;
            }
            else if (_showLabels)
            {
                buttonWidth = BUTTON_SIZE_WITH_LABEL;
                buttonHeight = BUTTON_SIZE_WITH_LABEL;
            }
            else
            {
                buttonWidth = BUTTON_SIZE;
                buttonHeight = BUTTON_SIZE;
            }

            int numberOfRows = (int)Math.Ceiling((double)maxPathItems / maxColumns);
            int dynamicWidth = maxColumns * (buttonWidth + BUTTON_MARGIN * 2);
            if (groupHeader == true && maxColumns < 2 && !useHorizontalLabels)
            {
                dynamicWidth = 2 * (buttonWidth + BUTTON_MARGIN * 2);
            }
            // Ensure minimum width for horizontal labels
            if (useHorizontalLabels)
            {
                dynamicWidth = Math.Max(dynamicWidth, BUTTON_WIDTH_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2));
            }

            int dynamicHeight = numberOfRows * (buttonHeight + BUTTON_MARGIN * 2);
            var displayInfo = GetDisplayInformation();
            float scaleFactor = displayInfo.Item1;

            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);
            if (groupHeader)
            {
                scaledHeight += 40;
            }

            //var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

            //this.AppWindow.SetIcon(iconPath);

            MainGrid.Margin = new Thickness(0, 0, -5, -15);

            int finalWidth = scaledWidth + 30;
            int finalHeight = scaledHeight + 20;



            int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height);
            int maxAllowedHeight = screenHeight - 30; // Reserve space for taskbar and window chrome
            if (finalHeight > maxAllowedHeight)
            {
                finalHeight = maxAllowedHeight;
            }




            _windowHelper.SetSize(finalWidth, finalHeight);
            NativeMethods.PositionWindowAboveTaskbar(this.GetWindowHandle());



        }


        private void SetWindowIcon()
        {
            try
            {
                // Get the window handle
                IntPtr hWnd = WindowNative.GetWindowHandle(this);

                // Try to load icon from embedded resource first
                var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

                if (File.Exists(iconPath))
                {
                    // Load and set the icon using Win32 APIs
                    IntPtr hIcon = NativeMethods.LoadIcon(iconPath);
                    if (hIcon != IntPtr.Zero)
                    {
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_SMALL, hIcon);
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_BIG, hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
            }
        }

        private void CreateDynamicContent()
        {
            // Clear existing items
            _viewModel.PopupItems.Clear();
            GridPanel.Children.Clear();
            _viewModel.HeaderText = string.Empty;
            _viewModel.HeaderVisibility = Visibility.Collapsed;
            _viewModel.ScrollMargin = new Thickness(0, 5, 0, 5);
            _anyGroupDisplayed = false;

            foreach (var group in _groups)
            {

                // Skip this group if filtering is active and this isn't the requested group
                if (_groupFilter != null && !group.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _anyGroupDisplayed = true;

                // Set header visibility
                if (group.Value.GroupHeader)
                {
                    _viewModel.HeaderVisibility = Visibility.Visible;
                    _viewModel.HeaderText = group.Value.GroupName;
                    _viewModel.ScrollMargin = new Thickness(0, 0, 0, 5);

                    // Set edit button visibility based on ShowGroupEdit setting
                    _viewModel.EditButtonVisibility = group.Value.ShowGroupEdit ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    _viewModel.HeaderVisibility = Visibility.Collapsed;
                    _viewModel.ScrollMargin = new Thickness(0, 5, 0, 5);
                }

                // Configure GridView
                bool useHorizontalLabels = _showLabels && _labelPosition == "Right";

                DataTemplate selectedItemTemplate;
                ItemsPanelTemplate selectedPanelTemplate;

                if (useHorizontalLabels)
                {
                    selectedItemTemplate = _itemTemplateHorizontalLabel;
                    selectedPanelTemplate = _panelTemplateHorizontalLabel;
                }
                else if (_showLabels)
                {
                    selectedItemTemplate = _itemTemplateWithLabel;
                    selectedPanelTemplate = _panelTemplateWithLabel;
                }
                else
                {
                    selectedItemTemplate = _itemTemplate;
                    selectedPanelTemplate = _panelTemplate;
                }

                _gridView = new GridView
                {
                    SelectionMode = ListViewSelectionMode.Extended,
                    IsItemClickEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CanDragItems = true,
                    CanReorderItems = true,
                    AllowDrop = true,
                    ItemTemplate = selectedItemTemplate,
                    ItemsPanel = selectedPanelTemplate
                };

                // Set up events
                _gridView.RightTapped += GridView_RightTapped;
                _gridView.DragItemsCompleted += GridView_DragItemsCompleted;
                _gridView.ItemClick += GridView_ItemClick;

                // Load items with updated PathData structure
                LoadGridItems(group.Value.Path);  // Now passing Dictionary<string, PathData>

                _gridView.ItemsSource = _viewModel.PopupItems;
                GridPanel.Children.Add(_gridView);

                // Handle case where no groups match filter
                if (!_anyGroupDisplayed)
                {
                    TextBlock noGroupsText = new TextBlock
                    {
                        Text = $"No group found matching '{_groupFilter}'",
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };
                    GridPanel.Children.Add(noGroupsText);

                    this.AppWindow.Resize(new SizeInt32(250, 120));
                }
            }
        }

        // Add these fields to your PopupWindow class
        //private string _originalIconPath;
        private string _currentGridIconPath;
        private bool _isGridIcon = false;

        // Add this method to PopupWindow class
        private async Task CreateGridIconFromReorder()
        {
            try
            {
                if (_viewModel.PopupItems == null || !_viewModel.PopupItems.Any())
                {
                    Debug.WriteLine("No items available for grid icon creation");
                    return;
                }

                // Get the group information
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null)
                {
                    Debug.WriteLine($"Error: Group '{_groupFilter}' not found");
                    return;
                }

                // Determine if this is a grid icon based on the current icon path
                string currentIcon = filteredGroup.Value.GroupIcon;
                bool isCurrentlyGridIcon = currentIcon.Contains("grid");

                if (!isCurrentlyGridIcon)
                {
                    Debug.WriteLine("Current icon is not a grid icon, skipping grid recreation");
                    return;
                }

                // Determine grid size from current icon name
                int gridSize = currentIcon.Contains("grid3") ? 3 : 2;

                // Take items up to grid size limit
                var gridItems = _viewModel.PopupItems.Take(gridSize * gridSize).Select(item => new ExeFileModel
                {
                    FileName = item.Name,
                    FilePath = item.Path,
                    Icon = item.Icon?.UriSource?.LocalPath ?? "", // Get the actual icon path
                    Tooltip = item.ToolTip,
                    Args = item.Args,
                    IconPath = item.CustomIconPath
                }).ToList();

                // Create the grid icon
                IconHelper iconHelper = new IconHelper();
                string newGridIconPath = await iconHelper.CreateGridIconForPopupAsync(
                    gridItems,
                    gridSize,
                    _groupFilter
                );

                if (!string.IsNullOrEmpty(newGridIconPath))
                {
                    _currentGridIconPath = newGridIconPath;

                    // Update the shortcut and JSON configuration
                    await UpdateShortcutAndConfig(newGridIconPath, gridSize);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating grid icon: {ex.Message}");
            }
        }

        private async Task UpdateShortcutAndConfig(string newIconPath, int gridSize)
        {
            try
            {
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");

                string groupFolder = Path.Combine(groupsFolder, _groupFilter);
                if (!Directory.Exists(groupFolder))
                {
                    Debug.WriteLine($"Group folder not found: {groupFolder}");
                    return;
                }

                // Update the shortcut icon
                string shortcutPath = Path.Combine(groupFolder, $"{_groupFilter}.lnk");
                if (File.Exists(shortcutPath))
                {
                    IWshShell wshShell = null;
                    IWshShortcut shortcut = null;
                    try
                    {
                        wshShell = new WshShell();
                        shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                        shortcut.IconLocation = newIconPath;
                        shortcut.Save();
                        Debug.WriteLine($"Updated shortcut icon: {shortcutPath}");
                    }
                    finally
                    {
                        // COM 객체 명시적 해제
                        if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                        if (wshShell != null) Marshal.ReleaseComObject(wshShell);
                    }
                }

                // Update the JSON configuration with new icon path and reordered items
                await UpdateJsonConfiguration(newIconPath, gridSize);

                // Update taskbar if pinned
                bool isPinned = await TaskbarManager.IsShortcutPinnedToTaskbar(_groupFilter);
                if (isPinned)
                {
                    await TaskbarManager.UpdateTaskbarShortcutIcon(_groupFilter, newIconPath);
                    TaskbarManager.TryRefreshTaskbarWithoutRestartAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating shortcut and config: {ex.Message}");
            }
        }

        private async Task UpdateJsonConfiguration(string newIconPath, int gridSize)
        {
            try
            {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) return;

                if (!int.TryParse(filteredGroup.Key, out int groupId))
                {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }

                // Create the reordered paths dictionary with custom icons preserved
                Dictionary<string, (string tooltip, string args, string icon)> reorderedPaths =
                    new Dictionary<string, (string tooltip, string args, string icon)>();

                foreach (var item in _viewModel.PopupItems)
                {
                    string customIcon = !string.IsNullOrEmpty(item.CustomIconPath) ? item.CustomIconPath : "";
                    reorderedPaths[item.Path] = (item.ToolTip, item.Args, customIcon);
                }

                // Update JSON with new icon path and reordered items
                JsonConfigHelper.AddGroupToJson(
                    JsonConfigHelper.GetDefaultConfigPath(),
                    groupId,
                    filteredGroup.Value.GroupName,
                    filteredGroup.Value.GroupHeader,
                    filteredGroup.Value.ShowGroupEdit,
                    newIconPath, // Use the new grid icon path
                    filteredGroup.Value.GroupCol,
                    filteredGroup.Value.ShowLabels,
                    filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE,
                        filteredGroup.Value.LabelPosition != null ? filteredGroup.Value.LabelPosition : DEFAULT_LABEL_POSITION,

                    reorderedPaths
                );

                // Reload the JSON to reflect changes
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                _json = JsonConfigHelper.ReadJsonFromFile(configPath);
                _groups = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);

                Debug.WriteLine($"Updated JSON configuration with new icon: {newIconPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating JSON configuration: {ex.Message}");
            }
        }

        // Update the existing GridView_DragItemsCompleted method in PopupWindow
        private async void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            try
            {
                if (_groups == null || string.IsNullOrEmpty(_groupFilter))
                {
                    Debug.WriteLine("Error: Unable to deserialize groups or group filter is not set");
                    return;
                }

                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                if (filteredGroup.Key == null)
                {
                    Debug.WriteLine($"Error: Group '{_groupFilter}' not found in configuration");
                    return;
                }

                // Create a new dictionary to hold the reordered paths with their properties INCLUDING custom icons
                Dictionary<string, (string tooltip, string args, string icon)> newPathOrder = new Dictionary<string, (string tooltip, string args, string icon)>();
                foreach (var item in _viewModel.PopupItems)
                {
                    // Include the custom icon path when reordering
                    string customIcon = !string.IsNullOrEmpty(item.CustomIconPath) ? item.CustomIconPath : "";
                    newPathOrder[item.Path] = (item.ToolTip, item.Args, customIcon);
                }

                if (!int.TryParse(filteredGroup.Key, out int groupId))
                {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }

                // Check if current icon is a grid icon and regenerate if needed
                string currentIcon = filteredGroup.Value.GroupIcon;
                bool isGridIcon = currentIcon.Contains("grid");

                if (isGridIcon)
                {
                    // Regenerate the grid icon with new order
                    await CreateGridIconFromReorder();
                }
                else
                {
                    // Just update the JSON with reordered items (no icon change needed)
                    JsonConfigHelper.AddGroupToJson(
                        JsonConfigHelper.GetDefaultConfigPath(),
                        groupId,
                        filteredGroup.Value.GroupName,
                        filteredGroup.Value.GroupHeader,
                        filteredGroup.Value.ShowGroupEdit,
                        filteredGroup.Value.GroupIcon,
                        filteredGroup.Value.GroupCol,
                        filteredGroup.Value.ShowLabels,
                        filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE,
                        filteredGroup.Value.LabelPosition != null ? filteredGroup.Value.LabelPosition : DEFAULT_LABEL_POSITION,
                        newPathOrder
                    );
                }

                _json = File.ReadAllText(JsonConfigHelper.GetDefaultConfigPath());
                Debug.WriteLine("Successfully updated configuration after drag reorder");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GridView_DragItemsCompleted: {ex.Message}");
                ShowErrorDialog($"Failed to save new item order: {ex.Message}");
            }
        }

        private void LoadGridItems(Dictionary<string, PathData> pathsWithProperties)
        {
            foreach (var pathEntry in pathsWithProperties)
            {

                string path = pathEntry.Key;
                PathData properties = pathEntry.Value;

                // Get tooltip from PathData if available, otherwise use default method
                string tooltip = !string.IsNullOrEmpty(properties.Tooltip)
                    ? properties.Tooltip
                    : GetDisplayName(path);

                // Get custom icon path from PathData if available
                string customIconPath = !string.IsNullOrEmpty(properties.Icon)
                    ? properties.Icon
                    : null;

                // ItemType 파싱 (기본값: App)
                ItemType itemType = ItemType.App;
                if (!string.IsNullOrEmpty(properties.ItemType))
                {
                    if (Enum.TryParse<ItemType>(properties.ItemType, true, out var parsedType))
                    {
                        itemType = parsedType;
                    }
                }

                var popupItem = new PopupItem
                {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    ToolTip = tooltip,
                    Icon = null,
                    Args = properties.Args ?? "",
                    IconPath = customIconPath,
                    CustomIconPath = customIconPath,
                    ItemType = itemType
                };

                _viewModel.PopupItems.Add(popupItem);
                // fire-and-forget 예외 처리 추가
                _ = LoadIconAsync(popupItem, path).ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        Debug.WriteLine($"LoadIconAsync failed for {path}: {t.Exception.InnerException?.Message}");
                    }
                }, TaskScheduler.Default);
            }
        }
        private async Task LoadIconAsync(PopupItem item, string path)
        {
            try
            {
                string iconPath;

                // Use custom icon if available and file exists
                if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
                {
                    iconPath = item.CustomIconPath;
                }
                else
                {
                    if (Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase))
                    {
                        iconPath = await IconHelper.GetUrlFileIconAsync(path);
                    }
                    else
                    {
                        iconPath = await IconCache.GetIconPathAsync(path);
                    }

                }

                BitmapImage icon = await IconCache.LoadImageFromPathAsync(iconPath);

                // Update on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    item.Icon = icon;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading icon for {path}: {ex.Message}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    item.Icon = null;
                });
            }
        }
        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PopupItem popupItem)
            {
                LaunchItem(popupItem);
            }
        }

        /// <summary>
        /// 항목 유형에 따라 적절한 방법으로 실행합니다.
        /// </summary>
        private void LaunchItem(PopupItem item)
        {
            switch (item.ItemType)
            {
                case Models.ItemType.Folder:
                    TryOpenFolder(item.Path);
                    break;
                case Models.ItemType.Web:
                    TryOpenWeb(item.Path);
                    break;
                case Models.ItemType.App:
                default:
                    TryLaunchApp(item.Path, item.Args);
                    break;
            }
        }

        private void GridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as PopupItem;

            if (item != null)
            {
                MenuFlyout flyout = CreateItemFlyout();
                flyout.ShowAt(_gridView, e.GetPosition(_gridView));
                _clickedItem = item;
            }
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                await OnWindowDeactivatedAsync();
            }
            else if (e.WindowActivationState == WindowActivationState.CodeActivated ||
                     e.WindowActivationState == WindowActivationState.PointerActivated)
            {
                OnWindowActivated();
            }
        }



        /// <summary>                                                                                                                           
        /// 윈도우가 비활성화될 때 호출됩니다.                                                                                                  
        /// </summary>                                                                                                                          
        private async Task OnWindowDeactivatedAsync()
        {
            int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height) * 2;
            int screenWidth = (int)(DisplayArea.Primary.WorkArea.Width) * 2;

            if (_groups != null)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    _viewModel.HeaderVisibility = Visibility.Collapsed;
                    _viewModel.HeaderText = string.Empty;
                    _viewModel.PopupItems.Clear();
                    GridPanel.Children.Clear();
                    _anyGroupDisplayed = false;
                });
            }

            var settings = await SettingsHelper.LoadSettingsAsync();
            CleanupUISettings();

            // 항상 창을 숨기고 리소스 정리 수행
            this.DispatcherQueue.TryEnqueue(() => CleanupWindowResources(screenWidth, screenHeight));

            // 아이콘 복원은 조건이 충족될 때만 수행
            if (!string.IsNullOrEmpty(_originalIconPath) && !string.IsNullOrEmpty(_groupFilter))
            {
                if (settings.UseGrayscaleIcon)
                {
                    var task = Task.Run(async () => await RestoreOriginalIconAsync());
                    _backgroundTasks.Add(task);
                }
            }
        }

        private void CleanupWindowResources(int screenWidth, int screenHeight)
        {
            try
            {
                this.Hide();
                _windowHelper.SetSize(screenWidth, screenHeight);
                NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());

                if (_gridView != null)
                {
                    _gridView.RightTapped -= GridView_RightTapped;
                    _gridView.DragItemsCompleted -= GridView_DragItemsCompleted;
                    _gridView.ItemClick -= GridView_ItemClick;
                }

                foreach (var item in _viewModel.PopupItems)
                {
                    if (item.Icon != null) { item.Icon.UriSource = null; item.Icon = null; }
                }
                _viewModel.PopupItems.Clear();
                GridPanel.Children.Clear();
                CleanupCompletedTasks();

                _groups = null;
                _json = "";
                _clickedItem = null;
                _gridView = null;
            }
            catch (Exception ex) { Debug.WriteLine($"UI cleanup error: {ex.Message}"); }
        }

        private void CleanupCompletedTasks()
        {
            foreach (var task in _backgroundTasks.ToList())
                if (task.IsCompleted) { task.Dispose(); _backgroundTasks.Remove(task); }
            foreach (var task in _iconLoadingTasks.ToList())
                if (task.IsCompleted) { task.Dispose(); _iconLoadingTasks.Remove(task); }
        }

        private async Task RestoreOriginalIconAsync()
        {
            try
            {
                await TaskbarManager.UpdateTaskbarShortcutIcon(_groupFilter, iconGroup);
                if (!string.IsNullOrEmpty(_iconWithBackgroundPath))
                {
                    IconHelper.RemoveBackgroundIcon(_iconWithBackgroundPath);
                    _iconWithBackgroundPath = null;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Background cleanup error: {ex.Message}"); }
        }

        private void OnWindowActivated()
        {
            if (useFileMode) LoadGroupFilterFromFile();
            else Debug.WriteLine("MESSAGE MODE");

            SubscribeToUISettings();
            UpdateMainGridBackground(_uiSettings);
            ScheduleTaskbarIconUpdate();
        }

        private void LoadGroupFilterFromFile()
        {
            Debug.WriteLine("FILE MODE");
            if (_cachedAppFolderPath == null)
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _cachedAppFolderPath = Path.Combine(appDataPath, "AppGroup");
                _cachedLastOpenPath = Path.Combine(_cachedAppFolderPath, "lastOpen");
            }
            try
            {
                if (File.Exists(_cachedLastOpenPath))
                {
                    string fileGroupFilter = File.ReadAllText(_cachedLastOpenPath).Trim();
                    if (!string.IsNullOrEmpty(fileGroupFilter)) _groupFilter = fileGroupFilter;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error reading group name from file: {ex.Message}"); }
        }

        private void SubscribeToUISettings()
        {
            if (!_isUISettingsSubscribed)
            {
                _uiSettings ??= new UISettings();
                _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
                _isUISettingsSubscribed = true;
            }
        }

        private void ScheduleTaskbarIconUpdate()
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        try { await UpdateTaskbarIcon(_groupFilter); }
                        catch (Exception ex) { Debug.WriteLine($"Background taskbar update error: {ex.Message}"); }
                    });
                }
                catch (Exception ex) { Debug.WriteLine($"Configuration loading error: {ex.Message}"); }
            });
        }


        // Add this cleanup method to your class
        private void CleanupUISettings()
        {
            if (_isUISettingsSubscribed && _uiSettings != null)
            {
                _uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
                _isUISettingsSubscribed = false;
            }
        }
        private async Task UpdateTaskbarIcon(string groupName)
        {
            var settings = await SettingsHelper.LoadSettingsAsync();

            try
            {
                // Determine the icon path based on your structure
                string basePath = Path.Combine("Groups", groupName, groupName);
                string iconPath;
                string groupIcon = IconHelper.FindOrigIcon(iconGroup);

                // Load settings to check grayscale preference


                _originalIconPath = groupIcon;



                if (!string.IsNullOrEmpty(_originalIconPath) && File.Exists(_originalIconPath))
                {
                    if (settings.UseGrayscaleIcon)
                    {
                        _iconWithBackgroundPath = await IconHelper.CreateBlackWhiteIconAsync(_originalIconPath);

                        await TaskbarManager.UpdateTaskbarShortcutIcon(groupName, _iconWithBackgroundPath);
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating taskbar icon with background: {ex.Message}");
            }
        }



        /// <summary>
        /// 앱을 실행합니다. cmd.exe /c start를 통해 Job Object에서 분리하여
        /// AppGroup 종료 시 실행된 앱이 함께 종료되지 않도록 합니다.
        /// </summary>
        private void TryLaunchApp(string path, string args)
        {
            try
            {
                // cmd.exe /c start를 통해 실행하면 Job Object에서 분리됨
                string arguments = string.IsNullOrEmpty(args)
                    ? $"/c start \"\" \"{path}\""
                : $"/c start \"\" \"{path}\" {args}";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {path}: {ex.Message}");
                ShowErrorDialog($"Failed to launch {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더를 Windows 탐색기에서 엽니다.
        /// </summary>
        private void TryOpenFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    Process.Start("explorer.exe", $"\"{folderPath}\"");
                }
                else
                {
                    ShowErrorDialog($"폴더를 찾을 수 없습니다: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open folder {folderPath}: {ex.Message}");
                ShowErrorDialog($"폴더 열기 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 웹 URL을 기본 브라우저에서 엽니다.
        /// </summary>
        private void TryOpenWeb(string url)
        {
            try
            {
                // URL 검증
                if (string.IsNullOrEmpty(url))
                {
                    ShowErrorDialog("URL이 비어 있습니다.");
                    return;
                }

                // http/https 접두사가 없으면 추가
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                // 기본 브라우저로 URL 열기
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL {url}: {ex.Message}");
                ShowErrorDialog($"웹 사이트 열기 실패: {ex.Message}");
            }
        }


        /// <summary>
        /// 앱을 관리자 권한으로 실행합니다. PowerShell Start-Process -Verb RunAs를 통해
        /// Job Object에서 분리하여 AppGroup 종료 시 실행된 앱이 함께 종료되지 않도록 합니다.
        /// </summary>
        private void TryRunAsAdmin(string path, string args)
        {
            try
            {
                // PowerShell의 작은 따옴표 이스케이프 (작은 따옴표를 두 번 사용)
                string escapedPath = path.Replace("'", "''");
                string escapedArgs = args?.Replace("'", "''") ?? "";

                // PowerShell Start-Process -Verb RunAs를 통해 관리자 권한으로 실행
                string psCommand = string.IsNullOrEmpty(escapedArgs)
                    ? $"Start-Process -FilePath '{escapedPath}' -Verb RunAs"
                    : $"Start-Process -FilePath '{escapedPath}' -ArgumentList '{escapedArgs}' -Verb RunAs";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{psCommand}\"",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to run as admin {path}: {ex.Message}");
                ShowErrorDialog($"Failed to run as admin {path}: {ex.Message}");
            }
        }

        private void OpenFileLocation(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);

                if (Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", directory);
                }
                else
                {
                    throw new Exception("Directory does not exist.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open file location {path}: {ex.Message}");
                ShowErrorDialog($"Failed to open file location {path}: {ex.Message}");
            }
        }

        // UI Helpers
        private void ShowErrorDialog(string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "오류",
                Content = message,
                CloseButtonText = "확인",
                XamlRoot = this.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private MenuFlyout CreateItemFlyout()
        {
            MenuFlyout flyout = new MenuFlyout();

            MenuFlyoutItem openItem = new MenuFlyoutItem
            {
                Text = "열기",
                Icon = new FontIcon { Glyph = "\ue8a7" }
            };
            openItem.Click += OpenItem_Click;
            flyout.Items.Add(openItem);

            MenuFlyoutItem runAsAdminItem = new MenuFlyoutItem
            {
                Text = "관리자 권한으로 실행",
                Icon = new FontIcon { Glyph = "\uE7EF" }
            };
            runAsAdminItem.Click += RunAsAdminItem_Click;
            flyout.Items.Add(runAsAdminItem);

            MenuFlyoutItem fileLocationItem = new MenuFlyoutItem
            {
                Text = "파일 위치 열기",
                Icon = new FontIcon { Glyph = "\ued43" }
            };
            fileLocationItem.Click += OpenFileLocation_Click;
            flyout.Items.Add(fileLocationItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            MenuFlyoutItem editItem = new MenuFlyoutItem
            {
                Text = "이 그룹 편집",
                Icon = new FontIcon { Glyph = "\ue70f" }
            };
            editItem.Click += EditGroup_Click;
            flyout.Items.Add(editItem);

            MenuFlyoutItem launchAll = new MenuFlyoutItem
            {
                Text = "Launch All",
                Icon = new FontIcon { Glyph = "\ue8a9" }
            };
            launchAll.Click += launchAllGroup_Click;



            flyout.Items.Add(launchAll);
            return flyout;
        }

        private async void launchAllGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_groupFilter))
            {
                // Find the group with the matching name
                var matchingGroup = _groups.Values.FirstOrDefault(g =>
                    g.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                // Check if we found a matching group
                if (matchingGroup != null)
                {
                    // Call the LaunchAll function with the matching group name
                    await JsonConfigHelper.LaunchAll(matchingGroup.GroupName);
                }
            }
        }



        private void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", _groupId);
            editGroup.Activate();
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            if (_clickedItem != null)
            {
                TryLaunchApp(_clickedItem.Path, _clickedItem.Args);
            }
        }

        private void RunAsAdminItem_Click(object sender, RoutedEventArgs e)
        {
            if (_clickedItem != null)
            {
                TryRunAsAdmin(_clickedItem.Path, _clickedItem.Args);
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (_clickedItem != null)
            {
                OpenFileLocation(_clickedItem.Path);
            }
        }

        private string GetDisplayName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "Unknown";
            }

            string extension = Path.GetExtension(filePath).ToLower();

            if (string.IsNullOrEmpty(extension))
            {
                return Path.GetFileName(filePath);
            }

            if (extension == ".exe")
            {
                try
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                    {
                        return versionInfo.FileDescription;
                    }
                }
                catch (Exception)
                {
                    // Fall through to default case
                }
            }
            else if (extension == ".lnk")
            {
                Path.GetFileNameWithoutExtension(filePath);
                //try {
                //    dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                //    dynamic shortcut = shell.CreateShortcut(filePath);
                //    string targetPath = shortcut.TargetPath;
                //    if (!string.IsNullOrEmpty(targetPath)) {
                //        return Path.GetFileNameWithoutExtension(targetPath);
                //    }
                //}
                //catch (Exception) {
                //}
            }

            return Path.GetFileNameWithoutExtension(filePath);
        }

        private Tuple<float, int, int> GetDisplayInformation()
        {
            var hwnd = WindowNative.GetWindowHandle(this);

            uint dpi = NativeMethods.GetDpiForWindow(hwnd);
            float scaleFactor = (float)dpi / 96.0f;

            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            NativeMethods.MONITORINFOEX monitorInfo = new NativeMethods.MONITORINFOEX();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
            NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

            int screenWidth = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
            int screenHeight = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;

            return new Tuple<float, int, int>(scaleFactor, screenWidth, screenHeight);
        }

        /// <summary>
        /// Releases unmanaged resources and disposes managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 백그라운드 작업 취소
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }

                // UISettings 구독 정리
                CleanupUISettings();

                // 윈도우 서브클래스 제거
                if (_hwnd != IntPtr.Zero && _subclassProc != null)
                {
                    NativeMethods.RemoveWindowSubclass(_hwnd, _subclassProc, SUBCLASS_ID);
                }

                // GridView 이벤트 핸들러 정리
                if (_gridView != null)
                {
                    _gridView.RightTapped -= GridView_RightTapped;
                    _gridView.DragItemsCompleted -= GridView_DragItemsCompleted;
                    _gridView.ItemClick -= GridView_ItemClick;
                    _gridView = null;
                }

                // 팝업 아이템 및 이미지 정리
                foreach (var item in _viewModel.PopupItems)
                {
                    if (item.Icon != null)
                    {
                        item.Icon.UriSource = null;
                        item.Icon = null;
                    }
                }
                _viewModel.PopupItems.Clear();

                // 백그라운드 Task 정리
                foreach (var task in _backgroundTasks.ToList())
                {
                    if (task.IsCompleted)
                    {
                        task.Dispose();
                    }
                }
                _backgroundTasks.Clear();

                foreach (var task in _iconLoadingTasks.ToList())
                {
                    if (task.IsCompleted)
                    {
                        task.Dispose();
                    }
                }
                _iconLoadingTasks.Clear();

                // CancellationTokenSource 정리
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                // Activated 이벤트 제거
                this.Activated -= Window_Activated;

                // 참조 정리
                _groups = null;
                _clickedItem = null;
            }

            _disposed = true;
        }

        ~PopupWindow()
        {
            Dispose(disposing: false);
        }

    }

}
