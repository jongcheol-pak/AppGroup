using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AppGroup
{
    /// <summary>
    /// 애플리케이션에서 공통으로 사용하는 경로 및 파일 유틸리티 클래스입니다.
    /// 중복 코드 제거를 위해 App.xaml.cs, MainWindow.xaml.cs, EditGroupWindow.xaml.cs의
    /// 공통 기능을 통합했습니다.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// AppGroup 데이터 폴더 경로 (LocalApplicationData/AppGroup)
        /// MSIX VFS 환경에서는 가상화된 경로로 자동 리디렉션됩니다.
        /// </summary>
        public static string AppDataFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppGroup");

        /// <summary>
        /// 마지막으로 편집한 그룹 ID 저장 파일 경로
        /// </summary>
        public static string LastEditFile => Path.Combine(AppDataFolder, "lastEdit");

        /// <summary>
        /// 마지막으로 열린 그룹 이름 저장 파일 경로
        /// </summary>
        public static string LastOpenFile => Path.Combine(AppDataFolder, "lastOpen");

        /// <summary>
        /// 설정 JSON 파일 경로
        /// </summary>
        public static string ConfigFile => Path.Combine(AppDataFolder, "appgroups.json");

        /// <summary>
        /// 사용자 지정 Groups 기본 경로 (설정에서 읽은 경로)
        /// </summary>
        private static string _customGroupsBasePath;

        /// <summary>
        /// 기본 Groups 기본 경로 (%USERPROFILE%\AppGroup)
        /// Shell이 접근해야 하는 파일(.lnk, .ico)을 비가상화 경로에 저장합니다.
        /// </summary>
        public static string DefaultGroupsBasePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppGroup");

        /// <summary>
        /// 그룹 폴더 경로 (Groups 폴더) - 비가상화 경로
        /// Shell이 접근하는 바로가기(.lnk)와 아이콘(.ico)이 저장됩니다.
        /// </summary>
        public static string GroupsFolder => Path.Combine(
            _customGroupsBasePath ?? DefaultGroupsBasePath,
            "Groups");

        /// <summary>
        /// 아이콘 폴더 경로 (Icons 폴더)
        /// </summary>
        public static string IconsFolder => Path.Combine(AppDataFolder, "Icons");

        /// <summary>
        /// Groups 기본 경로를 초기화합니다. 설정에서 읽은 경로를 적용합니다.
        /// </summary>
        /// <param name="basePath">사용자 지정 기본 경로 (빈 문자열이면 기본값 사용)</param>
        public static void InitializeGroupsPath(string basePath)
        {
            if (!string.IsNullOrEmpty(basePath) && !IsVirtualizedPath(basePath))
            {
                _customGroupsBasePath = basePath;
            }
        }

        /// <summary>
        /// 경로가 MSIX VFS 가상화 대상인지 확인합니다.
        /// %LocalAppData%, %AppData% 하위 경로는 MSIX에서 가상화됩니다.
        /// </summary>
        private static bool IsVirtualizedPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            return path.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(appData, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 주어진 경로가 MSIX VFS 가상화 대상인지 외부에서 확인할 수 있습니다.
        /// </summary>
        public static bool IsPathVirtualized(string path) => IsVirtualizedPath(path);

        /// <summary>
        /// 마지막으로 편집한 그룹 ID를 파일에 저장합니다.
        /// </summary>
        /// <param name="groupId">저장할 그룹 ID</param>
        public static void SaveGroupIdToFile(string groupId)
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(LastEditFile, groupId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save group ID: {ex.Message}");
            }
        }

        /// <summary>
        /// 마지막으로 열린 그룹 이름을 파일에 저장합니다.
        /// </summary>
        /// <param name="groupName">저장할 그룹 이름</param>
        public static void SaveGroupNameToFile(string groupName)
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(LastOpenFile, groupName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save group name: {ex.Message}");
            }
        }

        /// <summary>
        /// 마지막으로 편집한 그룹 ID를 파일에서 읽어옵니다.
        /// </summary>
        /// <returns>그룹 ID, 파일이 없거나 오류 시 -1 반환</returns>
        public static int ReadGroupIdFromFile()
        {
            try
            {
                if (File.Exists(LastEditFile))
                {
                    string content = File.ReadAllText(LastEditFile).Trim();
                    if (int.TryParse(content, out int groupId))
                    {
                        return groupId;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read group ID: {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// 마지막으로 열린 그룹 이름을 파일에서 읽어옵니다.
        /// </summary>
        /// <returns>그룹 이름, 파일이 없거나 오류 시 빈 문자열 반환</returns>
        public static string ReadGroupNameFromFile()
        {
            try
            {
                if (File.Exists(LastOpenFile))
                {
                    return File.ReadAllText(LastOpenFile).Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read group name: {ex.Message}");
            }
            return string.Empty;
        }

        /// <summary>
        /// 실행 시 캡처한 커서 위치 파일 경로
        /// </summary>
        private static string CursorPosFile => Path.Combine(AppDataFolder, "launch_cursor");

        /// <summary>
        /// 프로세스 시작 시 캡처한 커서 위치를 파일에 저장합니다.
        /// </summary>
        public static void SaveCursorPosition(int x, int y)
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(CursorPosFile, $"{x},{y}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save cursor position: {ex.Message}");
            }
        }

        /// <summary>
        /// 저장된 커서 위치를 파일에서 읽어옵니다.
        /// </summary>
        /// <returns>커서 위치 (x, y), 파일이 없거나 오류 시 null 반환</returns>
        public static (int X, int Y)? ReadCursorPosition()
        {
            try
            {
                if (File.Exists(CursorPosFile))
                {
                    string content = File.ReadAllText(CursorPosFile).Trim();
                    string[] parts = content.Split(',');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int x) &&
                        int.TryParse(parts[1], out int y))
                    {
                        return (x, y);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read cursor position: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// AppData 폴더 및 하위 폴더(Icons, Groups)가 존재하는지 확인하고 없으면 생성합니다.
        /// </summary>
        public static void EnsureAppDataFolderExists()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                Directory.CreateDirectory(IconsFolder);
                Directory.CreateDirectory(GroupsFolder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create AppData folder: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 Groups 기본 경로 표시용 문자열을 반환합니다.
        /// </summary>
        public static string GetGroupsBasePathDisplay()
        {
            return _customGroupsBasePath ?? DefaultGroupsBasePath;
        }

        /// <summary>
        /// Groups 데이터 마이그레이션 완료 표시 파일 경로
        /// </summary>
        private static string GroupsMigratedMarkerFile => Path.Combine(AppDataFolder, ".groups_migrated");

        /// <summary>
        /// 기존 패키지 가상화 폴더 마이그레이션 완료 표시 파일 경로
        /// </summary>
        private static string MigratedMarkerFile => Path.Combine(AppDataFolder, ".migrated");

        /// <summary>
        /// Groups 데이터를 비가상화 경로로 마이그레이션합니다.
        /// 기존 %LocalAppData%\AppGroup\Groups\ 또는 패키지 가상화 폴더에서 새 경로로 이동합니다.
        /// </summary>
        public static void MigrateGroupsDataIfNeeded()
        {
            try
            {
                // AppData 폴더 존재 보장
                Directory.CreateDirectory(AppDataFolder);
                Directory.CreateDirectory(IconsFolder);

                // 1단계: 기존 패키지 가상화 폴더에서 AppData로 설정 파일 마이그레이션
                MigrateFromPackageFolderIfNeeded();

                // 2단계: %LocalAppData%\AppGroup\Groups\ → 새 GroupsFolder 경로로 이동
                MigrateGroupsToNewPath();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Groups 마이그레이션 실패 (앱은 정상 시작됩니다): {ex.Message}");
            }
        }

        /// <summary>
        /// MSIX 패키지 가상화 폴더에서 AppData 경로로 설정 파일을 마이그레이션합니다.
        /// </summary>
        private static void MigrateFromPackageFolderIfNeeded()
        {
            try
            {
                // 이미 마이그레이션 완료된 경우 스킵
                if (File.Exists(MigratedMarkerFile))
                    return;

                // 패키지 가상화 경로 구성
                string packageFamilyName = GetPackageFamilyName();
                if (string.IsNullOrEmpty(packageFamilyName))
                    return;

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string packageAppDataFolder = Path.Combine(
                    localAppData, "Packages", packageFamilyName, "LocalCache", "Local", "AppGroup");

                // 패키지 폴더가 없거나 appgroups.json이 없으면 마이그레이션 불필요
                if (!Directory.Exists(packageAppDataFolder) ||
                    !File.Exists(Path.Combine(packageAppDataFolder, "appgroups.json")))
                {
                    File.WriteAllText(MigratedMarkerFile, DateTime.Now.ToString("o"));
                    return;
                }

                // 실제 경로에 이미 appgroups.json이 있으면 충돌 방지를 위해 스킵
                if (File.Exists(Path.Combine(AppDataFolder, "appgroups.json")))
                {
                    Debug.WriteLine("마이그레이션 스킵: 실제 경로에 이미 appgroups.json이 존재합니다.");
                    File.WriteAllText(MigratedMarkerFile, DateTime.Now.ToString("o"));
                    return;
                }

                // 설정 파일 마이그레이션
                string[] filesToMigrate = ["appgroups.json", "settings.json", "startmenu.json", "lastEdit", "lastOpen"];
                foreach (string fileName in filesToMigrate)
                {
                    string srcFile = Path.Combine(packageAppDataFolder, fileName);
                    if (File.Exists(srcFile))
                    {
                        string destFile = Path.Combine(AppDataFolder, fileName);
                        File.Copy(srcFile, destFile, overwrite: false);
                        Debug.WriteLine($"패키지 마이그레이션 완료: {fileName}");
                    }
                }

                // Icons 폴더 마이그레이션
                string srcIconsDir = Path.Combine(packageAppDataFolder, "Icons");
                if (Directory.Exists(srcIconsDir))
                {
                    CopyDirectoryRecursive(srcIconsDir, IconsFolder);
                    Debug.WriteLine("패키지 마이그레이션 완료: Icons/");
                }

                // Groups 폴더는 새 GroupsFolder 경로로 마이그레이션 (MigrateGroupsToNewPath에서 처리)
                string srcGroupsDir = Path.Combine(packageAppDataFolder, "Groups");
                if (Directory.Exists(srcGroupsDir))
                {
                    // 임시로 AppData/Groups에 복사 (MigrateGroupsToNewPath에서 최종 이동)
                    string tempGroupsDir = Path.Combine(AppDataFolder, "Groups");
                    CopyDirectoryRecursive(srcGroupsDir, tempGroupsDir);
                    Debug.WriteLine("패키지 마이그레이션 완료: Groups/ (임시)");
                }

                File.WriteAllText(MigratedMarkerFile, DateTime.Now.ToString("o"));
                Debug.WriteLine("MSIX 패키지 폴더 마이그레이션이 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"패키지 마이그레이션 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 기존 %LocalAppData%\AppGroup\Groups\를 새 GroupsFolder 경로로 마이그레이션합니다.
        /// appgroups.json의 groupIcon 절대 경로도 함께 갱신합니다.
        /// </summary>
        private static void MigrateGroupsToNewPath()
        {
            try
            {
                // 이미 마이그레이션 완료된 경우 스킵
                if (File.Exists(GroupsMigratedMarkerFile))
                    return;

                string oldGroupsFolder = Path.Combine(AppDataFolder, "Groups");

                // 기존 Groups 폴더가 없거나 새 경로와 동일하면 스킵
                if (!Directory.Exists(oldGroupsFolder) ||
                    string.Equals(oldGroupsFolder, GroupsFolder, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(GroupsFolder);
                    File.WriteAllText(GroupsMigratedMarkerFile, DateTime.Now.ToString("o"));
                    return;
                }

                // 새 GroupsFolder 생성
                Directory.CreateDirectory(GroupsFolder);

                // 기존 Groups 폴더 내용을 새 경로로 복사
                CopyDirectoryRecursive(oldGroupsFolder, GroupsFolder);
                Debug.WriteLine($"Groups 마이그레이션: {oldGroupsFolder} → {GroupsFolder}");

                // appgroups.json의 groupIcon 경로 갱신
                UpdateGroupIconPaths(oldGroupsFolder, GroupsFolder);

                // 기존 Groups 폴더 삭제
                try
                {
                    Directory.Delete(oldGroupsFolder, true);
                    Debug.WriteLine("기존 Groups 폴더 삭제 완료");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"기존 Groups 폴더 삭제 실패 (무시): {ex.Message}");
                }

                File.WriteAllText(GroupsMigratedMarkerFile, DateTime.Now.ToString("o"));
                Debug.WriteLine("Groups 경로 마이그레이션이 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Groups 경로 마이그레이션 실패: {ex.Message}");
                // 실패해도 새 GroupsFolder는 보장
                try { Directory.CreateDirectory(GroupsFolder); }
                catch { }
            }
        }

        /// <summary>
        /// appgroups.json의 groupIcon 절대 경로를 새 GroupsFolder 기준으로 갱신합니다.
        /// </summary>
        public static void UpdateGroupIconPaths(string oldGroupsFolder, string newGroupsFolder)
        {
            try
            {
                string configPath = ConfigFile;
                if (!File.Exists(configPath))
                    return;

                string jsonContent = File.ReadAllText(configPath);
                var jsonNode = JsonNode.Parse(jsonContent);
                if (jsonNode == null)
                    return;

                bool changed = false;
                foreach (var property in jsonNode.AsObject())
                {
                    var groupNode = property.Value;
                    if (groupNode == null) continue;

                    string groupIcon = groupNode["groupIcon"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(groupIcon) &&
                        groupIcon.StartsWith(oldGroupsFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = groupIcon.Substring(oldGroupsFolder.Length).TrimStart('\\', '/');
                        string newIconPath = Path.Combine(newGroupsFolder, relativePath);
                        groupNode["groupIcon"] = newIconPath;
                        changed = true;
                        Debug.WriteLine($"아이콘 경로 갱신: {groupIcon} → {newIconPath}");
                    }
                }

                if (changed)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(configPath, jsonNode.ToJsonString(options));
                    Debug.WriteLine("appgroups.json 아이콘 경로 갱신 완료");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"아이콘 경로 갱신 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 MSIX 패키지의 FamilyName을 반환합니다.
        /// 패키지되지 않은 환경에서는 빈 문자열을 반환합니다.
        /// </summary>
        private static string GetPackageFamilyName()
        {
            try
            {
                return Windows.ApplicationModel.Package.Current.Id.FamilyName;
            }
            catch
            {
                // 패키지되지 않은 환경 (디버그 등)
                return string.Empty;
            }
        }

        /// <summary>
        /// 디렉터리를 재귀적으로 복사합니다. 이미 존재하는 파일은 건너뜁니다.
        /// </summary>
        internal static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile);
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }
    }
}
