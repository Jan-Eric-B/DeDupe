using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace DeDupe.ViewModels.Settings
{
    public abstract partial class SettingsPageViewModelBase : ViewModelBase
    {
        public virtual void OnNavigatedTo()
        {
        }

        public virtual void OnNavigatedFrom()
        {
        }

        [RelayCommand]
        protected static async Task OpenLinkAsync(string url)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return;
            }

            await Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}