using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using AppGroup.Models;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace AppGroup
{
    /// <summary>
    /// IconHelper - 그리드 아이콘 생성 관련 partial class
    /// 여러 앱 아이콘을 조합하여 그리드 형태의 아이콘을 생성하는 기능을 담당합니다.
    /// </summary>
    public partial class IconHelper
    {
        /// <summary>
        /// 팝업용 그리드 아이콘 생성
        /// </summary>
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

        /// <summary>
        /// 그리드 아이콘 생성 및 미리보기 표시
        /// </summary>
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

                                    dynamic shell = null;
                                    dynamic shortcut = null;
                                    try
                                    {
                                        shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                                        shortcut = shell.CreateShortcut(filePath);
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
                                    finally
                                    {
                                        if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                                        if (shell != null) Marshal.ReleaseComObject(shell);
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
