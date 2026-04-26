using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using WinUIEx;
using AppGroup.Models;

namespace AppGroup.View
{
    /// <summary>
    /// 시작 메뉴 폴더 팝업 윈도우
    /// 트레이 아이콘 클릭 시 등록된 폴더 목록을 표시합니다.
    /// </summary>
    public sealed partial class StartMenuPopupWindow : Window, IDisposable
    {
        #region UI 크기 상수

        /// <summary>
        /// 아이콘 크기 (픽셀)
        /// </summary>
        private const int ICON_SIZE = 32;

        /// <summary>
        /// 그리드 레이아웃 아이콘 크기 (픽셀)
        /// </summary>
        private const int ICON_SIZE_GRID = 48;

        /// <summary>
        /// 항목 마진 (픽셀)
        /// </summary>
        private const int ITEM_MARGIN = 4;

        /// <summary>
        /// 윈도우 패딩 (픽셀)
        /// </summary>
        private const int WINDOW_PADDING = 10;

        /// <summary>
        /// 가로 레이아웃 텍스트 최대 너비 (픽셀)
        /// </summary>
        private const int HORIZONTAL_LAYOUT_TEXT_MAX_WIDTH = 150;

        /// <summary>
        /// 가로 레이아웃 스택 패널 간격 (픽셀)
        /// </summary>
        private const int HORIZONTAL_LAYOUT_STACK_SPACING = 10;

        /// <summary>
        /// 가로 레이아웃 버튼 패딩 (왼쪽, 위쪽, 오른쪽, 아래쪽, 픽셀)
        /// </summary>
        private const int HORIZONTAL_LAYOUT_BUTTON_PADDING_LEFT = 10;
        private const int HORIZONTAL_LAYOUT_BUTTON_PADDING_TOP = 8;
        private const int HORIZONTAL_LAYOUT_BUTTON_PADDING_RIGHT = 10;
        private const int HORIZONTAL_LAYOUT_BUTTON_PADDING_BOTTOM = 8;

        /// <summary>
        /// 그리드 레이아웃 텍스트 최대 너비 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_TEXT_MAX_WIDTH = 90;

        /// <summary>
        /// 그리드 레이아웃 스택 패널 수직 간격 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_STACK_VERTICAL_SPACING = 5;

        /// <summary>
        /// 그리드 레이아웃 텍스트 최대 줄 수
        /// </summary>
        private const int GRID_LAYOUT_TEXT_MAX_LINES = 2;

        /// <summary>
        /// 그리드 레이아웃 텍스트 폰트 크기
        /// </summary>
        private const int GRID_LAYOUT_TEXT_FONT_SIZE = 12;

        /// <summary>
        /// 그리드 레이아웃 버튼 너비 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_BUTTON_WIDTH = 100;

        /// <summary>
        /// 그리드 레이아웃 버튼 높이 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_BUTTON_HEIGHT = 100;

        /// <summary>
        /// 그리드 레이아웃 버튼 패딩 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_BUTTON_PADDING = 5;

        #endregion

        #region 윈도우 크기 계산 상수

        /// <summary>
        /// 1열 레이아웃 윈도우 너비 (픽셀)
        /// </summary>
        private const int SINGLE_COLUMN_WINDOW_WIDTH = 250;

        /// <summary>
        /// 1열 레이아웃 항목당 높이 (픽셀)
        /// </summary>
        private const int SINGLE_COLUMN_ITEM_HEIGHT = 56;

        /// <summary>
        /// 그리드 레이아웃 열당 너비 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_COLUMN_WIDTH = 110;

        /// <summary>
        /// 그리드 레이아웃 행당 높이 (픽셀)
        /// </summary>
        private const int GRID_LAYOUT_ROW_HEIGHT = 100;

        /// <summary>
        /// 윈도우 헤더 높이 (픽셀)
        /// </summary>
        private const int WINDOW_HEADER_HEIGHT = 45;

        /// <summary>
        /// 윈도우 너비 패딩 (픽셀)
        /// </summary>
        private const int WINDOW_WIDTH_PADDING = 30;

        /// <summary>
        /// 윈도우 높이 패딩 (픽셀)
        /// </summary>
        private const int WINDOW_HEIGHT_PADDING = 20;

        /// <summary>
        /// 화면 하단 마진 (픽셀)
        /// </summary>
        private const int SCREEN_BOTTOM_MARGIN = 100;

