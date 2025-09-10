using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        #region Fields

        private string _title = string.Empty;

        private bool _isBusy;

        private string _status = string.Empty;

        #endregion Fields

        #region Properties

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        #endregion Properties
    }
}