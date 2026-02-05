using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private const int ICON_SIZE = 20;
        private const int ITEM_MARGIN = 2;
        private const int WINDOW_WIDTH = 250;
        private const int MAX_WINDOW_HEIGHT = 600;
        private const int MIN_WINDOW_HEIGHT = 100;

        private readonly WindowHelper _windowHelper;
        private IntPtr _hwnd;
        private bool _disposed = false;
        private UISettings _uiSettings;
        private string _currentFolderPath = string.Empty;

        /// <summary>
        /// 확장자별 아이콘 경로 매핑 (추후 확장자 추가 시 여기에 추가)
        /// </summary>
        private static readonly Dictionary<string, string> ExtensionIconMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // 이미지 파일
            { ".png", "Assets/icon/png_2.png" },
            { ".jpg", "Assets/icon/jpg_2.png" },
            { ".jpeg", "Assets/icon/jpg_2.png" },
            
            // 오디오 파일
            { ".mp3", "Assets/icon/mp3_2.png" },
            
            // 비디오 파일
            { ".mp4", "Assets/icon/mp4_2.png" },
            
            // 문서 파일
            { ".md", "Assets/icon/md.png" },
            { ".doc", "Assets/icon/doc_2.png" },
            { ".docx", "Assets/icon/doc_2.png" },
            { ".pdf", "Assets/icon/pdf_2.png" },
            { ".ppt", "Assets/icon/ppt_2.png" },
            { ".pptx", "Assets/icon/ppt_2.png" },
            { ".txt", "Assets/icon/txt_3.png" },
            { ".xls", "Assets/icon/xls_2.png" },
            { ".xlsx", "Assets/icon/xls_2.png" },
            { ".csv", "Assets/icon/csv.png" },
            
            // 코드/개발 파일
            { ".php", "Assets/icon/php.png" },
            { ".cs", "Assets/icon/cs.png" },
            { ".css", "Assets/icon/css.png" },
            { ".html", "Assets/icon/html.png" },
            { ".xaml", "Assets/icon/xaml.png" },
            { ".json", "Assets/icon/json.png" },
            { ".xml", "Assets/icon/xml.png" },
            
            // 시스템/실행 파일
            { ".dll", "Assets/icon/dll.png" },
            { ".exe", "Assets/icon/exe.png" },
            { ".inf", "Assets/icon/inf.png" },
            { ".ini", "Assets/icon/ini.png" },
            
            // 압축 파일
            { ".zip", "Assets/icon/zip_2.png" },
        };

        /// <summary>
        /// 카테고리별 확장자 목록 (폴백 아이콘용)
        /// </summary>
        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".cab", ".iso"
        };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".doc", ".docx", ".pdf", ".rtf", ".odt", ".md", ".log", ".ppt", ".pptx", ".xls", ".xlsx", ".csv"
        };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp", ".tiff", ".tif"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpeg", ".mpg"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".aiff"
        };

        // 폴백 아이콘 경로
        private const string FallbackArchiveIcon = "Assets/icon/zip.png";
        private const string FallbackDocumentIcon = "Assets/icon/txt.png";
        private const string FallbackImageIcon = "Assets/icon/png.png";
        private const string FallbackVideoIcon = "Assets/icon/mp4.png";
        private const string FallbackAudioIcon = "Assets/icon/mp3.png";
        private const string FallbackDefaultIcon = "Assets/icon/file_4.png";

        public FolderContentsPopupWindow()
        {
            InitializeComponent();

            _windowHelper = new WindowHelper(this);
            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = false;
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
                bool isDarkMode = foreground.R > 128;

                if (isDarkMode)
                {
                    MainGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 32, 32, 32));
                }
                else
                {
                    MainGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 243, 243, 243));
                }
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
            _currentFolderPath = folderPath;
            HeaderText.Text = folderName;

            FileItemsControl.Items.Clear();
            FolderItemsControl.Items.Clear();

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    ShowEmptyState("폴더가 존재하지 않습니다.");
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
                    ShowEmptyState("내용이 없습니다.");
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

                UpdateWindowSize(files.Count, folders.Count);
            }
            catch (UnauthorizedAccessException)
            {
                ShowEmptyState("액세스가 거부되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 내용 로드 오류: {ex.Message}");
                ShowEmptyState("오류가 발생했습니다.");
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
            LoadFileIcon(icon, file.FullName);

            var nameText = new TextBlock
            {
                Text = file.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
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
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
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
                Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"))
            };

            var nameText = new TextBlock
            {
                Text = folder.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
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
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0)
            };
            button.Click += FolderButton_Click;
            button.PointerEntered += Button_PointerEntered;
            button.PointerExited += Button_PointerExited;

            ToolTipService.SetToolTip(button, folder.FullName);

            return button;
        }

        /// <summary>
        /// 파일 아이콘 로드
        /// </summary>
        private void LoadFileIcon(Image imageControl, string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath);
                var iconPath = GetIconPathForExtension(extension);
                imageControl.Source = new BitmapImage(new Uri($"ms-appx:///{iconPath}"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"파일 아이콘 로드 오류: {ex.Message}");
                imageControl.Source = new BitmapImage(new Uri($"ms-appx:///{FallbackDefaultIcon}"));
            }
        }

        /// <summary>
        /// 확장자에 해당하는 아이콘 경로를 반환합니다.
        /// </summary>
        private static string GetIconPathForExtension(string extension)
        {
            // 1. 정확한 확장자 매핑이 있으면 사용
            if (ExtensionIconMap.TryGetValue(extension, out var iconPath))
            {
                return iconPath;
            }

            // 2. 카테고리별 폴백 아이콘 사용
            if (ArchiveExtensions.Contains(extension))
            {
                return FallbackArchiveIcon;
            }

            if (ImageExtensions.Contains(extension))
            {
                return FallbackImageIcon;
            }

            if (VideoExtensions.Contains(extension))
            {
                return FallbackVideoIcon;
            }

            if (AudioExtensions.Contains(extension))
            {
                return FallbackAudioIcon;
            }

            if (DocumentExtensions.Contains(extension))
            {
                return FallbackDocumentIcon;
            }

            // 3. 기본 파일 아이콘
            return FallbackDefaultIcon;
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
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 128, 128, 128));
            }
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }
        }

        private const int TOP_MARGIN = 100; // 상단에서 최소 100픽셀 떨어지도록

        /// <summary>
        /// 윈도우 크기 업데이트
        /// </summary>
        private void UpdateWindowSize(int fileCount, int folderCount)
        {
            int itemCount = fileCount + folderCount;
            int headerCount = 0;
            if (fileCount > 0) headerCount++;
            if (folderCount > 0) headerCount++;

            // 화면 작업 영역을 확인하여 최대 높이를 동적으로 계산
            int maxAvailableHeight = MAX_WINDOW_HEIGHT;
            try
            {
                NativeMethods.POINT cursorPos;
                if (NativeMethods.GetCursorPos(out cursorPos))
                {
                    IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
                    NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                    monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                    
                    if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        // 작업 영역 높이에서 상단 100픽셀 여백을 뺀 값이 최대 높이
                        int workAreaHeight = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
                        maxAvailableHeight = Math.Min(MAX_WINDOW_HEIGHT, workAreaHeight - TOP_MARGIN);
                    }
                }
            }
            catch
            {
                // 오류 발생 시 기본 MAX_WINDOW_HEIGHT 사용
            }

            int height;
            if (itemCount == 0)
            {
                height = MIN_WINDOW_HEIGHT;
            }
            else
            {
                // 항목당 약 36px (버튼 높이 + 마진) + 섹션 헤더 + 상단 헤더 + 여유 공간
                int contentHeight = itemCount * 36 + headerCount * 30 + 60;
                height = Math.Min(maxAvailableHeight, Math.Max(MIN_WINDOW_HEIGHT, contentHeight));
            }

            this.AppWindow.Resize(new SizeInt32(WINDOW_WIDTH, height));
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
            }

            _disposed = true;
        }

        ~FolderContentsPopupWindow()
        {
            Dispose(false);
        }
    }
}
