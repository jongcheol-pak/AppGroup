using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AppGroup
{
    /// <summary>
    /// IconHelper - UWP/Shell 아이콘 추출 관련 partial class
    /// shell:AppsFolder 경로를 통한 UWP 앱 아이콘 추출 기능을 담당합니다.
    /// </summary>
    public partial class IconHelper
    {
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
    }
}
