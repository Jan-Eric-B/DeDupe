using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Localization;
using System;

namespace DeDupe.ViewModels
{
    public partial class PageViewModelBase : ViewModelBase, IDisposable
    {
        private bool _disposed;
        private readonly Action? _navigateToNextAction;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanNavigateToNext))]
        [NotifyCanExecuteChangedFor(nameof(NavigateToNextCommand))]
        public partial bool IsComplete { get; set; }

        public virtual bool CanNavigateToNext => IsComplete;

        public RelayCommand NavigateToNextCommand { get; }

        public PageViewModelBase(ILocalizer localizer, Action? navigateToNextAction = null) : base(localizer)
        {
            _navigateToNextAction = navigateToNextAction;
            NavigateToNextCommand = new RelayCommand(NavigateToNext, () => CanNavigateToNext);
        }

        public static void DispatchToUI(Action action)
        {
            if (App.Window?.DispatcherQueue is { } dispatcher)
            {
                dispatcher.TryEnqueue(() => action());
            }
        }

        #region Navigation

        protected virtual void NavigateToNext()
        {
            _navigateToNextAction?.Invoke();
        }

        #endregion Navigation

        #region Cleanup

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
                }
                _disposed = true;
            }
        }

        #endregion Cleanup
    }
}