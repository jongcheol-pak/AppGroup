using System;
using System.Runtime.InteropServices;

namespace AppGroup
{
    /// <summary>
    /// NativeMethods - 쉘/아이콘 API 관련 partial class
    /// IShellItem, IImageList, SHGetImageList 등 쉘 아이콘 추출 API를 담당합니다.
    /// </summary>
    public static partial class NativeMethods
    {
        #region 상수

        // SHGetFileInfo 상수
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_SYSICONINDEX = 0x000004000;

        // SHIL 상수 (시스템 이미지 리스트 크기)
        public const int SHIL_LARGE = 0;      // 32x32
        public const int SHIL_SMALL = 1;      // 16x16
        public const int SHIL_EXTRALARGE = 2; // 48x48
        public const int SHIL_SYSSMALL = 3;   // 시스템 작은 아이콘
        public const int SHIL_JUMBO = 4;      // 256x256

        // ILD 플래그 (IImageList.GetIcon에서 사용)
        public const uint ILD_NORMAL = 0x00000000;
        public const uint ILD_TRANSPARENT = 0x00000001;
        public const uint ILD_IMAGE = 0x00000020;

        public static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        public static readonly Guid IShellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        public static readonly Guid IShellItemImageFactoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        // AppsFolder 알려진 폴더 ID
        public static readonly Guid FOLDERID_AppsFolder = new Guid("1e87508d-89c2-42f0-8a7e-645a0f50ca58");

        #endregion

        #region 구조체

        /// <summary>
        /// SHFILEINFO 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        /// <summary>
        /// SIZE 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy)
            {
                this.cx = cx;
                this.cy = cy;
            }
        }

        /// <summary>
        /// BITMAP 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        /// <summary>
        /// BITMAPINFOHEADER 구조체 (top-down/bottom-up 비트맵 방향 확인용)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;  // 양수: bottom-up, 음수: top-down
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        /// <summary>
        /// DIBSECTION 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DIBSECTION
        {
            public BITMAP dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfields0;
            public uint dsBitfields1;
            public uint dsBitfields2;
            public IntPtr dshSection;
            public uint dsOffset;
        }

