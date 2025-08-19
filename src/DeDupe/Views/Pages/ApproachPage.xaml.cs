using DeDupe.Constants;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.Views.Pages
{
    public sealed partial class ApproachPage : Page
    {
        public ApproachViewModel ViewModel { get; }

        public ApproachPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ApproachViewModel>();
            DataContext = ViewModel;
        }

        private void ModelFileDropGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!ViewModel.IsDeepLearningSelected)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            // Dragged items contains files
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Add model file";
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
            // Only proceed if Deep Learning is selected
            if (!ViewModel.IsDeepLearningSelected)
            {
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                try
                {
                    // Get dragged items
                    IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();

                    if (items.Count > 0)
                    {
                        // First file only
                        IStorageItem? item = items[0];

                        if (item is StorageFile file)
                        {
                            string extension = file.FileType.ToLowerInvariant();

                            if (ModelFileExtensions.IsSupportedFile(extension))
                            {
                                // Set model file path
                                ViewModel.ModelFilePath = file.Path;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling file drop: {ex.Message}");
                }
            }
        }
    }
}