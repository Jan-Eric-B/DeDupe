using DeDupe.ViewModels;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace DeDupe.Views.Pages
{
    public sealed partial class ManagementPage : Page
    {
        public ManagementViewModel ViewModel { get; }

        public ManagementPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ManagementViewModel>();
            DataContext = ViewModel;

            // Subscribe to property changes to handle animations
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagementViewModel.SelectedCluster))
            {
                HandleClusterSelectionChanged();
            }
        }

        private void BackToConfiguration_Click(object sender, RoutedEventArgs e)
        {
            MainWindowViewModel? mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.BackToConfigurationCommand.Execute(null);
        }

        private void ClusterGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // The GridView's SelectedItem binding will handle setting ViewModel.SelectedCluster
            // The PropertyChanged event will trigger the animation
        }

        private void CloseGroupContentPanel_Click(object sender, RoutedEventArgs e)
        {
            // Clear the selected cluster
            ViewModel.SelectedCluster = null;
        }

        private void HandleClusterSelectionChanged()
        {
            if (ViewModel.SelectedCluster != null)
            {
                // Adjust grid columns to show split view
                SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Auto);

                // Reset to a specific pixel width to ensure consistent opening behavior
                // and set a minimum width to prevent the panel from being dragged too small
                SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(450, GridUnitType.Pixel);
                SplitViewGrid.ColumnDefinitions[2].MinWidth = 300;

                // Show the detail panel with animation
                ShowGroupContentPanelStoryboard.Begin();
            }
            else
            {
                // Clear the minimum width constraint
                SplitViewGrid.ColumnDefinitions[2].MinWidth = 0;

                // Adjust grid columns to hide right panel (left panel takes full width)
                SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0);
                SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(0);

                // Hide the detail panel with animation
                HideGroupContentPanelStoryboard.Begin();
            }
        }
    }
}