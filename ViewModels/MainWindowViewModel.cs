using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using AppGroup.Models;

namespace AppGroup.ViewModels {
    public partial class MainWindowViewModel : ObservableObject {
        private string _searchText = string.Empty;
        private string _groupsCountText = "그룹";
        private Visibility _emptyViewVisibility = Visibility.Visible;
        private string _startMenuSearchText = string.Empty;
        private string _startMenuCountText = "폴더";
        private Visibility _startMenuEmptyViewVisibility = Visibility.Visible;

        public ObservableCollection<GroupItem> GroupItems { get; } = new ObservableCollection<GroupItem>();
        public ObservableCollection<GroupItem> FilteredGroupItems { get; } = new ObservableCollection<GroupItem>();
        public ObservableCollection<StartMenuItem> StartMenuItems { get; } = new ObservableCollection<StartMenuItem>();
        public ObservableCollection<StartMenuItem> FilteredStartMenuItems { get; } = new ObservableCollection<StartMenuItem>();

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

        public string StartMenuSearchText {
            get => _startMenuSearchText;
            set => SetProperty(ref _startMenuSearchText, value);
        }

        public string StartMenuCountText {
            get => _startMenuCountText;
            set => SetProperty(ref _startMenuCountText, value);
        }

        public Visibility StartMenuEmptyViewVisibility {
            get => _startMenuEmptyViewVisibility;
            set => SetProperty(ref _startMenuEmptyViewVisibility, value);
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

        /// <summary>
        /// 시작 메뉴 폴더 목록에 필터를 적용합니다
        /// </summary>
        public void ApplyStartMenuFilter() {
            FilteredStartMenuItems.Clear();
            var normalizedQuery = (StartMenuSearchText ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(normalizedQuery)) {
                foreach (var item in StartMenuItems) {
                    FilteredStartMenuItems.Add(item);
                }
            }
            else {
                foreach (var item in StartMenuItems) {
                    if (item.FolderName.ToLowerInvariant().Contains(normalizedQuery) ||
                        item.FolderPath.ToLowerInvariant().Contains(normalizedQuery)) {
                        FilteredStartMenuItems.Add(item);
                    }
                }
            }
        }
    }
}
