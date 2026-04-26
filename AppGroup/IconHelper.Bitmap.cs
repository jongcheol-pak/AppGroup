using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AppGroup
{
    /// <summary>
    /// IconHelper - 비트맵 변환/크롭/처리 관련 partial class
    /// 아이콘 변환, 크롭, 흑백 변환 등 비트맵 처리 기능을 포함합니다.
    /// </summary>
    public partial class IconHelper
    {
        /// <summary>
        /// .url 파일 아이콘 경로 추출
        /// </summary>
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

        /// <summary>
        /// 원본 아이콘 파일 찾기
        /// </summary>
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

        /// <summary>
        /// PNG 아이콘을 흑백(그레이스케일)으로 변환
        /// </summary>
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
                using (var originalBitmap = new Bitmap(originalIconPath))
                {
                    Console.WriteLine($"PNG loaded: {originalBitmap.Width}x{originalBitmap.Height}");

                    // LockBits를 사용한 고성능 흑백 변환 (GetPixel/SetPixel 대신)
                    Bitmap bwBitmap = null;
                    try
                    {
                        // 원본 비트맵 데이터 직접 접근을 위한 LockBits
                        var rect = new Rectangle(0, 0, originalBitmap.Width, originalBitmap.Height);
                        var originalData = originalBitmap.LockBits(
                            rect,
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format32bppArgb);

                        try
                        {
                            // 결과 비트맵 생성 및 LockBits
                            bwBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, PixelFormat.Format32bppArgb);
                            var bwData = bwBitmap.LockBits(
                                rect,
                                ImageLockMode.WriteOnly,
                                PixelFormat.Format32bppArgb);

                            try
                            {
                                // unsafe 블록에서 포인터 사용으로 빠른 픽셀 접근
                                unsafe
                                {
                                    byte* originalPtr = (byte*)originalData.Scan0;
                                    byte* bwPtr = (byte*)bwData.Scan0;
                                    int stride = originalData.Stride;
                                    int height = originalData.Height;
                                    int width = originalData.Width;

                                    for (int y = 0; y < height; y++)
                                    {
                                        for (int x = 0; x < width; x++)
                                        {
                                            int offset = y * stride + x * 4;

                                            // B, G, R, A 순서 (32bpp ARGB)
                                            byte b = originalPtr[offset];
                                            byte g = originalPtr[offset + 1];
                                            byte r = originalPtr[offset + 2];
                                            byte a = originalPtr[offset + 3];

                                            // 휘도 공식을 사용하여 그레이스케일로 변환
                                            int grayValue = (int)(r * 0.299 + g * 0.587 + b * 0.114);

                                            // 투명도를 위해 원래의 알파 채널 유지
                                            bwPtr[offset] = (byte)grayValue;     // B
                                            bwPtr[offset + 1] = (byte)grayValue; // G
                                            bwPtr[offset + 2] = (byte)grayValue; // R
                                            bwPtr[offset + 3] = a;               // A
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                bwBitmap.UnlockBits(bwData);
                            }
                        }
                        finally
                        {
                            originalBitmap.UnlockBits(originalData);
                        }

                        // PNG로 저장
                        pngPath = Path.Combine(directory, $"{filenameWithoutExtension}_bw.png");
                        bwBitmap.Save(pngPath, ImageFormat.Png);
                        Console.WriteLine($"B&W PNG saved to: {pngPath}");
                    }
                    finally
                    {
                        bwBitmap?.Dispose();
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
                Bitmap cropped = new Bitmap(size, size, PixelFormat.Format32bppArgb);
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
        /// 이미지를 ICO 파일로 변환
        /// </summary>
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
                using (Image originalImage = Image.FromStream(imageStream))
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
                            using (Bitmap bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
                            {
                                using (Graphics g = Graphics.FromImage(bitmap))
                                {
                                    // 더 나은 아이콘 품질을 위해 고품질 렌더링 설정
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                                    // 투명 배경으로 초기화
                                    g.Clear(Color.Transparent);

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
                                    bitmap.Save(ms, ImageFormat.Png);
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
    }
}
