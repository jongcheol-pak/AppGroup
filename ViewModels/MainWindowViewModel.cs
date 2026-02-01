using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using AppGroup.Models;

namespace AppGroup.ViewModels {
    public partial class MainWindowViewModel : ObservableObject {
        private string _searchText = string.Empty;
        private string _groupsCountText = "그룹";
        private Visibility _emptyViewVisibility = Visibility.Visible;

        public ObservableCollection<GroupItem> GroupItems { get; } = new ObservableCollection<GroupItem>();
        public ObservableCollection<GroupItem> FilteredGroupItems { get; } = new ObservableCollection<GroupItem>();

        public string SearchText {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public string GroupsCountText {
            get => _groupsCountText;
            set => SetProperty(ref _groupsCountText, value);
        }

        public Visibility EmptyViewVisibility {
            get => _emptyViewVisibility;
            set => SetProperty(ref _emptyViewVisibility, value);
        }

        public void ApplyFilter() {
            FilteredGroupItems.Clear();
            var normalizedQuery = (SearchText ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(normalizedQuery)) {
                foreach (var item in GroupItems) {
                    FilteredGroupItems.Add(item);
                }
            }
            else {
                foreach (var item in GroupItems) {
                    if (item.GroupName.ToLowerInvariant().Contains(normalizedQuery)) {
                        FilteredGroupItems.Add(item);
                    }
                }
            }
        }
    }
}
