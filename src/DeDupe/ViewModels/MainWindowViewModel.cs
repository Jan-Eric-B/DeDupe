using CommunityToolkit.Mvvm.Input;

namespace DeDupe.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private bool _isInManagementMode = false;

        public bool IsInManagementMode
        {
            get => _isInManagementMode;
            set => SetProperty(ref _isInManagementMode, value);
        }

        public bool IsInConfigurationMode => !IsInManagementMode;

        public MainWindowViewModel()
        {
        }

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