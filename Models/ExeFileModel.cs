using CommunityToolkit.Mvvm.ComponentModel;

namespace AppGroup.Models {
    /// <summary>
    /// 항목 유형: App(앱), Folder(폴더), Web(웹)
    /// </summary>
    public enum ItemType {
        App,
        Folder,
        Web
    }

    public partial class ExeFileModel : ObservableObject {
        private string _fileName = string.Empty;
        private string _filePath = string.Empty;
        private string _icon = string.Empty;
        private string _tooltip = string.Empty;
        private string _args = string.Empty;
        private string _iconPath = string.Empty;
        private ItemType _itemType = ItemType.App;

        public string FileName {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string FilePath {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string Icon {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string Tooltip {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        public string Args {
            get => _args;
            set => SetProperty(ref _args, value);
        }

        public string IconPath {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        /// <summary>
        /// 항목 유형: App(앱), Folder(폴더), Web(웹)
        /// </summary>
        public ItemType ItemType {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }
    }
}