        /// <summary>
        /// 윈도우 최소 너비 (픽셀)
        /// </summary>
        private const int MIN_WINDOW_WIDTH = 150;

        /// <summary>
        /// 윈도우 최소 높이 (픽셀)
        /// </summary>
        private const int MIN_WINDOW_HEIGHT = 120;

        /// <summary>
        /// 1열 레이아웃 최대 높이 (픽셀)
        /// </summary>
        private const int MAX_HEIGHT_SINGLE_COLUMN = 1000;

        /// <summary>
        /// 그리드 레이아웃 최대 높이 (픽셀)
        /// </summary>
        private const int MAX_HEIGHT_GRID = 700;

        /// <summary>
        /// 윈도우 크롬 보정 마진 (왼쪽, 위쪽, 오른쪽, 아래쪽, 픽셀)
        /// </summary>
        private const int WINDOW_CHROME_MARGIN_LEFT = 0;
        private const int WINDOW_CHROME_MARGIN_TOP = 0;
        private const int WINDOW_CHROME_MARGIN_RIGHT = -5;
        private const int WINDOW_CHROME_MARGIN_BOTTOM = -15;

        /// <summary>
        /// 작업 표시줄 위 윈도우 배치 하단 마진 (픽셀)
        /// </summary>
        private const int TASKBAR_BOTTOM_MARGIN = 10;

        #endregion

        #region 색상 상수

        /// <summary>
        /// 다크 모드 배경색 (A, R, G, B)
        /// </summary>
        private const byte DARK_MODE_BACKGROUND_A = 230;
        private const byte DARK_MODE_BACKGROUND_R = 32;
        private const byte DARK_MODE_BACKGROUND_G = 32;
        private const byte DARK_MODE_BACKGROUND_B = 32;

        /// <summary>
        /// 라이트 모드 배경색 (A, R, G, B)
        /// </summary>
        private const byte LIGHT_MODE_BACKGROUND_A = 230;
        private const byte LIGHT_MODE_BACKGROUND_R = 243;
        private const byte LIGHT_MODE_BACKGROUND_G = 243;
        private const byte LIGHT_MODE_BACKGROUND_B = 243;

        /// <summary>
        /// 투명 배경색 (A, R, G, B)
        /// </summary>
        private const byte TRANSPARENT_BACKGROUND_A = 0;
        private const byte TRANSPARENT_BACKGROUND_R = 0;
        private const byte TRANSPARENT_BACKGROUND_G = 0;
        private const byte TRANSPARENT_BACKGROUND_B = 0;

        /// <summary>
        /// 호버 상태 배경색 (A, R, G, B)
        /// </summary>
        private const byte HOVER_BACKGROUND_A = 30;
        private const byte HOVER_BACKGROUND_R = 128;
        private const byte HOVER_BACKGROUND_G = 128;
        private const byte HOVER_BACKGROUND_B = 128;

        /// <summary>
        /// 테마 감지 임계값 (R 값 기준)
        /// </summary>
        private const byte THEME_DETECTION_THRESHOLD = 128;

        #endregion

        #region 경로 상수

        /// <summary>
        /// 기본 폴더 아이콘 경로
        /// </summary>
        private const string DEFAULT_FOLDER_ICON_PATH = "ms-appx:///Assets/icon/folder_3.png";

        #endregion

        #region 팝업 위치 계산 상수

        /// <summary>
        /// 폴더 내용 팝업 겹침 정도 (픽셀, 양수=겹침, 음수=간격)
        /// </summary>
        private const int POPUP_OVERLAP = 20;

        /// <summary>
        /// 상단 최소 여백 (픽셀)
        /// </summary>
        private const int TOP_MARGIN = 100;

        /// <summary>
        /// 호버 딜레이 (밀리초)
        /// </summary>
        private const int HOVER_DELAY_MS = 200;

        #endregion

        private readonly WindowHelper _windowHelper;
        private IntPtr _hwnd;
        private int _columnCount = 1;
        private int _subfolderDepth = 2;
        private bool _disposed = false;
        private UISettings _uiSettings;

        // 윈도우 크기 저장 (SetWindowPos에서 사용)
        private int _currentWindowWidth = 250;
        private int _currentWindowHeight = 200;

        // 이중 로딩 방지 플래그
        private bool _isShowingPopup = false;

