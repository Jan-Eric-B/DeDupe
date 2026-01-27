using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DeDupe.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        public partial bool IsInManagementMode { get; set; }

        public bool IsInConfigurationMode => !IsInManagementMode;

        [RelayCommand]
        private void StartManagementMode()
        {
            IsInManagementMode = true;
            OnPropertyChanged(nameof(IsInConfigurationMode));
        }

        [RelayCommand]
        private void BackToConfiguration()
        {
            IsInManagementMode = false;
            OnPropertyChanged(nameof(IsInConfigurationMode));
        }
    }
}