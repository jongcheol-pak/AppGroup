using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppGroup.Models;

namespace AppGroup.ViewModels {
    public partial class EditGroupViewModel : ObservableObject {
        private string _groupName = string.Empty;
        private string _applicationCountText = "항목";
        private string _selectedAppsCountText = "0개 선택됨";
        private bool _groupHeaderIsOn;
        private bool _showLabelsIsOn;
        private bool _showGroupEditIsOn;

        public ObservableCollection<ExeFileModel> ExeFiles { get; } = new ObservableCollection<ExeFileModel>();
        public ObservableCollection<InstalledAppModel> InstalledApps { get; } = new ObservableCollection<InstalledAppModel>();
        public List<InstalledAppModel> AllInstalledApps { get; } = new List<InstalledAppModel>();

        public string GroupName {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public string ApplicationCountText {
            get => _applicationCountText;
            set => SetProperty(ref _applicationCountText, value);
        }

        public string SelectedAppsCountText {
            get => _selectedAppsCountText;
            set => SetProperty(ref _selectedAppsCountText, value);
        }

        public bool GroupHeaderIsOn {
            get => _groupHeaderIsOn;
            set => SetProperty(ref _groupHeaderIsOn, value);
        }

        public bool ShowLabelsIsOn {
            get => _showLabelsIsOn;
            set => SetProperty(ref _showLabelsIsOn, value);
        }

        public bool ShowGroupEditIsOn {
            get => _showGroupEditIsOn;
            set => SetProperty(ref _showGroupEditIsOn, value);
        }
    }
}