        private FolderContentsPopupWindow? _folderContentsPopup;
        private Button? _currentHoveredButton;
        private DispatcherTimer? _hoverTimer;

        // 정적 SolidColorBrush (테마별 캐싱)
        private static readonly SolidColorBrush DarkModeBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(DARK_MODE_BACKGROUND_A, DARK_MODE_BACKGROUND_R, DARK_MODE_BACKGROUND_G, DARK_MODE_BACKGROUND_B));
        private static readonly SolidColorBrush LightModeBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(LIGHT_MODE_BACKGROUND_A, LIGHT_MODE_BACKGROUND_R, LIGHT_MODE_BACKGROUND_G, LIGHT_MODE_BACKGROUND_B));
        private static readonly SolidColorBrush TransparentBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(TRANSPARENT_BACKGROUND_A, TRANSPARENT_BACKGROUND_R, TRANSPARENT_BACKGROUND_G, TRANSPARENT_BACKGROUND_B));
        private static readonly SolidColorBrush HoverBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(HOVER_BACKGROUND_A, HOVER_BACKGROUND_R, HOVER_BACKGROUND_G, HOVER_BACKGROUND_B));

        public StartMenuPopupWindow()
        {
            InitializeComponent();

            // 저장된 테마 설정 적용
            if (Content is FrameworkElement rootElement)
            {
                string savedTheme = SettingsHelper.GetSavedTheme();
                if (!string.IsNullOrWhiteSpace(savedTheme))
                {
                    rootElement.RequestedTheme = savedTheme switch
                    {
                        "Dark" => ElementTheme.Dark,
                        "Light" => ElementTheme.Light,
                        _ => ElementTheme.Default
                    };
                }
            }

            _windowHelper = new WindowHelper(this);
            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = true;
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = false;
            _windowHelper.IsAlwaysOnTop = true;

            this.Hide();

            _hwnd = WindowNative.GetWindowHandle(this);
            this.AppWindow.IsShownInSwitchers = false;

            // 테마 변경 감지
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

            // 폴더 내용 팝업 윈도우 초기화
            _folderContentsPopup = new FolderContentsPopupWindow();
            _folderContentsPopup.FileExecuted += FolderContentsPopup_FileExecuted;

            // 호버 타이머 초기화 (200ms 지연)
            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(HOVER_DELAY_MS);
            _hoverTimer.Tick += HoverTimer_Tick;

            this.Activated += Window_Activated;
        }

        private void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateMainGridBackground(sender);
            });
        }

        private void UpdateMainGridBackground(UISettings settings)
        {
            try
            {
                bool isDarkMode;

                // 앱 자체 테마 설정이 있으면 우선 사용
                if (Content is FrameworkElement rootElement && rootElement.RequestedTheme != ElementTheme.Default)
                {
                    isDarkMode = rootElement.RequestedTheme == ElementTheme.Dark;
                }
                else
                {
                    var foreground = settings.GetColorValue(UIColorType.Foreground);
                    isDarkMode = foreground.R > THEME_DETECTION_THRESHOLD;
                }

                MainGrid.Background = isDarkMode ? DarkModeBackground : LightModeBackground;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating background: {ex.Message}");
            }
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                // ShowPopup에서 이미 로드한 경우 중복 로드 방지
                if (!_isShowingPopup)
                {
                    await LoadFoldersAsync();
                }
            }
            else
            {
                // 포커스를 잃으면 윈도우 숨기기
                _isShowingPopup = false;
                StopHoverTimer();
                HideFolderContentsPopup();
                this.Hide();
            }
        }

        /// <summary>
        /// 폴더 목록을 로드하고 UI를 업데이트합니다.
        /// </summary>
        public async Task LoadFoldersAsync()
        {
            try
            {
                // 설정에서 열 개수 및 하위 폴더 탐색 개수 로드
                var settings = await SettingsHelper.LoadSettingsAsync();
                _columnCount = Math.Max(1, Math.Min(5, settings.FolderColumnCount));
                _subfolderDepth = Math.Max(1, Math.Min(5, settings.SubfolderDepth));

                // 폴더 목록 로드
                var folders = await JsonConfigHelper.LoadStartMenuFoldersAsync();

                // UI 업데이트 - TaskCompletionSource를 사용하여 완료 대기
                var tcs = new TaskCompletionSource<bool>();
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        BuildFolderUI(folders);

                        // 레이아웃 강제 갱신 후 실제 콘텐츠 높이로 윈도우 크기 계산
                        FolderPanel.UpdateLayout();
                        UpdateWindowSizeFromActualHeight();

                        PositionWindowAboveTaskbar();

                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
                await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 로드 오류: {ex.Message}");
            }
        }

        private void OnFolderPanelLoaded(object sender, RoutedEventArgs e)
        {
            // 레이아웃 완료 후 실제 콘텐츠 높이에 맞춰 윈도우 크기 재조정
            UpdateWindowSizeFromActualHeight();
        }

        /// <summary>
        /// 항목 수 기반으로 윈도우 크기 계산 및 설정
        /// </summary>
        private void UpdateWindowSizeFromActualHeight()
        {
            // 항목 수 기반 콘텐츠 높이 계산 (숨겨진 윈도우에서 ActualHeight가 부정확할 수 있음)
            int itemCount = FolderPanel.Children.Count;
            double contentHeight;

            if (_columnCount == 1)
            {
                // 1열: 각 버튼이 FolderPanel에 직접 추가됨 → Children.Count = 아이템 수
                contentHeight = itemCount * SINGLE_COLUMN_ITEM_HEIGHT;
            }
            else
            {
                // 그리드: 행 StackPanel이 FolderPanel에 추가됨 → Children.Count = 행 수
                int actualRowHeight = GRID_LAYOUT_BUTTON_HEIGHT + ITEM_MARGIN * 2;
                contentHeight = itemCount * actualRowHeight;
            }

            // DPI 스케일 계수 가져오기
            uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
            float scaleFactor = (float)dpi / 96.0f;

            // 윈도우 크기 계산 (논리적 픽셀)
            // 콘텐츠 + 헤더 + ScrollViewer 마진(5+10) + 패딩
            double totalLogicalHeight = contentHeight + WINDOW_HEADER_HEIGHT + 15 + WINDOW_HEIGHT_PADDING;

            // 물리적 픽셀로 변환 (올림으로 서브픽셀 손실 방지)
            int newWindowHeight = (int)Math.Ceiling(totalLogicalHeight * scaleFactor);

            // 화면 최대 높이 제한 확인
            NativeMethods.POINT cursorPos;
            NativeMethods.GetCursorPos(out cursorPos);
            IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
            NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
            monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

            int screenHeight = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
            int maxFixedHeight = _columnCount == 1 ? MAX_HEIGHT_SINGLE_COLUMN : MAX_HEIGHT_GRID;
            int maxAllowedHeight = Math.Min(screenHeight - SCREEN_BOTTOM_MARGIN, maxFixedHeight);

            newWindowHeight = Math.Max(newWindowHeight, MIN_WINDOW_HEIGHT);
            if (newWindowHeight > maxAllowedHeight)
            {
                newWindowHeight = maxAllowedHeight;
            }

            // 너비 계산 (DPI 적용)
            // 그리드 레이아웃 열 너비 = 버튼 너비 + 좌우 마진 (상수 불일치 방지)
            int actualColumnWidth = GRID_LAYOUT_BUTTON_WIDTH + ITEM_MARGIN * 2;
            int dynamicWidth = _columnCount == 1 ? SINGLE_COLUMN_WINDOW_WIDTH : _columnCount * actualColumnWidth;
            int newWindowWidth = (int)(dynamicWidth * scaleFactor) + WINDOW_WIDTH_PADDING;
            newWindowWidth = Math.Max(newWindowWidth, MIN_WINDOW_WIDTH);

            // 윈도우 크롬 보정 마진 적용 (하단 non-client area 보정)
            MainGrid.Margin = new Thickness(
                WINDOW_CHROME_MARGIN_LEFT,
                WINDOW_CHROME_MARGIN_TOP,
                WINDOW_CHROME_MARGIN_RIGHT,
                WINDOW_CHROME_MARGIN_BOTTOM);

            // MaxHeight 제거 - Grid Row * 에 의해 자연스럽게 제한
            ScrollView.ClearValue(FrameworkElement.MaxHeightProperty);

            _currentWindowWidth = newWindowWidth;
            _currentWindowHeight = newWindowHeight;
            _windowHelper.SetSize(_currentWindowWidth, _currentWindowHeight);

            PositionWindowAboveTaskbar();
        }

        /// <summary>
        /// 폴더 UI를 구성합니다.
        /// </summary>
        private void BuildFolderUI(List<StartMenuItem> folders)
        {
            FolderPanel.Children.Clear();

            if (folders.Count == 0)
            {
                // 빈 상태 표시
                var emptyText = new TextBlock
                {
                    Text = "등록된 폴더가 없습니다.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 128, 128, 128))
                };
                FolderPanel.Children.Add(emptyText);
                return;
            }

            if (_columnCount == 1)
            {
                // 1열: 가로 레이아웃 (아이콘 왼쪽, 이름 오른쪽)
                BuildHorizontalLayout(folders);
            }
            else
            {
                // 2열 이상: 그리드 레이아웃 (아이콘 위, 이름 아래)
                BuildGridLayout(folders);
            }
        }

        /// <summary>
        /// 1열 레이아웃: 아이콘 왼쪽, 이름 오른쪽
        /// </summary>
        private void BuildHorizontalLayout(List<StartMenuItem> folders)
        {
            foreach (var folder in folders)
            {
                var button = CreateHorizontalFolderButton(folder);
                FolderPanel.Children.Add(button);
            }
        }

        /// <summary>
        /// 그리드 레이아웃: 아이콘 위, 이름 아래
        /// </summary>
        private void BuildGridLayout(List<StartMenuItem> folders)
        {
            int rowCount = (int)Math.Ceiling((double)folders.Count / _columnCount);

            for (int row = 0; row < rowCount; row++)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                for (int col = 0; col < _columnCount; col++)
                {
                    int index = row * _columnCount + col;
                    if (index < folders.Count)
                    {
                        var button = CreateGridFolderButton(folders[index]);
                        rowPanel.Children.Add(button);
                    }
                }

                FolderPanel.Children.Add(rowPanel);
            }
        }

        /// <summary>
        /// 가로 레이아웃 폴더 버튼 생성
        /// </summary>
        private Button CreateHorizontalFolderButton(StartMenuItem folder)
        {
            var icon = new Image
            {
                Width = ICON_SIZE,
                Height = ICON_SIZE,
                Stretch = Stretch.Uniform
            };
            LoadFolderIcon(icon, folder.FolderIcon);

            var nameText = new TextBlock
            {
                Text = folder.FolderName,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = HORIZONTAL_LAYOUT_TEXT_MAX_WIDTH
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = HORIZONTAL_LAYOUT_STACK_SPACING
            };
            content.Children.Add(icon);
            content.Children.Add(nameText);

            var button = new Button
            {
                Content = content,
                Tag = folder.FolderPath,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(ITEM_MARGIN),
                Padding = new Thickness(HORIZONTAL_LAYOUT_BUTTON_PADDING_LEFT, HORIZONTAL_LAYOUT_BUTTON_PADDING_TOP, HORIZONTAL_LAYOUT_BUTTON_PADDING_RIGHT, HORIZONTAL_LAYOUT_BUTTON_PADDING_BOTTOM),
                Background = TransparentBackground,
                BorderThickness = new Thickness(0)
            };
            button.Click += FolderButton_Click;
            button.PointerEntered += Button_PointerEntered;
            button.PointerExited += Button_PointerExited;

            ToolTipService.SetToolTip(button, folder.FolderPath);

            return button;
        }

        /// <summary>
        /// 그리드 레이아웃 폴더 버튼 생성
        /// </summary>
        private Button CreateGridFolderButton(StartMenuItem folder)
        {
            var icon = new Image
            {
                Width = ICON_SIZE_GRID,
                Height = ICON_SIZE_GRID,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            LoadFolderIcon(icon, folder.FolderIcon);

            var nameText = new TextBlock
            {
                Text = folder.FolderName,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = GRID_LAYOUT_TEXT_MAX_LINES,
                MaxWidth = GRID_LAYOUT_TEXT_MAX_WIDTH,
                FontSize = GRID_LAYOUT_TEXT_FONT_SIZE
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = GRID_LAYOUT_STACK_VERTICAL_SPACING,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(icon);
            content.Children.Add(nameText);

            var button = new Button
            {
                Content = content,
                Tag = folder.FolderPath,
                Width = GRID_LAYOUT_BUTTON_WIDTH,
                Height = GRID_LAYOUT_BUTTON_HEIGHT,
                Margin = new Thickness(ITEM_MARGIN),
                Padding = new Thickness(GRID_LAYOUT_BUTTON_PADDING),
                Background = TransparentBackground,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Click += FolderButton_Click;
            button.PointerEntered += Button_PointerEntered;
            button.PointerExited += Button_PointerExited;

            ToolTipService.SetToolTip(button, folder.FolderPath);

            return button;
        }

        /// <summary>
        /// 폴더 아이콘 로드
        /// </summary>
        private void LoadFolderIcon(Image imageControl, string iconPath)
        {
            try
            {
                if (string.IsNullOrEmpty(iconPath) || iconPath.StartsWith("/Assets"))
                {
                    imageControl.Source = new BitmapImage(new Uri(DEFAULT_FOLDER_ICON_PATH));
                }
                else if (File.Exists(iconPath))
                {
                    imageControl.Source = new BitmapImage(new Uri(iconPath));
                }
                else
                {
                    imageControl.Source = new BitmapImage(new Uri(DEFAULT_FOLDER_ICON_PATH));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"아이콘 로드 오류: {ex.Message}");
                imageControl.Source = new BitmapImage(new Uri(DEFAULT_FOLDER_ICON_PATH));
            }
        }

        /// <summary>
        /// 폴더 버튼 클릭 이벤트
        /// </summary>
        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string folderPath)
            {
                try
                {
                    if (Directory.Exists(folderPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"폴더가 존재하지 않습니다: {folderPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"폴더 열기 오류: {ex.Message}");
                }

                StopHoverTimer();
                HideFolderContentsPopup();
                this.Hide();
            }
        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = HoverBackground;

                // 폴더 내용 팝업 표시를 위한 타이머 시작
                _currentHoveredButton = button;
                _hoverTimer?.Start();
            }
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = TransparentBackground;

                // 타이머 중지
                _hoverTimer?.Stop();

                // 마우스가 폴더 내용 팝업으로 이동하지 않은 경우에만 숨기기
                // (팝업에 마우스가 있으면 숨기지 않음)
                if (_currentHoveredButton == button)
                {
                    _currentHoveredButton = null;
                }
            }
        }

        /// <summary>
        /// 호버 타이머 틱 이벤트 - 폴더 내용 팝업 표시
        /// </summary>
        private void HoverTimer_Tick(object? sender, object e)
        {
            // one-shot 동작: 틱 발생 시 즉시 중지하여 반복 호출 방지
            _hoverTimer?.Stop();
            if (_subfolderDepth >= 2 && _currentHoveredButton != null && _currentHoveredButton.Tag is string folderPath)
            {
                ShowFolderContentsPopup(_currentHoveredButton, folderPath);
            }
        }

        /// <summary>
        /// 폴더 내용 팝업 표시
        /// </summary>
        private void ShowFolderContentsPopup(Button button, string folderPath)
        {
            if (_folderContentsPopup == null || !Directory.Exists(folderPath))
                return;

            try
            {
                // 폴더 이름 가져오기
                string folderName = Path.GetFileName(folderPath);
                if (string.IsNullOrEmpty(folderName))
                    folderName = folderPath;

                // 하위 폴더 탐색 깊이 설정 (현재 depth=2, tray 폴더가 depth=1)
                _folderContentsPopup.SetDepth(2, _subfolderDepth);

                // 폴더 내용 로드
                _folderContentsPopup.LoadFolderContents(folderPath, folderName);

                // 버튼의 화면 위치 계산
                var transform = button.TransformToVisual(null);
                var buttonPosition = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                // 현재 윈도우의 위치와 크기 가져오기
                var windowPos = this.AppWindow.Position;
                var windowSize = this.AppWindow.Size;
                var popupSize = _folderContentsPopup.AppWindow.Size;

                // 팝업 윈도우를 현재 윈도우의 왼쪽에 배치
                int popupWidth = popupSize.Width; // 실제 팝업 크기 사용
                int popupX = windowPos.X - popupWidth + POPUP_OVERLAP;
                int popupY = windowPos.Y + (int)buttonPosition.Y;

                // 화면 밖으로 나가지 않도록 조정
                NativeMethods.POINT cursorPos = new NativeMethods.POINT { X = popupX, Y = popupY };
                IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

                if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    // X 위치 조정: 왼쪽에 공간이 없으면 오른쪽에 표시
                    if (popupX < monitorInfo.rcWork.left)
                    {
                        popupX = windowPos.X + windowSize.Width - POPUP_OVERLAP;
                    }

                    // X가 오른쪽 경계를 넘으면 조정
                    if (popupX + popupWidth > monitorInfo.rcWork.right)
                    {
                        popupX = monitorInfo.rcWork.right - popupWidth;
                    }

                    // Y 위치 조정: 작업 영역 하단을 넘으면 위로 이동
                    if (popupY + popupSize.Height > monitorInfo.rcWork.bottom)
                    {
                        popupY = monitorInfo.rcWork.bottom - popupSize.Height;
                    }

                    // 상단 경계 확인: 최소 100픽셀 떨어지도록
                    if (popupY < monitorInfo.rcWork.top + TOP_MARGIN)
                    {
                        popupY = monitorInfo.rcWork.top + TOP_MARGIN;
                    }
                }

                _folderContentsPopup.ShowAt(popupX, popupY);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 내용 팝업 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더 내용 팝업에서 파일/폴더 실행 시 호출
        /// </summary>
        private void FolderContentsPopup_FileExecuted(object? sender, EventArgs e)
        {
            // 모든 윈도우 숨기기
            StopHoverTimer();
            _folderContentsPopup?.HidePopup();
            this.Hide();
        }

        /// <summary>
        /// 호버 타이머를 중지하고 현재 호버 버튼 참조를 해제합니다.
        /// </summary>
        private void StopHoverTimer()
        {
            _hoverTimer?.Stop();
            _currentHoveredButton = null;
        }

        /// <summary>
        /// 폴더 내용 팝업 숨기기
        /// </summary>
        private void HideFolderContentsPopup()
        {
            _folderContentsPopup?.HidePopup();
        }








        /// <summary>
        /// 윈도우 크기 업데이트 - PopupWindow와 동일한 방식
        /// </summary>
        private void UpdateWindowSize(int folderCount)
        {
            // DPI 스케일 계수 가져오기 (PopupWindow와 동일)
            uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
            float scaleFactor = (float)dpi / 96.0f;

            // 콘텐츠 크기 계산
            int dynamicWidth;
            int dynamicHeight;

            if (_columnCount == 1)
            {
                // 1열: 가로 레이아웃
                dynamicWidth = SINGLE_COLUMN_WINDOW_WIDTH;
                dynamicHeight = folderCount * SINGLE_COLUMN_ITEM_HEIGHT; // 버튼 높이 + 마진
            }
            else
            {
                // 그리드 레이아웃
                int rowCount = (int)Math.Ceiling((double)folderCount / _columnCount);
                dynamicWidth = _columnCount * GRID_LAYOUT_COLUMN_WIDTH;
                dynamicHeight = rowCount * GRID_LAYOUT_ROW_HEIGHT;
            }

            // 헤더 높이 추가
            dynamicHeight += WINDOW_HEADER_HEIGHT;

            // DPI 스케일 적용 (PopupWindow와 동일)
            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);

            // 패딩 추가 (PopupWindow와 동일)
            int finalWidth = scaledWidth + WINDOW_WIDTH_PADDING;
            int finalHeight = scaledHeight + WINDOW_HEIGHT_PADDING;

            // 화면 최대 높이 제한 (커서가 있는 모니터 기준)
            NativeMethods.POINT cursorPos;
            NativeMethods.GetCursorPos(out cursorPos);
            IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
            NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
            monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

            int screenHeight = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
            int maxFixedHeight = _columnCount == 1 ? MAX_HEIGHT_SINGLE_COLUMN : MAX_HEIGHT_GRID;
            int maxAllowedHeight = Math.Min(screenHeight - SCREEN_BOTTOM_MARGIN, maxFixedHeight);
            if (finalHeight > maxAllowedHeight)
            {
                finalHeight = maxAllowedHeight;
            }

            // 최소 크기 보장
            finalWidth = Math.Max(finalWidth, MIN_WINDOW_WIDTH);
            finalHeight = Math.Max(finalHeight, MIN_WINDOW_HEIGHT);

            Debug.WriteLine($"UpdateWindowSize: folders={folderCount}, scaleFactor={scaleFactor}, finalWidth={finalWidth}, finalHeight={finalHeight}");

            // PopupWindow와 동일하게 음수 마진 적용 (윈도우 크롬 보정)
            MainGrid.Margin = new Thickness(WINDOW_CHROME_MARGIN_LEFT, WINDOW_CHROME_MARGIN_TOP, WINDOW_CHROME_MARGIN_RIGHT, WINDOW_CHROME_MARGIN_BOTTOM);

            // 윈도우 크기 저장 및 적용
            _currentWindowWidth = finalWidth;
            _currentWindowHeight = finalHeight;
            _windowHelper.SetSize(finalWidth, finalHeight);
        }

        /// <summary>
        /// 작업 표시줄 위에 윈도우 위치 설정
        /// </summary>
        private void PositionWindowAboveTaskbar()
        {
            try
            {
                NativeMethods.PositionWindowAboveTaskbar(_hwnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"윈도우 위치 설정 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 팝업 윈도우 표시
        /// </summary>
        public async void ShowPopup()
        {
            try
            {
                // 이중 로딩 방지 플래그 설정
                _isShowingPopup = true;

                // 저장된 테마 설정 재적용 (설정 변경 반영)
                if (Content is FrameworkElement rootElement)
                {
                    string savedTheme = SettingsHelper.GetSavedTheme();
                    rootElement.RequestedTheme = savedTheme switch
                    {
                        "Dark" => ElementTheme.Dark,
                        "Light" => ElementTheme.Light,
                        _ => ElementTheme.Default
                    };
                }

                // 테마에 맞는 배경색 설정
                UpdateMainGridBackground(_uiSettings);

                // 폴더 목록을 먼저 로드하여 크기 계산
                await LoadFoldersAsync();

                // 윈도우 크기 먼저 적용
                _windowHelper.SetSize(_currentWindowWidth, _currentWindowHeight);

                // 윈도우 위치 계산 (작업 표시줄 위)
                var (x, y) = CalculateWindowPosition();

                // SetWindowPos로 위치 및 표시 (크기는 이미 SetSize로 적용됨)
                NativeMethods.SetWindowPos(
                    _hwnd,
                    NativeMethods.HWND_TOPMOST,
                    x, y,
                    _currentWindowWidth, _currentWindowHeight,
                    NativeMethods.SWP_SHOWWINDOW);

                NativeMethods.SetForegroundWindow(_hwnd);
                this.Activate();

                // 플래그 해제는 Deactivated에서 처리
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"팝업 표시 오류: {ex.Message}");
                _isShowingPopup = false;
            }
        }

        /// <summary>
        /// 작업 표시줄 위 윈도우 위치 계산
        /// </summary>
        private (int x, int y) CalculateWindowPosition()
        {
            // 현재 커서 위치 가져오기
            NativeMethods.POINT cursorPos;
            NativeMethods.GetCursorPos(out cursorPos);

            // 모니터 정보 가져오기
            IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
            NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
            monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

            // 작업 영역 기준으로 위치 계산
            int x = cursorPos.X - (_currentWindowWidth / 2);
            int y = monitorInfo.rcWork.bottom - _currentWindowHeight - TASKBAR_BOTTOM_MARGIN;

            // 화면 경계 조정
            if (x < monitorInfo.rcWork.left)
                x = monitorInfo.rcWork.left;
            if (x + _currentWindowWidth > monitorInfo.rcWork.right)
                x = monitorInfo.rcWork.right - _currentWindowWidth;

            return (x, y);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (_uiSettings != null)
                {
                    _uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
                    _uiSettings = null;
                }

                if (_hoverTimer != null)
                {
                    _hoverTimer.Stop();
                    _hoverTimer.Tick -= HoverTimer_Tick;
                    _hoverTimer = null;
                }

                if (_folderContentsPopup != null)
                {
                    _folderContentsPopup.FileExecuted -= FolderContentsPopup_FileExecuted;
                    _folderContentsPopup.Dispose();
                    _folderContentsPopup = null;
                }

                this.Activated -= Window_Activated;
            }

            _disposed = true;
        }

        ~StartMenuPopupWindow()
        {
            Dispose(false);
        }

        /// <summary>
        /// 설정 버튼 클릭 이벤트 - MainWindow의 시작 탭을 표시합니다.
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 팝업 숨기기
                HideFolderContentsPopup();
                this.Hide();

                // MainWindow 가져오기 및 시작 탭으로 이동
                var mainWindow = App.Current.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.NavigateToStartMenuTab();
                    mainWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"설정 버튼 클릭 오류: {ex.Message}");
            }
        }
    }
}
