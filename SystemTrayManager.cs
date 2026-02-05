using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AppGroup {
/// <summary>
/// 시스템 트레이 아이콘을 관리하는 클래스입니다.
/// 네이티브 Win32 API를 사용하여 트레이 아이콘을 생성, 관리, 제거합니다.
/// </summary>
public class SystemTrayManager {
    private static IntPtr windowHandle;
    private static IntPtr hIcon;
    private static IntPtr hMenu;
    private static NativeMethods.WndProcDelegate wndProcDelegate;
    private static Action onShowCallback;
    private static Action onExitCallback;
    private static Func<System.Threading.Tasks.Task> onTrayClickCallback;
    private static bool isInitialized = false;
    private static bool isVisible = false;
    private static bool isCleanedUp = false;

    // TaskbarCreated 메시지 ID 저장을 위한 필드
    private static int WM_TASKBARCREATED;

    /// <summary>
    /// SystemTrayManager를 초기화합니다.
    /// </summary>
    /// <param name="showCallback">아이콘 더블 클릭 또는 '설정' 메뉴 선택 시 호출될 콜백</param>
    /// <param name="exitCallback">'종료' 메뉴 선택 시 호출될 콜백</param>
    /// <param name="trayClickCallback">트레이 아이콘 클릭 시 호출될 콜백 (시작 메뉴 팝업용)</param>
    public static void Initialize(Action showCallback, Action exitCallback, Func<System.Threading.Tasks.Task> trayClickCallback = null) {
        onShowCallback = showCallback;
        onExitCallback = exitCallback;
        onTrayClickCallback = trayClickCallback;
        isInitialized = true;

        // TaskbarCreated 메시지 등록 (탐색기 재시작 감지용)
        WM_TASKBARCREATED = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        // 설정에 따라 트레이 아이콘 표시 여부 결정
        _ = InitializeBasedOnSettingsAsync();
    }

    /// <summary>
    /// 설정 값을 비동기적으로 로드하고 트레이 아이콘 표시 여부를 초기화합니다.
    /// </summary>
    private static async System.Threading.Tasks.Task InitializeBasedOnSettingsAsync() {
        try {
            var settings = await SettingsHelper.LoadSettingsAsync();
            if (settings.ShowSystemTrayIcon) {
                ShowSystemTray();
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error loading settings for system tray: {ex.Message}");
            // 오류 발생 시 기본적으로 시스템 트레이 표시
            ShowSystemTray();
        }
    }

    /// <summary>
    /// 시스템 트레이 아이콘을 표시합니다.
    /// </summary>
    public static void ShowSystemTray() {
        if (!isInitialized) return;

        if (!isVisible) {
            InitializeSystemTray();
            isVisible = true;
        }
        }

        /// <summary>
        /// 시스템 트레이 아이콘을 숨깁니다.
        /// </summary>
        public static void HideSystemTray() {
            if (isVisible) {
                RemoveSystemTray();
                isVisible = false;
            }
        }

        /// <summary>
        /// 시스템 트레이 아이콘을 실제 제거합니다.
        /// </summary>
        private static void RemoveSystemTray() {
            if (windowHandle != IntPtr.Zero) {
                // 트레이 아이콘 제거
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1
                };
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref notifyIconData);
            }
        }

        /// <summary>
        /// 시스템 트레이 아이콘 및 관련 리소스를 초기화합니다.
        /// </summary>
        private static void InitializeSystemTray() {
            try {
                // 메시지 처리를 위한 숨겨진 윈도우 생성 (아직 생성되지 않은 경우)
                if (windowHandle == IntPtr.Zero) {
                    wndProcDelegate = new NativeMethods.WndProcDelegate(WndProc);

                    var wndClass = new NativeMethods.WNDCLASSEX {
                        cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                        style = 0,
                        lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                        cbClsExtra = 0,
                        cbWndExtra = 0,
                        hInstance = NativeMethods.GetModuleHandle(null),
                        hIcon = IntPtr.Zero,
                        hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512u), // IDC_ARROW
                        hbrBackground = IntPtr.Zero,
                        lpszMenuName = null,
                        lpszClassName = "WinUI3AppGroupTrayWndClass",
                        hIconSm = IntPtr.Zero
                    };

                    NativeMethods.RegisterClassEx(ref wndClass);

                    windowHandle = NativeMethods.CreateWindowEx(
                        0,
                        "WinUI3AppGroupTrayWndClass",
                        "WinUI3 AppGroup Tray Window",
                        0,
                        0, 0, 0, 0,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        NativeMethods.GetModuleHandle(null),
                        IntPtr.Zero);
                }

                CreateTrayIcon();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error initializing system tray: {ex.Message}");
            }
        }

        /// <summary>
        /// 실제 트레이 아이콘을 생성하고 등록합니다.
        /// </summary>
        private static void CreateTrayIcon() {
            try {
                // 사용자 지정 아이콘 로드 (아직 로드되지 않은 경우)
                if (hIcon == IntPtr.Zero) {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.ico");
                    if (File.Exists(iconPath)) {
                        hIcon = NativeMethods.LoadImage(IntPtr.Zero, iconPath, NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);
                        if (hIcon == IntPtr.Zero) {
                            // 로드 실패 시 시스템 아이콘 대체
                            hIcon = NativeMethods.LoadImage(IntPtr.Zero, "#32516", NativeMethods.IMAGE_ICON, 16, 16, 0);
                        }
                    }
                    else {
                        // 파일이 없으면 시스템 아이콘 대체
                        hIcon = NativeMethods.LoadImage(IntPtr.Zero, "#32516", NativeMethods.IMAGE_ICON, 16, 16, 0);
                        Debug.WriteLine($"Icon file not found at: {iconPath}, using system icon");
                    }
                }

                // 트레이 아이콘 생성
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1,
                    uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                    uCallbackMessage = NativeMethods.WM_TRAYICON,
                    hIcon = hIcon,
                    szTip = "App Group"
                };

                bool result = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref notifyIconData);
                if (!result) {
                    Debug.WriteLine("Failed to add system tray icon");
                }

                // 팝업 메뉴 생성 (아직 생성되지 않은 경우)
                if (hMenu == IntPtr.Zero) {
                    hMenu = NativeMethods.CreatePopupMenu();
                    NativeMethods.AppendMenu(hMenu, 0, (uint)NativeMethods.ID_SHOW, "설정");
                    NativeMethods.AppendMenu(hMenu, 0, (uint)NativeMethods.ID_EXIT, "종료");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error creating tray icon: {ex.Message}");
            }
        }

        /// <summary>
        /// 윈도우 메시지를 처리하는 프로시저입니다.
        /// </summary>
        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
            // TaskbarCreated 메시지 처리 - 탐색기가 재시작되었을 때 전송됨
            if (msg == WM_TASKBARCREATED) {
                Debug.WriteLine("TaskbarCreated message received - recreating tray icon");
                if (isVisible) {
                    // 탐색기가 재시작되었으므로 트레이 아이콘 다시 생성
                    CreateTrayIcon();
                }
                return IntPtr.Zero;
            }

            switch (msg) {
                case NativeMethods.WM_TRAYICON:
                    HandleTrayIconMessage(lParam);
                    break;

                case NativeMethods.WM_COMMAND:
                    int command = wParam.ToInt32() & 0xFFFF;
                    HandleMenuCommand(command);
                    break;

                case NativeMethods.WM_DESTROY:
                    Cleanup();
                    break;
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        // 더블클릭 감지를 위한 필드
        private static DateTime _lastClickTime = DateTime.MinValue;
        private static bool _isDoubleClick = false;
        private static Action? onHidePopupCallback;

        /// <summary>
        /// 팝업을 숨기기 위한 콜백을 설정합니다.
        /// </summary>
        /// <param name="hidePopupCallback">팝업을 숨길 때 호출될 콜백</param>
        public static void SetHidePopupCallback(Action hidePopupCallback) {
            onHidePopupCallback = hidePopupCallback;
        }

        /// <summary>
        /// 트레이 아이콘 이벤트를 처리합니다.
        /// </summary>
        private static void HandleTrayIconMessage(IntPtr lParam) {
            switch (lParam.ToInt32()) {
                case (int)NativeMethods.WM_LBUTTONUP:
                    // 더블클릭의 두 번째 클릭인 경우 무시
                    if (_isDoubleClick) {
                        _isDoubleClick = false;
                        return;
                    }
                    
                    // 클릭 시 시작 메뉴 팝업 또는 메인 창 표시
                    if (onTrayClickCallback != null) {
                        _ = onTrayClickCallback.Invoke();
                    }
                    else {
                        onShowCallback?.Invoke();
                    }
                    break;

                case (int)NativeMethods.WM_LBUTTONDBLCLK:
                    // 더블클릭 플래그 설정 (다음 WM_LBUTTONUP 무시용)
                    _isDoubleClick = true;
                    
                    // 팝업이 열려있으면 숨기기
                    onHidePopupCallback?.Invoke();
                    
                    // 더블 클릭 시 메인 창 표시
                    onShowCallback?.Invoke();
                    break;

                case (int)NativeMethods.WM_RBUTTONUP:
                    // 우클릭 시 컨텍스트 메뉴 표시
                    ShowContextMenu();
                    break;
            }
        }

        /// <summary>
        /// 메뉴 명령을 처리합니다.
        /// </summary>
        private static void HandleMenuCommand(int command) {
            switch (command) {
                case NativeMethods.ID_SHOW:
                    onShowCallback?.Invoke();
                    break;

                case NativeMethods.ID_EXIT:
                    onExitCallback?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// 컨텍스트 메뉴를 표시합니다.
        /// </summary>
        private static void ShowContextMenu() {
            if (hMenu != IntPtr.Zero) {
                // 커서 위치 가져오기
                NativeMethods.POINT pt;
                NativeMethods.GetCursorPos(out pt);

                // 중요 수정: 포그라운드 윈도우 설정 및 더미 메시지 전송
                // 이렇게 해야 메뉴 밖을 클릭했을 때 메뉴가 제대로 닫힘
                NativeMethods.SetForegroundWindow(windowHandle);

                // 컨텍스트 메뉴 표시
                uint result = NativeMethods.TrackPopupMenu(
                    hMenu,
                    NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                    pt.X, pt.Y,
                    0,
                    windowHandle,
                    IntPtr.Zero);

                // 중요 수정: 메뉴가 제대로 닫히도록 더미 메시지 전송
                NativeMethods.PostMessage(windowHandle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

                // 메뉴 선택 처리
                if (result != 0) {
                    HandleMenuCommand((int)result);
                }
            }
        }

        /// <summary>
        /// 트레이 아이콘의 툴팁을 업데이트합니다.
        /// </summary>
        public static void UpdateTooltip(string tooltip) {
            if (windowHandle != IntPtr.Zero && isVisible) {
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1,
                    uFlags = NativeMethods.NIF_TIP,
                    szTip = tooltip
                };

                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref notifyIconData);
            }
        }

        /// <summary>
        /// 리소스를 정리하고 파괴합니다.
        /// </summary>
        public static void Cleanup() {
            if (isCleanedUp) return;
            isCleanedUp = true;

            try {
                if (windowHandle != IntPtr.Zero) {
                    // 트레이 아이콘 제거
                    var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                        cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                        hWnd = windowHandle,
                        uID = 1
                    };
                    NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref notifyIconData);

                    // 리소스 정리
                    if (hIcon != IntPtr.Zero) {
                        NativeMethods.DestroyIcon(hIcon);
                        hIcon = IntPtr.Zero;
                    }

                    if (hMenu != IntPtr.Zero) {
                        NativeMethods.DestroyMenu(hMenu);
                        hMenu = IntPtr.Zero;
                    }

                    if (windowHandle != IntPtr.Zero) {
                        NativeMethods.DestroyWindow(windowHandle);
                        windowHandle = IntPtr.Zero;
                    }

                    isVisible = false;
                }

                // GC가 구독자를 제거하지 못하는 것을 방지하기 위해 콜백 초기화
                onShowCallback = null;
                onExitCallback = null;
                isInitialized = false;

                // 참고: wndProcDelegate 참조는 프로세스 종료 시까지 유지합니다.
                // 윈도우 메시지 루프가 활성 상태일 때 대리자가 GC 수집되는 것을 방지하기 위함
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error during SystemTrayManager cleanup: {ex.Message}");
            }
        }
    }
}
