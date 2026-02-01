using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppGroup.Models {
    public partial class InstalledAppModel : ObservableObject {
        private string _displayName = string.Empty;
        private string _executablePath = string.Empty;
        private string _icon = string.Empty;
        private bool _isSelected;

        public string DisplayName {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string ExecutablePath {
            get => _executablePath;
            set => SetProperty(ref _executablePath, value);
        }

        public string Icon {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsSelected {
            get => _isSelected;
            set {
                if (SetProperty(ref _isSelected, value)) {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? SelectionChanged;
    }
}
