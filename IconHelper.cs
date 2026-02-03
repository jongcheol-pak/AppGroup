using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using AppGroup.Models;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace AppGroup
{
    public class IconHelper
    {
        public static async Task<string> GetUrlFileIconAsync(string filePath)
        {
            try
            {
                // .url 파일의 모든 줄 읽기
                var lines = await File.ReadAllLinesAsync(filePath);

                // IconFile 줄 찾기
                var iconLine = lines.FirstOrDefault(l => l.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(iconLine))
                {
                    // "IconFile=" 이후의 경로 추출
                    var iconPath = iconLine.Substring("IconFile=".Length).Trim();

                    // 아이콘 파일이 존재하는지 확인
                    if (File.Exists(iconPath))
                    {
                        // 추출된 경로로 기존 아이콘 캐시 사용
                        return await IconCache.GetIconPathAsync(iconPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading .url file: {ex.Message}");
            }

            // 기본 아이콘으로 대체
            return "ms-appx:///Assets/default-icon.png";
        }

        public static string FindOrigIcon(string icoFilePath)
        {
            if (string.IsNullOrEmpty(icoFilePath))
            {
                return icoFilePath;
            }

            string[] possibleExtensions = { ".png", ".jpg", ".jpeg" };
            string directory = Path.GetDirectoryName(icoFilePath);
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(icoFilePath);

            foreach (string ext in possibleExtensions)
            {
                string potentialPath = Path.Combine(directory, filenameWithoutExtension + ext);
                if (File.Exists(potentialPath))
                {
                    return potentialPath;
                }
            }

            return icoFilePath;
        }

        /// <summary>
        /// 아이콘 파일의 배경 버전을 제거합니다 (PNG 및 ICO 모두).
        /// </summary>
        /// <param name="iconWithBackgroundPath">배경이 있는 아이콘 경로</param>
        public static void RemoveBackgroundIcon(string iconWithBackgroundPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(iconWithBackgroundPath) && iconWithBackgroundPath.Contains("_bg"))
                {
                    // ICO 파일 제거
                    if (File.Exists(iconWithBackgroundPath))
                    {
                        File.Delete(iconWithBackgroundPath);
                    }

                    // PNG 파일도 존재하면 제거
                    string pngPath = Path.ChangeExtension(iconWithBackgroundPath, ".png");
                    if (File.Exists(pngPath))
                    {
                        File.Delete(pngPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing background icon: {ex.Message}");
            }
        }

        // PNG 아이콘을 흑백(그레이스케일)으로 변환
        public static async Task<string> CreateBlackWhiteIconAsync(string originalIconPath)
        {
            try
            {
                if (string.IsNullOrEmpty(originalIconPath) || !File.Exists(originalIconPath))
                {
                    Console.WriteLine("Invalid path or file doesn't exist");
                    return originalIconPath;
                }

                string directory = Path.GetDirectoryName(originalIconPath);
                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(originalIconPath);

                Console.WriteLine($"Converting PNG to B&W: {originalIconPath}");

                string pngPath;
                // using 블록으로 Bitmap 리소스 해제 보장
                using (var originalBitmap = new System.Drawing.Bitmap(originalIconPath))
                {
                    Console.WriteLine($"PNG loaded: {originalBitmap.Width}x{originalBitmap.Height}");

                    using (var bwBitmap = new System.Drawing.Bitmap(originalBitmap.Width, originalBitmap.Height))
                    {
                        // 픽셀 단위로 흑백 변환
                        for (int x = 0; x < originalBitmap.Width; x++)
                        {
                            for (int y = 0; y < originalBitmap.Height; y++)
                            {
                                System.Drawing.Color originalColor = originalBitmap.GetPixel(x, y);

                                // 휘도 공식을 사용하여 그레이스케일로 변환
                                int grayValue = (int)(originalColor.R * 0.299 + originalColor.G * 0.587 + originalColor.B * 0.114);

                                // 투명도를 위해 원래의 알파 채널 유지
                                System.Drawing.Color grayColor = System.Drawing.Color.FromArgb(originalColor.A, grayValue, grayValue, grayValue);
                                bwBitmap.SetPixel(x, y, grayColor);
                            }
                        }

                        // PNG로 저장
                        pngPath = Path.Combine(directory, $"{filenameWithoutExtension}_bw.png");
                        bwBitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine($"B&W PNG saved to: {pngPath}");
                    }
                }

                // 기존 메서드를 사용하여 PNG를 ICO로 변환
                string icoPath = Path.Combine(directory, $"{filenameWithoutExtension}_bw.ico");
                bool iconSuccess = await ConvertToIco(pngPath, icoPath);

                if (iconSuccess)
                {
                    Console.WriteLine($"B&W ICO created successfully: {icoPath}");
                    return icoPath;
                }
                else
                {
                    Console.WriteLine("Failed to convert PNG to ICO, returning PNG path");
                    return pngPath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating B&W icon: {ex.Message}");
                return originalIconPath;
            }
        }

        private static async Task<Bitmap> ExtractWindowsAppIconAsync(string shortcutPath, string outputDirectory)
        {
            dynamic shell = null;
            dynamic folder = null;
            dynamic shortcutItem = null;
            try
            {
                // Shell COM 객체를 사용하여 바로가기 대상 가져오기
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return null;

                shell = Activator.CreateInstance(shellType);
                folder = shell.Namespace(Path.GetDirectoryName(shortcutPath));
                shortcutItem = folder?.ParseName(Path.GetFileName(shortcutPath));

                if (folder == null || shortcutItem == null) return null;

                // "Link target" 속성 찾기
                string linkTarget = null;
                for (int i = 0; i < 500; i++)
                {
                    string propertyName = folder.GetDetailsOf(null, i);
                    if (propertyName == "Link target")
                    {
                        linkTarget = folder.GetDetailsOf(shortcutItem, i);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(linkTarget)) return null;

                // 링크 대상에서 앱 이름 추출 (첫 번째 "_" 이후의 모든 문자열 제거)
                string appName = System.Text.RegularExpressions.Regex.Replace(linkTarget, "_.*$", "");
                if (string.IsNullOrEmpty(appName)) return null;

                // Windows Runtime API를 사용하여 패키지 찾기
                Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
                IEnumerable<Windows.ApplicationModel.Package> packages = packageManager.FindPackagesForUser("");

                // 앱 이름과 일치하는 패키지 찾기
                Windows.ApplicationModel.Package appPackage = packages.FirstOrDefault(p => p.Id.Name.StartsWith(appName, StringComparison.OrdinalIgnoreCase));
                if (appPackage == null) return null;

                string installPath = appPackage.InstalledLocation.Path;
                string manifestPath = Path.Combine(installPath, "AppxManifest.xml");

                if (!File.Exists(manifestPath)) return null;

                // 매니페스트 XML 로드 및 파싱
                XmlDocument manifest = new XmlDocument();
                manifest.Load(manifestPath);

                // 네임스페이스 관리자 생성
                XmlNamespaceManager nsManager = new XmlNamespaceManager(manifest.NameTable);
                nsManager.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                // 매니페스트에서 로고 경로 가져오기
                XmlNode logoNode = manifest.SelectSingleNode("/ns:Package/ns:Properties/ns:Logo", nsManager);
                if (logoNode == null) return null;

                string logoPath = logoNode.InnerText;
                string logoDir = Path.Combine(installPath, Path.GetDirectoryName(logoPath));

                if (!Directory.Exists(logoDir)) return null;

                string[] logoPatterns = new[] {

    "*StoreLogo*.png",

        };

                string highestResLogoPath = null;
                long highestSize = 0;

                foreach (string pattern in logoPatterns)
                {
                    foreach (string file in Directory.GetFiles(logoDir, pattern, SearchOption.AllDirectories))
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (fileInfo.Length > highestSize)
                        {
                            highestSize = fileInfo.Length;
                            highestResLogoPath = file;
                        }
                    }

                    if (highestResLogoPath != null) break;
                }

                if (string.IsNullOrEmpty(highestResLogoPath) || !File.Exists(highestResLogoPath)) return null;

                // 원본 크기 그대로 로드하여 반환 (리사이즈 제거)
                using (FileStream stream = new FileStream(highestResLogoPath, FileMode.Open, FileAccess.Read))
                {
                    using (var originalBitmap = new Bitmap(stream))
                    {
                        // 원본 비트맵 복사하여 반환
                        var result = new Bitmap(originalBitmap);
                        Debug.WriteLine($"ExtractWindowsAppIconAsync: Loaded logo {result.Width}x{result.Height} from {highestResLogoPath}");
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting Windows app icon: {ex.Message}");
                return null;
            }
            finally
            {
                // COM 객체 명시적 해제
                if (shortcutItem != null) Marshal.ReleaseComObject(shortcutItem);
                if (folder != null) Marshal.ReleaseComObject(folder);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>
        /// shell:AppsFolder 경로 형식을 사용하여 UWP 앱에서 아이콘을 추출합니다.
        /// </summary>
        private static async Task<Bitmap> ExtractUwpAppIconAsync(string shellPath, string outputDirectory)
        {
            try
            {
                // shell:AppsFolder 경로에서 AppUserModelId 추출
                // 형식: shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App
                string appUserModelId = shellPath.Replace("shell:AppsFolder\\", "", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(appUserModelId)) return null;

                Debug.WriteLine($"ExtractUwpAppIconAsync: Processing AUMID: {appUserModelId}");

                // 패키지 패밀리 이름 추출 (! 이전의 모든 것)
                string packageFamilyName = appUserModelId.Contains("!")
                    ? appUserModelId.Substring(0, appUserModelId.IndexOf("!"))
                    : appUserModelId;

                // 패키지 이름 추출 (_ 이전의 모든 것)
                string packageName = packageFamilyName.Contains("_")
                    ? packageFamilyName.Substring(0, packageFamilyName.IndexOf("_"))
                    : packageFamilyName;

                if (string.IsNullOrEmpty(packageName)) return null;

                // Windows Runtime API를 사용하여 패키지 찾기
                Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
                IEnumerable<Windows.ApplicationModel.Package> packages = packageManager.FindPackagesForUser("");

                // 일치하는 패키지 찾기 - 여러 매칭 전략 시도
                Windows.ApplicationModel.Package appPackage = packages.FirstOrDefault(p =>
                    p.Id.FamilyName.Equals(packageFamilyName, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.Name.StartsWith(packageName, StringComparison.OrdinalIgnoreCase) ||
                    packageName.StartsWith(p.Id.Name, StringComparison.OrdinalIgnoreCase));

                if (appPackage == null)
                {
                    Debug.WriteLine($"ExtractUwpAppIconAsync: No package found for {packageName}, trying shell item extraction");
                    // 폴백: Shell API를 통한 아이콘 추출 시도
                    return ExtractIconFromShellItem(shellPath);
                }

                Debug.WriteLine($"ExtractUwpAppIconAsync: Found package: {appPackage.Id.FamilyName}");

                string installPath = appPackage.InstalledLocation.Path;
                string manifestPath = Path.Combine(installPath, "AppxManifest.xml");

                if (!File.Exists(manifestPath)) return null;

                // 매니페스트 XML 로드 및 파싱
                XmlDocument manifest = new XmlDocument();
                manifest.Load(manifestPath);

                // 다양한 매니페스트 버전을 위한 네임스페이스 관리자 생성
                XmlNamespaceManager nsManager = new XmlNamespaceManager(manifest.NameTable);
                nsManager.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
                nsManager.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

                // 로고 검색 순서: Square44x44Logo > Square150x150Logo > Logo (StoreLogo)
                string[] logoXPaths = new[] {
                    "/ns:Package/ns:Applications/ns:Application/uap:VisualElements/@Square44x44Logo",
                    "/ns:Package/ns:Applications/ns:Application/uap:VisualElements/@Square150x150Logo",
                    "/ns:Package/ns:Properties/ns:Logo"
                };

                string highestResLogoPath = null;

                foreach (string xpath in logoXPaths)
                {
                    XmlNode logoNode = manifest.SelectSingleNode(xpath, nsManager);
                    if (logoNode == null) continue;

                    string logoRelativePath = logoNode.InnerText ?? logoNode.Value;
                    if (string.IsNullOrEmpty(logoRelativePath)) continue;

                    // 로고 파일 검색 (다양한 해상도 버전 포함)
                    string logoBaseName = Path.GetFileNameWithoutExtension(logoRelativePath);
                    string logoDir = Path.Combine(installPath, Path.GetDirectoryName(logoRelativePath) ?? "");

                    if (!Directory.Exists(logoDir))
                    {
                        // Assets 폴더에서 검색 시도
                        logoDir = Path.Combine(installPath, "Assets");
                        if (!Directory.Exists(logoDir)) continue;
                    }

                    // 가장 큰 해상도의 로고 파일 찾기
                    long highestSize = 0;
                    string[] searchPatterns = new[] {
                        $"{logoBaseName}*.png",
                        $"*{logoBaseName}*.png",
                        "*.png"
                    };

                    foreach (string pattern in searchPatterns)
                    {
                        try
                        {
                            foreach (string file in Directory.GetFiles(logoDir, pattern, SearchOption.AllDirectories))
                            {
                                // contrast-black/white, targetsize 등 특수 버전 제외
                                string fileName = Path.GetFileName(file).ToLower();
                                if (fileName.Contains("contrast-") || fileName.Contains("_contrast")) continue;

                                FileInfo fileInfo = new FileInfo(file);
                                if (fileInfo.Length > highestSize)
                                {
                                    highestSize = fileInfo.Length;
                                    highestResLogoPath = file;
                                }
                            }
                        }
                        catch { }

                        if (highestResLogoPath != null) break;
                    }

                    if (highestResLogoPath != null) break;
                }

                if (!string.IsNullOrEmpty(highestResLogoPath) && File.Exists(highestResLogoPath))
                {
                    // 원본 크기 그대로 로드하여 반환 (리사이즈 제거)
                    using (FileStream stream = new FileStream(highestResLogoPath, FileMode.Open, FileAccess.Read))
                    {
                        using (var originalBitmap = new Bitmap(stream))
                        {
                            var result = new Bitmap(originalBitmap);
                            Debug.WriteLine($"ExtractUwpAppIconAsync: Loaded logo {result.Width}x{result.Height} from {highestResLogoPath}");
                            return result;
                        }
                    }
                }

                // 폴백: Shell API를 통한 아이콘 추출 시도
                return ExtractIconFromShellItem(shellPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting UWP app icon: {ex.Message}");
                // 최종 폴백: Shell API 시도
                try
                {
                    return ExtractIconFromShellItem(shellPath);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Shell API를 사용하여 shell:AppsFolder 항목의 아이콘 추출
        /// </summary>
        // AppsFolder CLSID
        private static readonly string AppsFolderClsid = "{1e87508d-89c2-42f0-8a7e-645a0f50ca58}";

        private static Bitmap ExtractIconFromShellItem(string shellPath)
        {
            Debug.WriteLine($"ExtractIconFromShellItem: Trying to extract icon from: {shellPath}");

            // AUMID 추출
            string aumid = shellPath.Replace("shell:AppsFolder\\", "", StringComparison.OrdinalIgnoreCase);

            // 여러 경로 형식 시도
            string[] pathFormats = new[] {
                shellPath,  // 원본 (shell:AppsFolder\{AUMID})
                $"shell:::{AppsFolderClsid}\\{aumid}",  // shell:::{CLSID}\{AUMID}
                $"::{AppsFolderClsid}\\{aumid}",  // ::{CLSID}\{AUMID}
            };

            foreach (var path in pathFormats)
            {
                Debug.WriteLine($"ExtractIconFromShellItem: Trying path format: {path}");
                var result = TryExtractIconFromShellPath(path);
                if (result != null)
                {
                    Debug.WriteLine($"ExtractIconFromShellItem: Success with path: {path}");
                    return result;
                }
            }

            // IShellFolder를 통한 추출 시도
            var shellFolderResult = TryExtractIconViaShellFolder(aumid);
            if (shellFolderResult != null)
            {
                Debug.WriteLine($"ExtractIconFromShellItem: Success via IShellFolder for: {aumid}");
                return shellFolderResult;
            }

            Debug.WriteLine($"ExtractIconFromShellItem: All methods failed for: {shellPath}");
            return null;
        }

        /// <summary>
        /// IShellFolder를 통해 AppsFolder 항목의 아이콘 추출
        /// </summary>
        private static Bitmap TryExtractIconViaShellFolder(string aumid)
        {
            try
            {
                // AppsFolder의 PIDL 가져오기
                IntPtr appsFolderPidl = IntPtr.Zero;
                int hr = NativeMethods.SHGetKnownFolderIDList(
                    NativeMethods.FOLDERID_AppsFolder,
                    0,
                    IntPtr.Zero,
                    out appsFolderPidl);

                if (hr != 0 || appsFolderPidl == IntPtr.Zero)
                {
                    Debug.WriteLine($"TryExtractIconViaShellFolder: SHGetKnownFolderIDList failed: 0x{hr:X8}");
                    return null;
                }

                try
                {
                    // IShellFolder 인터페이스 가져오기
                    object shellFolderObj;
                    Guid shellFolderGuid = typeof(NativeMethods.IShellFolder).GUID;
                    hr = NativeMethods.SHBindToObject(
                        IntPtr.Zero,
                        appsFolderPidl,
                        IntPtr.Zero,
                        ref shellFolderGuid,
                        out shellFolderObj);

                    if (hr != 0 || shellFolderObj == null)
                    {
                        Debug.WriteLine($"TryExtractIconViaShellFolder: SHBindToObject failed: 0x{hr:X8}");
                        return null;
                    }

                    NativeMethods.IShellFolder shellFolder = (NativeMethods.IShellFolder)shellFolderObj;

                    // AUMID로 PIDL 파싱
                    IntPtr itemPidl = IntPtr.Zero;
                    uint eaten = 0;
                    uint attributes = 0;
                    hr = shellFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, aumid, out eaten, out itemPidl, ref attributes);

                    if (hr != 0 || itemPidl == IntPtr.Zero)
                    {
                        Debug.WriteLine($"TryExtractIconViaShellFolder: ParseDisplayName failed for {aumid}: 0x{hr:X8}");
                        return null;
                    }

                    try
                    {
                        // 아이콘 추출
                        return ExtractIconFromPidl(shellFolder, itemPidl);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(itemPidl);
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(appsFolderPidl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryExtractIconViaShellFolder failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// PIDL에서 아이콘 추출
        /// </summary>
        private static Bitmap ExtractIconFromPidl(NativeMethods.IShellFolder folder, IntPtr pidl)
        {
            try
            {
                // IExtractIcon 인터페이스 가져오기
                Guid extractIconGuid = typeof(NativeMethods.IExtractIconW).GUID;
                object extractIconObj;
                int hr = folder.GetUIObjectOf(IntPtr.Zero, 1, new[] { pidl }, ref extractIconGuid, IntPtr.Zero, out extractIconObj);

                if (hr == 0 && extractIconObj != null)
                {
                    NativeMethods.IExtractIconW extractIcon = (NativeMethods.IExtractIconW)extractIconObj;

                    StringBuilder iconFile = new StringBuilder(260);
                    int iconIndex = 0;
                    uint flags = 0;

                    hr = extractIcon.GetIconLocation(0, iconFile, 260, out iconIndex, out flags);
                    if (hr == 0)
                    {
                        IntPtr largeIcon = IntPtr.Zero;
                        IntPtr smallIcon = IntPtr.Zero;
                        hr = extractIcon.Extract(iconFile.ToString(), (uint)iconIndex, out largeIcon, out smallIcon, (256 << 16) | 16);

                        if (hr == 0 && largeIcon != IntPtr.Zero)
                        {
                            try
                            {
                                using (var icon = Icon.FromHandle(largeIcon))
                                {
                                    return new Bitmap(icon.ToBitmap());
                                }
                            }
                            finally
                            {
                                NativeMethods.DestroyIcon(largeIcon);
                                if (smallIcon != IntPtr.Zero) NativeMethods.DestroyIcon(smallIcon);
                            }
                        }
                    }
                }

                // 폴백: IShellItemImageFactory 시도
                IntPtr absolutePidl = NativeMethods.ILCombine(IntPtr.Zero, pidl);
                if (absolutePidl != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.IShellItemImageFactory imageFactory;
                        Guid imageFactoryGuid = NativeMethods.IShellItemImageFactoryGuid;
                        hr = NativeMethods.SHCreateItemFromIDList(absolutePidl, ref imageFactoryGuid, out imageFactory);

                        if (hr == 0 && imageFactory != null)
                        {
                            IntPtr hBitmap;
                            NativeMethods.SIZE size = new NativeMethods.SIZE(256, 256);
                            hr = imageFactory.GetImage(size, NativeMethods.SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);

                            if (hr == 0 && hBitmap != IntPtr.Zero)
                            {
                                try
                                {
                                    // 알파 채널을 보존하면서 변환
                                    return ConvertHBitmapToArgbBitmap(hBitmap);
                                }
                                finally
                                {
                                    NativeMethods.DeleteObject(hBitmap);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(absolutePidl);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractIconFromPidl failed: {ex.Message}");
                return null;
            }
        }

        private static Bitmap TryExtractIconFromShellPath(string shellPath)
        {
            try
            {
                // IShellItemImageFactory를 직접 요청
                NativeMethods.IShellItemImageFactory imageFactory;
                Guid imageFactoryGuid = NativeMethods.IShellItemImageFactoryGuid;
                int hr = NativeMethods.SHCreateItemFromParsingName(shellPath, IntPtr.Zero, ref imageFactoryGuid, out imageFactory);

                if (hr != 0 || imageFactory == null)
                {
                    Debug.WriteLine($"SHCreateItemFromParsingName failed for {shellPath}: HRESULT 0x{hr:X8}");
                    return null;
                }

                // 256x256 크기의 아이콘 요청 (가능한 큰 아이콘 추출)
                IntPtr hBitmap;
                NativeMethods.SIZE size = new NativeMethods.SIZE(256, 256);
                hr = imageFactory.GetImage(size, NativeMethods.SIIGBF.SIIGBF_BIGGERSIZEOK | NativeMethods.SIIGBF.SIIGBF_ICONONLY, out hBitmap);

                if (hr != 0 || hBitmap == IntPtr.Zero)
                {
                    Debug.WriteLine($"GetImage failed for {shellPath}: HRESULT 0x{hr:X8}");
                    return null;
                }

                try
                {
                    // HBITMAP을 알파 채널 보존하면서 Bitmap으로 변환
                    using (Bitmap rawBitmap = ConvertHBitmapToArgbBitmap(hBitmap))
                    {
                        // 실제 아이콘 영역만 크롭
                        Bitmap bitmap = CropToActualContent(rawBitmap);
                        Debug.WriteLine($"TryExtractIconFromShellPath: {rawBitmap.Width}x{rawBitmap.Height} -> {bitmap.Width}x{bitmap.Height} for {shellPath}");
                        return bitmap;
                    }
                }
                finally
                {
                    NativeMethods.DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryExtractIconFromShellPath failed for {shellPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// SHGetFileInfo를 사용하여 파일의 아이콘 추출
        /// </summary>
        private static Bitmap ExtractIconUsingSHGetFileInfo(string filePath)
        {
            try
            {
                NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
                uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;

                IntPtr result = NativeMethods.SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                {
                    Debug.WriteLine($"SHGetFileInfo returned no icon for {filePath}");
                    return null;
                }

                try
                {
                    using (var icon = Icon.FromHandle(shfi.hIcon))
                    {
                        Bitmap bitmap = new Bitmap(icon.ToBitmap());
                        Debug.WriteLine($"Successfully extracted icon using SHGetFileInfo: {filePath}");
                        return bitmap;
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(shfi.hIcon);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractIconUsingSHGetFileInfo failed for {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// HBITMAP을 알파 채널을 보존하면서 Bitmap으로 변환
        /// Bitmap.FromHbitmap()은 알파 채널을 제대로 처리하지 못하므로 수동으로 변환
        /// Shell API(IShellItemImageFactory)에서 반환하는 HBITMAP은 top-down 순서로 저장됨
        /// </summary>
        private static Bitmap ConvertHBitmapToArgbBitmap(IntPtr hBitmap)
        {
            try
            {
                // BITMAP 정보 가져오기
                NativeMethods.BITMAP bmp = new NativeMethods.BITMAP();
                NativeMethods.GetObject(hBitmap, Marshal.SizeOf(typeof(NativeMethods.BITMAP)), ref bmp);

                // 32비트 비트맵인지 확인
                if (bmp.bmBitsPixel != 32)
                {
                    // 32비트가 아니면 기본 방법 사용
                    return new Bitmap(Bitmap.FromHbitmap(hBitmap));
                }

                // 32비트 ARGB 비트맵 생성
                Bitmap resultBitmap = new Bitmap(bmp.bmWidth, bmp.bmHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // 비트맵 데이터 복사
                System.Drawing.Imaging.BitmapData bmpData = resultBitmap.LockBits(
                    new Rectangle(0, 0, bmp.bmWidth, bmp.bmHeight),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    // 소스 비트맵의 비트 데이터 크기 계산
                    int stride = bmp.bmWidth * 4; // 32비트 = 4바이트
                    int size = stride * bmp.bmHeight;
                    byte[] bits = new byte[size];

                    // HBITMAP에서 비트 데이터 가져오기
                    // Shell API에서 반환하는 HBITMAP은 이미 top-down 순서이므로 뒤집지 않음
                    NativeMethods.GetBitmapBits(hBitmap, size, bits);

                    // 데이터를 그대로 복사 (뒤집기 제거)
                    Marshal.Copy(bits, 0, bmpData.Scan0, size);
                }
                finally
                {
                    resultBitmap.UnlockBits(bmpData);
                }

                // 알파 채널이 모두 0인지 확인 (투명도가 없는 경우)
                bool hasAlpha = false;
                for (int y = 0; y < resultBitmap.Height && !hasAlpha; y++)
                {
                    for (int x = 0; x < resultBitmap.Width && !hasAlpha; x++)
                    {
                        if (resultBitmap.GetPixel(x, y).A != 0)
                        {
                            hasAlpha = true;
                        }
                    }
                }

                if (!hasAlpha)
                {
                    // 알파 채널이 없으면 모든 픽셀을 불투명하게 설정
                    for (int y = 0; y < resultBitmap.Height; y++)
                    {
                        for (int x = 0; x < resultBitmap.Width; x++)
                        {
                            Color c = resultBitmap.GetPixel(x, y);
                            resultBitmap.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                        }
                    }
                }

                return resultBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConvertHBitmapToArgbBitmap failed: {ex.Message}");
                // 실패하면 기본 방법으로 폴백
                try
                {
                    return new Bitmap(Bitmap.FromHbitmap(hBitmap));
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 비율을 유지하면서 이미지를 정사각형에 맞게 리사이즈합니다 (자르기 없음)
        /// </summary>
        private static Bitmap ResizeImageToFitSquare(Bitmap originalImage, int size)
        {
            try
            {
                // 투명 배경의 새 정사각형 비트맵 생성
                Bitmap resizedImage = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                int sourceWidth = originalImage.Width;
                int sourceHeight = originalImage.Height;

                // 이미지를 정사각형에 맞추기 위한 비율 계산
                float scale = Math.Min((float)size / sourceWidth, (float)size / sourceHeight);
                int newWidth = (int)(sourceWidth * scale);
                int newHeight = (int)(sourceHeight * scale);

                // 이미지를 정사각형 중앙에 배치
                int x = (size - newWidth) / 2;
                int y = (size - newHeight) / 2;

                using (Graphics g = Graphics.FromImage(resizedImage))
                {
                    // 투명 배경으로 초기화
                    g.Clear(Color.Transparent);

                    // 더 나은 결과를 위해 고품질 모드 설정
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

                    // 중앙에 정렬되고 크기 조정된 이미지 그리기
                    g.DrawImage(originalImage,
                        new Rectangle(x, y, newWidth, newHeight),
                        new Rectangle(0, 0, sourceWidth, sourceHeight),
                        GraphicsUnit.Pixel);
                }

                return resizedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resizing image to fit: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 지정된 크기의 정사각형으로 이미지를 리사이즈하고 자릅니다
        /// </summary>
        private static Bitmap ResizeAndCropImageToSquare(Bitmap originalImage, int size, float zoomFactor = 1.0f)
        {
            try
            {
                // 새 정사각형 비트맵 생성
                Bitmap resizedImage = new Bitmap(size, size);

                // 비율 유지를 위한 크기 계산
                int sourceWidth = originalImage.Width;
                int sourceHeight = originalImage.Height;

                // 가장 작은 차원을 찾아 크롭 영역 계산
                int cropSize = Math.Min(sourceWidth, sourceHeight);

                // 줌 배율 적용 (크롭 크기가 작을수록 더 확대됨)
                cropSize = (int)(cropSize / zoomFactor);

                // 크롭 사각형 중앙 정렬
                int cropX = (sourceWidth - cropSize) / 2;
                int cropY = (sourceHeight - cropSize) / 2;

                // 리사이즈를 수행할 그래픽 객체 생성
                using (Graphics g = Graphics.FromImage(resizedImage))
                {
                    // 더 나은 결과를 위해 고품질 모드 설정
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    // 비율을 유지하면서 중앙에 정렬되고 크롭된 이미지 그리기
                    g.DrawImage(originalImage,
                        new Rectangle(0, 0, size, size),
                        new Rectangle(cropX, cropY, cropSize, cropSize),
                        GraphicsUnit.Pixel);
                }

                return resizedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resizing image: {ex.Message}");
                return null;
            }
        }

        // 모든 아이콘에 대해 이 리사이즈 함수를 사용하도록 메인 ExtractIconAndSaveAsync 메서드 수정
        public static async Task<string> ExtractIconAndSaveAsync(string filePath, string outputDirectory, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(3);
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.WriteLine($"File path is empty");
                return null;
            }

            // 유효한 경로인지 확인 - 파일이 존재하거나 UWP 앱 경로인 경우
            bool isUwpApp = filePath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase);
            if (!isUwpApp && !File.Exists(filePath))
            {
                Debug.WriteLine($"File does not exist: {filePath}");
                return null;
            }

            try
            {
                using var cancellationTokenSource = new CancellationTokenSource(timeout.Value);
                return await Task.Run(async () =>
                {
                    try
                    {
                        Bitmap iconBitmap = null;
                        string appName = string.Empty;

                        // UWP 앱 처리 (shell:AppsFolder 경로)
                        if (isUwpApp)
                        {
                            iconBitmap = await ExtractUwpAppIconAsync(filePath, outputDirectory);
                        }
                        else if (Path.GetExtension(filePath).ToLower() == ".lnk")
                        {
                            // Windows 앱 바로가기 아이콘 추출 우선 시도
                            iconBitmap = await ExtractWindowsAppIconAsync(filePath, outputDirectory);

                            // 바로가기 타겟 경로를 미리 가져옴
                            string targetPath = null;
                            string iconLocationPath = null;
                            int iconIndex = 0;

                            try
                            {
                                dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                                dynamic shortcut = shell.CreateShortcut(filePath);
                                string iconLocation = shortcut.IconLocation;
                                targetPath = shortcut.TargetPath;

                                if (!string.IsNullOrEmpty(iconLocation) && iconLocation != ",")
                                {
                                    string[] iconInfo = iconLocation.Split(',');
                                    iconLocationPath = iconInfo[0].Trim();
                                    iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error reading shortcut properties: {ex.Message}");
                            }

                            // Windows 앱 아이콘 추출 실패 시 기존 방법으로 대체
                            if (iconBitmap == null)
                            {
                                // 타겟이 shell:AppsFolder 경로인 경우 UWP 앱으로 처리
                                if (!string.IsNullOrEmpty(targetPath) &&
                                    targetPath.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
                                {
                                    iconBitmap = await ExtractUwpAppIconAsync(targetPath, outputDirectory);
                                }

                                // 우선순위 1: 타겟 경로에서 직접 아이콘 추출 (화살표 없음)
                                if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                                {
                                    iconBitmap = ExtractIconWithoutArrow(targetPath);
                                    if (iconBitmap != null)
                                    {
                                        Debug.WriteLine($"Successfully extracted icon from target path: {targetPath}");
                                    }
                                }

                                // 우선순위 2: IconLocation에서 아이콘 추출 시도
                                if (iconBitmap == null && !string.IsNullOrEmpty(iconLocationPath) && File.Exists(iconLocationPath))
                                {
                                    iconBitmap = ExtractSpecificIcon(iconLocationPath, iconIndex);
                                    if (iconBitmap != null)
                                    {
                                        Debug.WriteLine($"Successfully extracted icon from IconLocation: {iconLocationPath}");
                                    }
                                }

                                // 우선순위 3: 바로가기 파일에서 SHGetImageList로 고해상도 아이콘 추출 시도
                                if (iconBitmap == null)
                                {
                                    iconBitmap = TryExtractIconViaSHGetImageList(filePath);
                                    if (iconBitmap != null)
                                    {
                                        Debug.WriteLine($"Extracted icon from shortcut via SHGetImageList: {filePath}");
                                    }
                                }

                                // 우선순위 4: 바로가기 파일에서 ExtractIconEx로 추출 (32x32)
                                if (iconBitmap == null)
                                {
                                    try
                                    {
                                        IntPtr[] hIcons = new IntPtr[1];
                                        uint iconCount = NativeMethods.ExtractIconEx(filePath, 0, hIcons, null, 1);
                                        if (iconCount > 0 && hIcons[0] != IntPtr.Zero)
                                        {
                                            using (var icon = Icon.FromHandle(hIcons[0]))
                                            {
                                                using (var rawBitmap = new Bitmap(icon.ToBitmap()))
                                                {
                                                    iconBitmap = CropToActualContent(rawBitmap);
                                                    Debug.WriteLine($"Extracted icon from shortcut via ExtractIconEx: {filePath} ({rawBitmap.Width}x{rawBitmap.Height} -> {iconBitmap.Width}x{iconBitmap.Height})");
                                                }
                                            }
                                            NativeMethods.DestroyIcon(hIcons[0]);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"ExtractIconEx failed for shortcut: {ex.Message}");
                                    }
                                }

                                // 우선순위 5: 최종 폴백 - 타겟 파일의 ExtractAssociatedIcon 사용 (32x32)
                                if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                                {
                                    try
                                    {
                                        Icon icon = Icon.ExtractAssociatedIcon(targetPath);
                                        if (icon != null)
                                        {
                                            using (var rawBitmap = icon.ToBitmap())
                                            {
                                                iconBitmap = CropToActualContent(rawBitmap);
                                                Debug.WriteLine($"Fallback: ExtractAssociatedIcon from target: {targetPath} ({rawBitmap.Width}x{rawBitmap.Height} -> {iconBitmap.Width}x{iconBitmap.Height})");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"ExtractAssociatedIcon failed for target: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            iconBitmap = ExtractIconWithoutArrow(filePath);

                        }
                        if (iconBitmap == null)
                        {
                            Debug.WriteLine($"No icon found for file: {filePath}");
                            return null;
                        }

                        // 추출된 아이콘 크기 로그 출력
                        Debug.WriteLine($"[IconSize] {iconBitmap.Width}x{iconBitmap.Height} - {Path.GetFileName(filePath)}");

                        //using (Bitmap resizedIcon = ResizeAndCropImageToSquare(iconBitmap, 200)) {
                        Directory.CreateDirectory(outputDirectory);
                        string iconFileName = GenerateUniqueIconFileName(filePath, iconBitmap);
                        string iconFilePath = Path.Combine(outputDirectory, iconFileName);

                        if (File.Exists(iconFilePath))
                        {
                            Debug.WriteLine($"[IconSize] {iconBitmap.Width}x{iconBitmap.Height} - {Path.GetFileName(filePath)} (cached)");
                            iconBitmap.Dispose();
                            return iconFilePath;
                        }

                        using (var stream = new FileStream(iconFilePath, FileMode.Create))
                        {
                            cancellationTokenSource.Token.ThrowIfCancellationRequested();
                            iconBitmap.Save(stream, ImageFormat.Png);
                        }
                        iconBitmap.Dispose();

                        Debug.WriteLine($"Icon saved to: {iconFilePath}");
                        return iconFilePath;
                        //}
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"Icon extraction timed out for: {filePath}");
                        return null;
                    }
                }, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
                return null;
            }
        }





        private static string GenerateUniqueIconFileName(string filePath, Bitmap iconBitmap)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] filePathBytes = System.Text.Encoding.UTF8.GetBytes(filePath);

                using (var ms = new MemoryStream())
                {
                    iconBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bitmapBytes = ms.ToArray();

                    byte[] combinedBytes = new byte[filePathBytes.Length + bitmapBytes.Length];
                    filePathBytes.CopyTo(combinedBytes, 0);
                    bitmapBytes.CopyTo(combinedBytes, filePathBytes.Length);

                    byte[] hashBytes = md5.ComputeHash(combinedBytes);

                    string hash = BitConverter.ToString(hashBytes)
                        .Replace("-", "")
                        .Substring(0, 16)
                        .ToLower();

                    return $"{Path.GetFileNameWithoutExtension(filePath)}_{hash}.png";
                }
            }
        }




        public static async Task<BitmapImage> ExtractIconFastAsync(string filePath, DispatcherQueue dispatcher)
        {
            if (!File.Exists(filePath)) return null;

            if (Path.GetExtension(filePath).ToLower() == ".lnk")
            {
                return await ExtractLnkIconWithoutArrowAsync(filePath, dispatcher);
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (var icon = Icon.ExtractAssociatedIcon(filePath))
                    {
                        if (icon == null) return null;

                        using (var stream = new MemoryStream())
                        {
                            icon.ToBitmap().Save(stream, ImageFormat.Png);
                            stream.Position = 0;

                            BitmapImage bitmapImage = null;
                            var resetEvent = new ManualResetEvent(false);

                            dispatcher.TryEnqueue(() =>
                            {
                                try
                                {
                                    bitmapImage = new BitmapImage();
                                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                                    resetEvent.Set();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error setting bitmap source: {ex.Message}");
                                    resetEvent.Set();
                                }
                            });

                            resetEvent.WaitOne();
                            return bitmapImage;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error extracting icon: {ex.Message}");
                    return null;
                }
            });
        }
        public static async Task<BitmapImage> ExtractLnkIconWithoutArrowAsync(string lnkPath, DispatcherQueue dispatcher)
        {
            return await Task.Run(() =>
            {
                try
                {
                    dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                    dynamic shortcut = shell.CreateShortcut(lnkPath);

                    string iconPath = shortcut.IconLocation;
                    string targetPath = shortcut.TargetPath;

                    // 우선순위 1: 타겟 경로에서 직접 아이콘 추출 (화살표 없음)
                    if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                    {
                        using (var targetIcon = ExtractIconWithoutArrow(targetPath))
                        {
                            if (targetIcon != null)
                            {
                                Debug.WriteLine($"ExtractLnkIconWithoutArrowAsync: Extracted from target path: {targetPath}");
                                return CreateBitmapImageFromBitmap(targetIcon, dispatcher);
                            }
                        }
                    }

                    // 우선순위 2: IconLocation에서 아이콘 추출 시도
                    if (!string.IsNullOrEmpty(iconPath) && iconPath != ",")
                    {
                        string[] iconInfo = iconPath.Split(',');
                        string actualIconPath = iconInfo[0].Trim();
                        int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;

                        if (File.Exists(actualIconPath))
                        {
                            using (var extractedIcon = ExtractSpecificIcon(actualIconPath, iconIndex))
                            {
                                if (extractedIcon != null)
                                {
                                    Debug.WriteLine($"ExtractLnkIconWithoutArrowAsync: Extracted from IconLocation: {actualIconPath}");
                                    return CreateBitmapImageFromBitmap(extractedIcon, dispatcher);
                                }
                            }
                        }
                    }

                    // 우선순위 3: 바로가기 파일에서 SHGetImageList로 고해상도 아이콘 추출 시도
                    {
                        var bitmapFromImageList = TryExtractIconViaSHGetImageList(lnkPath);
                        if (bitmapFromImageList != null)
                        {
                            Debug.WriteLine($"ExtractLnkIconWithoutArrowAsync: Extracted via SHGetImageList: {lnkPath}");
                            return CreateBitmapImageFromBitmap(bitmapFromImageList, dispatcher);
                        }
                    }

                    // 우선순위 4: 바로가기 파일에서 직접 ExtractIconEx로 추출 시도 (32x32)
                    try
                    {
                        IntPtr[] hIcons = new IntPtr[1];
                        uint iconCount = NativeMethods.ExtractIconEx(lnkPath, 0, hIcons, null, 1);
                        if (iconCount > 0 && hIcons[0] != IntPtr.Zero)
                        {
                            using (var icon = Icon.FromHandle(hIcons[0]))
                            {
                                var bitmap = new Bitmap(icon.ToBitmap());
                                NativeMethods.DestroyIcon(hIcons[0]);
                                Debug.WriteLine($"ExtractLnkIconWithoutArrowAsync: Extracted via ExtractIconEx: {lnkPath}");
                                return CreateBitmapImageFromBitmap(bitmap, dispatcher);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ExtractIconEx failed for lnk: {ex.Message}");
                    }

                    // 우선순위 5: 최종 폴백 - 타겟 파일의 ExtractAssociatedIcon 사용 (32x32)
                    if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                    {
                        try
                        {
                            using (var icon = Icon.ExtractAssociatedIcon(targetPath))
                            {
                                if (icon != null)
                                {
                                    Debug.WriteLine($"ExtractLnkIconWithoutArrowAsync: Fallback to ExtractAssociatedIcon: {targetPath}");
                                    return CreateBitmapImageFromBitmap(icon.ToBitmap(), dispatcher);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ExtractAssociatedIcon failed for target: {ex.Message}");
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error extracting .lnk icon: {ex.Message}");
                    return null;
                }
            });
        }

        private static Bitmap ExtractSpecificIcon(string iconPath, int iconIndex)
        {
            try
            {
                // iconIndex가 0인 경우에만 고해상도 아이콘 추출 시도
                if (iconIndex == 0)
                {
                    // Method 1: IShellItemImageFactory 시도 (256/128/64/48/32)
                    var bitmapFromShell = TryExtractIconViaShellItemImageFactory(iconPath);
                    if (bitmapFromShell != null)
                    {
                        Debug.WriteLine($"ExtractSpecificIcon: Got icon via IShellItemImageFactory for {iconPath}");
                        return bitmapFromShell;
                    }

                    // Method 2: SHGetImageList 시도 (256x256 또는 48x48)
                    var bitmapFromImageList = TryExtractIconViaSHGetImageList(iconPath);
                    if (bitmapFromImageList != null)
                    {
                        Debug.WriteLine($"ExtractSpecificIcon: Got icon via SHGetImageList for {iconPath}");
                        return bitmapFromImageList;
                    }
                }

                // Method 3: ExtractIconEx (32x32)
                IntPtr[] hLargeIcons = new IntPtr[1];
                uint iconCount = NativeMethods.ExtractIconEx(iconPath, iconIndex, hLargeIcons, null, 1);

                if (iconCount > 0 && hLargeIcons[0] != IntPtr.Zero)
                {
                    try
                    {
                        using (var icon = Icon.FromHandle(hLargeIcons[0]))
                        {
                            using (var rawBitmap = new Bitmap(icon.ToBitmap()))
                            {
                                var bitmap = CropToActualContent(rawBitmap);
                                Debug.WriteLine($"ExtractSpecificIcon: Got icon via ExtractIconEx for {iconPath} ({rawBitmap.Width}x{rawBitmap.Height} -> {bitmap.Width}x{bitmap.Height})");
                                return bitmap;
                            }
                        }
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(hLargeIcons[0]);
                    }
                }

                // Method 4: SHGetFileInfo 폴백 (32x32)
                NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
                uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;

                IntPtr result = NativeMethods.SHGetFileInfo(iconPath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        using (var icon = Icon.FromHandle(shfi.hIcon))
                        {
                            using (var rawBitmap = new Bitmap(icon.ToBitmap()))
                            {
                                var bitmap = CropToActualContent(rawBitmap);
                                Debug.WriteLine($"ExtractSpecificIcon: Got icon via SHGetFileInfo for {iconPath} ({rawBitmap.Width}x{rawBitmap.Height} -> {bitmap.Width}x{bitmap.Height})");
                                return bitmap;
                            }
                        }
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(shfi.hIcon);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting specific icon: {ex.Message}");
                return null;
            }
        }

        private static Bitmap ExtractIconWithoutArrow(string targetPath)
        {
            try
            {
                Debug.WriteLine($"ExtractIconWithoutArrow: Attempting to extract icon from: {targetPath}");

                // Method 1: Try IShellItemImageFactory first (works better with protected folders like Program Files)
                var bitmapFromShell = TryExtractIconViaShellItemImageFactory(targetPath);
                if (bitmapFromShell != null)
                {
                    Debug.WriteLine($"ExtractIconWithoutArrow: Successfully extracted via IShellItemImageFactory for {targetPath}");
                    return bitmapFromShell;
                }

                // Method 2: Try SHGetImageList (256x256 Jumbo 또는 48x48 ExtraLarge 아이콘)
                var bitmapFromImageList = TryExtractIconViaSHGetImageList(targetPath);
                if (bitmapFromImageList != null)
                {
                    Debug.WriteLine($"ExtractIconWithoutArrow: Successfully extracted via SHGetImageList for {targetPath}");
                    return bitmapFromImageList;
                }

                // Method 3: Try ExtractIconEx (gets icon directly without overlay, 32x32)
                IntPtr[] hLargeIcons = new IntPtr[1];
                uint iconCount = NativeMethods.ExtractIconEx(targetPath, 0, hLargeIcons, null, 1);

                if (iconCount > 0 && hLargeIcons[0] != IntPtr.Zero)
                {
                    try
                    {
                        using (var icon = Icon.FromHandle(hLargeIcons[0]))
                        {
                            using (var rawBitmap = new Bitmap(icon.ToBitmap()))
                            {
                                var bitmap = CropToActualContent(rawBitmap);
                                Debug.WriteLine($"ExtractIconWithoutArrow: Successfully extracted via ExtractIconEx for {targetPath} ({rawBitmap.Width}x{rawBitmap.Height} -> {bitmap.Width}x{bitmap.Height})");
                                return bitmap;
                            }
                        }
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(hLargeIcons[0]);
                    }
                }

                // Method 4: Try SHGetFileInfo (works well with protected paths, 32x32)
                try
                {
                    NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
                    uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;

                    IntPtr result = NativeMethods.SHGetFileInfo(targetPath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                    if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            using (var icon = Icon.FromHandle(shfi.hIcon))
                            {
                                using (var rawBitmap = new Bitmap(icon.ToBitmap()))
                                {
                                    var bitmap = CropToActualContent(rawBitmap);
                                    Debug.WriteLine($"ExtractIconWithoutArrow: Successfully extracted via SHGetFileInfo for {targetPath} ({rawBitmap.Width}x{rawBitmap.Height} -> {bitmap.Width}x{bitmap.Height})");
                                    return bitmap;
                                }
                            }
                        }
                        finally
                        {
                            NativeMethods.DestroyIcon(shfi.hIcon);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SHGetFileInfo failed for {targetPath}: {ex.Message}");
                }

                // Method 5: Fallback to ExtractAssociatedIcon (32x32)
                try
                {
                    var icon2 = Icon.ExtractAssociatedIcon(targetPath);
                    if (icon2 != null)
                    {
                        using (var rawBitmap = icon2.ToBitmap())
                        {
                            var bitmap = CropToActualContent(rawBitmap);
                            Debug.WriteLine($"ExtractIconWithoutArrow: Fallback to ExtractAssociatedIcon for {targetPath} ({rawBitmap.Width}x{rawBitmap.Height} -> {bitmap.Width}x{bitmap.Height})");
                            return bitmap;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ExtractAssociatedIcon failed for {targetPath}: {ex.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting icon without arrow from {targetPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 비트맵에서 투명하지 않은 실제 아이콘 영역만 크롭하여 반환
        /// 256x256 캔버스에 32x32 아이콘이 중앙 배치된 경우 32x32만 추출
        /// </summary>
        private static Bitmap CropToActualContent(Bitmap source)
        {
            if (source == null) return null;

            try
            {
                // 코너 픽셀들의 색상을 배경색으로 간주
                Color[] cornerPixels = new Color[] {
                    source.GetPixel(0, 0),
                    source.GetPixel(source.Width - 1, 0),
                    source.GetPixel(0, source.Height - 1),
                    source.GetPixel(source.Width - 1, source.Height - 1)
                };

                // 가장 많이 나타나는 코너 색상을 배경색으로 사용
                Color bgColor = cornerPixels.GroupBy(c => c.ToArgb())
                    .OrderByDescending(g => g.Count())
                    .First().First();

                int minX = source.Width;
                int minY = source.Height;
                int maxX = 0;
                int maxY = 0;

                // 배경색이 아닌 픽셀의 경계 찾기
                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                    {
                        Color pixel = source.GetPixel(x, y);
                        // 투명하지 않고 배경색이 아닌 픽셀
                        bool isContent = pixel.A > 0 && pixel.ToArgb() != bgColor.ToArgb();
                        // 배경이 투명인 경우도 고려 (A > 10으로 거의 투명한 픽셀 제외)
                        if (bgColor.A == 0)
                        {
                            isContent = pixel.A > 10;
                        }

                        if (isContent)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                // 유효한 영역이 없으면 원본 반환
                if (maxX < minX || maxY < minY)
                {
                    Debug.WriteLine($"CropToActualContent: No content found, returning original {source.Width}x{source.Height}");
                    return new Bitmap(source);
                }

                int contentWidth = maxX - minX + 1;
                int contentHeight = maxY - minY + 1;

                // 크롭할 필요 없으면 (거의 전체가 채워져 있으면) 원본 반환
                // 여백이 10% 미만이면 크롭하지 않음
                if (contentWidth >= source.Width * 0.9 && contentHeight >= source.Height * 0.9)
                {
                    Debug.WriteLine($"CropToActualContent: Content fills image, returning original {source.Width}x{source.Height}");
                    return new Bitmap(source);
                }

                // 정사각형으로 만들기 (가장 큰 면 기준)
                int size = Math.Max(contentWidth, contentHeight);

                // 크롭된 비트맵 생성
                Bitmap cropped = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(cropped))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(source,
                        new Rectangle((size - contentWidth) / 2, (size - contentHeight) / 2, contentWidth, contentHeight),
                        new Rectangle(minX, minY, contentWidth, contentHeight),
                        GraphicsUnit.Pixel);
                }

                Debug.WriteLine($"CropToActualContent: {source.Width}x{source.Height} -> {size}x{size} (bg: {bgColor})");
                return cropped;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CropToActualContent failed: {ex.Message}");
                return new Bitmap(source);
            }
        }

        /// <summary>
        /// SHGetImageList와 IImageList를 사용하여 시스템 이미지 리스트에서 아이콘 추출
        /// SHIL_JUMBO(256x256) 또는 SHIL_EXTRALARGE(48x48) 크기 지원
        /// </summary>
        private static Bitmap TryExtractIconViaSHGetImageList(string filePath)
        {
            try
            {
                // SHGetFileInfo로 시스템 아이콘 인덱스 획득
                NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
                uint flags = NativeMethods.SHGFI_SYSICONINDEX;

                IntPtr result = NativeMethods.SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (result == IntPtr.Zero)
                {
                    Debug.WriteLine($"TryExtractIconViaSHGetImageList: SHGetFileInfo failed for {filePath}");
                    return null;
                }

                int iconIndex = shfi.iIcon;

                // SHIL_JUMBO(256x256) → SHIL_EXTRALARGE(48x48) 순서로 시도
                int[] imageListSizes = { NativeMethods.SHIL_JUMBO, NativeMethods.SHIL_EXTRALARGE };

                foreach (int shilSize in imageListSizes)
                {
                    Guid iidImageList = NativeMethods.IID_IImageList;
                    NativeMethods.IImageList imageList = null;

                    int hr = NativeMethods.SHGetImageList(shilSize, ref iidImageList, out imageList);
                    if (hr != 0 || imageList == null)
                    {
                        continue;
                    }

                    IntPtr hIcon = IntPtr.Zero;
                    try
                    {
                        // ILD_TRANSPARENT로 아이콘 추출
                        hr = imageList.GetIcon(iconIndex, NativeMethods.ILD_TRANSPARENT, out hIcon);
                        if (hr != 0 || hIcon == IntPtr.Zero)
                        {
                            // ILD_IMAGE로 재시도
                            hr = imageList.GetIcon(iconIndex, NativeMethods.ILD_IMAGE, out hIcon);
                        }

                        if (hr == 0 && hIcon != IntPtr.Zero)
                        {
                            using (var icon = Icon.FromHandle(hIcon))
                            {
                                using (var rawBitmap = new Bitmap(icon.ToBitmap()))
                                {
                                    // 실제 아이콘 영역만 크롭 (256 캔버스에서 32 아이콘 추출)
                                    var bitmap = CropToActualContent(rawBitmap);
                                    string sizeLabel = shilSize == NativeMethods.SHIL_JUMBO ? "JUMBO(256)" : "EXTRALARGE(48)";
                                    Debug.WriteLine($"TryExtractIconViaSHGetImageList: {sizeLabel} -> {bitmap.Width}x{bitmap.Height} for {filePath}");
                                    return bitmap;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (hIcon != IntPtr.Zero)
                        {
                            NativeMethods.DestroyIcon(hIcon);
                        }
                        if (imageList != null)
                        {
                            Marshal.ReleaseComObject(imageList);
                        }
                    }
                }

                Debug.WriteLine($"TryExtractIconViaSHGetImageList: Failed to get icon for {filePath}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryExtractIconViaSHGetImageList failed for {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// IShellItemImageFactory를 사용하여 아이콘 추출 - 바로가기 오버레이 없는 깨끗한 아이콘 획득
        /// Program Files와 같은 보호된 폴더에서 잘 동작함
        /// </summary>
        private static Bitmap TryExtractIconViaShellItemImageFactory(string filePath)
        {
            try
            {
                NativeMethods.IShellItemImageFactory imageFactory = null;
                Guid imageFactoryGuid = NativeMethods.IShellItemImageFactoryGuid;
                int hr = NativeMethods.SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref imageFactoryGuid, out imageFactory);

                if (hr != 0 || imageFactory == null)
                {
                    Debug.WriteLine($"TryExtractIconViaShellItemImageFactory: SHCreateItemFromParsingName failed for {filePath}, HRESULT: 0x{hr:X8}");
                    return null;
                }

                IntPtr hBitmap = IntPtr.Zero;

                // 여러 크기로 시도 (큰 크기부터)
                int[] sizes = { 256, 128, 64, 48, 32 };

                foreach (int size in sizes)
                {
                    NativeMethods.SIZE iconSize = new NativeMethods.SIZE(size, size);

                    // SIIGBF_ICONONLY: 아이콘만 가져옴 (오버레이 없음)
                    hr = imageFactory.GetImage(iconSize, NativeMethods.SIIGBF.SIIGBF_BIGGERSIZEOK | NativeMethods.SIIGBF.SIIGBF_ICONONLY, out hBitmap);

                    if (hr == 0 && hBitmap != IntPtr.Zero)
                    {
                        break;
                    }

                    // 아이콘만 안되면 일반 이미지로 시도
                    hr = imageFactory.GetImage(iconSize, NativeMethods.SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);

                    if (hr == 0 && hBitmap != IntPtr.Zero)
                    {
                        break;
                    }
                }

                if (hr != 0 || hBitmap == IntPtr.Zero)
                {
                    Debug.WriteLine($"TryExtractIconViaShellItemImageFactory: GetImage failed for {filePath}, HRESULT: 0x{hr:X8}");
                    return null;
                }

                try
                {
                    // 알파 채널을 보존하면서 HBITMAP을 Bitmap으로 변환
                    using (Bitmap rawBitmap = ConvertHBitmapToArgbBitmap(hBitmap))
                    {
                        // 실제 아이콘 영역만 크롭
                        Bitmap result = CropToActualContent(rawBitmap);
                        Debug.WriteLine($"TryExtractIconViaShellItemImageFactory: {rawBitmap.Width}x{rawBitmap.Height} -> {result.Width}x{result.Height} for {filePath}");
                        return result;
                    }
                }
                finally
                {
                    NativeMethods.DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryExtractIconViaShellItemImageFactory failed for {filePath}: {ex.Message}");
                return null;
            }
        }


        private static BitmapImage CreateBitmapImageFromBitmap(Bitmap bitmap, DispatcherQueue dispatcher)
        {
            if (bitmap == null) return null;

            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                BitmapImage bitmapImage = null;
                var resetEvent = new ManualResetEvent(false);

                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        bitmapImage = new BitmapImage();
                        bitmapImage.SetSource(stream.AsRandomAccessStream());
                        resetEvent.Set();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error setting bitmap source: {ex.Message}");
                        resetEvent.Set();
                    }
                });

                resetEvent.WaitOne();
                return bitmapImage;
            }
        }

        public static async Task<BitmapImage> ExtractIconFromFileAsync(string filePath, DispatcherQueue dispatcher)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.WriteLine($"File not found: {filePath}");
                    return null;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
                        uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;

                        IntPtr result = NativeMethods.SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

                        if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                        {
                            Debug.WriteLine($"SHGetFileInfo failed for: {filePath}");
                            return null;
                        }

                        Debug.WriteLine($"Successfully extracted icon for: {filePath}");

                        using (var icon = System.Drawing.Icon.FromHandle(shfi.hIcon))
                        using (var bitmap = icon.ToBitmap())
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            stream.Position = 0;

                            BitmapImage bitmapImage = null;

                            var resetEvent = new ManualResetEvent(false);

                            dispatcher.TryEnqueue(() =>
                            {
                                try
                                {
                                    bitmapImage = new BitmapImage();
                                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                                    resetEvent.Set();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error setting bitmap source: {ex.Message}");
                                    resetEvent.Set();
                                }
                            });

                            resetEvent.WaitOne();

                            NativeMethods.DestroyIcon(shfi.hIcon);

                            return bitmapImage;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Icon extraction error: {ex.Message}");
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> ConvertToIco(string sourcePath, string icoFilePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(icoFilePath))
            {
                Debug.WriteLine("Invalid source or destination path.");
                return false;
            }

            if (!File.Exists(sourcePath))
            {
                Debug.WriteLine($"Source file not found: {sourcePath}");
                return false;
            }

            try
            {
                string tempIconPath = null;

                // 소스 파일이 .exe인 경우 아이콘 먼저 추출
                if (Path.GetExtension(sourcePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    tempIconPath = await IconCache.GetIconPathAsync(sourcePath);
                    if (string.IsNullOrEmpty(tempIconPath))
                    {
                        Debug.WriteLine("Failed to extract icon from .exe file.");
                        return false;
                    }
                    sourcePath = tempIconPath;
                }

                // 파일을 메모리로 읽어서 잠금 방지
                byte[] imageBytes = await File.ReadAllBytesAsync(sourcePath);
                using (MemoryStream imageStream = new MemoryStream(imageBytes))
                using (System.Drawing.Image originalImage = System.Drawing.Image.FromStream(imageStream))
                {
                    Size[] sizes = new Size[] { new Size(256, 256), new Size(128, 128), new Size(64, 64), new Size(32, 32), new Size(16, 16) };

                    using (FileStream fs = new FileStream(icoFilePath, FileMode.Create))
                    {
                        BinaryWriter bw = new BinaryWriter(fs);
                        bw.Write((short)0);
                        bw.Write((short)1);
                        bw.Write((short)sizes.Length);

                        int headerSize = 6 + (16 * sizes.Length);
                        int dataOffset = headerSize;
                        List<byte[]> imageDataList = new List<byte[]>();

                        foreach (Size size in sizes)
                        {
                            // 투명도 보존을 위해 ARGB 형식으로 비트맵 생성
                            using (Bitmap bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                            {
                                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
                                {
                                    // 더 나은 아이콘 품질을 위해 고품질 렌더링 설정
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                                    // 투명 배경으로 초기화
                                    g.Clear(System.Drawing.Color.Transparent);

                                    // 원본 이미지 비율을 유지하면서 정사각형 캔버스에 중앙 배치
                                    float scale = Math.Min((float)size.Width / originalImage.Width, (float)size.Height / originalImage.Height);
                                    int newWidth = (int)(originalImage.Width * scale);
                                    int newHeight = (int)(originalImage.Height * scale);
                                    int x = (size.Width - newWidth) / 2;
                                    int y = (size.Height - newHeight) / 2;
                                    g.DrawImage(originalImage, x, y, newWidth, newHeight);
                                }
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                    byte[] imageData = ms.ToArray();
                                    imageDataList.Add(imageData);
                                }
                            }
                        }

                        for (int i = 0; i < sizes.Length; i++)
                        {
                            Size size = sizes[i];
                            byte[] imageData = imageDataList[i];

                            bw.Write((byte)size.Width);
                            bw.Write((byte)size.Height);
                            bw.Write((byte)0);
                            bw.Write((byte)0);
                            bw.Write((short)1);
                            bw.Write((short)32);
                            bw.Write((int)imageData.Length);
                            bw.Write((int)dataOffset);

                            dataOffset += imageData.Length;
                        }

                        foreach (byte[] imageData in imageDataList)
                        {
                            bw.Write(imageData);
                        }

                        bw.Flush();
                    }
                }

                // 임시 아이콘 파일이 생성되었다면 정리
                if (!string.IsNullOrEmpty(tempIconPath) && File.Exists(tempIconPath))
                {
                    File.Delete(tempIconPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting to ICO: {ex.Message}");
                return false;
            }
        }


        //public static bool ConvertToIco(string sourcePath, string icoFilePath) {
        //    if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(icoFilePath)) {
        //        Debug.WriteLine("Invalid source or destination path.");
        //        return false;
        //    }

        //    if (!File.Exists(sourcePath)) {
        //        Debug.WriteLine($"Source file not found: {sourcePath}");
        //        return false;
        //    }

        //    try {
        //        using (System.Drawing.Image originalImage = System.Drawing.Image.FromFile(sourcePath)) {
        //            Size[] sizes = new Size[] { new Size(256, 256), new Size(128, 128), new Size(64, 64), new Size(32, 32), new Size(16, 16) };

        //            using (FileStream fs = new FileStream(icoFilePath, FileMode.Create)) {
        //                BinaryWriter bw = new BinaryWriter(fs);
        //                bw.Write((short)0);
        //                bw.Write((short)1);
        //                bw.Write((short)sizes.Length);

        //                int headerSize = 6 + (16 * sizes.Length);
        //                int dataOffset = headerSize;
        //                List<byte[]> imageDataList = new List<byte[]>();

        //                foreach (Size size in sizes) {
        //                    using (Bitmap bitmap = new Bitmap(originalImage, size)) {
        //                        using (MemoryStream ms = new MemoryStream()) {
        //                            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //                            byte[] imageData = ms.ToArray();
        //                            imageDataList.Add(imageData);
        //                        }
        //                    }
        //                }

        //                for (int i = 0; i < sizes.Length; i++) {
        //                    Size size = sizes[i];
        //                    byte[] imageData = imageDataList[i];

        //                    bw.Write((byte)size.Width);
        //                    bw.Write((byte)size.Height);
        //                    bw.Write((byte)0);
        //                    bw.Write((byte)0);
        //                    bw.Write((short)1);
        //                    bw.Write((short)32);
        //                    bw.Write((int)imageData.Length);
        //                    bw.Write((int)dataOffset);

        //                    dataOffset += imageData.Length;
        //                }

        //                foreach (byte[] imageData in imageDataList) {
        //                    bw.Write(imageData);
        //                }

        //                bw.Flush();
        //            }
        //        }
        //        return true;
        //    }
        //    catch (Exception ex) {
        //        Debug.WriteLine($"Error converting to ICO: {ex.Message}");
        //        return false;
        //    }
        //}

        //      public async Task<string> CreateGridIconAsync(
        //List<ExeFileModel> selectedItems,
        //int selectedSize,
        //Image iconPreviewImage,
        //Border iconPreviewBorder) {
        //          try {
        //              if (selectedItems == null || selectedSize <= 0) {
        //                  throw new ArgumentException("Invalid selected items or grid size.");
        //              }

        //              selectedItems = selectedItems.Take(selectedSize * selectedSize).ToList();

        //              int finalSize = 256;
        //              int gridSize;
        //              int cellSize;

        //              if (selectedItems.Count == 2) {
        //                  gridSize = 2;
        //                  cellSize = finalSize / 2;
        //              }
        //              else {
        //                  gridSize = (int)Math.Ceiling(Math.Sqrt(selectedItems.Count));
        //                  cellSize = finalSize / gridSize;
        //              }

        //              string tempFolder = Path.Combine(Path.GetTempPath(), "GridIconTemp");
        //              Directory.CreateDirectory(tempFolder);
        //              string outputPath = Path.Combine(tempFolder, "grid_icon.png");

        //              using (var bitmap = new System.Drawing.Bitmap(finalSize, finalSize)) {
        //                  using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) {
        //                      graphics.Clear(System.Drawing.Color.Transparent);

        //                      for (int i = 0; i < selectedItems.Count; i++) {
        //                          string filePath = selectedItems[i].FilePath;
        //                          if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) {
        //                              Debug.WriteLine($"File not found: {filePath}");
        //                              continue;
        //                          }

        //                          int x, y;
        //                          if (selectedItems.Count == 2) {
        //                              if (i == 0) {
        //                                  x = 0;
        //                                  y = cellSize;
        //                              }
        //                              else {
        //                                  x = cellSize;
        //                                  y = 0;
        //                              }
        //                          }
        //                          else {
        //                              int row = i / gridSize;
        //                              int col = i % gridSize;
        //                              x = col * cellSize;
        //                              y = row * cellSize;
        //                          }

        //                          System.Drawing.Bitmap iconBitmap = null;

        //                          if (Path.GetExtension(filePath).ToLower() == ".lnk") {
        //                              iconBitmap = await ExtractWindowsAppIconAsync(filePath, tempFolder);



        //                              dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
        //                              dynamic shortcut = shell.CreateShortcut(filePath);

        //                              string iconPath = shortcut.IconLocation;
        //                              string targetPath = shortcut.TargetPath;

        //                              if (!string.IsNullOrEmpty(iconPath) && iconPath != ",") {
        //                                  string[] iconInfo = iconPath.Split(',');
        //                                  string actualIconPath = iconInfo[0].Trim();
        //                                  int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;

        //                                  if (File.Exists(actualIconPath)) {
        //                                      iconBitmap = ExtractSpecificIcon(actualIconPath, iconIndex);
        //                                  }
        //                              }

        //                              if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
        //                                  iconBitmap = ExtractIconWithoutArrow(targetPath);
        //                              }
        //                              //Use the Icon with arrow as fallback
        //                              if (iconBitmap == null) {
        //                                  Icon icon = Icon.ExtractAssociatedIcon(filePath);
        //                                  iconBitmap = icon.ToBitmap();
        //                              }
        //                          }
        //                          else {
        //                              iconBitmap = ExtractIconWithoutArrow(filePath);
        //                          }

        //                          if (iconBitmap != null) {
        //                              try {
        //                                  int padding = 5;
        //                                  int drawSize = cellSize - (padding * 2);
        //                                  graphics.DrawImage(iconBitmap, new System.Drawing.Rectangle(
        //                                      x + padding, y + padding, drawSize, drawSize));
        //                              }
        //                              catch (Exception ex) {
        //                                  Debug.WriteLine($"Error processing icon {i}: {ex.Message}");
        //                              }
        //                              finally {
        //                                  iconBitmap.Dispose();
        //                              }
        //                          }
        //                          else {
        //                              Debug.WriteLine($"Failed to get icon for file: {filePath}");
        //                          }
        //                      }

        //                      bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        //                  }
        //              }

        //              StorageFile iconFile = await StorageFile.GetFileFromPathAsync(outputPath);
        //              BitmapImage gridIcon = new BitmapImage();

        //              using (var stream = await iconFile.OpenReadAsync()) {
        //                  await gridIcon.SetSourceAsync(stream);
        //              }

        //              iconPreviewImage.Source = gridIcon;
        //              iconPreviewBorder.Visibility = Visibility.Visible;
        //              return outputPath;
        //          }
        //          catch (Exception ex) {
        //              Debug.WriteLine($"Grid icon creation error: {ex.Message}");
        //              return null;
        //          }
        //      }

        public async Task<string> CreateGridIconForPopupAsync(List<ExeFileModel> items, int gridSize, string groupName)
        {
            try
            {
                if (items == null || !items.Any())
                {
                    throw new ArgumentException("No items provided for grid icon creation");
                }

                // 그리드에 맞는 아이템 개수 확인
                int maxItems = gridSize * gridSize;
                var gridItems = items.Take(maxItems).ToList();

                // 필요한 경우 빈 슬롯을 빈 아이템으로 채움
                //while (gridItems.Count < maxItems) {
                //    gridItems.Add(new ExeFileModel {
                //        FileName = "",
                //        FilePath = "",
                //        Icon = "",
                //        Tooltip = "",
                //        Args = "",
                //        IconPath = ""
                //    });
                //}

                // 임시 UI 요소 생성 (화면에 표시되지 않음)
                var tempImage = new Image();
                var tempBorder = new Border();

                // 동일한 배치를 보장하기 위해 CreateGridIconAsync 재사용
                string tempGridIconPath = await CreateGridIconAsync(
                    gridItems,
                    gridSize,
                    tempImage,
                    tempBorder
                );

                if (string.IsNullOrEmpty(tempGridIconPath))
                {
                    throw new Exception("Failed to create grid icon using CreateGridIconAsync");
                }

                // 생성된 아이콘을 그룹의 적절한 위치로 복사
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                string groupFolder = Path.Combine(groupsFolder, groupName);
                string uniqueFolderName = groupName;
                string uniqueFolderPath = Path.Combine(groupFolder, uniqueFolderName);

                Directory.CreateDirectory(uniqueFolderPath);

                string iconBaseName = $"{groupName}_{(gridSize == 3 ? "grid3" : "grid")}";
                string finalIcoFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.ico");
                string finalPngFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.png");

                // 임시 PNG를 최종 위치로 복사
                File.Copy(tempGridIconPath, finalPngFilePath, true);

                // ICO로 변환
                bool success = await ConvertToIco(finalPngFilePath, finalIcoFilePath);

                // 임시 파일 정리
                try
                {
                    File.Delete(tempGridIconPath);
                    // 임시 디렉터리도 존재하면 정리
                    string tempDir = Path.GetDirectoryName(tempGridIconPath);
                    if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir).Length == 0)
                    {
                        Directory.Delete(tempDir);
                    }
                }
                catch
                {
                    // 정리 오류 무시
                }

                if (success)
                {
                    return finalIcoFilePath;
                }
                else
                {
                    throw new Exception("Failed to convert grid icon to ICO format");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating grid icon for popup: {ex.Message}");
                throw;
            }
        }
        public async Task<string> CreateGridIconAsync(
    List<ExeFileModel> selectedItems,
    int selectedSize,
    Image iconPreviewImage,
    Border iconPreviewBorder)
        {
            try
            {
                if (selectedItems == null || selectedSize <= 0)
                {
                    throw new ArgumentException("Invalid selected items or grid size.");
                }
                selectedItems = selectedItems.Take(selectedSize * selectedSize).ToList();
                int finalSize = 256;
                int gridSize;
                int cellSize;
                if (selectedItems.Count == 2)
                {
                    gridSize = 2;
                    cellSize = finalSize / 2;
                }
                else
                {
                    gridSize = (int)Math.Ceiling(Math.Sqrt(selectedItems.Count));
                    cellSize = finalSize / gridSize;
                }
                string tempFolder = Path.Combine(Path.GetTempPath(), "GridIconTemp");
                Directory.CreateDirectory(tempFolder);
                string outputPath = Path.Combine(tempFolder, "grid_icon.png");

                using (var bitmap = new System.Drawing.Bitmap(finalSize, finalSize))
                {
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.Transparent);
                        for (int i = 0; i < selectedItems.Count; i++)
                        {
                            var item = selectedItems[i];
                            string iconPath = !string.IsNullOrEmpty(item.IconPath) ? item.IconPath : item.Icon;

                            int x, y;
                            if (selectedItems.Count == 2)
                            {
                                if (i == 0)
                                {
                                    x = 0;
                                    y = cellSize;
                                }
                                else
                                {
                                    x = cellSize;
                                    y = 0;
                                }
                            }
                            else
                            {
                                int row = i / gridSize;
                                int col = i % gridSize;
                                x = col * cellSize;
                                y = row * cellSize;
                            }

                            System.Drawing.Bitmap iconBitmap = null;
                            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                            {
                                // 사용자 지정 아이콘 경로가 있으면 사용
                                iconBitmap = new System.Drawing.Bitmap(iconPath);
                            }
                            else
                            {
                                // 폴백: 파일 경로에서 아이콘 추출
                                string filePath = item.FilePath;
                                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                                {
                                    Debug.WriteLine($"File not found: {filePath}");
                                    continue;
                                }

                                if (Path.GetExtension(filePath).ToLower() == ".lnk")
                                {
                                    iconBitmap = await ExtractWindowsAppIconAsync(filePath, tempFolder);

                                    // 바로가기 속성 가져오기
                                    string targetPath = null;
                                    string shortcutIconPath = null;
                                    int iconIndex = 0;

                                    try
                                    {
                                        dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                                        dynamic shortcut = shell.CreateShortcut(filePath);
                                        shortcutIconPath = shortcut.IconLocation;
                                        targetPath = shortcut.TargetPath;

                                        if (!string.IsNullOrEmpty(shortcutIconPath) && shortcutIconPath != ",")
                                        {
                                            string[] iconInfo = shortcutIconPath.Split(',');
                                            shortcutIconPath = iconInfo[0].Trim();
                                            iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;
                                        }
                                        else
                                        {
                                            shortcutIconPath = null;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error reading shortcut: {ex.Message}");
                                    }

                                    // IconLocation에서 아이콘 추출 시도
                                    if (iconBitmap == null && !string.IsNullOrEmpty(shortcutIconPath) && File.Exists(shortcutIconPath))
                                    {
                                        iconBitmap = ExtractSpecificIcon(shortcutIconPath, iconIndex);
                                    }

                                    // 타겟 경로에서 아이콘 추출 시도
                                    if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                                    {
                                        iconBitmap = ExtractIconWithoutArrow(targetPath);
                                    }

                                    // 바로가기 파일에서 직접 ExtractIconEx로 추출 시도
                                    if (iconBitmap == null)
                                    {
                                        try
                                        {
                                            IntPtr[] hIcons = new IntPtr[1];
                                            uint iconCount = NativeMethods.ExtractIconEx(filePath, 0, hIcons, null, 1);
                                            if (iconCount > 0 && hIcons[0] != IntPtr.Zero)
                                            {
                                                using (var icon = Icon.FromHandle(hIcons[0]))
                                                {
                                                    iconBitmap = new Bitmap(icon.ToBitmap());
                                                }
                                                NativeMethods.DestroyIcon(hIcons[0]);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"ExtractIconEx failed: {ex.Message}");
                                        }
                                    }

                                    // 최종 폴백: 타겟 파일의 ExtractAssociatedIcon 사용 (화살표 없음)
                                    if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                                    {
                                        try
                                        {
                                            Icon icon = Icon.ExtractAssociatedIcon(targetPath);
                                            iconBitmap = icon?.ToBitmap();
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"ExtractAssociatedIcon failed for target: {ex.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    iconBitmap = ExtractIconWithoutArrow(filePath);
                                }
                            }

                            if (iconBitmap != null)
                            {
                                try
                                {
                                    int padding = 5;
                                    int drawSize = cellSize - (padding * 2);
                                    graphics.DrawImage(iconBitmap, new System.Drawing.Rectangle(
                                        x + padding, y + padding, drawSize, drawSize));
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error processing icon {i}: {ex.Message}");
                                }
                                finally
                                {
                                    iconBitmap?.Dispose();
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"Failed to get icon for file: {item.FilePath}");
                            }
                        }
                        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }

                StorageFile iconFile = await StorageFile.GetFileFromPathAsync(outputPath);
                BitmapImage gridIcon = new BitmapImage();
                using (var stream = await iconFile.OpenReadAsync())
                {
                    await gridIcon.SetSourceAsync(stream);
                }
                iconPreviewImage.Source = gridIcon;
                iconPreviewBorder.Visibility = Visibility.Visible;
                return outputPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Grid icon creation error: {ex.Message}");
                return null;
            }
        }


    }


}
