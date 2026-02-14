using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
namespace AppGroup
{
    public class Program
    {
        /// <summary>
        /// 프로세스 시작 시 캡처된 커서 위치 (작업 표시줄 아이콘 클릭 위치)
        /// </summary>
        internal static NativeMethods.POINT LaunchCursorPosition { get; private set; }

        [STAThread]
        //static int Main(string[] args) {
        //    WinRT.ComWrappersSupport.InitializeComWrappers();
        //    bool isRedirect = DecideRedirection();

        //    if (!isRedirect) {
        //        Application.Start((p) => {
        //            var context = new DispatcherQueueSynchronizationContext(
        //                DispatcherQueue.GetForCurrentThread());
        //            SynchronizationContext.SetSynchronizationContext(context);
        //            _ = new App();
        //        });
        //    }

        //    return 0;
        //}

        static void Main(string[] args)
        {
            // 1. settings.json에서 GroupsDataPath 읽기
            try
            {
                string groupsDataPath = SettingsHelper.LoadGroupsDataPathSync();
                AppPaths.InitializeGroupsPath(groupsDataPath);
            }
            catch (Exception ex) { Debug.WriteLine($"Groups 경로 초기화 실패: {ex.Message}"); }

            // 2. Groups 데이터 마이그레이션 (기존 경로 → 새 경로)
            try { AppPaths.MigrateGroupsDataIfNeeded(); }
            catch (Exception ex) { Debug.WriteLine($"마이그레이션 호출 실패: {ex.Message}"); }

            // 프로세스 시작 즉시 커서 위치 캡처 (작업 표시줄 아이콘 클릭 위치)
            NativeMethods.GetCursorPos(out NativeMethods.POINT launchCursorPos);

            // 캡처된 커서 위치를 파일에 저장 (팝업 프로세스에서 읽기 위해)
            AppPaths.SaveCursorPosition(launchCursorPos.X, launchCursorPos.Y);

            IntPtr targetWindow = NativeMethods.FindWindow(null, "Popup Window");
            string[] cmdArgs = Environment.GetCommandLineArgs();

            if (targetWindow != IntPtr.Zero)
            {
                if (cmdArgs.Length > 1)
                {
                    string command = cmdArgs[1];

                    // EditGroupWindow/LaunchAll은 팝업이 아닌 별도 명령이므로 팝업에 전달하지 않음
                    if (command != "EditGroupWindow" && command != "LaunchAll")
                    {
                        // 팝업 프로세스에 포그라운드 권한 부여
                        NativeMethods.GetWindowThreadProcessId(targetWindow, out uint popupPid);
                        NativeMethods.AllowSetForegroundWindow(popupPid);

                        // 명령 전송 (팝업이 자체적으로 크기 조정 → 위치 설정 → 표시)
                        NativeMethods.SendString(targetWindow, command);
                    }
                }
            }

            // 캡처된 커서 위치를 App에서 사용할 수 있도록 저장
            LaunchCursorPosition = launchCursorPos;

            WinRT.ComWrappersSupport.InitializeComWrappers();

            bool isSilent = HasSilentFlag(cmdArgs);

            if (cmdArgs.Length <= 1 && !isSilent)
            {
                // No arguments provided - check for existing main window instance
                IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");
                if (existingMainHWnd != IntPtr.Zero)
                {
                    // Bring existing instance to foreground and exit
                    NativeMethods.SetForegroundWindow(existingMainHWnd);
                    NativeMethods.ShowWindow(existingMainHWnd, NativeMethods.SW_RESTORE);
                    return;
                }
            }



            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });


        }
        private static bool HasSilentFlag(string[] args)
        {
            try
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking silent flag: {ex.Message}");
                return false;
            }
        }

        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = args.Kind;
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey("MySingleInstanceApp");

            if (keyInstance.IsCurrent)
            {
                keyInstance.Activated += OnActivated;
            }
            else
            {
                isRedirect = true;
            }

            return isRedirect;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(
    IntPtr lpEventAttributes, bool bManualReset,
    bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("ole32.dll")]
        private static extern uint CoWaitForMultipleObjects(
            uint dwFlags, uint dwMilliseconds, ulong nHandles,
            IntPtr[] pHandles, out uint dwIndex);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private static IntPtr redirectEventHandle = IntPtr.Zero;

        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        public static void RedirectActivationTo(AppActivationArguments args,
                                                AppInstance keyInstance)
        {
            redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);

            // .Wait() 제거로 데드락 위험 방지: async/await 사용하여 비동기 처리
            Task.Run(async () =>
            {
                try
                {
                    await keyInstance.RedirectActivationToAsync(args);
                    SetEvent(redirectEventHandle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RedirectActivationToAsync failed: {ex.Message}");
                    SetEvent(redirectEventHandle);
                }
            });

            uint CWMO_DEFAULT = 0;
            uint INFINITE = 0xFFFFFFFF;
            _ = CoWaitForMultipleObjects(
               CWMO_DEFAULT, INFINITE, 1,
               [redirectEventHandle], out uint handleIndex);

            // Bring the window to the foreground
            Process process = Process.GetProcessById((int)keyInstance.ProcessId);

            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_SHOW);
            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
            SetForegroundWindow(process.MainWindowHandle);

            NativeMethods.ForceForegroundWindow(process.MainWindowHandle);
        }

        private static void OnActivated(object sender, AppActivationArguments args)
        {
            ExtendedActivationKind kind = args.Kind;
        }
    }

}
