using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using AppGroup.Models;

namespace AppGroup.ViewModels {
    public partial class PopupWindowViewModel : ObservableObject {
        private string _headerText = string.Empty;
        private Visibility _headerVisibility = Visibility.Collapsed;
        private Visibility _editButtonVisibility = Visibility.Visible;
        private Thickness _scrollMargin = new Thickness(0, 5, 0, 5);

        public ObservableCollection<PopupItem> PopupItems { get; } = new ObservableCollection<PopupItem>();

        public string HeaderText {
            get => _headerText;
            set => SetProperty(ref _headerText, value);
        }

        public Visibility HeaderVisibility {
            get => _headerVisibility;
            set => SetProperty(ref _headerVisibility, value);
        }

        public Visibility EditButtonVisibility {
            get => _editButtonVisibility;
            set => SetProperty(ref _editButtonVisibility, value);
        }

        public Thickness ScrollMargin {
            get => _scrollMargin;
            set => SetProperty(ref _scrollMargin, value);
        }
    }
}
