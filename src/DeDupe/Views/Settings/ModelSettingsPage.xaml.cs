using CommunityToolkit.WinUI.Controls;
using DeDupe.Constants;
using DeDupe.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.Views.Settings
{
    public sealed partial class ModelSettingsPage : Page
    {
        private ModelSettingsViewModel ViewModel { get; }

        public ModelSettingsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ModelSettingsViewModel>();
            DataContext = ViewModel;
        }

        #region Model Selection

        private void ModelSourceSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is Segmented segmented && segmented.SelectedItem is SegmentedItem item)
            {
                bool isBundled = item.Tag?.ToString() == "Bundled";
                ViewModel.UseBundledModel = isBundled;
            }
        }

        private void ModelFileDropGrid_DragOver(object sender, DragEventArgs e)
        {
            if (ViewModel.UseBundledModel)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Use this model";
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void ModelFileDropGrid_Drop(object sender, DragEventArgs e)
        {
            if (ViewModel.UseBundledModel || !e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            try
            {
                IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();

                if (items.Count == 0 || items[0] is not StorageFile file)
                {
                    return;
                }

                string extension = file.FileType.ToLowerInvariant();

                if (SupportedFileExtensions.IsSupportedModelFile(extension))
                {
                    ViewModel.CustomModelFilePath = file.Path;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling file drop: {ex.Message}");
            }
        }

        private void ModelFileTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ViewModel.OpenModelLocationCommand.Execute(null);
        }

        #endregion Model Selection

        #region Navigation

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.OnNavigatedFrom();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.OnNavigatedTo();

            // Sync Segmented control with ViewModel's state
            ModelSourceSegmented.SelectedIndex = ViewModel.UseBundledModel ? 0 : 1;
        }

        #endregion Navigation
    }
}