using DeDupe.Constants;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.Views.Pages
{
    public sealed partial class ModelConfigurationPage : Page
    {
        public ModelConfigurationViewModel ViewModel { get; }

        public ModelConfigurationPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ModelConfigurationViewModel>();
            DataContext = ViewModel;
        }

        private void ModelFileDropGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!ViewModel.UseCustomModel)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            // Dragged items contains files
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Use as custom model";
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
            if (!ViewModel.UseCustomModel)
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

                            if (SupportedFileExtensions.IsSupportedModelFile(extension))
                            {
                                // Set custom model file path
                                ViewModel.CustomModelFilePath = file.Path;
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

        private void ModelFileTextBlock_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            string pathToOpen = ViewModel.UseCustomModel ? ViewModel.CustomModelFilePath : ViewModel.ModelFilePath;

            if (!string.IsNullOrEmpty(pathToOpen) && File.Exists(pathToOpen))
            {
                try
                {
                    // Open folder and select file
                    Process.Start("explorer.exe", $"/select,\"{pathToOpen}\"");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening folder: {ex.Message}");
                }
            }
        }
    }
}