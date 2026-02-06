using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;

namespace AppGroup
{
    /// <summary>
    /// IconHelper - 아이콘 추출 관련 partial class
    /// 다양한 소스에서 아이콘을 추출하는 메서드를 포함합니다.
    /// </summary>
    public partial class IconHelper
    {
        /// <summary>
        /// 파일에서 아이콘을 추출하여 저장합니다.
        /// </summary>
        /// <param name="filePath">아이콘을 추출할 파일 경로</param>
        /// <param name="outputDirectory">아이콘을 저장할 출력 디렉터리</param>
        /// <param name="timeout">추출 시간 초과 (기본 3초)</param>
        /// <param name="size">추출 및 저장할 아이콘 크기 (기본 48)</param>
        public static async Task<string> ExtractIconAndSaveAsync(string filePath, string outputDirectory, TimeSpan? timeout = null, int size = 48)
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

                                // 우선순위 3: 바로가기 파일에서 SHGetImageList로 아이콘 추출 시도
                                if (iconBitmap == null)
                                {
                                    iconBitmap = TryExtractIconViaSHGetImageList(filePath, size);
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
                                                // 복사본 생성 (dispose되지 않도록)
                                                iconBitmap = new Bitmap(icon.ToBitmap());
                                                Debug.WriteLine($"Extracted icon from shortcut via ExtractIconEx: {filePath} ({iconBitmap.Width}x{iconBitmap.Height})");
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
                                                iconBitmap = rawBitmap;
                                                Debug.WriteLine($"Fallback: ExtractAssociatedIcon from target: {targetPath} ({rawBitmap.Width}x{rawBitmap.Height})");
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
                            iconBitmap = ExtractIconWithoutArrow(filePath, size);

                        }
                        if (iconBitmap == null)
                        {
                            Debug.WriteLine($"No icon found for file: {filePath}");
                            return null;
                        }

                        // 추출된 아이콘 크기 로그 출력
                        Debug.WriteLine($"[IconSize] {iconBitmap.Width}x{iconBitmap.Height} - {Path.GetFileName(filePath)}");

                        Directory.CreateDirectory(outputDirectory);
                        
                        // 아이콘을 지정된 크기로 리사이즈 (기본 48x48)
                        Bitmap resizedBitmap = iconBitmap;
                        if (iconBitmap.Width != size || iconBitmap.Height != size)
                        {
                            resizedBitmap = new Bitmap(size, size);
                            using (var graphics = Graphics.FromImage(resizedBitmap))
                            {
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                graphics.DrawImage(iconBitmap, 0, 0, size, size);
                            }
                            iconBitmap.Dispose();
                            Debug.WriteLine($"[IconSize] Resized to {size}x{size} - {Path.GetFileName(filePath)}");
                        }

                        string iconFileName = GenerateUniqueIconFileName(filePath, resizedBitmap);
                        string iconFilePath = Path.Combine(outputDirectory, iconFileName);

                        if (File.Exists(iconFilePath))
                        {
                            Debug.WriteLine($"[IconSize] {resizedBitmap.Width}x{resizedBitmap.Height} - {Path.GetFileName(filePath)} (cached)");
                            resizedBitmap.Dispose();
                            return iconFilePath;
                        }

