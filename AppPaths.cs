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
    }
}
