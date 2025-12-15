using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;

namespace DeDupe.Views.Pages
{
    public sealed partial class ManagementPage : Page
    {
        public ManagementViewModel ViewModel { get; }

        private bool _isEditingGroupName;
        private bool _isConfirmingRename;

        private double _rightPanelWidthRatio = 0.4;
        private bool _isPanelOpen = false;

        public ManagementPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ManagementViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Subscribe to size changes
            SplitViewGrid.SizeChanged += SplitViewGrid_SizeChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagementViewModel.SelectedCluster))
            {
                HandleClusterSelectionChanged();
            }
        }

        private void SplitViewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isPanelOpen && e.NewSize.Width > 0)
            {
                UpdateRightPanelWidth();
            }
        }

        private void BackToConfiguration_Click(object sender, RoutedEventArgs e)
        {
            MainWindowViewModel? mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.BackToConfigurationCommand.Execute(null);
        }

        private void CloseGroupContentPanel_Click(object sender, RoutedEventArgs e)
        {
            // Exit edit mode
            ExitEditMode(false);

            // Clear selected cluster
            ViewModel.SelectedCluster = null;
        }

        private void HandleClusterSelectionChanged()
        {
            // Exit edit mode
            ExitEditMode(false);

            if (ViewModel.SelectedCluster != null)
            {
                double totalWidth = SplitViewGrid.ActualWidth;

                // Fallback - Grid has no size
                if (totalWidth <= 0)
                {
                    totalWidth = 1000;
                }

                // Width based on stored ratio
                double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

                // Ensure min width of 300 and max of 70% of total
                double minWidth = 300;
                double maxWidth = totalWidth * 0.7;

                // Ensure maxWidth >= minWidth
                if (maxWidth < minWidth)
                {
                    maxWidth = minWidth;
                }

                rightPanelWidth = System.Math.Clamp(rightPanelWidth, minWidth, maxWidth);

                // Set width if panel was closed
                if (!_isPanelOpen)
                {
                    SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Auto);
                    SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
                    SplitViewGrid.ColumnDefinitions[2].MinWidth = minWidth;

                    _isPanelOpen = true;
                }

                ShowGroupContentPanelStoryboard.Begin();
                UpdateSelectAllCheckboxState();
            }
            else
            {
                // Save width ratio
                if (_isPanelOpen && SplitViewGrid.ActualWidth > 0)
                {
                    double currentRightWidth = SplitViewGrid.ColumnDefinitions[2].ActualWidth;
                    if (currentRightWidth > 0)
                    {
                        _rightPanelWidthRatio = currentRightWidth / SplitViewGrid.ActualWidth;
                        _rightPanelWidthRatio = System.Math.Clamp(_rightPanelWidthRatio, 0.2, 0.7);
                    }
                }

                // Clear min width constraint
                SplitViewGrid.ColumnDefinitions[2].MinWidth = 0;

                // Hide right panel
                SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0);
                SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(0);

                _isPanelOpen = false;

                // Hide right panel with animation
                HideGroupContentPanelStoryboard.Begin();
            }
        }

        private void UpdateRightPanelWidth()
        {
            // Keep proportional width on window resize
            if (!_isPanelOpen || SplitViewGrid.ActualWidth <= 0)
            {
                return;
            }

            double totalWidth = SplitViewGrid.ActualWidth;
            double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

            // Ensure min and max constraints
            double minWidth = 300;
            double maxWidth = totalWidth * 0.7;
            rightPanelWidth = System.Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
        }

        #region Selection Handling

        private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCluster == null || SelectAllCheckBox.IsChecked == null)
            {
                return;
            }

            // Update all images selection
            bool newState = SelectAllCheckBox.IsChecked.Value;
            ViewModel.SelectedCluster.SetGroupSelection(newState);
        }

        private void UpdateSelectAllCheckboxState()
        {
            if (ViewModel.SelectedCluster == null)
            {
                return;
            }

            SelectAllCheckBox.IsChecked = ViewModel.SelectedCluster.IsSelected;
        }

        #endregion Selection Handling

        #region Group Name Editing

        private void GroupNameTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            EnterEditMode();
        }

        private void EditGroupNameButton_Click(object sender, RoutedEventArgs e)
        {
            EnterEditMode();
        }

        private void ConfirmRenameButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(true);
        }

        private void CancelRenameButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(false);
        }

        private void GroupNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ExitEditMode(true);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ExitEditMode(false);
                e.Handled = true;
            }
        }

        private void GroupNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Save on lost focus if cancel button not clicked
            if (_isEditingGroupName && !_isConfirmingRename)
            {
                // Small delay to check if focus moved
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isEditingGroupName && !_isConfirmingRename)
                    {
                        // Focus moved
                        ExitEditMode(true);
                    }
                });
            }
        }

        private void EnterEditMode()
        {
            if (_isEditingGroupName || ViewModel.SelectedCluster == null)
            {
                return;
            }

            _isEditingGroupName = true;

            // Set current name
            GroupNameTextBox.Text = ViewModel.SelectedCluster.Name;

            // Switch visibility
            GroupNameDisplayPanel.Visibility = Visibility.Collapsed;
            GroupNameEditPanel.Visibility = Visibility.Visible;

            // Focus TextBox and select text
            GroupNameTextBox.Focus(FocusState.Programmatic);
            GroupNameTextBox.SelectAll();
        }

        private void ExitEditMode(bool save)
        {
            if (!_isEditingGroupName)
            {
                return;
            }

            _isConfirmingRename = true;

            if (save && ViewModel.SelectedCluster != null)
            {
                string newName = GroupNameTextBox.Text?.Trim() ?? string.Empty;

                // Only update if name is not empty and different
                if (!string.IsNullOrWhiteSpace(newName) && newName != ViewModel.SelectedCluster.Name)
                {
                    if (FolderNameValidator.Validate(newName))
                    {
                        ViewModel.SelectedCluster.Name = newName;
                    }
                    else
                    {
                        ViewModel.SelectedCluster.Name = FolderNameValidator.Sanitize(newName);
                    }
                }
            }

            // Switch visibility back
            GroupNameEditPanel.Visibility = Visibility.Collapsed;
            GroupNameDisplayPanel.Visibility = Visibility.Visible;

            _isEditingGroupName = false;
            _isConfirmingRename = false;
        }

        #endregion Group Name Editing
    }
}