                        using (var stream = new FileStream(iconFilePath, FileMode.Create))
                        {
                            cancellationTokenSource.Token.ThrowIfCancellationRequested();
                            resizedBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        resizedBitmap.Dispose();

                        Debug.WriteLine($"Icon saved to: {iconFilePath} ({size}x{size})");
                        return iconFilePath;
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

        /// <summary>
        /// 빠른 아이콘 추출 (비동기)
        /// </summary>
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
                            icon.ToBitmap().Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            stream.Position = 0;

                            BitmapImage bitmapImage = null;
                            using (var resetEvent = new ManualResetEvent(false))
                            {
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
                            }
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

        /// <summary>
        /// 바로가기 파일에서 화살표 없는 아이콘 추출 (비동기)
        /// </summary>
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

                    // 우선순위 3: 바로가기 파일에서 SHGetImageList로 48x48 아이콘 추출 시도
                    {
                        var bitmapFromImageList = TryExtractIconViaSHGetImageList(lnkPath, 48);
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

        /// <summary>
        /// 파일에서 아이콘 추출 (비동기)
        /// </summary>
        public static async Task<BitmapImage> ExtractIconFromFileAsync(string filePath, DispatcherQueue dispatcher)
        {
            try
            {
                if (!File.Exists(filePath))
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

                        using (var icon = Icon.FromHandle(shfi.hIcon))
                        using (var bitmap = icon.ToBitmap())
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            stream.Position = 0;

                            BitmapImage bitmapImage = null;

                            using (var resetEvent = new ManualResetEvent(false))
                            {
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
                            }

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

        /// <summary>
        /// 특정 인덱스의 아이콘 추출 (리소스 정리 강화)
        /// </summary>
        private static Bitmap ExtractSpecificIcon(string iconPath, int iconIndex)
        {
            try
            {
                // iconIndex가 0인 경우에만 고해상도 아이콘 추출 시도
                if (iconIndex == 0)
                {
                    // Method 1: IShellItemImageFactory 시도 (48/32)
                    try
                    {
                        var bitmapFromShell = TryExtractIconViaShellItemImageFactory(iconPath, 48);
                        if (bitmapFromShell != null)
                        {
                            Debug.WriteLine($"ExtractSpecificIcon: Got icon via IShellItemImageFactory for {iconPath}");
                            return bitmapFromShell;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ExtractSpecificIcon: IShellItemImageFactory failed - {ex.Message}");
                    }

                    // Method 2: SHGetImageList 시도 (48x48)
                    try
                    {
                        var bitmapFromImageList = TryExtractIconViaSHGetImageList(iconPath, 48);
                        if (bitmapFromImageList != null)
                        {
                            Debug.WriteLine($"ExtractSpecificIcon: Got icon via SHGetImageList for {iconPath}");
                            return bitmapFromImageList;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ExtractSpecificIcon: SHGetImageList failed - {ex.Message}");
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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ExtractSpecificIcon: ExtractIconEx processing failed - {ex.Message}");
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(hLargeIcons[0]);
                    }
                }

                // Method 4: SHGetFileInfo 폴백 (32x32)
                try
                {
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
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ExtractSpecificIcon: SHGetFileInfo processing failed - {ex.Message}");
                        }
                        finally
                        {
                            NativeMethods.DestroyIcon(shfi.hIcon);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ExtractSpecificIcon: SHGetFileInfo failed - {ex.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractSpecificIcon failed for {iconPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 화살표 없는 아이콘 추출
        /// </summary>
        /// <param name="targetPath">대상 파일 경로</param>
        /// <param name="size">요청할 아이콘 크기 (기본 48)</param>
        private static Bitmap ExtractIconWithoutArrow(string targetPath, int size = 48)
        {
            try
            {
                Debug.WriteLine($"ExtractIconWithoutArrow: Attempting to extract icon from: {targetPath} (size: {size})");

                // Method 1: Try IShellItemImageFactory first (works better with protected folders like Program Files)
                var bitmapFromShell = TryExtractIconViaShellItemImageFactory(targetPath, size);
                if (bitmapFromShell != null)
                {
                    Debug.WriteLine($"ExtractIconWithoutArrow: Successfully extracted via IShellItemImageFactory for {targetPath}");
                    return bitmapFromShell;
                }

                // Method 2: Try SHGetImageList (48x48 ExtraLarge 아이콘)
                var bitmapFromImageList = TryExtractIconViaSHGetImageList(targetPath, size);
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
        /// Windows 앱 아이콘 추출 (COM 객체 해제 경로 강화)
        /// </summary>
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

                try
                {
                    folder = shell.Namespace(Path.GetDirectoryName(shortcutPath));
                    if (folder == null)
                    {
                        // 중간 단계 실패 시 즉시 COM 객체 해제
                        Marshal.ReleaseComObject(shell);
                        shell = null;
                        return null;
                    }

                    try
                    {
                        shortcutItem = folder.ParseName(Path.GetFileName(shortcutPath));
                        if (shortcutItem == null)
                        {
                            // 중간 단계 실패 시 즉시 COM 객체 해제 (역순)
                            Marshal.ReleaseComObject(folder);
                            folder = null;
                            Marshal.ReleaseComObject(shell);
                            shell = null;
                            return null;
                        }

                        try
                        {
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

                            if (string.IsNullOrEmpty(linkTarget))
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

                            // 링크 대상에서 앱 이름 추출 (첫 번째 "_" 이후의 모든 문자열 제거)
                            string appName = System.Text.RegularExpressions.Regex.Replace(linkTarget, "_.*$", "");
                            if (string.IsNullOrEmpty(appName))
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

                            // Windows Runtime API를 사용하여 패키지 찾기
                            Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
                            IEnumerable<Windows.ApplicationModel.Package> packages = packageManager.FindPackagesForUser("");

                            // 앱 이름과 일치하는 패키지 찾기
                            Windows.ApplicationModel.Package appPackage = packages.FirstOrDefault(p => p.Id.Name.StartsWith(appName, StringComparison.OrdinalIgnoreCase));
                            if (appPackage == null)
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

                            string installPath = appPackage.InstalledLocation.Path;
                            string manifestPath = Path.Combine(installPath, "AppxManifest.xml");

                            if (!File.Exists(manifestPath))
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

                            // 매니페스트 XML 로드 및 파싱
                            XmlDocument manifest = new XmlDocument();
                            manifest.Load(manifestPath);

                            // 네임스페이스 관리자 생성
                            XmlNamespaceManager nsManager = new XmlNamespaceManager(manifest.NameTable);
                            nsManager.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                            // 매니페스트에서 로고 경로 가져오기
                            XmlNode logoNode = manifest.SelectSingleNode("/ns:Package/ns:Properties/ns:Logo", nsManager);
                            if (logoNode == null)
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

                            string logoPath = logoNode.InnerText;
                            string logoDir = Path.Combine(installPath, Path.GetDirectoryName(logoPath));

                            if (!Directory.Exists(logoDir))
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

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

                            if (string.IsNullOrEmpty(highestResLogoPath) || !File.Exists(highestResLogoPath))
                            {
                                // 중간 단계 실패 시 COM 객체 해제
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                                Marshal.ReleaseComObject(folder);
                                folder = null;
                                Marshal.ReleaseComObject(shell);
                                shell = null;
                                return null;
                            }

                            // 원본 크기 그대로 로드하여 반환 (리사이즈 제거)
                            using (FileStream stream = new FileStream(highestResLogoPath, FileMode.Open, FileAccess.Read))
                            {
                                using (var originalBitmap = new Bitmap(stream))
                                {
                                    // 원본 비트맵 복사하여 반환
                                    var result = new Bitmap(originalBitmap);
                                    Debug.WriteLine($"ExtractWindowsAppIconAsync: Loaded logo {result.Width}x{result.Height} from {highestResLogoPath}");

                                    // 성공 시 COM 객체 해제
                                    Marshal.ReleaseComObject(shortcutItem);
                                    shortcutItem = null;
                                    Marshal.ReleaseComObject(folder);
                                    folder = null;
                                    Marshal.ReleaseComObject(shell);
                                    shell = null;

                                    return result;
                                }
                            }
                        }
                        finally
                        {
                            // shortcutItem 해제 (성공/실패 모두 해제)
                            if (shortcutItem != null)
                            {
                                Marshal.ReleaseComObject(shortcutItem);
                                shortcutItem = null;
                            }
                        }
                    }
                    finally
                    {
                        // folder 해제
                        if (folder != null)
                        {
                            Marshal.ReleaseComObject(folder);
                            folder = null;
                        }
                    }
                }
                finally
                {
                    // shell 해제
                    if (shell != null)
                    {
                        Marshal.ReleaseComObject(shell);
                        shell = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting Windows app icon: {ex.Message}");

                // 예외 발생 시에도 COM 객체 해제 시도
                if (shortcutItem != null)
                {
                    try { Marshal.ReleaseComObject(shortcutItem); } catch { }
                    shortcutItem = null;
                }
                if (folder != null)
                {
                    try { Marshal.ReleaseComObject(folder); } catch { }
                    folder = null;
                }
                if (shell != null)
                {
                    try { Marshal.ReleaseComObject(shell); } catch { }
                    shell = null;
                }

                return null;
            }
        }

        /// <summary>
        /// <summary>
        /// 요청한 크기에 따라 적절한 SHIL 상수 배열을 반환합니다.
        /// </summary>
        /// <param name="size">요청한 아이콘 크기</param>
        /// <returns>시도할 SHIL 상수 배열</returns>
        private static int[] GetImageListSizesForSize(int size)
        {
            // SHIL_LARGE = 0 (32x32), SHIL_EXTRALARGE = 2 (48x48), SHIL_JUMBO = 4 (256x256)
            // 요청한 크기에 맞는 아이콘부터 시도 (다운스케일 방지)
            if (size <= 32)
            {
                return new[] { NativeMethods.SHIL_LARGE };
            }
            else if (size <= 48)
            {
                // 48 요청: EXTRALARGE(48) → LARGE(32) 순서로 시도
                return new[] { NativeMethods.SHIL_EXTRALARGE, NativeMethods.SHIL_LARGE };
            }
            else
            {
                // 49 이상: JUMBO(256) → EXTRALARGE(48) → LARGE(32) 순서로 시도
                return new[] { NativeMethods.SHIL_JUMBO, NativeMethods.SHIL_EXTRALARGE, NativeMethods.SHIL_LARGE };
            }
        }

        /// SHGetImageList를 사용하여 아이콘 추출 시도
        /// </summary>
        /// <param name="filePath">아이콘을 추출할 파일 경로</param>
        /// <param name="size">추출할 아이콘 크기 (기본 256)</param>
        private static Bitmap TryExtractIconViaSHGetImageList(string filePath, int size = 256)
        {
            try
            {
                // SHGetFileInfo로 시스템 아이콘 인덱스 획득
                NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
                uint flags = NativeMethods.SHGFI_SYSICONINDEX;

                IntPtr shfiResult = NativeMethods.SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (shfiResult == IntPtr.Zero)
                {
                    Debug.WriteLine($"TryExtractIconViaSHGetImageList: SHGetFileInfo failed for {filePath}");
                    return null;
                }

                int iconIndex = shfi.iIcon;

                // 크기에 따라 적절한 SHIL 상수 선택
                int[] imageListSizes = GetImageListSizesForSize(size);

                foreach (int shilSize in imageListSizes)
                {
                    Guid iidImageList = NativeMethods.IID_IImageList;
                    NativeMethods.IImageList imageList = null;

                    try
                    {
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
                                    // 복사본 생성하여 반환
                                    var resultBitmap = new Bitmap(icon.ToBitmap());
                                    string sizeLabel = shilSize == NativeMethods.SHIL_JUMBO ? "JUMBO(256)" : shilSize == NativeMethods.SHIL_EXTRALARGE ? "EXTRALARGE(48)" : $"SHIL({shilSize})";
                                    Debug.WriteLine($"TryExtractIconViaSHGetImageList: {sizeLabel} -> {resultBitmap.Width}x{resultBitmap.Height} for {filePath}");
                                    return resultBitmap;
                                }
                            }
                        }
                        finally
                        {
                            // 아이콘 핸들 해제
                            if (hIcon != IntPtr.Zero)
                            {
                                NativeMethods.DestroyIcon(hIcon);
                            }
                        }
                    }
                    finally
                    {
                        // imageList COM 객체 해제 (모든 경로에서 해제 보장)
                        if (imageList != null)
                        {
                            Marshal.ReleaseComObject(imageList);
                            imageList = null;
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
        /// IShellItemImageFactory를 사용하여 아이콘 추출 시도
        /// </summary>
        private static Bitmap TryExtractIconViaShellItemImageFactory(string filePath, int size = 48)
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

                // 요청한 크기에 맞는 아이콘 시도 (다운스케일 방지)
                int[] sizes;
                if (size <= 32)
                {
                    sizes = new[] { 32 };
                }
                else if (size <= 48)
                {
                    sizes = new[] { 48, 32 };
                }
                else
                {
                    sizes = new[] { 256, 128, 64, 48, 32 };
                }

                foreach (int trySize in sizes)
                {
                    NativeMethods.SIZE iconSize = new NativeMethods.SIZE(trySize, trySize);

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
                    Bitmap rawBitmap = ConvertHBitmapToArgbBitmap(hBitmap);
                    if (rawBitmap != null)
                    {
                        Debug.WriteLine($"TryExtractIconViaShellItemImageFactory: {rawBitmap.Width}x{rawBitmap.Height} for {filePath}");
                        return rawBitmap;
                    }
                    return null;
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

        /// <summary>
        /// Bitmap에서 BitmapImage로 변환
        /// </summary>
        private static BitmapImage CreateBitmapImageFromBitmap(Bitmap bitmap, DispatcherQueue dispatcher)
        {
            if (bitmap == null) return null;

            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;

                BitmapImage bitmapImage = null;
                using (var resetEvent = new ManualResetEvent(false))
                {
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
                }
                return bitmapImage;
            }
        }

        /// <summary>
        /// 고유한 아이콘 파일 이름 생성
        /// </summary>
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
    }
}
