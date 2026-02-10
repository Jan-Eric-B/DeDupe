using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DeDupe.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInConfigurationMode))]
        public partial bool IsInManagementMode { get; set; }

        public bool IsInConfigurationMode => !IsInManagementMode;

        public MainWindowViewModel()
        {
            Title = "DeDupe";
        }

        [RelayCommand]
        private void StartManagementMode()
        {
            IsInManagementMode = true;
        }

        [RelayCommand]
        private void BackToConfiguration()
        {
            IsInManagementMode = false;
        }
    }
}