using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.ViewModels
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = "Settings";

        public SettingsWindowViewModel()
        {
        }
    }
}