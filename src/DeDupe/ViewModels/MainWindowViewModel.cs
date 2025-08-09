using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DeDupe.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private int _selectedStepIndex = 0;

        public int SelectedStepIndex
        {
            get => _selectedStepIndex;
            set => SetProperty(ref _selectedStepIndex, value);
        }

        public MainWindowViewModel()
        {
        }

        [RelayCommand]
        private void NavigateToTab(int stepIndex)
        {
            if (stepIndex >= 0 && stepIndex <= 4)
            {
                SelectedStepIndex = stepIndex;
            }
        }
    }
}