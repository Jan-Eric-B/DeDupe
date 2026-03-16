using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Localization;

namespace DeDupe.ViewModels
{
    public partial class ViewModelBase(ILocalizer localizer) : ObservableObject
    {
        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string Status { get; set; } = string.Empty;

        protected ILocalizer Localizer { get; } = localizer;

        protected string L(string key) => Localizer.GetLocalizedString(key) ?? key;

        protected string L(string key, params object[] args) => string.Format(Localizer?.GetLocalizedString(key) ?? key, args);
    }
}