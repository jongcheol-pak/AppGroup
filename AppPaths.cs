using System;
using System.Diagnostics;
using System.IO;

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
        /// MSIX 패키지 가상화를 우회하여 실제 경로를 사용합니다.
        /// </summary>
        public static string AppDataFolder => Path.Combine(
            Environment.GetEnvironmentVariable("LOCALAPPDATA")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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
        /// 그룹 폴더 경로 (Groups 폴더)
        /// </summary>
        public static string GroupsFolder => Path.Combine(AppDataFolder, "Groups");

        /// <summary>
        /// 아이콘 폴더 경로 (Icons 폴더)
        /// </summary>
        public static string IconsFolder => Path.Combine(AppDataFolder, "Icons");

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
        /// 마이그레이션 완료 표시 파일 경로
        /// </summary>
        private static string MigratedMarkerFile => Path.Combine(AppDataFolder, ".migrated");

        /// <summary>
        /// MSIX 패키지 가상화 폴더에서 실제 경로로 데이터를 마이그레이션합니다.
        /// VFS 비활성화 후 기존 패키지 폴더에 남아있는 데이터를 일회성으로 이동합니다.
        /// </summary>
        public static void MigrateFromPackageFolderIfNeeded()
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

                string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA")
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string packageAppDataFolder = Path.Combine(
                    localAppData, "Packages", packageFamilyName, "LocalCache", "Local", "AppGroup");

                // 패키지 폴더가 없거나 appgroups.json이 없으면 마이그레이션 불필요
                if (!Directory.Exists(packageAppDataFolder) ||
                    !File.Exists(Path.Combine(packageAppDataFolder, "appgroups.json")))
                {
                    // 마이그레이션 대상 없음 - 완료 표시 후 종료
                    EnsureAppDataFolderExists();
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

                // 실제 경로 폴더 생성
                EnsureAppDataFolderExists();

                // 파일 마이그레이션
                string[] filesToMigrate = ["appgroups.json", "settings.json", "startmenu.json", "lastEdit", "lastOpen"];
                foreach (string fileName in filesToMigrate)
                {
                    string srcFile = Path.Combine(packageAppDataFolder, fileName);
                    if (File.Exists(srcFile))
                    {
                        string destFile = Path.Combine(AppDataFolder, fileName);
                        File.Copy(srcFile, destFile, overwrite: false);
                        Debug.WriteLine($"마이그레이션 완료: {fileName}");
                    }
                }

                // 폴더 마이그레이션
                string[] foldersToMigrate = ["Groups", "Icons"];
                foreach (string folderName in foldersToMigrate)
                {
                    string srcDir = Path.Combine(packageAppDataFolder, folderName);
                    if (Directory.Exists(srcDir))
                    {
                        string destDir = Path.Combine(AppDataFolder, folderName);
                        CopyDirectoryRecursive(srcDir, destDir);
                        Debug.WriteLine($"마이그레이션 완료: {folderName}/");
                    }
                }

                // 마이그레이션 완료 표시
                File.WriteAllText(MigratedMarkerFile, DateTime.Now.ToString("o"));
                Debug.WriteLine("MSIX 패키지 폴더 마이그레이션이 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"마이그레이션 실패 (앱은 정상 시작됩니다): {ex.Message}");
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
        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
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
