using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using IWshRuntimeLibrary;
using AppGroup.Models;
using File = System.IO.File;

namespace AppGroup.View {
    /// <summary>
    /// EditGroupWindow - AllApps 다이얼로그 관련 partial class
    /// 설치된 앱 목록 표시 및 선택 기능을 담당합니다.
    /// </summary>
    public sealed partial class EditGroupWindow {

        #region AllApps Dialog Handlers

        /// <summary>
        /// AllApps 다이얼로그 닫기 버튼 클릭
        /// </summary>
        private void CloseAllAppsDialog(object sender, RoutedEventArgs e)
        {
            // 로딩 중인 작업 취소
            _appLoadingCts?.Cancel();
            AllAppsDialog.Hide();
        }

        /// <summary>
        /// 설치된 앱 목록 다이얼로그 표시
        /// </summary>
        private async void AllAppsButton_Click(object sender, RoutedEventArgs e)
        {
            // 이전 로딩 작업 취소
            _appLoadingCts?.Cancel();
            _appLoadingCts = new System.Threading.CancellationTokenSource();
            var cancellationToken = _appLoadingCts.Token;

            try
            {
                AllAppsDialog.XamlRoot = this.Content.XamlRoot;
                AppsLoadingRing.IsActive = true;

                // 로딩 중 컨트롤 비활성화
                AllAppsListView.IsEnabled = false;
                AddSelectedAppsButton.IsEnabled = false;
                AppSearchTextBox.IsEnabled = false;

                _viewModel.InstalledApps.Clear();
                _viewModel.AllInstalledApps.Clear();
                AllAppsListView.ItemsSource = _viewModel.InstalledApps;
                AppSearchTextBox.Text = "";
                _viewModel.SelectedAppsCountText = "0개 선택됨";

                _ = AllAppsDialog.ShowAsync();

                // UI가 렌더링될 시간을 확보한 후 백그라운드에서 앱 목록 가져오기
                await Task.Yield();

                var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // shell:AppsFolder를 통해 Windows 설치된 앱 목록 가져오기 (Win32 + UWP 모두 포함)
                var shellApps = await Task.Run(GetAppsFromShellFolder);

                foreach (var appInfo in shellApps)
                {
                    // 취소 요청 확인
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.WriteLine("App loading cancelled");
                        return;
                    }

                    try
                    {
                        // 중복 이름 건너뛰기
                        if (addedNames.Contains(appInfo.DisplayName)) continue;
                        addedNames.Add(appInfo.DisplayName);

                        string icon = null;

                        // 1단계: IconPath가 있으면 사용 (exe 경로)
                        if (!string.IsNullOrEmpty(appInfo.IconPath) && File.Exists(appInfo.IconPath))
                        {
                            try
                            {
                                icon = await IconCache.GetIconPathAsync(appInfo.IconPath);
                                Debug.WriteLine($"Got icon from IconPath for {appInfo.DisplayName}: {icon}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to get icon from IconPath for {appInfo.DisplayName}: {ex.Message}");
                            }
                        }

                        // 2단계: exe 파일 경로에서 아이콘 추출
                        if (string.IsNullOrEmpty(icon) && 
                            !string.IsNullOrEmpty(appInfo.ExecutablePath) &&
                            appInfo.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(appInfo.ExecutablePath))
                        {
                            try
                            {
                                icon = await IconCache.GetIconPathAsync(appInfo.ExecutablePath);
                                Debug.WriteLine($"Got icon from exe for {appInfo.DisplayName}: {icon}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to get icon for exe {appInfo.ExecutablePath}: {ex.Message}");
                            }
                        }
                        
                        // 3단계: AUMID로 UWP 아이콘 추출 시도
                        if (string.IsNullOrEmpty(icon) && !string.IsNullOrEmpty(appInfo.AppUserModelId))
                        {
                            try
                            {
                                icon = await GetAppIconFromShellAsync(appInfo.AppUserModelId, appInfo.DisplayName);
                                Debug.WriteLine($"Got icon from AUMID for {appInfo.DisplayName}: {icon}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to get icon from shell for {appInfo.DisplayName}: {ex.Message}");
                            }
                        }

                        // 취소 요청 재확인 (비동기 작업 후)
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Debug.WriteLine("App loading cancelled after icon extraction");
                            return;
                        }

                        var app = new InstalledAppModel
                        {
                            DisplayName = appInfo.DisplayName,
                            ExecutablePath = appInfo.ExecutablePath ?? $"shell:AppsFolder\\{appInfo.AppUserModelId}",
                            Icon = icon,
                            IsSelected = false
                        };
                        app.SelectionChanged += (s, args) => UpdateSelectedAppsCount();
                        _viewModel.AllInstalledApps.Add(app);
                        _viewModel.InstalledApps.Add(app);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing app {appInfo.DisplayName}: {ex.Message}");
                    }
                }

                // 취소 요청 확인
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("App loading cancelled before sorting");
                    return;
                }

                // 이름순 정렬
                var sorted = _viewModel.AllInstalledApps.OrderBy(a => a.DisplayName).ToList();
                _viewModel.AllInstalledApps.Clear();
                _viewModel.InstalledApps.Clear();
                foreach (var app in sorted)
                {
                    _viewModel.AllInstalledApps.Add(app);
                    _viewModel.InstalledApps.Add(app);
                }

