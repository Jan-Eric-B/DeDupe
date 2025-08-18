using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        // TODO Integrate
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _title = string.Empty;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}