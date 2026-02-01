using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AppGroup {
    public static partial class NativeMethods {
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SPI_GETWORKAREA = 0x0030;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_HIDE = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_ASYNCWINDOWPOS = 0x4000;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        public const int WM_USER = 0x0400;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;
        public const int SW_NORMAL = 1;
        public const int SWP_HIDEWINDOW = 0x0080;
        public const int SHCNE_RENAMEITEM = 0x00000001;
        public const int SHCNE_CREATE = 0x00000002;
        public const int SHCNE_DELETE = 0x00000004;
        public const int SHCNE_UPDATEITEM = 0x00002000;
        public const int SHCNE_UPDATEIMAGE = 0x00008000;
        public const int SHCNE_UPDATEDIR = 0x00001000;
        public const int SHCNE_RENAMEFOLDER = 0x00020000;
        public const uint SHCNF_PATH = 0x0005;
        public const uint SHCNF_IDLIST = 0x0000;
        public const uint RDW_ERASE = 0x0004;
        public const uint RDW_FRAME = 0x0400;
        public const uint RDW_INVALIDATE = 0x0001;
        public const uint WM_SHOWWINDOW = 0x0018;
        public const uint RDW_ALLCHILDREN = 0x0080;
        public const uint WM_TRAYICON = 0x8000;
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_LBUTTONDBLCLK = 0x0203;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_NULL = 0x0000;
        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;
        public const uint NIM_ADD = 0x00000000;
        public const uint NIM_MODIFY = 0x00000001;
        public const uint NIM_DELETE = 0x00000002;
        public const uint IMAGE_ICON = 1;
        public const uint LR_LOADFROMFILE = 0x00000010;
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_RIGHTBUTTON = 0x0002;
        public const int ID_SHOW = 1001;
        public const int ID_EXIT = 1002;
        public const uint MIN_ALL = 419;
        public const uint RESTORE_ALL = 416;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_FLUSH = 0x1000;
        public const int WM_COPYDATA = 0x004A;
        // SHAppBarMessage 상의 상수
        public const uint ABM_GETSTATE = 0x4;
        public const uint ABM_GETTASKBARPOS = 0x5;

        // 작업 표시줄 위치 상수
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        public const uint GW_OWNER = 4;
        // 발신자 코드
        public static void SendString(IntPtr targetWindow, string message) {
            COPYDATASTRUCT cds = new COPYDATASTRUCT();
            cds.dwData = (IntPtr)100; // 사용자 정의 식별자
            cds.cbData = (message.Length + 1) * 2; // 바이트 단위 유니코드 문자열 길이
            cds.lpData = Marshal.StringToHGlobalUni(message);

            try {
                IntPtr cdsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
                Marshal.StructureToPtr(cds, cdsPtr, false);

                NativeMethods.SendMessage(targetWindow, NativeMethods.WM_COPYDATA,
                    IntPtr.Zero, cdsPtr);

                Marshal.FreeHGlobal(cdsPtr);
            }
            finally {
                Marshal.FreeHGlobal(cds.lpData);
            }
        }
        public delegate IntPtr SubclassProc(
    IntPtr hWnd,
    uint uMsg,
    IntPtr wParam,
    IntPtr lParam,
    IntPtr uIdSubclass,
    IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass(
         IntPtr hWnd,
         SubclassProc pfnSubclass,
         int uIdSubclass,
         IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool RemoveWindowSubclass(
            IntPtr hWnd,
            SubclassProc pfnSubclass,
            int uIdSubclass);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern IntPtr DefSubclassProc(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam);


        public static readonly int WM_UPDATE_GROUP = RegisterWindowMessage("AppGroup.WM_UPDATE_GROUP");
        // 고유 메시지 ID 등록 (시작 시 한 번 호출)
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string lpString);


        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const int GWL_WNDPROC = -4;


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out RECT pvParam, uint fWinIni);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }


        public static void ForceForegroundWindow(IntPtr hWnd) {


            if (GetForegroundWindow() == hWnd)
                return;


            // 포그라운드 창의 스레드 ID 가져오기
            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            uint currentThreadId = GetCurrentThreadId();

            // 포그라운드 스레드의 입력 큐에 연결
            if (currentThreadId != foregroundThreadId) {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            // 창을 맨 위로 가져오고 포그라운드 창으로 설정
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);

            // 포그라운드 스레드의 입력 큐에서 연결 해제
            if (currentThreadId != foregroundThreadId) {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }



        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);



        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }




        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);


        // 상수

        // 델리게이트
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // 구조체
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        // NOTIFYICONDATA 구조체를 이 수정된 버전으로 교체:
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }


        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);


        //[DllImport("user32.dll", CharSet = CharSet.Auto)]
        //public static extern uint RegisterWindowMessage(string lpString);



        // 유니코드를 명시적으로 사용하도록 Shell_NotifyIcon 선언도 업데이트:
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);


        // P/Invoke 선언
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
            IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, uint lpCursorName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType,
            int cxDesired, int cyDesired, uint fuLoad);



        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hWnd, IntPtr prcRect);


        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);



        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);



        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        public const int WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr LoadIcon(string iconPath) {
            return LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_LOADFROMFILE);
        }

        public const uint LR_DEFAULTSIZE = 0x00000040;

        public const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        public const int SW_SHOWNORMAL = 1;

        //[DllImport("psapi.dll")]
        //public static extern int EmptyWorkingSet(IntPtr hwProc);
        //public const int SW_SHOWNOACTIVATE = 4;  // 활성화/포커스 없이 창 표시

        public static void PositionWindowAboveTaskbar(IntPtr hWnd) {
            try {



                // 창 크기 가져오기
                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(hWnd, out windowRect)) {
                    return;
                }
                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // 현재 커서 위치 가져오기
                NativeMethods.POINT cursorPos;
                if (!NativeMethods.GetCursorPos(out cursorPos)) {
                    return;
                }

                // 모니터 정보 가져오기
                IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo)) {
                    return;
                }


                // 작업 표시줄 위치를 기반으로 위치 계산
                float dpiScale = GetDpiScaleForMonitor(monitor);
                int baseTaskbarHeight = 52;
                int taskbarHeight = (int)(baseTaskbarHeight * dpiScale);

                // 모든 면에 대해 일관된 간격 값 정의
                int spacing = 6; // 창과 작업 표시줄 사이의 픽셀 간격

                // 작업 표시줄 자동 숨김 여부 확인 및 필요 시 간격 조정
                bool isTaskbarAutoHide = IsTaskbarAutoHide();
                Debug.WriteLine($"Taskbar Auto-Hide: {isTaskbarAutoHide}");

                // 작업 영역과 모니터 영역을 비교하여 작업 표시줄 위치 결정
                TaskbarPosition taskbarPosition = GetTaskbarPosition(monitorInfo);
                Debug.WriteLine($"Taskbar Position: {taskbarPosition}");

                if (isTaskbarAutoHide) {
                    if (IsCursorOnTaskbar(cursorPos, monitorInfo, taskbarPosition)) {
                        int autoHideSpacing = (int)((baseTaskbarHeight) * dpiScale);
                        spacing = autoHideSpacing;
                    }
                    else {
                        spacing += (int)(5 * dpiScale);
                    }
                }
                else {
                    // 일반 (표시된) 작업 표시줄의 경우 더 큰 간격 사용
                    if (taskbarPosition == TaskbarPosition.Top) {
                        spacing = (int)(10 * dpiScale); // 상단은 더 작은 간격
                    }
                    else {
                        spacing = (int)(6 * dpiScale); // 하단은 더 큰 간격
                    }
                }


                // 초기 위치 (커서 기준 수평 중앙 정렬)
                int x = cursorPos.X - (windowWidth / 2);
                int y;

                // 작업 표시줄 위치에 따라 위치 설정
                switch (taskbarPosition) {
                     case TaskbarPosition.Top:
                    case TaskbarPosition.Bottom:
                        // 커서가 작업 표시줄 위에 있는지 확인
                        if (IsCursorOnTaskbar(cursorPos, monitorInfo, taskbarPosition)) {
                            // 작업 표시줄 위/아래에 배치
                            if (taskbarPosition == TaskbarPosition.Top)
                                y = monitorInfo.rcWork.top  + spacing;
                            else
                                y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        }
                        else {
                            // 커서가 작업 표시줄 위에 있지 않음 (바탕 화면/탐색기) - 커서 근처에 배치
                            y = cursorPos.Y - windowHeight - spacing;
                            // 작업 영역으로 제한
                            if (y < monitorInfo.rcWork.top + spacing)
                                y = monitorInfo.rcWork.top + spacing;
                            if (y + windowHeight > monitorInfo.rcWork.bottom - spacing)
                                y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        }
                        break;
                    case TaskbarPosition.Left:
                        // 자동 숨김의 경우 작업 영역이 전체 화면일 수 있으므로 간격을 두고 모니터 왼쪽 사용
                        if (isTaskbarAutoHide)
                            x = monitorInfo.rcMonitor.left + spacing;
                        else
                            x = monitorInfo.rcWork.left + spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;
                    case TaskbarPosition.Right:
                        // 자동 숨김의 경우 작업 영역이 전체 화면일 수 있으므로 간격을 두고 모니터 오른쪽 사용
                        if (isTaskbarAutoHide)
                            x = monitorInfo.rcMonitor.right - windowWidth - spacing;
                        else
                            x = monitorInfo.rcWork.right - windowWidth - spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;
                    default:
                        // 기본값은 하단 배치
                        if (isTaskbarAutoHide)
                            y = monitorInfo.rcMonitor.bottom - windowHeight - spacing;
                        else
                            y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        break;
                }

                Debug.WriteLine($"Calculated Position (before bounds check): X={x}, Y={y}");

                // 창이 수평 모니터 경계 내에 머무르도록 보장
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;

                Debug.WriteLine($"Final Position (after bounds check): X={x}, Y={y}");
                Debug.WriteLine($"================================");

                // 창 이동 (크기 유지, 위치만 변경)
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW  );
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }

        public static void PositionWindowBelowTaskbar(IntPtr hWnd) {
            try {
                // Get window dimensions
                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(hWnd, out windowRect))
                    return;

                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // Get cursor position
                NativeMethods.POINT cursorPos;
                if (!NativeMethods.GetCursorPos(out cursorPos))
                    return;

                // Get monitor info
                IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                    return;

                // DPI scaling
                float dpiScale = GetDpiScaleForMonitor(monitor);
                int baseTaskbarHeight = 52;
                int taskbarHeight = (int)(baseTaskbarHeight * dpiScale);

                // Spacing
                int spacing = 99999;
                if (IsTaskbarAutoHide()) {
                    int autoHideSpacing = (int)(baseTaskbarHeight * dpiScale);
                    spacing += autoHideSpacing;
                }

                // Taskbar position
                TaskbarPosition taskbarPosition = GetTaskbarPosition(monitorInfo);

                // Initial x = center horizontally on cursor
                int x = cursorPos.X - (windowWidth / 2);
                int y;

                // 작업 표시줄 아래에 배치 (하단인 경우 화면 밖)
                switch (taskbarPosition) {
                    case TaskbarPosition.Top:
                        y = monitorInfo.rcMonitor.top + taskbarHeight + spacing;
                        break;

                    case TaskbarPosition.Bottom:
                        y = monitorInfo.rcMonitor.bottom + spacing; // 👈 화면 아래로
                        break;

                    case TaskbarPosition.Left:
                        x = monitorInfo.rcMonitor.left + taskbarHeight + spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;

                    case TaskbarPosition.Right:
                        x = monitorInfo.rcMonitor.right - windowWidth - taskbarHeight - spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;

                    default:
                        y = monitorInfo.rcMonitor.bottom + spacing; // 👈 화면 아래로
                        break;
                }

                // 창을 화면 수평 내부에 유지
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;

                // 창 이동 (화면 밖으로 나갈 수 있도록 수직 제한 없음)
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }

        public static void PositionWindowOffScreen(IntPtr hWnd) {
            int screenHeight = (int)DisplayArea.Primary.WorkArea.Height;
            int screenWidth = (int)DisplayArea.Primary.WorkArea.Width;

            // 깜박임을 방지하기 위해 먼저 숨김
            NativeMethods.SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_HIDEWINDOW
            );

            // screenHeight를 사용하여 창을 화면 아래로 이동
            NativeMethods.SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0, screenHeight + 100, // 하단 가장자리 아래로 밀기
                0, 0,
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE
            );
        }


        public static void PositionWindowOffScreenBelow(IntPtr hWnd) {
            try {
                // Get current cursor position
                NativeMethods.POINT cursorPos;
                if (!NativeMethods.GetCursorPos(out cursorPos)) {
                    // 커서 위치 확인 실패 시 화면 중앙으로 폴백
                    cursorPos.X = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) / 2;
                    cursorPos.Y = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) / 2;
                }

                // Get window dimensions
                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(hWnd, out windowRect)) {
                    return;
                }
                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // 기본 모니터 정보 가져오기 (화면 밖 배치에 가장 신뢰할 수 있음)
                IntPtr primaryMonitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = 0, Y = 0 },
                    NativeMethods.MONITOR_DEFAULTTOPRIMARY);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

                if (!NativeMethods.GetMonitorInfo(primaryMonitor, ref monitorInfo)) {
                    // 모니터 정보 실패 시 시스템 메트릭으로 폴백
                    int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);

                    // 커서 Y 위치에 맞춰 창을 화면 오른쪽 밖으로 배치
                    int x = screenWidth + 100; // 화면 오른쪽으로 100픽셀
                    int y = cursorPos.Y; // 커서 Y 위치에 정렬

                    NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
                    return;
                }

                // 커서 Y에 맞춰 기본 모니터 오른쪽의 화면 밖 위치 계산
                int offScreenX = monitorInfo.rcMonitor.right + 100; // 모니터 오른쪽으로 100픽셀
                int offScreenY = cursorPos.Y; // 커서 Y 위치 사용

                // 수평으로 완전히 화면 밖으로 나가도록 추가 안전 여백
                int safetyMargin = Math.Max(windowWidth, 200);
                offScreenX += safetyMargin;

                // 창을 화면 밖 위치로 이동
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, offScreenX, offScreenY, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);

                Debug.WriteLine($"Window positioned off-screen at: ({offScreenX}, {offScreenY}), cursor at: ({cursorPos.X}, {cursorPos.Y})");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window off-screen: {ex.Message}");
            }
        }


        private static bool IsCursorOnTaskbar(POINT cursorPos, MONITORINFO monitorInfo, TaskbarPosition taskbarPosition) {
            float dpiScale = GetDpiScaleForMonitor(MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST));
            int taskbarThickness = (int)(52 * dpiScale); // 크기 조정된 기본 작업 표시줄 높이/너비

            switch (taskbarPosition) {
                case TaskbarPosition.Top:
                    return cursorPos.Y >= monitorInfo.rcMonitor.top &&
                           cursorPos.Y <= monitorInfo.rcWork.top + taskbarThickness;
                case TaskbarPosition.Bottom:
                    return cursorPos.Y >= monitorInfo.rcWork.bottom - taskbarThickness &&
                           cursorPos.Y <= monitorInfo.rcMonitor.bottom;
                case TaskbarPosition.Left:
                    return cursorPos.X >= monitorInfo.rcMonitor.left &&
                           cursorPos.X <= monitorInfo.rcWork.left + taskbarThickness;
                case TaskbarPosition.Right:
                    return cursorPos.X >= monitorInfo.rcWork.right - taskbarThickness &&
                           cursorPos.X <= monitorInfo.rcMonitor.right;
                default:
                    return false;
            }
        }
        private static TaskbarPosition GetTaskbarPosition(NativeMethods.MONITORINFO monitorInfo) {
            // 작업 영역이 모니터 영역과 같은 경우 (자동 숨김 작업 표시줄에서 발생 가능), 
            // 다른 수단을 통한 작업 표시줄 위치 감지로 폴백
            if (monitorInfo.rcWork.top == monitorInfo.rcMonitor.top &&
                monitorInfo.rcWork.bottom == monitorInfo.rcMonitor.bottom &&
                monitorInfo.rcWork.left == monitorInfo.rcMonitor.left &&
                monitorInfo.rcWork.right == monitorInfo.rcMonitor.right) {
                // 자동 숨김 작업 표시줄의 경우 AppBar 정보를 사용하여 위치 확인 시도
                return GetTaskbarPositionFromAppBarInfo();
            }

            // 작업 영역과 화면 영역을 비교하여 작업 표시줄 위치 결정
            if (monitorInfo.rcWork.top > monitorInfo.rcMonitor.top)
                return TaskbarPosition.Top;
            else if (monitorInfo.rcWork.bottom < monitorInfo.rcMonitor.bottom)
                return TaskbarPosition.Bottom;
            else if (monitorInfo.rcWork.left > monitorInfo.rcMonitor.left)
                return TaskbarPosition.Left;
            else if (monitorInfo.rcWork.right < monitorInfo.rcMonitor.right)
                return TaskbarPosition.Right;
            else
                return TaskbarPosition.Bottom; // Default
        }

        private enum TaskbarPosition {
            Top,
            Bottom,
            Left,
            Right
        }
        public static bool IsTaskbarAutoHide() {
            NativeMethods.APPBARDATA appBarData = new NativeMethods.APPBARDATA();
            appBarData.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA));

            // 작업 표시줄 상태 가져오기
            IntPtr result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref appBarData);

            // 자동 숨김 비트 설정 확인 (ABS_AUTOHIDE = 0x01)
            return ((uint)result & 0x01) != 0;
        }


        /// <summary>
        /// AppBar 정보를 사용하여 작업 표시줄 위치 가져오기 (자동 숨김 작업 표시줄에서 작동)
        /// </summary>
        private static TaskbarPosition GetTaskbarPositionFromAppBarInfo() {
            NativeMethods.APPBARDATA appBarData = new NativeMethods.APPBARDATA();
            appBarData.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA));

            // 작업 표시줄 위치 데이터 가져오기
            IntPtr result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref appBarData);
            if (result != IntPtr.Zero) {
                // uEdge 필드는 작업 표시줄이 도킹된 가장자리를 포함
                switch (appBarData.uEdge) {
                    case NativeMethods.ABE_TOP: return TaskbarPosition.Top;
                    case NativeMethods.ABE_BOTTOM: return TaskbarPosition.Bottom;
                    case NativeMethods.ABE_LEFT: return TaskbarPosition.Left;
                    case NativeMethods.ABE_RIGHT: return TaskbarPosition.Right;
                }
            }

            // 확인할 수 없는 경우 기본값 하단
            return TaskbarPosition.Bottom;
        }


        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);


        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }
        public static float GetDpiScaleForMonitor(IntPtr hMonitor) {
            try {
                if (Environment.OSVersion.Version.Major > 6 ||
                    (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 3)) {
                    uint dpiX, dpiY;
                    // 모니터 DPI를 가져오기 위한 시도
                    if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0) {
                        return dpiX / 96.0f;
                    }
                }
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) {
                    return g.DpiX / 96.0f;
                }
            }
            catch {
                return 1.0f;
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_SYSICONINDEX = 0x000004000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
       IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);


        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);



        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);


        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);


        [DllImport("Shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // shell:AppsFolder 항목에서 아이콘을 추출하기 위한 Shell API
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        // IShellItemImageFactory를 직접 요청하는 오버로드
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        public interface IShellItem {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        public enum SIGDN : uint {
            NORMALDISPLAY = 0x00000000,
            PARENTRELATIVEPARSING = 0x80018001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000,
            PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            PARENTRELATIVE = 0x80080001
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItemImageFactory {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) {
                this.cx = cx;
                this.cy = cy;
            }
        }

        [Flags]
        public enum SIIGBF : uint {
            SIIGBF_RESIZETOFIT = 0x00000000,
            SIIGBF_BIGGERSIZEOK = 0x00000001,
            SIIGBF_MEMORYONLY = 0x00000002,
            SIIGBF_ICONONLY = 0x00000004,
            SIIGBF_THUMBNAILONLY = 0x00000008,
            SIIGBF_INCACHEONLY = 0x00000010,
            SIIGBF_CROPTOSQUARE = 0x00000020,
            SIIGBF_WIDETHUMBNAILS = 0x00000040,
            SIIGBF_ICONBACKGROUND = 0x00000080,
            SIIGBF_SCALEUP = 0x00000100
        }

        public static readonly Guid IShellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        public static readonly Guid IShellItemImageFactoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("gdi32.dll")]
        public static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);
        
        [DllImport("gdi32.dll")]
        public static extern int GetBitmapBits(IntPtr hbmp, int cbBuffer, [Out] byte[] lpvBits);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        // DIB Section 구조체 (top-down/bottom-up 비트맵 방향 확인용)
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER {
            public uint biSize;
            public int biWidth;
            public int biHeight;  // 양수: bottom-up, 음수: top-down
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DIBSECTION {
            public BITMAP dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfields0;
            public uint dsBitfields1;
            public uint dsBitfields2;
            public IntPtr dshSection;
            public uint dsOffset;
        }

        [DllImport("gdi32.dll")]
        public static extern int GetObject(IntPtr hObject, int nCount, ref DIBSECTION lpObject);

        /// <summary>
        /// 지정된 핸들의 윈도우를 안전하게 최상위로 가져옵니다.
        /// App.xaml.cs, MainWindow.xaml.cs의 중복 코드 통합
        /// </summary>
        /// <param name="hWnd">윈도우 핸들</param>
        public static void BringWindowToFrontSafe(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) return;
            try {
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, SW_RESTORE);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
            }
        }

        // AppsFolder 알려진 폴더 ID
        public static readonly Guid FOLDERID_AppsFolder = new Guid("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
        
        [DllImport("shell32.dll")]
        public static extern int SHGetKnownFolderIDList(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppidl);
        
        [DllImport("shell32.dll")]
        public static extern int SHBindToObject(
            IntPtr pShellFolder,
            IntPtr pidl,
            IntPtr pbc,
            ref Guid riid,
            out object ppv);
        
        [DllImport("shell32.dll")]
        public static extern int SHCreateItemFromIDList(
            IntPtr pidl,
            ref Guid riid,
            out IShellItemImageFactory ppv);
        
        [DllImport("shell32.dll")]
        public static extern IntPtr ILCombine(IntPtr pidl1, IntPtr pidl2);
        
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214E6-0000-0000-C000-000000000046")]
        public interface IShellFolder {
            [PreserveSig]
            int ParseDisplayName(
                IntPtr hwnd,
                IntPtr pbc,
                [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
                out uint pchEaten,
                out IntPtr ppidl,
                ref uint pdwAttributes);
            
            [PreserveSig]
            int EnumObjects(
                IntPtr hwnd,
                uint grfFlags,
                out IntPtr ppenumIDList);
            
            [PreserveSig]
            int BindToObject(
                IntPtr pidl,
                IntPtr pbc,
                ref Guid riid,
                out object ppv);
            
            [PreserveSig]
            int BindToStorage(
                IntPtr pidl,
                IntPtr pbc,
                ref Guid riid,
                out object ppv);
            
            [PreserveSig]
            int CompareIDs(
                IntPtr lParam,
                IntPtr pidl1,
                IntPtr pidl2);
            
            [PreserveSig]
            int CreateViewObject(
                IntPtr hwndOwner,
                ref Guid riid,
                out object ppv);
            
            [PreserveSig]
            int GetAttributesOf(
                uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                ref uint rgfInOut);
            
            [PreserveSig]
            int GetUIObjectOf(
                IntPtr hwndOwner,
                uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                ref Guid riid,
                IntPtr rgfReserved,
                out object ppv);
            
            [PreserveSig]
            int GetDisplayNameOf(
                IntPtr pidl,
                uint uFlags,
                out IntPtr pName);
            
            [PreserveSig]
            int SetNameOf(
                IntPtr hwnd,
                IntPtr pidl,
                [MarshalAs(UnmanagedType.LPWStr)] string pszName,
                uint uFlags,
                out IntPtr ppidlOut);
        }
        
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214FA-0000-0000-C000-000000000046")]
        public interface IExtractIconW {
            int GetIconLocation(
                uint uFlags,
                [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder szIconFile,
                int cchMax,
                out int piIndex,
                out uint pwFlags);

            int Extract(
                [MarshalAs(UnmanagedType.LPWStr)] string pszFile,
                uint nIconIndex,
                out IntPtr phiconLarge,
                out IntPtr phiconSmall,
                uint nIconSize);
        }

        // SHIL 상수 (시스템 이미지 리스트 크기)
        public const int SHIL_LARGE = 0;      // 32x32
        public const int SHIL_SMALL = 1;      // 16x16
        public const int SHIL_EXTRALARGE = 2; // 48x48
        public const int SHIL_SYSSMALL = 3;   // 시스템 작은 아이콘
        public const int SHIL_JUMBO = 4;      // 256x256

        // IImageList COM 인터페이스 GUID
        public static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        // IImageList COM 인터페이스 (시스템 이미지 리스트에서 아이콘 추출)
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        public interface IImageList {
            [PreserveSig]
            int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);

            [PreserveSig]
            int ReplaceIcon(int i, IntPtr hicon, out int pi);

            [PreserveSig]
            int SetOverlayImage(int iImage, int iOverlay);

            [PreserveSig]
            int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

            [PreserveSig]
            int AddMasked(IntPtr hbmImage, int crMask, out int pi);

            [PreserveSig]
            int Draw(ref IMAGELISTDRAWPARAMS pimldp);

            [PreserveSig]
            int Remove(int i);

            [PreserveSig]
            int GetIcon(int i, uint flags, out IntPtr picon);

            [PreserveSig]
            int GetImageInfo(int i, out IMAGEINFO pImageInfo);

            [PreserveSig]
            int Copy(int iDst, IImageList punkSrc, int iSrc, uint uFlags);

            [PreserveSig]
            int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int Clone(ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetImageRect(int i, out RECT prc);

            [PreserveSig]
            int GetIconSize(out int cx, out int cy);

            [PreserveSig]
            int SetIconSize(int cx, int cy);

            [PreserveSig]
            int GetImageCount(out int pi);

            [PreserveSig]
            int SetImageCount(uint uNewCount);

            [PreserveSig]
            int SetBkColor(int clrBk, out int pclr);

            [PreserveSig]
            int GetBkColor(out int pclr);

            [PreserveSig]
            int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);

            [PreserveSig]
            int EndDrag();

            [PreserveSig]
            int DragEnter(IntPtr hwndLock, int x, int y);

            [PreserveSig]
            int DragLeave(IntPtr hwndLock);

            [PreserveSig]
            int DragMove(int x, int y);

            [PreserveSig]
            int SetDragCursorImage(IImageList punk, int iDrag, int dxHotspot, int dyHotspot);

            [PreserveSig]
            int DragShowNolock(bool fShow);

            [PreserveSig]
            int GetDragImage(out POINT ppt, out POINT pptHotspot, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetItemFlags(int i, out uint dwFlags);

            [PreserveSig]
            int GetOverlayImage(int iOverlay, out int piIndex);
        }

        // IImageList.Draw에 사용되는 구조체
        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGELISTDRAWPARAMS {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public uint fStyle;
            public uint dwRop;
            public uint fState;
            public uint Frame;
            public int crEffect;
        }

        // IImageList.GetImageInfo에 사용되는 구조체
        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGEINFO {
            public IntPtr hbmImage;
            public IntPtr hbmMask;
            public int Unused1;
            public int Unused2;
            public RECT rcImage;
        }

        // ILD 플래그 (IImageList.GetIcon에서 사용)
        public const uint ILD_NORMAL = 0x00000000;
        public const uint ILD_TRANSPARENT = 0x00000001;
        public const uint ILD_IMAGE = 0x00000020;

        // SHGetImageList: 시스템 이미지 리스트 획득 (shell32.dll ordinal #727)
        [DllImport("shell32.dll", EntryPoint = "#727")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);
    }
}
