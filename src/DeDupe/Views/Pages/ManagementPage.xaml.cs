using DeDupe.Models.Analysis;
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
        #region Fields

        private bool _isEditingGroupName;
        private bool _isConfirmingRename;
        private double _rightPanelWidthRatio = 0.4;
        private bool _isPanelOpen = false;

        private ImageCluster? _subscribedCluster;

        #endregion Fields

        #region Properties

        public ManagementViewModel ViewModel { get; }

        #endregion Properties

        #region Constructor

        public ManagementPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ManagementViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            SplitViewGrid.SizeChanged += SplitViewGrid_SizeChanged;
        }

        #endregion Constructor

        #region ViewModel Event Handlers

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagementViewModel.SelectedCluster))
            {
                HandleClusterSelectionChanged();
            }
        }

        #endregion ViewModel Event Handlers

        #region Panel Management

        private void SplitViewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isPanelOpen && e.NewSize.Width > 0)
            {
                UpdateRightPanelWidth();
            }
        }

        private void HandleClusterSelectionChanged()
        {
            ExitEditMode(false);

            // Unsubscribe from cluster
            UnsubscribeFromCluster();

            if (ViewModel.SelectedCluster != null)
            {
                // Subscribe to cluster events
                SubscribeToCluster(ViewModel.SelectedCluster);

                SetupPanelForCluster();

                UpdateSelection();

                ShowGroupContentPanelStoryboard.Begin();
            }
            else
            {
                // Save width ratio before closing
                SavePanelWidthRatio();

                HideRightPanel();

                HideGroupContentPanelStoryboard.Begin();
            }
        }

        private void SubscribeToCluster(ImageCluster cluster)
        {
            _subscribedCluster = cluster;
            cluster.PropertyChanged += OnClusterPropertyChanged;
        }

        private void UnsubscribeFromCluster()
        {
            if (_subscribedCluster != null)
            {
                _subscribedCluster.PropertyChanged -= OnClusterPropertyChanged;
                _subscribedCluster = null;
            }
        }

        private void OnClusterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // update UI when selection change
            if (e.PropertyName is nameof(ImageCluster.IsSelected) or nameof(ImageCluster.SelectedCount) or nameof(ImageCluster.AllSelected) or nameof(ImageCluster.NoneSelected))
            {
                DispatcherQueue.TryEnqueue(UpdateSelection);
            }
        }

        private void SetupPanelForCluster()
        {
            double totalWidth = SplitViewGrid.ActualWidth;

            if (totalWidth <= 0)
            {
                totalWidth = 1000; // Fallback
            }

            double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

            // Constraints
            double minWidth = 300;
            double maxWidth = totalWidth * 0.7;

            if (maxWidth < minWidth)
            {
                maxWidth = minWidth;
            }

            rightPanelWidth = System.Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            if (!_isPanelOpen)
            {
                SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Auto);
                SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
                SplitViewGrid.ColumnDefinitions[2].MinWidth = minWidth;

                _isPanelOpen = true;
            }
        }

        private void SavePanelWidthRatio()
        {
            if (_isPanelOpen && SplitViewGrid.ActualWidth > 0)
            {
                double currentRightWidth = SplitViewGrid.ColumnDefinitions[2].ActualWidth;
                if (currentRightWidth > 0)
                {
                    _rightPanelWidthRatio = currentRightWidth / SplitViewGrid.ActualWidth;
                    _rightPanelWidthRatio = System.Math.Clamp(_rightPanelWidthRatio, 0.2, 0.7);
                }
            }
        }

        private void HideRightPanel()
        {
            SplitViewGrid.ColumnDefinitions[2].MinWidth = 0;
            SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(0);

            _isPanelOpen = false;
        }

        private void UpdateRightPanelWidth()
        {
            if (!_isPanelOpen || SplitViewGrid.ActualWidth <= 0)
            {
                return;
            }

            double totalWidth = SplitViewGrid.ActualWidth;
            double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

            double minWidth = 300;
            double maxWidth = totalWidth * 0.7;
            rightPanelWidth = System.Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
        }

        private void BackToConfiguration_Click(object sender, RoutedEventArgs e)
        {
            MainWindowViewModel? mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.BackToConfigurationCommand.Execute(null);
        }

        private void CloseGroupContentPanel_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(false);
            ViewModel.SelectedCluster = null;
        }

        #endregion Panel Management

        #region Selection Handling

        /// <summary>
        /// Update SelectAllCheckBox and SelectionCountText to match current cluster's state.
        /// </summary>
        private void UpdateSelection()
        {
            if (ViewModel.SelectedCluster == null)
            {
                return;
            }

            ImageCluster cluster = ViewModel.SelectedCluster;

            // Update checkbox state to match cluster's IsSelected
            SelectAllCheckBox.IsChecked = cluster.IsSelected;

            SelectionCountText.Text = $"{cluster.SelectedCount} of {cluster.Count} selected";
        }

        /// <summary>
        /// Handle user clicking SelectAll checkbox.
        /// </summary>
        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCluster == null)
            {
                return;
            }

            if (ViewModel.SelectedCluster.AllSelected)
            {
                ViewModel.SelectedCluster.DeselectAll();
            }
            else
            {
                ViewModel.SelectedCluster.SelectAll();
            }
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
            if (_isEditingGroupName && !_isConfirmingRename)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isEditingGroupName && !_isConfirmingRename)
                    {
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

            GroupNameTextBox.Text = ViewModel.SelectedCluster.Name;

            GroupNameDisplayPanel.Visibility = Visibility.Collapsed;
            GroupNameEditPanel.Visibility = Visibility.Visible;

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

            GroupNameEditPanel.Visibility = Visibility.Collapsed;
            GroupNameDisplayPanel.Visibility = Visibility.Visible;

            _isEditingGroupName = false;
            _isConfirmingRename = false;
        }

        #endregion Group Name Editing
    }
}