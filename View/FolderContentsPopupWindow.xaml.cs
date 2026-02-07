using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup.View
{
    /// <summary>
    /// 폴더 내용 팝업 윈도우
    /// 폴더에 마우스를 올리면 왼쪽에 해당 폴더의 파일과 하위 폴더 목록을 표시합니다.
    /// </summary>
    public sealed partial class FolderContentsPopupWindow : Window, IDisposable
    {
        private static readonly ResourceLoader _resourceLoader = new ResourceLoader();
        #region UI 크기 상수

        /// <summary>
        /// 아이콘 크기 (픽셀)
        /// </summary>
        private const int ICON_SIZE = 20;

        /// <summary>
        /// 항목 마진 (픽셀)
        /// </summary>
        private const int ITEM_MARGIN = 2;

        /// <summary>
        /// 윈도우 너비 (픽셀)
        /// </summary>
        private const int WINDOW_WIDTH = 250;

        /// <summary>
        /// 윈도우 최대 높이 (픽셀)
        /// </summary>
        private const int MAX_WINDOW_HEIGHT = 1000;

        /// <summary>
        /// 윈도우 최소 높이 (픽셀)
        /// </summary>
        private const int MIN_WINDOW_HEIGHT = 100;

        /// <summary>
        /// 텍스트 최대 너비 (픽셀)
        /// </summary>
        private const int TEXT_MAX_WIDTH = 180;

        /// <summary>
        /// 스택 패널 간격 (픽셀)
        /// </summary>
        private const int STACK_PANEL_SPACING = 8;

        /// <summary>
        /// 버튼 패딩 (왼쪽, 위쪽, 오른쪽, 아래쪽, 픽셀)
        /// </summary>
        private const int BUTTON_PADDING_LEFT = 8;
        private const int BUTTON_PADDING_TOP = 6;
        private const int BUTTON_PADDING_RIGHT = 8;
        private const int BUTTON_PADDING_BOTTOM = 6;

        #endregion

        #region 윈도우 크기 계산 상수

        /// <summary>
        /// 항목당 높이 (버튼 높이 + 마진, 픽셀)
        /// </summary>
        private const int ITEM_HEIGHT = 36;

        /// <summary>
        /// 섹션 헤더 높이 (픽셀)
        /// </summary>
        private const int SECTION_HEADER_HEIGHT = 30;

        /// <summary>
        /// 상단 헤더 높이 (픽셀)
        /// </summary>
        private const int TOP_HEADER_HEIGHT = 50;

        /// <summary>
        /// 빈 상태일 때 윈도우 높이 (픽셀)
        /// </summary>
        private const int EMPTY_STATE_HEIGHT = 80;

        /// <summary>
        /// 윈도우 너비 패딩 (픽셀)
        /// </summary>
        private const int WINDOW_WIDTH_PADDING = 30;

        /// <summary>
        /// 윈도우 높이 패딩 (픽셀)
        /// </summary>
        private const int WINDOW_HEIGHT_PADDING = 20;

        /// <summary>
        /// 화면 하단 여백 (픽셀)
        /// </summary>
        private const int SCREEN_BOTTOM_MARGIN = 100;

        /// <summary>
        /// 윈도우 최소 너비 (픽셀)
        /// </summary>
        private const int MIN_WINDOW_WIDTH = 150;

        /// <summary>
        /// 윈도우 크롬 보정 마진 (왼쪽, 위쪽, 오른쪽, 아래쪽, 픽셀)
        /// </summary>
        private const int WINDOW_CHROME_MARGIN_LEFT = 0;
        private const int WINDOW_CHROME_MARGIN_TOP = 0;
        private const int WINDOW_CHROME_MARGIN_RIGHT = -5;
        private const int WINDOW_CHROME_MARGIN_BOTTOM = -15;

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

        /// <summary>
        /// 앱 내 리소스 경로 접두사
        /// </summary>
        private const string APP_RESOURCE_PREFIX = "ms-appx:///";

        #endregion

        private readonly WindowHelper _windowHelper;
        private IntPtr _hwnd;
        private bool _disposed = false;
        private UISettings _uiSettings;
        private string _currentFolderPath = string.Empty;

        // 윈도우 크기 저장
        private int _currentWindowWidth = 250;
        private int _currentWindowHeight = 200;

        // 정적 SolidColorBrush (테마별 캐싱)
        private static readonly SolidColorBrush DarkModeBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(DARK_MODE_BACKGROUND_A, DARK_MODE_BACKGROUND_R, DARK_MODE_BACKGROUND_G, DARK_MODE_BACKGROUND_B));
        private static readonly SolidColorBrush LightModeBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(LIGHT_MODE_BACKGROUND_A, LIGHT_MODE_BACKGROUND_R, LIGHT_MODE_BACKGROUND_G, LIGHT_MODE_BACKGROUND_B));
        private static readonly SolidColorBrush TransparentBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(TRANSPARENT_BACKGROUND_A, TRANSPARENT_BACKGROUND_R, TRANSPARENT_BACKGROUND_G, TRANSPARENT_BACKGROUND_B));
        private static readonly SolidColorBrush HoverBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(HOVER_BACKGROUND_A, HOVER_BACKGROUND_R, HOVER_BACKGROUND_G, HOVER_BACKGROUND_B));

        // 기본 파일 아이콘 경로
        private const string FallbackDefaultIcon = "Assets/icon/file_4.png";

        public FolderContentsPopupWindow()
        {
            InitializeComponent();

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

            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
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
                var foreground = settings.GetColorValue(UIColorType.Foreground);
                bool isDarkMode = foreground.R > THEME_DETECTION_THRESHOLD;

                MainGrid.Background = isDarkMode ? DarkModeBackground : LightModeBackground;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating background: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더 내용을 로드하고 표시합니다.
        /// </summary>
        /// <param name="folderPath">표시할 폴더 경로</param>
        /// <param name="folderName">폴더 이름 (헤더에 표시)</param>
        public void LoadFolderContents(string folderPath, string folderName)
        {
            // 이미 같은 폴더가 로드되어 있으면 다시 로드하지 않음 (아이콘 깜빡임 방지)
            if (_currentFolderPath == folderPath && (FileItemsControl.Items.Count > 0 || FolderItemsControl.Items.Count > 0))
            {
                return;
            }

            _currentFolderPath = folderPath;
            HeaderText.Text = folderName;

            FileItemsControl.Items.Clear();
            FolderItemsControl.Items.Clear();

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    ShowEmptyState(_resourceLoader.GetString("FolderNotFound"));
                    return;
                }

                var files = Directory.GetFiles(folderPath)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.Name)
                    .ToList();

                var folders = Directory.GetDirectories(folderPath)
                    .Select(d => new DirectoryInfo(d))
                    .OrderBy(d => d.Name)
                    .ToList();

                if (files.Count == 0 && folders.Count == 0)
                {
                    ShowEmptyState(_resourceLoader.GetString("EmptyFolder"));
                    return;
                }

                EmptyText.Visibility = Visibility.Collapsed;

                // 파일 목록 표시
                if (files.Count > 0)
                {
                    FileSectionHeader.Visibility = Visibility.Visible;
                    foreach (var file in files)
                    {
                        var button = CreateFileButton(file);
                        FileItemsControl.Items.Add(button);
                    }
                }
                else
                {
                    FileSectionHeader.Visibility = Visibility.Collapsed;
                }

                // 폴더 목록 표시
                if (folders.Count > 0)
                {
                    FolderSectionHeader.Visibility = Visibility.Visible;
                    foreach (var folder in folders)
                    {
                        var button = CreateFolderButton(folder);
                        FolderItemsControl.Items.Add(button);
                    }
                }
                else
                {
                    FolderSectionHeader.Visibility = Visibility.Collapsed;
                }

                // 레이아웃 강제 갱신 후 실제 콘텐츠 높이로 윈도우 크기 계산
                ContentsPanel.UpdateLayout();
                UpdateWindowSizeFromActualHeight();
            }
            catch (UnauthorizedAccessException)
            {
                ShowEmptyState(_resourceLoader.GetString("AccessDenied"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 내용 로드 오류: {ex.Message}");
                ShowEmptyState(_resourceLoader.GetString("ErrorOccurred"));
            }
        }

        private void ShowEmptyState(string message)
        {
            FileSectionHeader.Visibility = Visibility.Collapsed;
            FolderSectionHeader.Visibility = Visibility.Collapsed;
            EmptyText.Text = message;
            EmptyText.Visibility = Visibility.Visible;
            UpdateWindowSize(0, 0);
        }

        /// <summary>
        /// 파일 버튼 생성
        /// </summary>
        private Button CreateFileButton(FileInfo file)
        {
            var icon = new Image
            {
                Width = ICON_SIZE,
                Height = ICON_SIZE,
                Stretch = Stretch.Uniform
            };

            // 기본 아이콘으로 초기화 후 비동기로 실제 아이콘 로드
            LoadFileIcon(icon, file.FullName);

            var nameText = new TextBlock
            {
                Text = file.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = TEXT_MAX_WIDTH
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = STACK_PANEL_SPACING
            };
            content.Children.Add(icon);
            content.Children.Add(nameText);

            var button = new Button
            {
                Content = content,
                Tag = file.FullName,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(ITEM_MARGIN),
                Padding = new Thickness(BUTTON_PADDING_LEFT, BUTTON_PADDING_TOP, BUTTON_PADDING_RIGHT, BUTTON_PADDING_BOTTOM),
                Background = TransparentBackground,
                BorderThickness = new Thickness(0)
            };
            button.Click += FileButton_Click;
            button.PointerEntered += Button_PointerEntered;
            button.PointerExited += Button_PointerExited;

            ToolTipService.SetToolTip(button, file.FullName);

            return button;
        }

        /// <summary>
        /// 폴더 버튼 생성
        /// </summary>
        private Button CreateFolderButton(DirectoryInfo folder)
        {
            var icon = new Image
            {
                Width = ICON_SIZE,
                Height = ICON_SIZE,
                Stretch = Stretch.Uniform,
                Source = new BitmapImage(new Uri(DEFAULT_FOLDER_ICON_PATH))
            };

            var nameText = new TextBlock
            {
                Text = folder.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = TEXT_MAX_WIDTH
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = STACK_PANEL_SPACING
            };
            content.Children.Add(icon);
            content.Children.Add(nameText);

            var button = new Button
            {
                Content = content,
                Tag = folder.FullName,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(ITEM_MARGIN),
                Padding = new Thickness(BUTTON_PADDING_LEFT, BUTTON_PADDING_TOP, BUTTON_PADDING_RIGHT, BUTTON_PADDING_BOTTOM),
                Background = TransparentBackground,
                BorderThickness = new Thickness(0)
            };
            button.Click += FolderButton_Click;
            button.PointerEntered += Button_PointerEntered;
            button.PointerExited += Button_PointerExited;

            ToolTipService.SetToolTip(button, folder.FullName);

            return button;
        }

        /// <summary>
        /// 파일 아이콘 로드 (실제 파일 아이콘 추출)
        /// </summary>
        private void LoadFileIcon(Image imageControl, string filePath)
        {
            try
            {
                // 이미지 컨트롤에 태그로 파일 경로 저장 (중복 업데이트 방지)
                imageControl.Tag = filePath;

                // 먼저 기본 아이콘으로 설정 (로딩 중 표시)
                imageControl.Source = new BitmapImage(new Uri($"{APP_RESOURCE_PREFIX}{FallbackDefaultIcon}"));

                // 비동기로 실제 아이콘 로드 시작 (Fire-and-forget)
                _ = LoadFileIconAsync(imageControl, filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"파일 아이콘 로드 오류: {ex.Message}");
                imageControl.Source = new BitmapImage(new Uri($"{APP_RESOURCE_PREFIX}{FallbackDefaultIcon}"));
            }
        }

        /// <summary>
        /// 파일 아이콘 비동기 로드 (실제 파일 아이콘 추출)
        /// </summary>
        private async Task LoadFileIconAsync(Image imageControl, string filePath)
        {
            try
            {
                // 1. 캐시된 아이콘이 있는지 확인
                var cachedIconPath = await IconCache.GetIconPathAsync(filePath);
                if (cachedIconPath != null && File.Exists(cachedIconPath))
                {
                    UpdateIconSource(imageControl, filePath, cachedIconPath);
                    return;
                }

                // 2. 캐시에 없으면 실제 아이콘 추출 (48x48 크기)
                var extractedIconPath = await IconHelper.ExtractIconAndSaveAsync(filePath, AppPaths.IconsFolder, size: 48);
                if (extractedIconPath != null && File.Exists(extractedIconPath))
                {
                    UpdateIconSource(imageControl, filePath, extractedIconPath);
                    return;
                }

                // 3. 추출 실패 시 기본 아이콘 사용
                Debug.WriteLine($"아이콘 추출 실패, 기본 아이콘 사용: {filePath}");
                SetFallbackIcon(imageControl, filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"실제 아이콘 로드 실패 ({filePath}): {ex.Message}");
                SetFallbackIcon(imageControl, filePath);
            }
        }

        /// <summary>
        /// 기본 아이콘 설정 (아이콘 추출 실패 시)
        /// </summary>
        private void SetFallbackIcon(Image imageControl, string filePath)
        {
            if (DispatcherQueue != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (imageControl.Tag is string currentFilePath && currentFilePath == filePath)
                        {
                            imageControl.Source = new BitmapImage(new Uri($"{APP_RESOURCE_PREFIX}{FallbackDefaultIcon}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"기본 아이콘 설정 실패: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// UI 스레드에서 안전하게 아이콘 소스 업데이트
        /// </summary>
        private void UpdateIconSource(Image imageControl, string expectedFilePath, string iconPath)
        {
            // UI 스레드에서 안전하게 이미지 업데이트
            if (DispatcherQueue != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // 이미지 컨트롤의 태그가 여전히 같은 파일 경로인지 확인 (다른 파일로 변경되었으면 업데이트 중단)
                        if (imageControl.Tag is string currentFilePath && currentFilePath == expectedFilePath)
                        {
                            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                            {
                                imageControl.Source = new BitmapImage(new Uri(iconPath));
                            }
                            else
                            {
                                // 아이콘 파일이 없으면 기본 아이콘 사용
                                imageControl.Source = new BitmapImage(new Uri($"{APP_RESOURCE_PREFIX}{FallbackDefaultIcon}"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"아이콘 소스 업데이트 실패: {ex.Message}");
                        try
                        {
                            imageControl.Source = new BitmapImage(new Uri($"{APP_RESOURCE_PREFIX}{FallbackDefaultIcon}"));
                        }
                        catch { }
                    }
                });
            }
        }

        /// <summary>
        /// 파일 버튼 클릭 - 파일 실행 후 윈도우 닫기
        /// </summary>
        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string filePath)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"파일이 존재하지 않습니다: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"파일 실행 오류: {ex.Message}");
                }

                // 부모 윈도우와 함께 닫기 위해 이벤트 발생
                OnFileExecuted();
            }
        }

        /// <summary>
        /// 폴더 버튼 클릭 - 탐색기로 폴더 열기
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
                            FileName = "explorer.exe",
                            Arguments = folderPath,
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

                // 부모 윈도우와 함께 닫기 위해 이벤트 발생
                OnFileExecuted();
            }
        }

        /// <summary>
        /// 파일/폴더 실행 후 발생하는 이벤트
        /// </summary>
        public event EventHandler? FileExecuted;

        private void OnFileExecuted()
        {
            FileExecuted?.Invoke(this, EventArgs.Empty);
        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = HoverBackground;
            }
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = TransparentBackground;
            }
        }

        /// <summary>
        /// ContentsPanel 로드 완료 후 실제 콘텐츠 높이에 맞춰 윈도우 크기 재조정
        /// </summary>
        private void OnContentsPanelLoaded(object sender, RoutedEventArgs e)
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
            int fileCount = FileItemsControl.Items.Count;
            int folderCount = FolderItemsControl.Items.Count;
            int itemCount = fileCount + folderCount;

            int dynamicHeight;
            if (itemCount == 0)
            {
                dynamicHeight = EMPTY_STATE_HEIGHT;
            }
            else
            {
                int headerCount = 0;
                if (fileCount > 0) headerCount++;
                if (folderCount > 0) headerCount++;
                dynamicHeight = itemCount * ITEM_HEIGHT + headerCount * SECTION_HEADER_HEIGHT + TOP_HEADER_HEIGHT;
            }

            // DPI 스케일 계수 가져오기
            uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
            float scaleFactor = (float)dpi / 96.0f;

            // 물리적 픽셀로 변환 (올림으로 서브픽셀 손실 방지)
            int newWindowHeight = (int)Math.Ceiling(dynamicHeight * scaleFactor) + WINDOW_HEIGHT_PADDING;

            // 화면 최대 높이 제한 확인
            int screenHeight = (int)(Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea.Height);
            int maxAllowedHeight = screenHeight - SCREEN_BOTTOM_MARGIN;
            int absoluteMaxHeight = Math.Min(maxAllowedHeight, MAX_WINDOW_HEIGHT);

            newWindowHeight = Math.Max(newWindowHeight, MIN_WINDOW_HEIGHT);
            if (newWindowHeight > absoluteMaxHeight)
            {
                newWindowHeight = absoluteMaxHeight;
            }

            // 너비 계산 (DPI 적용)
            int newWindowWidth = (int)(WINDOW_WIDTH * scaleFactor) + WINDOW_WIDTH_PADDING;
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
        }

        /// <summary>
        /// 윈도우 크기 업데이트 - PopupWindow와 동일한 방식
        /// </summary>
        private void UpdateWindowSize(int fileCount, int folderCount)
        {
            // DPI 스케일 계수 가져오기 (PopupWindow와 동일)
            uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
            float scaleFactor = (float)dpi / 96.0f;

            int itemCount = fileCount + folderCount;
            int headerCount = 0;
            if (fileCount > 0) headerCount++;
            if (folderCount > 0) headerCount++;

            // 콘텐츠 크기 계산
            int dynamicWidth = WINDOW_WIDTH;
            int dynamicHeight;

            if (itemCount == 0)
            {
                dynamicHeight = EMPTY_STATE_HEIGHT;
            }
            else
            {
                // 항목당 약 36px (버튼 높이 + 마진) + 섹션 헤더 + 상단 헤더
                dynamicHeight = itemCount * ITEM_HEIGHT + headerCount * SECTION_HEADER_HEIGHT + TOP_HEADER_HEIGHT;
            }

            // DPI 스케일 적용 (PopupWindow와 동일)
            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);

            // 패딩 추가 (PopupWindow와 동일)
            int finalWidth = scaledWidth + WINDOW_WIDTH_PADDING;
            int finalHeight = scaledHeight + WINDOW_HEIGHT_PADDING;

            // 화면 최대 높이 제한
            int screenHeight = (int)(Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea.Height);
            int maxAllowedHeight = screenHeight - SCREEN_BOTTOM_MARGIN;
            if (finalHeight > maxAllowedHeight)
            {
                finalHeight = maxAllowedHeight;
            }

            // 최소/최대 크기 보장
            finalWidth = Math.Max(finalWidth, MIN_WINDOW_WIDTH);
            finalHeight = Math.Max(finalHeight, MIN_WINDOW_HEIGHT);
            finalHeight = Math.Min(finalHeight, MAX_WINDOW_HEIGHT);

            Debug.WriteLine($"FolderContentsPopupWindow.UpdateWindowSize: items={itemCount}, scaleFactor={scaleFactor}, finalWidth={finalWidth}, finalHeight={finalHeight}");

            // PopupWindow와 동일하게 음수 마진 적용 (윈도우 크롬 보정)
            MainGrid.Margin = new Thickness(WINDOW_CHROME_MARGIN_LEFT, WINDOW_CHROME_MARGIN_TOP, WINDOW_CHROME_MARGIN_RIGHT, WINDOW_CHROME_MARGIN_BOTTOM);

            // 윈도우 크기 저장 및 적용
            _currentWindowWidth = finalWidth;
            _currentWindowHeight = finalHeight;
            _windowHelper.SetSize(finalWidth, finalHeight);
        }

        /// <summary>
        /// 지정된 위치에 팝업 윈도우 표시
        /// </summary>
        /// <param name="x">X 좌표 (화면 기준)</param>
        /// <param name="y">Y 좌표 (화면 기준)</param>
        public void ShowAt(int x, int y)
        {
            try
            {
                UpdateMainGridBackground(_uiSettings);

                // 윈도우 위치 설정 및 최상위로 표시 (z-order)
                NativeMethods.SetWindowPos(
                    _hwnd,
                    NativeMethods.HWND_TOPMOST,
                    x, y,
                    0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"팝업 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 팝업 윈도우 숨기기
        /// </summary>
        public void HidePopup()
        {
            try
            {
                this.Hide();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"팝업 숨기기 오류: {ex.Message}");
            }
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

                // 버튼 이벤트 핸들러 해제
                UnregisterButtonEvents(FileItemsControl);
                UnregisterButtonEvents(FolderItemsControl);
            }

            _disposed = true;
        }

        /// <summary>
        /// ItemsControl 내 Button 이벤트 핸들러 일괄 해제
        /// </summary>
        private void UnregisterButtonEvents(ItemsControl itemsControl)
        {
            if (itemsControl?.Items == null) return;
            foreach (var item in itemsControl.Items)
            {
                if (item is Button button)
                {
                    button.Click -= FileButton_Click;
                    button.Click -= FolderButton_Click;
                    button.PointerEntered -= Button_PointerEntered;
                    button.PointerExited -= Button_PointerExited;
                }
            }
        }

        ~FolderContentsPopupWindow()
        {
            Dispose(false);
        }
    }
}
