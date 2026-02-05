using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AppGroup
{
    /// <summary>
    /// NativeMethods - Win32 API P/Invoke 선언
    /// WindowPosition, ShellIcon 관련 기능은 별도 partial class 파일로 분리되었습니다.
    /// </summary>
    public static partial class NativeMethods
    {
        #region 상수

        // 윈도우 위치/크기 관련 상수
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_ASYNCWINDOWPOS = 0x4000;
        public const uint SWP_HIDEWINDOW = 0x0080;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_HIDE = 0;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;
        public const int SW_NORMAL = 1;
        public const int SW_SHOWNORMAL = 1;

        // 시스템 메트릭 상수
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        // 윈도우 메시지 상수
        public const int WM_USER = 0x0400;
        public const uint WM_SHOWWINDOW = 0x0018;
        public const uint WM_TRAYICON = 0x8000;
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_LBUTTONDBLCLK = 0x0203;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_NULL = 0x0000;
        public const int WM_COPYDATA = 0x004A;
        public const int WM_SETICON = 0x0080;

        // 시스템 변경 알림 상수
        public const int SHCNE_RENAMEITEM = 0x00000001;
        public const int SHCNE_CREATE = 0x00000002;
        public const int SHCNE_DELETE = 0x00000004;
        public const int SHCNE_UPDATEITEM = 0x00002000;
        public const int SHCNE_UPDATEIMAGE = 0x00008000;
        public const int SHCNE_UPDATEDIR = 0x00001000;
        public const int SHCNE_RENAMEFOLDER = 0x00020000;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_PATH = 0x0005;
        public const uint SHCNF_IDLIST = 0x0000;
        public const uint SHCNF_FLUSH = 0x1000;

        // 다시 그리기 상수
        public const uint RDW_ERASE = 0x0004;
        public const uint RDW_FRAME = 0x0400;
        public const uint RDW_INVALIDATE = 0x0001;
        public const uint RDW_ALLCHILDREN = 0x0080;

        // 트레이 아이콘 상수
        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;
        public const uint NIM_ADD = 0x00000000;
        public const uint NIM_MODIFY = 0x00000001;
        public const uint NIM_DELETE = 0x00000002;

        // 이미지/아이콘 관련 상수
        public const uint IMAGE_ICON = 1;
        public const uint LR_LOADFROMFILE = 0x00000010;
        public const uint LR_DEFAULTSIZE = 0x00000040;

        // 메뉴 관련 상수
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_RIGHTBUTTON = 0x0002;
        public const int ID_SHOW = 1001;
        public const int ID_EXIT = 1002;
        public const uint MIN_ALL = 419;
        public const uint RESTORE_ALL = 416;

        // 아이콘 크기 상수
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        // MessageBox 상수
        public const uint MB_OK = 0x00000000;
        public const uint MB_ICONERROR = 0x00000010;
        public const uint MB_ICONWARNING = 0x00000030;
        public const uint MB_ICONINFORMATION = 0x00000040;

        // 윈도우 프로시저 관련 상수
        public const int GWL_WNDPROC = -4;
        public const uint GW_OWNER = 4;

        // 시스템 정보 상수
        public const uint SPI_GETWORKAREA = 0x0030;

        #endregion

        #region 구조체

        /// <summary>
        /// COPYDATASTRUCT 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        /// <summary>
        /// WNDCLASSEX 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
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

        /// <summary>
        /// NOTIFYICONDATA 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        #endregion

        #region 델리게이트

        /// <summary>
        /// 윈도우 프로시저 델리게이트
        /// </summary>
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 열거형 스레드 델리게이트
        /// </summary>
        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// 서브클래스 프로시저 델리게이트
        /// </summary>
        public delegate IntPtr SubclassProc(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam,
            IntPtr uIdSubclass,
            IntPtr dwRefData);

        #endregion

        #region P/Invoke 선언 - 윈도우 메시지/관리

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

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
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out RECT pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        #endregion

        #region P/Invoke 선언 - 윈도우 생성/메시지

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

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
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        #endregion

        #region P/Invoke 선언 - 서브클래싱

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

        #endregion

        #region P/Invoke 선언 - 기타

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        #endregion

        #region 정적 필드

        public static readonly int WM_UPDATE_GROUP = RegisterWindowMessage("AppGroup.WM_UPDATE_GROUP");

        #endregion

        #region 공용 메서드

        /// <summary>
        /// 문자열을 WM_COPYDATA를 통해 다른 윈도우로 전송합니다.
        /// </summary>
        public static void SendString(IntPtr targetWindow, string message)
        {
            COPYDATASTRUCT cds = new COPYDATASTRUCT();
            cds.dwData = (IntPtr)100; // 사용자 정의 식별자
            cds.cbData = (message.Length + 1) * 2; // 바이트 단위 유니코드 문자열 길이
            cds.lpData = Marshal.StringToHGlobalUni(message);

            try
            {
                IntPtr cdsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
                Marshal.StructureToPtr(cds, cdsPtr, false);

                SendMessage(targetWindow, WM_COPYDATA, IntPtr.Zero, cdsPtr);

                Marshal.FreeHGlobal(cdsPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(cds.lpData);
            }
        }

        /// <summary>
        /// 아이콘을 파일에서 로드합니다.
        /// </summary>
        public static IntPtr LoadIcon(string iconPath)
        {
            return LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_LOADFROMFILE);
        }

        /// <summary>
        /// 윈도우를 강제로 포그라운드로 가져옵니다.
        /// </summary>
        public static void ForceForegroundWindow(IntPtr hWnd)
        {
            if (GetForegroundWindow() == hWnd)
                return;

            // 포그라운드 창의 스레드 ID 가져오기
            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            uint currentThreadId = GetCurrentThreadId();

            // 포그라운드 스레드의 입력 큐에 연결
            if (currentThreadId != foregroundThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            // 창을 맨 위로 가져오고 포그라운드 창으로 설정
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);

            // 포그라운드 스레드의 입력 큐에서 연결 해제
            if (currentThreadId != foregroundThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }

        /// <summary>
        /// 지정된 핸들의 윈도우를 안전하게 최상위로 가져옵니다.
        /// App.xaml.cs, MainWindow.xaml.cs의 중복 코드 통합
        /// </summary>
        public static void BringWindowToFrontSafe(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, SW_RESTORE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
            }
        }

        #endregion
    }
}
