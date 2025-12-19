using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Models.Ui;
using DeDupe.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;

namespace DeDupe.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        private int _selectedStepIndex = 0;
        private bool _isInManagementMode = false;

        public ObservableCollection<StepItem> Steps { get; }

        public int SelectedStepIndex
        {
            get => _selectedStepIndex;
            set
            {
                if (SetProperty(ref _selectedStepIndex, value))
                {
                    UpdateStepStates();
                }
            }
        }

        public bool IsInManagementMode
        {
            get => _isInManagementMode;
            set => SetProperty(ref _isInManagementMode, value);
        }

        public bool IsInConfigurationMode => !IsInManagementMode;

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            Steps =
            [
                new() { StepNumber = 1, Title = "Files", Description = "Select Sources", IsEnabled = true, IsCurrent = true },
                new() { StepNumber = 2, Title = "Pre-Processing", Description = "Configure Images", IsEnabled = false },
                new() { StepNumber = 3, Title = "Model", Description = "Configure Model", IsEnabled = false }
            ];

            SubscribeToPageCompletion();
        }

        [RelayCommand]
        private void NavigateToTab(int stepIndex)
        {
            if (stepIndex >= 0 && stepIndex <= 2)
            {
                SelectedStepIndex = stepIndex;
            }
        }

        [RelayCommand]
        private void NavigateToStep(int stepNumber)
        {
            // Convert 1-based step number to 0-based index
            int stepIndex = stepNumber - 1;
            if (stepIndex >= 0 && stepIndex < Steps.Count && Steps[stepIndex].IsEnabled)
            {
                SelectedStepIndex = stepIndex;
            }
        }

        [RelayCommand]
        private void StartManagementMode()
        {
            IsInManagementMode = true;
            OnPropertyChanged(nameof(IsInConfigurationMode));
        }

        [RelayCommand]
        private void BackToConfiguration()
        {
            IsInManagementMode = false;
            OnPropertyChanged(nameof(IsInConfigurationMode));
        }

        private void UpdateStepStates()
        {
            // Update step
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].IsCurrent = i == SelectedStepIndex;
            }
        }

        private void SubscribeToPageCompletion()
        {
            // Subscribe to IsComplete changes
            FileInputViewModel? fileInputViewModel = _serviceProvider.GetService<FileInputViewModel>();
            PreProcessingViewModel? preProcessingViewModel = _serviceProvider.GetService<PreProcessingViewModel>();
            ModelConfigurationViewModel? modelConfigurationViewModel = _serviceProvider.GetService<ModelConfigurationViewModel>();

            fileInputViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PageViewModelBase.IsComplete))
                {
                    Steps[0].IsCompleted = fileInputViewModel.IsComplete;
                    Steps[1].IsEnabled = fileInputViewModel.IsComplete;
                }
            };

            preProcessingViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PageViewModelBase.IsComplete))
                {
                    Steps[1].IsCompleted = preProcessingViewModel.IsComplete;
                    Steps[2].IsEnabled = preProcessingViewModel.IsComplete;
                }
            };

            modelConfigurationViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PageViewModelBase.IsComplete))
                {
                    Steps[2].IsCompleted = modelConfigurationViewModel.IsComplete;
                }
            };
        }
    }
}