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
        /// AppData 폴더가 존재하는지 확인하고 없으면 생성합니다.
        /// </summary>
        public static void EnsureAppDataFolderExists()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create AppData folder: {ex.Message}");
            }
        }
    }
}
