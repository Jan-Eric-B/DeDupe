using CommunityToolkit.Mvvm.Input;
using DeDupe.Services;
using System;

namespace DeDupe.ViewModels
{
    public partial class PageViewModelBase : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private bool _disposed = false;

        protected int StepIndex { get; set; }

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
        public virtual bool CanNavigateToPrevious => StepIndex > 0;

        public RelayCommand<int> NavigateToStepCommand { get; }
        public RelayCommand NavigateToNextCommand { get; }
        public RelayCommand NavigateToPreviousCommand { get; }

        public PageViewModelBase(int stepIndex = 0)
        {
            _navigationService = App.Current.GetService<INavigationService>();
            StepIndex = stepIndex;

            NavigateToStepCommand = new RelayCommand<int>(NavigateToStep);
            NavigateToNextCommand = new RelayCommand(NavigateToNext, () => CanNavigateToNext);
            NavigateToPreviousCommand = new RelayCommand(NavigateToPrevious, () => CanNavigateToPrevious);

            _isComplete = false;
        }

        [RelayCommand]
        private void Complete()
        {
            IsComplete = true;
        }

        [RelayCommand]
        private void UndoComplete()
        {
            IsComplete = false;
        }

        #region Navigation

        protected void NavigateToStep(int stepIndex)
        {
            _navigationService.NavigateToStep(stepIndex);
        }

        private void NavigateToNext()
        {
            _navigationService.NavigateToStep(StepIndex + 1);
        }

        private void NavigateToPrevious()
        {
            _navigationService.NavigateToStep(StepIndex - 1);
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
                    // Dispose managed resources here
                }

                _disposed = true;
            }
        }

        #endregion IDisposable
    }
}