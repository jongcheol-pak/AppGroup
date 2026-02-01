using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppGroup.Models {
    public partial class GroupItem : ObservableObject {
        private string _groupName = string.Empty;
        private string _groupIcon = string.Empty;
        private List<string> _pathIcons = new();
        private int _additionalIconsCount;

        public int GroupId { get; set; }

        public string GroupName {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public string GroupIcon {
            get => _groupIcon;
            set => SetProperty(ref _groupIcon, value);
        }

        public List<string> PathIcons {
            get => _pathIcons;
            set {
                if (SetProperty(ref _pathIcons, value)) {
                    OnPropertyChanged(nameof(AdditionalIconsText));
                }
            }
        }

        public int AdditionalIconsCount {
            get => _additionalIconsCount;
            set {
                if (SetProperty(ref _additionalIconsCount, value)) {
                    OnPropertyChanged(nameof(AdditionalIconsText));
                }
            }
        }

        public string AdditionalIconsText => AdditionalIconsCount > 0 ? $"+{AdditionalIconsCount}" : string.Empty;

        public Dictionary<string, string> Tooltips { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> CustomIcons { get; set; } = new Dictionary<string, string>();
    }
}
