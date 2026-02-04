using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
        private const int ICON_SIZE = 32;
        private const int ICON_SIZE_GRID = 48;
        private const int ITEM_MARGIN = 4;
        private const int WINDOW_PADDING = 10;

        private readonly WindowHelper _windowHelper;
        private IntPtr _hwnd;
        private int _columnCount = 1;
        private bool _disposed = false;
        private UISettings _uiSettings;

        public StartMenuPopupWindow()
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

            // 테마 변경 감지
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

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

        private async void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                await LoadFoldersAsync();
            }
            else
            {
                // 포커스를 잃으면 윈도우 숨기기
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
                // 설정에서 열 개수 로드
                var settings = await SettingsHelper.LoadSettingsAsync();
                _columnCount = Math.Max(1, Math.Min(5, settings.FolderColumnCount));

                // 폴더 목록 로드
                var folders = await JsonConfigHelper.LoadStartMenuFoldersAsync();

                // UI 업데이트
                DispatcherQueue.TryEnqueue(() =>
                {
                    BuildFolderUI(folders);
                    UpdateWindowSize(folders.Count);
                    PositionWindowAboveTaskbar();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 로드 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 폴더 UI를 구성합니다.
        /// </summary>
        private void BuildFolderUI(List<StartMenuItem> folders)
        {
            FolderItemsControl.Items.Clear();

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
                FolderItemsControl.Items.Add(emptyText);
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
                FolderItemsControl.Items.Add(button);
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
                    HorizontalAlignment = HorizontalAlignment.Center
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

                FolderItemsControl.Items.Add(rowPanel);
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
                MaxWidth = 150
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
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
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
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
                MaxLines = 2,
                MaxWidth = 70,
                FontSize = 12
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(icon);
            content.Children.Add(nameText);

            var button = new Button
            {
                Content = content,
                Tag = folder.FolderPath,
                Width = 90,
                Height = 90,
                Margin = new Thickness(ITEM_MARGIN),
                Padding = new Thickness(5),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
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
                    imageControl.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
                }
                else if (File.Exists(iconPath))
                {
                    imageControl.Source = new BitmapImage(new Uri(iconPath));
                }
                else
                {
                    imageControl.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"아이콘 로드 오류: {ex.Message}");
                imageControl.Source = new BitmapImage(new Uri("ms-appx:///Assets/icon/folder_3.png"));
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

                this.Hide();
            }
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

        /// <summary>
        /// 윈도우 크기 업데이트
        /// </summary>
        private void UpdateWindowSize(int folderCount)
        {
            int width, height;

            if (_columnCount == 1)
            {
                // 1열: 가로 레이아웃
                width = 220;
                height = Math.Min(400, Math.Max(100, folderCount * 50 + 60));
            }
            else
            {
                // 그리드 레이아웃
                int rowCount = (int)Math.Ceiling((double)folderCount / _columnCount);
                width = _columnCount * 98 + WINDOW_PADDING * 2;
                height = Math.Min(500, Math.Max(150, rowCount * 98 + 60));
            }

            this.AppWindow.Resize(new SizeInt32(width, height));
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
        public void ShowPopup()
        {
            try
            {
                // 테마에 맞는 배경색 설정
                UpdateMainGridBackground(_uiSettings);

                // 윈도우 표시
                NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
                NativeMethods.SetForegroundWindow(_hwnd);
                this.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"팝업 표시 오류: {ex.Message}");
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

        ~StartMenuPopupWindow()
        {
            Dispose(false);
        }
    }
}
