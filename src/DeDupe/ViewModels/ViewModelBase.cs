using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string Status { get; set; } = string.Empty;
    }
}