                AppsLoadingRing.IsActive = false;

                // 로딩 완료 후 컨트롤 활성화
                AllAppsListView.IsEnabled = true;
                AddSelectedAppsButton.IsEnabled = true;
                AppSearchTextBox.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("App loading operation was cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing installed apps: {ex.Message}");
                AppsLoadingRing.IsActive = false;

                // 에러 발생 시에도 컨트롤 활성화
                AllAppsListView.IsEnabled = true;
                AddSelectedAppsButton.IsEnabled = true;
                AppSearchTextBox.IsEnabled = true;
            }
        }

        /// <summary>
        /// shell:AppsFolder에서 설치된 앱 목록 가져오기 (아이콘 경로 포함)
        /// </summary>
        private List<(string DisplayName, string ExecutablePath, string AppUserModelId, string IconPath)> GetAppsFromShellFolder()
        {
            var apps = new List<(string DisplayName, string ExecutablePath, string AppUserModelId, string IconPath)>();

            try
            {
                // Shell.Application COM 객체를 통해 AppsFolder 접근
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return apps;

                dynamic shell = Activator.CreateInstance(shellType);
                if (shell == null) return apps;

                // shell:AppsFolder 네임스페이스 열기
                dynamic folder = shell.NameSpace("shell:AppsFolder");
                if (folder == null) return apps;

                foreach (dynamic item in folder.Items())
                {
                    try
                    {
                        string name = item.Name;
                        string path = item.Path; // AUMID 또는 exe 경로

                        if (string.IsNullOrEmpty(name)) continue;

                        // 시스템 항목 필터링
                        if (name.StartsWith("Microsoft.") &&
                            (name.Contains("Extension") || name.Contains("Client"))) continue;

                        string exePath = null;
                        string aumid = null;
                        string iconPath = null;

                        // path가 exe 경로인지 AUMID인지 확인
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                            {
                                exePath = path;
                            }
                            else
                            {
                                // AUMID로 간주
                                aumid = path;

                                // AUMID에서 exe 경로 추출 시도 (Win32 앱의 경우)
                                exePath = TryGetExePathFromAumid(path);
                            }
                        }

                        // Shell item에서 아이콘 추출 시도
                        try
                        {
                            // ExtractedIconLocation 속성 사용 시도 (일부 앱에서 작동)
                            string iconLocation = null;
                            try
                            {
                                // FolderItem의 ExtendedProperty를 통해 아이콘 정보 가져오기
                                iconLocation = folder.GetDetailsOf(item, 0); // 이름
                            }
                            catch { }

                            // exe 경로가 있으면 해당 경로에서 아이콘 추출
                            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                            {
                                iconPath = exePath;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error getting icon for {name}: {ex.Message}");
                        }

                        apps.Add((name, exePath, aumid, iconPath));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error enumerating shell item: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing shell:AppsFolder: {ex.Message}");
            }

            return apps;
        }

        /// <summary>
        /// AUMID에서 exe 경로 추출 시도
        /// </summary>
        private string TryGetExePathFromAumid(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return null;

            try
            {
                // AUMID 형식: {PFN}!{AppId} 또는 경로 형식
                // 레지스트리에서 AUMID로 exe 경로 찾기
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"Software\Classes\ActivatableClasses\Package\{aumid.Split('!')[0]}\Server");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var exePath = subKey?.GetValue("ExePath") as string;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            return exePath;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// UWP 앱 아이콘 추출
        /// </summary>
        private async Task<string> GetAppIconFromShellAsync(string aumid, string displayName)
        {
            try
            {
                // shell:AppsFolder 경로로 직접 IconCache 호출
                string shellPath = $"shell:AppsFolder\\{aumid}";
                var icon = await IconCache.GetIconPathAsync(shellPath);

                if (!string.IsNullOrEmpty(icon))
                {
                    Debug.WriteLine($"GetAppIconFromShellAsync: Got icon from IconCache for {displayName}");
                    return icon;
                }

                // 폴백: 임시 바로가기 생성하여 실제 아이콘 추출 (화살표 없이)
                string tempFolder = Path.Combine(Path.GetTempPath(), "AppGroup", "TempIcons");
                Directory.CreateDirectory(tempFolder);

                string tempLnkPath = Path.Combine(tempFolder, $"{SanitizeFileName(displayName)}.lnk");

                // 바로가기 생성
                IWshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(tempLnkPath);
                shortcut.TargetPath = shellPath;
                shortcut.Save();

                // 바로가기의 타겟에서 아이콘 추출 (화살표 없는 실제 아이콘)
                if (File.Exists(tempLnkPath))
                {
                    try
                    {
                        IWshShortcut savedShortcut = (IWshShortcut)wshShell.CreateShortcut(tempLnkPath);
                        string iconLocation = savedShortcut.IconLocation;
                        string targetPath = savedShortcut.TargetPath;

                        // 1. IconLocation에서 아이콘 파일 추출 시도 (exe, dll 등에서 추출)
                        if (!string.IsNullOrEmpty(iconLocation) && iconLocation != ",")
                        {
                            string[] iconInfo = iconLocation.Split(',');
                            string iconPath = iconInfo[0].Trim();

                            // IconLocation의 파일이 존재하면 해당 파일에서 아이콘 추출
                            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                            {
                                icon = await IconCache.GetIconPathAsync(iconPath);
                                if (!string.IsNullOrEmpty(icon))
                                {
                                    Debug.WriteLine($"GetAppIconFromShellAsync: Extracted from IconLocation: {iconPath}");
                                    try { File.Delete(tempLnkPath); } catch { }
                                    return icon;
                                }
                            }
                        }

                        // 2. 타겟 경로에서 직접 아이콘 추출 (화살표 없음)
                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            icon = await IconCache.GetIconPathAsync(targetPath);
                            if (!string.IsNullOrEmpty(icon))
                            {
                                Debug.WriteLine($"GetAppIconFromShellAsync: Extracted from target: {targetPath}");
                                try { File.Delete(tempLnkPath); } catch { }
                                return icon;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading shortcut properties: {ex.Message}");
                    }

                    // 3. 바로가기 파일 자체에서 아이콘 추출 (IconCache 사용 - 화살표 제거 로직 적용됨)
                    icon = await IconCache.GetIconPathAsync(tempLnkPath);

                    // 임시 파일 정리
                    try { File.Delete(tempLnkPath); } catch { }

                    if (!string.IsNullOrEmpty(icon))
                    {
                        Debug.WriteLine($"GetAppIconFromShellAsync: Extracted from shortcut file for {displayName}");
                        return icon;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting UWP app icon for {displayName}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 파일명에 사용할 수 없는 문자 제거
        /// </summary>
        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "app";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>
        /// 바로가기 파일의 대상 경로 가져오기
        /// </summary>
        private string GetShortcutTarget(string shortcutPath)
        {
            try
            {
                IWshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                return shortcut.TargetPath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 앱 검색 텍스트 변경 이벤트
        /// </summary>
        private void AppSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = AppSearchTextBox.Text?.ToLower() ?? "";
            _viewModel.InstalledApps.Clear();

            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? _viewModel.AllInstalledApps
                : _viewModel.AllInstalledApps.Where(a => a.DisplayName.ToLower().Contains(searchText)).ToList();

            foreach (var app in filtered)
            {
                _viewModel.InstalledApps.Add(app);
            }

            UpdateSelectedAppsCount();
        }

        /// <summary>
        /// 선택된 앱 개수 업데이트
        /// </summary>
        private void UpdateSelectedAppsCount()
        {
            var count = _viewModel.AllInstalledApps.Count(a => a.IsSelected);
            _viewModel.SelectedAppsCountText = $"{count}개 선택됨";
        }

        /// <summary>
        /// 선택된 앱 추가 버튼 클릭
        /// </summary>
        private async void AddSelectedApps_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = _viewModel.AllInstalledApps.Where(a => a.IsSelected).ToList();

            if (selectedApps.Count == 0)
            {
                return;
            }

            foreach (var app in selectedApps)
            {
                // 이미 추가되었는지 확인
                if (_viewModel.ExeFiles.Any(f => f.FilePath.Equals(app.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // 다시 추출하는 대신 InstalledAppModel에서 이미 추출된 아이콘 사용
                // ExecutablePath가 shell:AppsFolder\... 형식인 UWP 앱 처리
                var icon = app.Icon ?? await IconCache.GetIconPathAsync(app.ExecutablePath);
                _viewModel.ExeFiles.Add(new ExeFileModel
                {
                    FileName = string.IsNullOrEmpty(app.DisplayName) ? Path.GetFileName(app.ExecutablePath) : app.DisplayName,
                    Icon = icon,
                    FilePath = app.ExecutablePath,
                    Tooltip = app.DisplayName,
                    Args = "",
                    IconPath = icon
                });
            }

            ExeListView.ItemsSource = _viewModel.ExeFiles;
            lastSelectedItem = GroupColComboBox.SelectedItem as string;
            _viewModel.ApplicationCountText = ExeListView.Items.Count > 1
                ? ExeListView.Items.Count.ToString() + "개 항목"
                : ExeListView.Items.Count == 1
                ? "1개 항목"
                : "";

            IconGridComboBox.Items.Clear();
            IconGridComboBox.Items.Add("2");
            IconGridComboBox.SelectedItem = "2";

            GroupColComboBox.Items.Clear();
            for (int i = 1; i <= _viewModel.ExeFiles.Count; i++)
            {
                GroupColComboBox.Items.Add(i.ToString());
            }

            if (_viewModel.ExeFiles.Count > 3)
            {
                GroupColComboBox.SelectedItem = lastSelectedItem ?? "3";
            }
            else
            {
                GroupColComboBox.SelectedItem = _viewModel.ExeFiles.Count.ToString();
            }

            AllAppsDialog.Hide();
        }

        #endregion

        #region COM Interfaces for Shell Link

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        }

        [ClassInterface(ClassInterfaceType.None)]
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        #endregion
    }
}
