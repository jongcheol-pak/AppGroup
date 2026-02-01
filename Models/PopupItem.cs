using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AppGroup.Models {
    public partial class PopupItem : ObservableObject {
        private string _path = string.Empty;
        private string _name = string.Empty;
        private string _toolTip = string.Empty;
        private string _args = string.Empty;
        private string _iconPath = string.Empty;
        private string _customIconPath = string.Empty;
        private BitmapImage? _icon;
        private ItemType _itemType = ItemType.App;

        public string Path {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public string Name {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ToolTip {
            get => _toolTip;
            set => SetProperty(ref _toolTip, value);
        }

        public string Args {
            get => _args;
            set => SetProperty(ref _args, value);
        }

        public string IconPath {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public string CustomIconPath {
            get => _customIconPath;
            set => SetProperty(ref _customIconPath, value);
        }

        public BitmapImage? Icon {
            get => _icon;
            set => SetProperty(ref _icon, value);
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
