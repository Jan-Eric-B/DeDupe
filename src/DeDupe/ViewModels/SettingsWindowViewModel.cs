using DeDupe.Localization;

namespace DeDupe.ViewModels
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        public SettingsWindowViewModel(ILocalizer localizer) : base(localizer)
        {
            Title = "Settings_WindowTitle";
        }
    }
}