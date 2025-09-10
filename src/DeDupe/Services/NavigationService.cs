using DeDupe.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DeDupe.Services
{
    internal class NavigationService(IServiceProvider serviceProvider) : INavigationService
    {
        #region Properties

        private readonly IServiceProvider _serviceProvider = serviceProvider;

        private const int StepIndexMin = 0;
        private const int StepIndexMax = 3;

        #endregion Properties

        #region Methods

        public void NavigateToStep(int stepIndex)
        {
            int clampedIndex = Math.Clamp(stepIndex, StepIndexMin, StepIndexMax);

            MainWindowViewModel? mainViewModel = _serviceProvider.GetService<MainWindowViewModel>();
            mainViewModel?.NavigateToTabCommand.Execute(clampedIndex);
        }

        #endregion Methods
    }
}