using CommunityToolkit.Mvvm.Input;
using System;

namespace DeDupe.ViewModels
{
    public partial class PageViewModelBase : ViewModelBase, IDisposable
    {
        private bool _disposed = false;

        private bool _isComplete;

        public bool IsComplete
        {
            get => _isComplete;
            set
            {
                if (SetProperty(ref _isComplete, value))
                {
                    OnPropertyChanged(nameof(CanNavigateToNext));
                    NavigateToNextCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public virtual bool CanNavigateToNext => IsComplete;
        public RelayCommand NavigateToNextCommand { get; }

        public PageViewModelBase(int stepIndex = 0)
        {
            NavigateToNextCommand = new RelayCommand(NavigateToNext, () => CanNavigateToNext);

            _isComplete = false;
        }

        #region Navigation

        protected virtual void NavigateToNext()
        {
            MainWindowViewModel mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.StartManagementModeCommand.Execute(null);
        }

        #endregion Navigation

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO Fill this
                }
                _disposed = true;
            }
        }

        #endregion IDisposable
    }
}