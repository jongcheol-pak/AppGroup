

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI.StartScreen;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using WinUIEx;
using AppGroup.View;

namespace AppGroup {

    /// <summary>
    /// 애플리케이션의 진입점과 수명 주기를 관리하는 클래스입니다.
    /// </summary>
    public partial class App : Application {
        // 메인 윈도우 인스턴스
        private MainWindow? m_window;
        // 팝업 윈도우 인스턴스 (그룹 선택/실행용)
        private PopupWindow? popupWindow;
        // 그룹 편집 윈도우 인스턴스
        private EditGroupWindow? editWindow;

        private bool useFileMode = false;
        // LaunchAll 명령의 데드락 방지를 위한 지연 실행용 필드
        private string? _pendingLaunchAllGroupName = null;

        /// <summary>
        /// App 클래스의 생성자입니다.
        /// 애플리케이션 초기화, 중복 실행 방지, 명령줄 인수 처리 등을 수행합니다.
        /// </summary>
        public App() {
            try {
                // 명령줄 인수 가져오기
                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool isSilent = HasSilentFlag(cmdArgs);

                // --silent 플래그와 그룹 이름이 함께 사용된 경우 종료 (잘못된 조합)
                if (isSilent && cmdArgs.Length > 2) {
                    Environment.Exit(0);
                    return;
                }

                // 인수가 없고 이미 실행 중인 인스턴스가 있는지 확인
                if (cmdArgs.Length <= 1 && !isSilent) {
                    // 인수가 제공되지 않음 - 기존 메인 윈도우 인스턴스 확인
                    IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");
                    if (existingMainHWnd != IntPtr.Zero) {
                        // 기존 인스턴스를 맨 앞으로 가져오고 현재 인스턴스 종료
                        NativeMethods.SetForegroundWindow(existingMainHWnd);
                        NativeMethods.ShowWindow(existingMainHWnd, NativeMethods.SW_RESTORE);
                        Environment.Exit(0);
                        return;
                    }
                }

                // 인수가 있고 자동 실행 모드가 아닌 경우 처리
                if (cmdArgs.Length > 1 && !isSilent) {
                    string groupName = cmdArgs[1];

                    if (groupName != "EditGroupWindow" && groupName != "LaunchAll") {
                        // JSON 데이터에서 그룹 존재 여부 빠르게 확인
                        if (!JsonConfigHelper.GroupExistsInJson(groupName)) {
                            Environment.Exit(0);
                        }
                    }
                }

                // 기존 윈도우 찾기 - 인수가 있는 경우에만 확인 (첫 실행 아님)
                if (!isSilent && cmdArgs.Length > 1) {
                    IntPtr existingPopupHWnd = NativeMethods.FindWindow(null, "Popup Window");
                    IntPtr existingEditHWnd = NativeMethods.FindWindow(null, "Edit Group");
                    IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");

                    // 기존 윈도우를 생성자에서 처리하여 더 빠른 응답 제공
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        // 그룹 편집 명령: AppGroup.exe EditGroupWindow --id
                        int groupId = ExtractIdFromCommandLine(cmdArgs);
                        AppPaths.SaveGroupIdToFile(groupId.ToString());

                        // 점프 목록 초기화는 기존 윈도우 처리 전에 수행

                        if (existingEditHWnd != IntPtr.Zero) {
                            // 이미 편집 창이 열려있으면 활성화
                            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", groupId);
                            editGroup.Activate(); 
                            Environment.Exit(0);
                            return;
                        }
                        else if (existingMainHWnd != IntPtr.Zero || existingPopupHWnd != IntPtr.Zero) {
                            // 메인 창이나 팝업 창이 열려있으면 종료
                            Environment.Exit(0);
                            return;
                        }
                    }
                    else if (command == "LaunchAll") {
                        // 전체 실행 명령 처리 - 데드락 방지를 위해 동기 래퍼 대신 별도 처리
                        string targetGroupName = ExtractGroupNameFromCommandLine(cmdArgs);
                        // OnLaunched에서 비동기로 처리하도록 플래그 설정
                        _pendingLaunchAllGroupName = targetGroupName;
                        // 생성자에서는 초기화만 수행하고 실제 실행은 OnLaunched에서 처리
                    }
                    else {
                        // LaunchAll이 아니면 그룹 이름으로 간주 (예: "CH")
                        // AppGroup.exe "GroupName"
                        if (useFileMode) {
                            AppPaths.SaveGroupNameToFile(command);
                        }
                        
                        // 이 그룹 이름에 대한 그룹 ID 저장
                        try {
                            int groupId = JsonConfigHelper.FindKeyByGroupName(command);
                            AppPaths.SaveGroupIdToFile(groupId.ToString());
                        }
                        catch (Exception ex) {
                            System.Diagnostics.Debug.WriteLine($"Failed to find group ID for '{command}': {ex.Message}");
                        }


                        if (existingPopupHWnd != IntPtr.Zero) {
                            // 팝업 창이 이미 존재하면 점프 목록 동기화 후 종료
                            //BringWindowToFront(existingPopupHWnd);
                            InitializeJumpListSync();
                            Environment.Exit(0);
                            return;
                        }
                        else if (existingMainHWnd != IntPtr.Zero || existingEditHWnd != IntPtr.Zero) {
                            // 다른 창이 열려있으면 종료
                            Environment.Exit(0);
                            return;
                        }
                    }
                }

                // 설정 초기화 - 필요한 경우 기본 시작 설정을 적용
                _ = InitializeSettingsAsync();

                this.InitializeComponent();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"App initialization failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 비동기적으로 설정을 초기화합니다.
        /// </summary>
        private async Task InitializeSettingsAsync() {
            try {
                // 설정 로드 - 필요한 경우 시작 프로그램 등록을 자동으로 처리
                await SettingsHelper.LoadSettingsAsync();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Settings initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 생성자에서 사용할 점프 목록 초기화 (동기 래퍼)
        /// </summary>
        private void InitializeJumpListSync() {
            try {
                Task.Run(async () => await InitializeJumpListAsync()).Wait();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Sync jump list initialization failed: {ex.Message}");
            }
        }


        /// <summary>
        /// 작업 표시줄 점프 목록(Jump List)을 비동기적으로 초기화합니다.
        /// 가장 최근에 사용한 그룹을 점프 목록에 추가하여 빠른 접근을 돕습니다.
        /// </summary>
        private async Task InitializeJumpListAsync() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                JumpList jumpList = await JumpList.LoadCurrentAsync();

                System.Diagnostics.Debug.WriteLine($"Jump list initialization started with args: {string.Join(", ", cmdArgs)}");

                // 인수가 있을 때만 점프 목록 수정
                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    System.Diagnostics.Debug.WriteLine($"Processing command: '{command}'");

                    jumpList.Items.Clear();

                    if (command == "EditGroupWindow") {
                        // 편집 창 명령인 경우
                        System.Diagnostics.Debug.WriteLine("Creating jump list for EditGroupWindow");
                        var jumpListItem = CreateJumpListItemTask();
                        var launchAllItem = CreateLaunchAllJumpListItem();

                        jumpList.Items.Add(jumpListItem);
                        jumpList.Items.Add(launchAllItem);
                    }
                    else if (command == "LaunchAll") {
                        // 전체 실행 명령인 경우 - 일회성 동작이므로 점프 목록 아이템 생성 안 함
                        System.Diagnostics.Debug.WriteLine("Creating jump list for LaunchAll");
                    }
                    else {
                        // 그룹 이름인 경우 (예: "CH")
                        System.Diagnostics.Debug.WriteLine($"Creating jump list for group name: '{command}'");

                        // 그룹이 존재하는지 확인 후 점프 목록 아이템 생성
                        if (JsonConfigHelper.GroupExistsInJson(command)) {
                            var jumpListItem = CreateJumpListItemTask();
                            var launchAllItem = CreateLaunchAllJumpListItem();

                            jumpList.Items.Add(jumpListItem);
                            jumpList.Items.Add(launchAllItem);

                            System.Diagnostics.Debug.WriteLine($"Jump list items created for group '{command}'");
                        }
                        else {
                            System.Diagnostics.Debug.WriteLine($"Group '{command}' does not exist in JSON");
                        }
                    }

                    await jumpList.SaveAsync();
                    System.Diagnostics.Debug.WriteLine("Jump list saved successfully");
                }
                else {
                    System.Diagnostics.Debug.WriteLine("No arguments provided, jump list not modified");
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Jump list initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }


        /// <summary>
        /// 그룹 편집을 위한 점프 목록 아이템을 생성합니다.
        /// </summary>
        private JumpListItem CreateJumpListItemTask() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                System.Diagnostics.Debug.WriteLine($"CreateJumpListItemTask called with args: {string.Join(", ", cmdArgs)}");

                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];
                    System.Diagnostics.Debug.WriteLine($"Processing command: '{command}'");

                    if (command == "EditGroupWindow") {
                        int groupId = ExtractIdFromCommandLine(cmdArgs);
                        AppPaths.SaveGroupIdToFile(groupId.ToString());
                        var taskItem = JumpListItem.CreateWithArguments("EditGroupWindow --id=" + groupId, "그룹 편집"); // "이 그룹 편집"
                        System.Diagnostics.Debug.WriteLine($"Created EditGroupWindow jump list item with ID: {groupId}");
                        return taskItem;
                    }
                    else if (command != "LaunchAll") {
                        // 그룹 이름 처리
                        try {
                            int groupId = JsonConfigHelper.FindKeyByGroupName(command);
                            AppPaths.SaveGroupIdToFile(groupId.ToString());

                            var taskItem = JumpListItem.CreateWithArguments("EditGroupWindow --id=" + groupId, "그룹 편집"); // "이 그룹 편집"
                            System.Diagnostics.Debug.WriteLine($"Created jump list item for group '{command}' with ID: {groupId}");
                            return taskItem;
                        }
                        catch (Exception ex) {
                            System.Diagnostics.Debug.WriteLine($"Failed to find group ID for '{command}': {ex.Message}");
                        }
                    }
                }

