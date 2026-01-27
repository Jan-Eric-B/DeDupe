using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace DeDupe.ViewModels
{
    public partial class PageViewModelBase : ViewModelBase, IDisposable
    {
        private bool _disposed = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanNavigateToNext))]
        [NotifyCanExecuteChangedFor(nameof(NavigateToNextCommand))]
        public partial bool IsComplete { get; set; }

        public virtual bool CanNavigateToNext => IsComplete;
        public RelayCommand NavigateToNextCommand { get; }

        public PageViewModelBase()
        {
            NavigateToNextCommand = new RelayCommand(NavigateToNext, () => CanNavigateToNext);

            IsComplete = false;
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