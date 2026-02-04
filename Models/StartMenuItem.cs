using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppGroup.Models
{
    /// <summary>
    /// 시작 메뉴 폴더 항목을 나타내는 모델 클래스
    /// </summary>
    public partial class StartMenuItem : ObservableObject
    {
        private string _folderName = string.Empty;
        private string _folderPath = string.Empty;
        private string _folderIcon = string.Empty;

        /// <summary>
        /// 폴더 ID (고유 식별자)
        /// </summary>
        public int FolderId { get; set; }

        /// <summary>
        /// 폴더 이름
        /// </summary>
        public string FolderName
        {
            get => _folderName;
            set => SetProperty(ref _folderName, value);
        }

        /// <summary>
        /// 폴더 전체 경로
        /// </summary>
        public string FolderPath
        {
            get => _folderPath;
            set => SetProperty(ref _folderPath, value);
        }

        /// <summary>
        /// 폴더 아이콘 경로
        /// </summary>
        public string FolderIcon
        {
            get => _folderIcon;
            set => SetProperty(ref _folderIcon, value);
        }
    }
}