                // 예외 발생 시 기본값
                System.Diagnostics.Debug.WriteLine("Using fallback jump list item");
                return JumpListItem.CreateWithArguments("EditGroupWindow --id=0", "그룹 편집");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to create edit jump list item: {ex.Message}");
                return JumpListItem.CreateWithArguments("EditGroupWindow --id=0", "그룹 편집");
            }
        }

        /// <summary>
        /// '모두 실행' 기능을 위한 점프 목록 아이템을 생성합니다.
        /// </summary>
        private JumpListItem CreateLaunchAllJumpListItem() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                System.Diagnostics.Debug.WriteLine($"CreateLaunchAllJumpListItem called with args: {string.Join(", ", cmdArgs)}");

                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        int groupId = ExtractIdFromCommandLine(cmdArgs);
                        var taskItem = JumpListItem.CreateWithArguments($"LaunchAll --groupId={groupId}", "모두 실행"); // "모두 실행"
                        System.Diagnostics.Debug.WriteLine($"Created LaunchAll item for EditGroupWindow with ID: {groupId}");
                        return taskItem;
                    }
                    else if (command != "LaunchAll") {
                        // 그룹 이름 처리
                        string groupName = command;
                        var taskItem = JumpListItem.CreateWithArguments($"LaunchAll --groupName=\"{groupName}\"", "모두 실행"); // "모두 실행"
                        System.Diagnostics.Debug.WriteLine($"Created LaunchAll item for group: '{groupName}'");
                        return taskItem;
                    }
                }

                // 예외 발생 시 기본값
                System.Diagnostics.Debug.WriteLine("Using fallback LaunchAll item");
                return JumpListItem.CreateWithArguments("LaunchAll", "모두 실행");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to create launch all jump list item: {ex.Message}");
                return JumpListItem.CreateWithArguments("LaunchAll", "모두 실행");
            }
        }

        /// <summary>
        /// 애플리케이션이 실행될 때 호출됩니다.
        /// </summary>
        /// <param name="args">실행 인수 등 이벤트 데이터</param>
        protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            try {
                // LaunchAll 명령이 생성자에서 설정된 경우 비동기로 처리
                if (!string.IsNullOrEmpty(_pendingLaunchAllGroupName)) {
                    await JsonConfigHelper.LaunchAll(_pendingLaunchAllGroupName);
                    Environment.Exit(0);
                    return;
                }

                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool isSilent = HasSilentFlag(cmdArgs);
                IntPtr existingPopupHWnd = NativeMethods.FindWindow(null, "Popup Window");

                System.Diagnostics.Debug.WriteLine($"OnLaunched - isSilent: {isSilent}, args: {string.Join(", ", cmdArgs)}");

                // --silent 플래그 처리 (자동 시작 등)
                if (isSilent) {
                    System.Diagnostics.Debug.WriteLine("Silent mode detected - creating windows in hidden state");
                    if (existingPopupHWnd != IntPtr.Zero) {
                        Environment.Exit(0);
                        return;
                    }
                    CreateAllWindows(hideAll: true);
                    
                    // Silent 모드에서 모든 윈도우가 확실히 숨겨지도록 재확인
                    HideAllWindows();
                    
                    await InitializeJumpListAsync();
                    InitializeSystemTray();
                    System.Diagnostics.Debug.WriteLine("Silent mode initialization complete - only tray should be visible");
                    return;
                }

                // 인수가 있으면 항상 점프 목록 업데이트
                if (cmdArgs.Length > 1) {
                    await InitializeJumpListAsync();
                }

                // 첫 실행 시 모든 윈도우 생성
                CreateAllWindows();

                // 윈도우 생성 후 시스템 트레이 초기화
                InitializeSystemTray();

                // 인수에 따라 적절한 윈도우 표시
                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        ShowEditWindow();
                        HideMainWindow();
                        HidePopupWindow();
                    }
                    else if (command != "LaunchAll") {
                        // 그룹 이름인 경우 팝업 창 표시
                        ShowPopupWindow();
                        HideMainWindow();
                        HideEditWindow();
                    }
                }
                else {
                    // 인수 없으면 메인 창 표시
                    HidePopupWindow();
                    HideEditWindow();
                    ShowMainWindow();
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"OnLaunched failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 지정된 핸들의 윈도우를 최상위로 가져오고 화면에 표시합니다.
        /// </summary>
        /// <param name="hWnd">윈도우 핸들</param>
        private void BringWindowToFront(IntPtr hWnd) {
            if (useFileMode) {
                try {
                    if (hWnd != IntPtr.Zero) {
                        NativeMethods.PositionWindowOffScreen(hWnd);
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                        NativeMethods.ForceForegroundWindow(hWnd);

                        System.Threading.Thread.Sleep(5);


                        NativeMethods.PositionWindowAboveTaskbar(hWnd);

                    }
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
                }
            }
            else { 
                try {
                    if (hWnd != IntPtr.Zero) {
                        // 먼저 윈도우를 화면 밖으로 위치시키고 표시
                        NativeMethods.PositionWindowOffScreen(hWnd);

                        // 내용을 업데이트하기 위해 메시지 전송 (비동기, 논블로킹)
                        string[] cmdArgs = Environment.GetCommandLineArgs();
                        if (cmdArgs.Length > 1) {
                            string command = cmdArgs[1];
                            NativeMethods.SendString(hWnd, command);
                        }
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);

                        NativeMethods.ForceForegroundWindow(hWnd);

                        // 작업 표시줄 위로 이동
                        NativeMethods.PositionWindowAboveTaskbar(hWnd);
                    }
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// 앱의 모든 윈도우(메인, 팝업, 편집)를 미리 생성합니다.
        /// </summary>
        /// <param name="hideAll">true이면 모든 윈도우를 숨긴 상태로 생성 (silent 모드용)</param>
        private void CreateAllWindows(bool hideAll = false) {
            try {
                editWindow = new EditGroupWindow(-1);
                // InitializeComponent()는 생성자에서 이미 호출됨

                // 편집 윈도우 즉시 숨기기
                IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                if (editHWnd != IntPtr.Zero) {
                    NativeMethods.ShowWindow(editHWnd, NativeMethods.SW_HIDE);
                }

                // 메인 윈도우 생성
                m_window = new MainWindow();
                // InitializeComponent()는 생성자에서 이미 호출됨

                // 메인 윈도우 즉시 숨기기 (silent 모드이거나 hideAll인 경우)
                IntPtr mainHWnd = WindowNative.GetWindowHandle(m_window);
                if (mainHWnd != IntPtr.Zero) {
                    NativeMethods.ShowWindow(mainHWnd, NativeMethods.SW_HIDE);
                }

                // 팝업 윈도우 생성 (숨김 상태)
                popupWindow = new PopupWindow("Popup Window");
                popupWindow.AppWindow.Resize(new SizeInt32(0,0));
                // InitializeComponent()는 생성자에서 이미 호출됨

                NativeMethods.PositionWindowOffScreen(popupWindow.GetWindowHandle());

                // 팝업 윈도우도 숨기기
                IntPtr popupHWnd = WindowNative.GetWindowHandle(popupWindow);
                if (popupHWnd != IntPtr.Zero) {
                    NativeMethods.ShowWindow(popupHWnd, NativeMethods.SW_HIDE);
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to create windows: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 메인 윈도우를 표시합니다.
        /// </summary>
        private void ShowMainWindow() {
            try {
                m_window?.Activate();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show main window: {ex.Message}");
            }
        }

       
        /// <summary>
        /// 팝업 윈도우를 표시합니다.
        /// </summary>
        private void ShowPopupWindow() {
            try {
                if (popupWindow != null) {
                    IntPtr popupHWnd = NativeMethods.FindWindow(null, "Popup Window");

                    System.Threading.Thread.Sleep(200);
                    BringWindowToFront(popupWindow.GetWindowHandle());
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show popup window: {ex.Message}");
            }
        }
       

        /// <summary>
        /// 그룹 편집 윈도우를 표시합니다.
        /// </summary>
        private void ShowEditWindow() {
            try {
                if (editWindow != null) {
                    IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                    if (editHWnd != IntPtr.Zero) {
                        NativeMethods.SetForegroundWindow(editHWnd);
                        NativeMethods.ShowWindow(editHWnd, NativeMethods.SW_RESTORE);
                        editWindow.Activate();
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show edit window: {ex.Message}");
            }
        }

        /// <summary>
        /// 메인 윈도우를 숨깁니다.
        /// </summary>
        private void HideMainWindow() {
            try {
                if (m_window != null) {
                    IntPtr mainHWnd = WindowNative.GetWindowHandle(m_window);
                    if (mainHWnd != IntPtr.Zero) {
                        NativeMethods.ShowWindow(mainHWnd, NativeMethods.SW_HIDE);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide main window: {ex.Message}");
            }
        }

        /// <summary>
        /// 팝업 윈도우를 숨깁니다.
        /// </summary>
        private void HidePopupWindow() {
            try {
                if (popupWindow != null) {
                    IntPtr popupHWnd = WindowNative.GetWindowHandle(popupWindow);

                    if (popupHWnd != IntPtr.Zero) {
                        NativeMethods.ShowWindow(popupHWnd, NativeMethods.SW_HIDE);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide popup window: {ex.Message}");
            }
        }

        /// <summary>
        /// 그룹 편집 윈도우를 숨깁니다.
        /// </summary>
        private void HideEditWindow() {
            try {
                if (editWindow != null) {
                    IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                    if (editHWnd != IntPtr.Zero) {
                        NativeMethods.ShowWindow(editHWnd, NativeMethods.SW_HIDE);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide edit window: {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 윈도우를 숨깁니다 (silent 모드용).
        /// </summary>
        private void HideAllWindows() {
            try {
                HideMainWindow();
                HidePopupWindow();
                HideEditWindow();
                System.Diagnostics.Debug.WriteLine("All windows hidden for silent mode");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide all windows: {ex.Message}");
            }
        }

        /// <summary>
        /// 시스템 트레이 아이콘을 초기화합니다.
        /// </summary>
        private void InitializeSystemTray() {
            try {
                SystemTrayManager.Initialize(
                    showCallback: () => {
                        ShowAppGroup();
                    },
                    exitCallback: () => {
                        KillAppGroup();
                    }
                );
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize system tray: {ex.Message}");
            }
        }

        /// <summary>
        /// 시스템 트레이 아이콘을 표시합니다.
        /// </summary>
        public void ShowSystemTray() {
            try {
                SystemTrayManager.ShowSystemTray();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show system tray: {ex.Message}");
            }
        }

        /// <summary>
        /// 시스템 트레이 아이콘을 숨깁니다.
        /// </summary>
        public void HideSystemTray() {
            try {
                SystemTrayManager.HideSystemTray();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide system tray: {ex.Message}");
            }
        }

        /// <summary>
        /// AppGroup 애플리케이션 창을 화면에 표시합니다.
        /// 트레이 아이콘 더블 클릭 시 등에 호출됩니다.
        /// </summary>
        private void ShowAppGroup() {
            try {
                IntPtr appGroupWindow = NativeMethods.FindWindow(null, "App Group");
                if (appGroupWindow != IntPtr.Zero) {

                    Debug.WriteLine("AppGroup.exe window found, bringing to front");
                    NativeMethods.ShowWindow(appGroupWindow, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(appGroupWindow);
                    return;
                }

                // 프로세스 이름으로도 확인
                Process[] existingProcesses = Process.GetProcessesByName("App Group");
                if (existingProcesses.Length > 0) {
                    Debug.WriteLine("AppGroup.exe process found, attempting to show window");
                    foreach (var process in existingProcesses) {
                        if (process.MainWindowHandle != IntPtr.Zero) {
                            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
                            NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                            return;
                        }
                    }

                    // 창 핸들이 없는 좀비 프로세스 정리
                    foreach (var process in existingProcesses) {
                        try {
                            process.Kill();
                            Debug.WriteLine($"Killed existing AppGroup process with ID: {process.Id}");
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Failed to kill process: {ex.Message}");
                        }
                    }
                }

                if (m_window == null) {
                    m_window = new MainWindow();
                    m_window.InitializeComponent();
                }
                m_window.Activate();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing AppGroup: {ex.Message}");
            }
        }

        /// <summary>
        /// AppGroup 애플리케이션 프로세스를 강제 종료합니다.
        /// </summary>
        private static void KillAppGroup() {
            try {
                // 먼저 시스템 트레이 아이콘 정리
                SystemTrayManager.Cleanup();

                var startInfo = new ProcessStartInfo {
                    FileName = "taskkill",
                    Arguments = "/f /t /im AppGroup.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo)) {
                    if (process != null) {
                        process.WaitForExit();

                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        if (process.ExitCode == 0) {
                            Debug.WriteLine("Successfully killed all AppGroup.exe processes");
                            Debug.WriteLine(output);
                        }
                        else {
                            Debug.WriteLine($"taskkill exit code: {process.ExitCode}");
                            if (!string.IsNullOrEmpty(error)) {
                                Debug.WriteLine($"Error: {error}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error running taskkill: {ex.Message}");
            }
            finally {
                Application.Current?.Exit();
            }
        }

        /// <summary>
        /// 명령줄 인수에 무음(silent) 플래그가 있는지 확인합니다.
        /// </summary>
        private bool HasSilentFlag(string[] args) {
            try {
                // 명령줄 인자에서 --silent 플래그 확인
                foreach (string arg in args) {
                    if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                // MSIX StartupTask에 의해 시작된 경우도 silent 모드로 처리
                if (IsStartedByStartupTask()) {
                    System.Diagnostics.Debug.WriteLine("App started by StartupTask - running in silent mode");
                    return true;
                }

                return false;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error checking silent flag: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// MSIX StartupTask에 의해 앱이 시작되었는지 확인
        /// </summary>
        private bool IsStartedByStartupTask() {
            try {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                return activatedArgs?.Kind == ExtendedActivationKind.StartupTask;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error checking StartupTask activation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 명령줄 인수에서 그룹 이름을 추출합니다.
        /// </summary>
        private string ExtractGroupNameFromCommandLine(string[] args) {
            try {
                foreach (string arg in args) {
                    if (arg.StartsWith("--groupName=")) {
                        return arg.Substring(12).Trim('"');
                    }
                    else if (arg.StartsWith("--groupId=")) {
                        if (int.TryParse(arg.Substring(10), out int groupId)) {
                            return JsonConfigHelper.FindGroupNameByKey(groupId);
                        }
                    }
                }
                return string.Empty;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error extracting group name: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 명령줄 인수에서 ID를 추출합니다.
        /// </summary>
        private int ExtractIdFromCommandLine(string[] args) {
            try {
                foreach (string arg in args) {
                    if (arg.StartsWith("--id=")) {
                        if (int.TryParse(arg.Substring(5), out int id)) {
                            return id;
                        }
                    }
                }
                // ID가 없으면 새 ID 생성
                return JsonConfigHelper.GetNextGroupId();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error extracting ID: {ex.Message}");
                return JsonConfigHelper.GetNextGroupId();
            }
        }
    }
}
