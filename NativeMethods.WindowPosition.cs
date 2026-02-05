using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AppGroup
{
    /// <summary>
    /// NativeMethods - 창 위치 관련 partial class
    /// 작업 표시줄 위치 감지, 윈도우 배치, DPI 스케일링 등을 담당합니다.
    /// </summary>
    public static partial class NativeMethods
    {
        #region 상수

        // 작업 표시줄 AppBar 상수
        public const uint ABM_GETSTATE = 0x4;
        public const uint ABM_GETTASKBARPOS = 0x5;

        // 작업 표시줄 위치 상수
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;

        // 모니터 관련 상수
        public const int MDT_EFFECTIVE_DPI = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;

        #endregion

        #region 구조체

        /// <summary>
        /// 포인트 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// 사각형 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        /// <summary>
        /// 모니터 정보 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        /// <summary>
        /// 확장된 모니터 정보 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        /// <summary>
        /// AppBar 데이터 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        #endregion

        #region P/Invoke 선언

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("Shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        #endregion

        #region 열거형

        /// <summary>
        /// 작업 표시줄 위치
        /// </summary>
        private enum TaskbarPosition
        {
            Top,
            Bottom,
            Left,
            Right
        }

        #endregion

        #region 공용 메서드

        /// <summary>
        /// 윈도우를 작업 표시줄 위에 배치합니다.
        /// </summary>
        public static void PositionWindowAboveTaskbar(IntPtr hWnd)
        {
            try
            {
                // 창 크기 가져오기
                RECT windowRect;
                if (!GetWindowRect(hWnd, out windowRect))
                {
                    return;
                }
                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // 현재 커서 위치 가져오기
                POINT cursorPos;
                if (!GetCursorPos(out cursorPos))
                {
                    return;
                }

                // 모니터 정보 가져오기
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                {
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

                if (isTaskbarAutoHide)
                {
                    if (IsCursorOnTaskbar(cursorPos, monitorInfo, taskbarPosition))
                    {
                        int autoHideSpacing = (int)((baseTaskbarHeight) * dpiScale);
                        spacing = autoHideSpacing;
                    }
                    else
                    {
                        spacing += (int)(5 * dpiScale);
                    }
                }
                else
                {
                    // 일반 (표시된) 작업 표시줄의 경우 더 큰 간격 사용
                    if (taskbarPosition == TaskbarPosition.Top)
                    {
                        spacing = (int)(10 * dpiScale); // 상단은 더 작은 간격
                    }
                    else
                    {
                        spacing = (int)(6 * dpiScale); // 하단은 더 큰 간격
                    }
                }

                // 초기 위치 (커서 기준 수평 중앙 정렬)
                int x = cursorPos.X - (windowWidth / 2);
                int y;
                const int TOP_MARGIN = 100; // 상단에서 최소 100픽셀 떨어지도록

                // 작업 표시줄 위치에 따라 위치 설정
                switch (taskbarPosition)
                {
                    case TaskbarPosition.Top:
                    case TaskbarPosition.Bottom:
                        // 커서가 작업 표시줄 위에 있는지 확인
                        if (IsCursorOnTaskbar(cursorPos, monitorInfo, taskbarPosition))
                        {
                            // 작업 표시줄 위/아래에 배치
                            if (taskbarPosition == TaskbarPosition.Top)
                                y = monitorInfo.rcWork.top + spacing;
                            else
                                y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        }
                        else
                        {
                            // 커서가 작업 표시줄 위에 있지 않음 (바탕 화면/탐색기) - 커서 근처에 배치
                            y = cursorPos.Y - windowHeight - spacing;
                            // 작업 영역으로 제한 (상단 최소 100픽셀)
                            if (y < monitorInfo.rcWork.top + TOP_MARGIN)
                                y = monitorInfo.rcWork.top + TOP_MARGIN;
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
                        // 상단 최소 100픽셀 유지
                        if (y < monitorInfo.rcWork.top + TOP_MARGIN)
                            y = monitorInfo.rcWork.top + TOP_MARGIN;
                        break;
                    case TaskbarPosition.Right:
                        // 자동 숨김의 경우 작업 영역이 전체 화면일 수 있으므로 간격을 두고 모니터 오른쪽 사용
                        if (isTaskbarAutoHide)
                            x = monitorInfo.rcMonitor.right - windowWidth - spacing;
                        else
                            x = monitorInfo.rcWork.right - windowWidth - spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        // 상단 최소 100픽셀 유지
                        if (y < monitorInfo.rcWork.top + TOP_MARGIN)
                            y = monitorInfo.rcWork.top + TOP_MARGIN;
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

                // 상단 최소 100픽셀 유지 (최종 확인)
                if (y < monitorInfo.rcWork.top + TOP_MARGIN)
                    y = monitorInfo.rcWork.top + TOP_MARGIN;

                Debug.WriteLine($"Final Position (after bounds check): X={x}, Y={y}");
                Debug.WriteLine($"================================");

                // 창 이동 (크기 유지, 위치만 변경)
                SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }

        /// <summary>
        /// 윈도우를 작업 표시줄 아래에 배치합니다 (화면 밖으로).
        /// </summary>
        public static void PositionWindowBelowTaskbar(IntPtr hWnd)
        {
            try
            {
                // Get window dimensions
                RECT windowRect;
                if (!GetWindowRect(hWnd, out windowRect))
                    return;

                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // Get cursor position
                POINT cursorPos;
                if (!GetCursorPos(out cursorPos))
                    return;

                // Get monitor info
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                    return;

                // DPI scaling
                float dpiScale = GetDpiScaleForMonitor(monitor);
                int baseTaskbarHeight = 52;
                int taskbarHeight = (int)(baseTaskbarHeight * dpiScale);

                // Spacing
                int spacing = 99999;
                if (IsTaskbarAutoHide())
                {
                    int autoHideSpacing = (int)(baseTaskbarHeight * dpiScale);
                    spacing += autoHideSpacing;
                }

                // Taskbar position
                TaskbarPosition taskbarPosition = GetTaskbarPosition(monitorInfo);

                // Initial x = center horizontally on cursor
                int x = cursorPos.X - (windowWidth / 2);
                int y;

                // 작업 표시줄 아래에 배치 (하단인 경우 화면 밖)
                switch (taskbarPosition)
                {
                    case TaskbarPosition.Top:
                        y = monitorInfo.rcMonitor.top + taskbarHeight + spacing;
                        break;

                    case TaskbarPosition.Bottom:
                        y = monitorInfo.rcMonitor.bottom + spacing; // 화면 아래로
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
                        y = monitorInfo.rcMonitor.bottom + spacing; // 화면 아래로
                        break;
                }

                // 창을 화면 수평 내부에 유지
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;

                // 창 이동 (화면 밖으로 나갈 수 있도록 수직 제한 없음)
                SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }

        /// <summary>
        /// 윈도우를 화면 밖으로 배치합니다.
        /// </summary>
        public static void PositionWindowOffScreen(IntPtr hWnd)
        {
            int screenHeight = (int)DisplayArea.Primary.WorkArea.Height;
            int screenWidth = (int)DisplayArea.Primary.WorkArea.Width;

            // 깜박임을 방지하기 위해 먼저 숨김
            SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                SWP_NOMOVE |
                SWP_NOSIZE |
                SWP_NOZORDER |
                SWP_NOACTIVATE |
                SWP_HIDEWINDOW
            );

            // screenHeight를 사용하여 창을 화면 아래로 이동
            SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0, screenHeight + 100, // 하단 가장자리 아래로 밀기
                0, 0,
                SWP_NOSIZE |
                SWP_NOZORDER |
                SWP_NOACTIVATE
            );
        }

        /// <summary>
        /// 윈도우를 화면 오른쪽 밖으로 배치합니다.
        /// </summary>
        public static void PositionWindowOffScreenBelow(IntPtr hWnd)
        {
            try
            {
                // Get current cursor position
                POINT cursorPos;
                if (!GetCursorPos(out cursorPos))
                {
                    // 커서 위치 확인 실패 시 화면 중앙으로 폴백
                    cursorPos.X = GetSystemMetrics(SM_CXSCREEN) / 2;
                    cursorPos.Y = GetSystemMetrics(SM_CYSCREEN) / 2;
                }

                // Get window dimensions
                RECT windowRect;
                if (!GetWindowRect(hWnd, out windowRect))
                {
                    return;
                }
                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // 기본 모니터 정보 가져오기 (화면 밖 배치에 가장 신뢰할 수 있음)
                IntPtr primaryMonitor = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));

                if (!GetMonitorInfo(primaryMonitor, ref monitorInfo))
                {
                    // 모니터 정보 실패 시 시스템 메트릭으로 폴백
                    int screenWidth = GetSystemMetrics(SM_CXSCREEN);

                    // 커서 Y 위치에 맞춰 창을 화면 오른쪽 밖으로 배치
                    int x = screenWidth + 100; // 화면 오른쪽으로 100픽셀
                    int y = cursorPos.Y; // 커서 Y 위치에 정렬

                    SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
                    return;
                }

                // 커서 Y에 맞춰 기본 모니터 오른쪽의 화면 밖 위치 계산
                int offScreenX = monitorInfo.rcMonitor.right + 100; // 모니터 오른쪽으로 100픽셀
                int offScreenY = cursorPos.Y; // 커서 Y 위치 사용

                // 수평으로 완전히 화면 밖으로 나가도록 추가 안전 여백
                int safetyMargin = Math.Max(windowWidth, 200);
                offScreenX += safetyMargin;

                // 창을 화면 밖 위치로 이동
                SetWindowPos(hWnd, IntPtr.Zero, offScreenX, offScreenY, 0, 0, SWP_NOSIZE | SWP_NOZORDER);

                Debug.WriteLine($"Window positioned off-screen at: ({offScreenX}, {offScreenY}), cursor at: ({cursorPos.X}, {cursorPos.Y})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error positioning window off-screen: {ex.Message}");
            }
        }

        /// <summary>
        /// 모니터의 DPI 스케일을 가져옵니다.
        /// </summary>
        public static float GetDpiScaleForMonitor(IntPtr hMonitor)
        {
            try
            {
                if (Environment.OSVersion.Version.Major > 6 ||
                    (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 3))
                {
                    uint dpiX, dpiY;
                    // 모니터 DPI를 가져오기 위한 시도
                    if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0)
                    {
                        return dpiX / 96.0f;
                    }
                }
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return g.DpiX / 96.0f;
                }
            }
            catch
            {
                return 1.0f;
            }
        }

        #endregion

        #region private 메서드

        /// <summary>
        /// 커서가 작업 표시줄 위에 있는지 확인합니다.
        /// </summary>
        private static bool IsCursorOnTaskbar(POINT cursorPos, MONITORINFO monitorInfo, TaskbarPosition taskbarPosition)
        {
            float dpiScale = GetDpiScaleForMonitor(MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST));
            int taskbarThickness = (int)(52 * dpiScale); // 크기 조정된 기본 작업 표시줄 높이/너비

            switch (taskbarPosition)
            {
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
                    return cursorPos.X >= monitorInfo.rcMonitor.right - taskbarThickness &&
                           cursorPos.X <= monitorInfo.rcMonitor.right;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 작업 표시줄 위치를 가져옵니다.
        /// </summary>
        private static TaskbarPosition GetTaskbarPosition(MONITORINFO monitorInfo)
        {
            // 작업 영역이 모니터 영역과 같은 경우 (자동 숨김 작업 표시줄에서 발생 가능),
            // 다른 수단을 통한 작업 표시줄 위치 감지로 폴백
            if (monitorInfo.rcWork.top == monitorInfo.rcMonitor.top &&
                monitorInfo.rcWork.bottom == monitorInfo.rcMonitor.bottom &&
                monitorInfo.rcWork.left == monitorInfo.rcMonitor.left &&
                monitorInfo.rcWork.right == monitorInfo.rcMonitor.right)
            {
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

        /// <summary>
        /// 작업 표시줄 자동 숨김 여부를 확인합니다.
        /// </summary>
        private static bool IsTaskbarAutoHide()
        {
            APPBARDATA appBarData = new APPBARDATA();
            appBarData.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));

            // 작업 표시줄 상태 가져오기
            IntPtr result = SHAppBarMessage(ABM_GETSTATE, ref appBarData);

            // 자동 숨김 비트 설정 확인 (ABS_AUTOHIDE = 0x01)
            return ((uint)result & 0x01) != 0;
        }

        /// <summary>
        /// AppBar 정보를 사용하여 작업 표시줄 위치 가져오기 (자동 숨김 작업 표시줄에서 작동)
        /// </summary>
        private static TaskbarPosition GetTaskbarPositionFromAppBarInfo()
        {
            APPBARDATA appBarData = new APPBARDATA();
            appBarData.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));

            // 작업 표시줄 위치 데이터 가져오기
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref appBarData);
            if (result != IntPtr.Zero)
            {
                // uEdge 필드는 작업 표시줄이 도킹된 가장자리를 포함
                switch (appBarData.uEdge)
                {
                    case ABE_TOP: return TaskbarPosition.Top;
                    case ABE_BOTTOM: return TaskbarPosition.Bottom;
                    case ABE_LEFT: return TaskbarPosition.Left;
                    case ABE_RIGHT: return TaskbarPosition.Right;
                }
            }

            // 확인할 수 없는 경우 기본값 하단
            return TaskbarPosition.Bottom;
        }

        #endregion
    }
}