        /// <summary>
        /// IMAGELISTDRAWPARAMS 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public uint fStyle;
            public uint dwRop;
            public uint fState;
            public uint Frame;
            public int crEffect;
        }

        /// <summary>
        /// IMAGEINFO 구조체
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGEINFO
        {
            public IntPtr hbmImage;
            public IntPtr hbmMask;
            public int Unused1;
            public int Unused2;
            public RECT rcImage;
        }

        #endregion

        #region 열거형

        /// <summary>
        /// SIGDN 열거형
        /// </summary>
        public enum SIGDN : uint
        {
            NORMALDISPLAY = 0x00000000,
            PARENTRELATIVEPARSING = 0x80018001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000,
            PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            PARENTRELATIVE = 0x80080001
        }

        /// <summary>
        /// SIIGBF 플래그
        /// </summary>
        [Flags]
        public enum SIIGBF : uint
        {
            SIIGBF_RESIZETOFIT = 0x00000000,
            SIIGBF_BIGGERSIZEOK = 0x00000001,
            SIIGBF_MEMORYONLY = 0x00000002,
            SIIGBF_ICONONLY = 0x00000004,
            SIIGBF_THUMBNAILONLY = 0x00000008,
            SIIGBF_INCACHEONLY = 0x00000010,
            SIIGBF_CROPTOSQUARE = 0x00000020,
            SIIGBF_WIDETHUMBNAILS = 0x00000040,
            SIIGBF_ICONBACKGROUND = 0x00000080,
            SIIGBF_SCALEUP = 0x00000100
        }

        #endregion

        #region P/Invoke 선언

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
           IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);

        [DllImport("gdi32.dll")]
        public static extern int GetBitmapBits(IntPtr hbmp, int cbBuffer, [Out] byte[] lpvBits);

        [DllImport("gdi32.dll")]
        public static extern int GetObject(IntPtr hObject, int nCount, ref DIBSECTION lpObject);

        // shell:AppsFolder 항목에서 아이콘을 추출하기 위한 Shell API
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        // IShellItemImageFactory를 직접 요청하는 오버로드
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("shell32.dll")]
        public static extern int SHGetKnownFolderIDList(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppidl);

        [DllImport("shell32.dll")]
        public static extern int SHBindToObject(
            IntPtr pShellFolder,
            IntPtr pidl,
            IntPtr pbc,
            ref Guid riid,
            out object ppv);

        [DllImport("shell32.dll")]
        public static extern int SHCreateItemFromIDList(
            IntPtr pidl,
            ref Guid riid,
            out IShellItemImageFactory ppv);

        [DllImport("shell32.dll")]
        public static extern IntPtr ILCombine(IntPtr pidl1, IntPtr pidl2);

        // SHGetImageList: 시스템 이미지 리스트 획득 (shell32.dll ordinal #727)
        [DllImport("shell32.dll", EntryPoint = "#727")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        #endregion

        #region COM 인터페이스

        /// <summary>
        /// IShellItem 인터페이스
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        public interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        /// <summary>
        /// IShellItemImageFactory 인터페이스
        /// </summary>
        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        /// <summary>
        /// IShellFolder 인터페이스
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214E6-0000-0000-C000-000000000046")]
        public interface IShellFolder
        {
            [PreserveSig]
            int ParseDisplayName(
                IntPtr hwnd,
                IntPtr pbc,
                [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
                out uint pchEaten,
                out IntPtr ppidl,
                ref uint pdwAttributes);

            [PreserveSig]
            int EnumObjects(
                IntPtr hwnd,
                uint grfFlags,
                out IntPtr ppenumIDList);

            [PreserveSig]
            int BindToObject(
                IntPtr pidl,
                IntPtr pbc,
                ref Guid riid,
                out object ppv);

            [PreserveSig]
            int BindToStorage(
                IntPtr pidl,
                IntPtr pbc,
                ref Guid riid,
                out object ppv);

            [PreserveSig]
            int CompareIDs(
                IntPtr lParam,
                IntPtr pidl1,
                IntPtr pidl2);

            [PreserveSig]
            int CreateViewObject(
                IntPtr hwndOwner,
                ref Guid riid,
                out object ppv);

            [PreserveSig]
            int GetAttributesOf(
                uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                ref uint rgfInOut);

            [PreserveSig]
            int GetUIObjectOf(
                IntPtr hwndOwner,
                uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                ref Guid riid,
                IntPtr rgfReserved,
                out object ppv);

            [PreserveSig]
            int GetDisplayNameOf(
                IntPtr pidl,
                uint uFlags,
                out IntPtr pName);

            [PreserveSig]
            int SetNameOf(
                IntPtr hwnd,
                IntPtr pidl,
                [MarshalAs(UnmanagedType.LPWStr)] string pszName,
                uint uFlags,
                out IntPtr ppidlOut);
        }

        /// <summary>
        /// IExtractIconW 인터페이스
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214FA-0000-0000-C000-000000000046")]
        public interface IExtractIconW
        {
            int GetIconLocation(
                uint uFlags,
                [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder szIconFile,
                int cchMax,
                out int piIndex,
                out uint pwFlags);

            int Extract(
                [MarshalAs(UnmanagedType.LPWStr)] string pszFile,
                uint nIconIndex,
                out IntPtr phiconLarge,
                out IntPtr phiconSmall,
                uint nIconSize);
        }

        /// <summary>
        /// IImageList COM 인터페이스 (시스템 이미지 리스트에서 아이콘 추출)
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        public interface IImageList
        {
            [PreserveSig]
            int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);

            [PreserveSig]
            int ReplaceIcon(int i, IntPtr hicon, out int pi);

            [PreserveSig]
            int SetOverlayImage(int iImage, int iOverlay);

            [PreserveSig]
            int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

            [PreserveSig]
            int AddMasked(IntPtr hbmImage, int crMask, out int pi);

            [PreserveSig]
            int Draw(ref IMAGELISTDRAWPARAMS pimldp);

            [PreserveSig]
            int Remove(int i);

            [PreserveSig]
            int GetIcon(int i, uint flags, out IntPtr picon);

            [PreserveSig]
            int GetImageInfo(int i, out IMAGEINFO pImageInfo);

            [PreserveSig]
            int Copy(int iDst, IImageList punkSrc, int iSrc, uint uFlags);

            [PreserveSig]
            int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int Clone(ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetImageRect(int i, out RECT prc);

            [PreserveSig]
            int GetIconSize(out int cx, out int cy);

            [PreserveSig]
            int SetIconSize(int cx, int cy);

            [PreserveSig]
            int GetImageCount(out int pi);

            [PreserveSig]
            int SetImageCount(uint uNewCount);

            [PreserveSig]
            int SetBkColor(int clrBk, out int pclr);

            [PreserveSig]
            int GetBkColor(out int pclr);

            [PreserveSig]
            int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);

            [PreserveSig]
            int EndDrag();

            [PreserveSig]
            int DragEnter(IntPtr hwndLock, int x, int y);

            [PreserveSig]
            int DragLeave(IntPtr hwndLock);

            [PreserveSig]
            int DragMove(int x, int y);

            [PreserveSig]
            int SetDragCursorImage(IImageList punk, int iDrag, int dxHotspot, int dyHotspot);

            [PreserveSig]
            int DragShowNolock(bool fShow);

            [PreserveSig]
            int GetDragImage(out POINT ppt, out POINT pptHotspot, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetItemFlags(int i, out uint dwFlags);

            [PreserveSig]
            int GetOverlayImage(int iOverlay, out int piIndex);
        }

        #endregion
    }
}
