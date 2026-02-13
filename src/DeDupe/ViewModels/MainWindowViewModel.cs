using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace DeDupe.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ILogger<MainWindowViewModel> _logger;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInConfigurationMode))]
        public partial bool IsInManagementMode { get; set; }

        public bool IsInConfigurationMode => !IsInManagementMode;

        public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
        {
            _logger = logger;
            Title = "DeDupe";
        }

        [RelayCommand]
        private void StartManagementMode()
        {
            IsInManagementMode = true;
            LogManagementModeEntered();
        }

        [RelayCommand]
        private void BackToConfiguration()
        {
            IsInManagementMode = false;
            LogConfigurationModeEntered();
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "Management mode entered")]
        private partial void LogManagementModeEntered();

        [LoggerMessage(Level = LogLevel.Debug, Message = "Configuration mode entered")]
        private partial void LogConfigurationModeEntered();

        #endregion Logging
    }
}