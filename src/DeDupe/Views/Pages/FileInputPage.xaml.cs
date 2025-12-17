using DeDupe.Constants;
using DeDupe.Models;
using DeDupe.Models.Input;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.Views.Pages
{
    public sealed partial class FileInputPage : Page
    {
        public FileInputViewModel ViewModel { get; }

        public FileInputPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<FileInputViewModel>();
            DataContext = ViewModel;
        }

        private void LvMediaSelectionList_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Add items";
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
        }

        private async void LvMediaSelectionList_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // Get dragged items
                IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();

                if (items.Count > 0)
                {
                    List<SourcePathItem> newItems = [];

                    foreach (IStorageItem item in items)
                    {
                        // Skip item if already in collection
                        if (ViewModel.SourcePathItems.Any(existingItem => existingItem.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        if (item is StorageFolder folder)
                        {
                            // Add folder
                            try
                            {
                                // Check if folder has subdirectories
                                IReadOnlyList<StorageFolder>? subfolders = await folder.GetFoldersAsync();
                                bool hasSubdirectories = subfolders.Count > 0;

                                SourcePathItem? mediaPathItem = new()
                                {
                                    Path = folder.Path,
                                    IsFolder = true,
                                    HasSubdirectories = hasSubdirectories,
                                    IncludeSubdirectories = hasSubdirectories
                                };

                                newItems.Add(mediaPathItem);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }
                        else if (item is StorageFile file)
                        {
                            // Check file extension
                            string extension = file.FileType.ToLowerInvariant();
                            if (IsImageFile(extension))
                            {
                                SourcePathItem? mediaPathItem = new()
                                {
                                    Path = file.Path,
                                    IsFolder = false,
                                    HasSubdirectories = false,
                                    IncludeSubdirectories = false
                                };

                                newItems.Add(mediaPathItem);
                            }
                        }
                    }

                    // Add items to collection
                    foreach (SourcePathItem newItem in newItems)
                    {
                        ViewModel.SourcePathItems.Add(newItem);

                        if (newItem.IsFolder)
                        {
                            await ViewModel.ProcessFolderAsync(newItem);
                        }
                        else
                        {
                            ViewModel.AddFilePath(newItem.Path, newItem.Path);
                        }
                    }
                }
            }
        }

        private static bool IsImageFile(string extension)
        {
            return MediaFileExtensions.IsImageFile(extension);
        }
    }
}