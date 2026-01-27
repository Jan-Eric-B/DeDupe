using DeDupe.Constants;
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
    public sealed partial class ConfigurationPage : Page
    {
        public ConfigurationViewModel ViewModel { get; }

        public ConfigurationPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ConfigurationViewModel>();
            DataContext = ViewModel;
        }

        private void LvInputList_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Add items";
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
        }

        private async void LvInputList_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            // Get dragged items
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();

            if (items.Count == 0)
            {
                return;
            }

            // Collect items to add
            List<InputListItem> foldersToProcess = [];
            List<InputListItem> filesToAdd = [];

            foreach (IStorageItem item in items)
            {
                // Skip item if already in collection
                if (ViewModel.InputListItems.Any(existingItem =>
                    existingItem.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (item is StorageFolder folder)
                {
                    try
                    {
                        // Check if folder has subdirectories
                        IReadOnlyList<StorageFolder>? subfolders = await folder.GetFoldersAsync();
                        bool hasSubdirectories = subfolders.Count > 0;

                        InputListItem sourcePathItem = new()
                        {
                            Path = folder.Path,
                            IsFolder = true,
                            HasSubdirectories = hasSubdirectories,
                            IncludeSubdirectories = hasSubdirectories
                        };

                        foldersToProcess.Add(sourcePathItem);
                    }
                    catch (Exception)
                    {
                        // Skip folders we can't access
                        continue;
                    }
                }
                else if (item is StorageFile file)
                {
                    string extension = file.FileType.ToLowerInvariant();
                    if (IsImageFile(extension))
                    {
                        InputListItem sourcePathItem = new()
                        {
                            Path = file.Path,
                            IsFolder = false,
                            HasSubdirectories = false,
                            IncludeSubdirectories = false
                        };

                        filesToAdd.Add(sourcePathItem);
                    }
                }
            }

            // Nothing to add
            if (foldersToProcess.Count == 0 && filesToAdd.Count == 0)
            {
                return;
            }

            // Process folders (ProcessFolderAsync handles IsBusy internally)
            foreach (InputListItem folderItem in foldersToProcess)
            {
                ViewModel.InputListItems.Add(folderItem);
                await ViewModel.ProcessFolderAsync(folderItem);
            }

            // Process files with IsBusy wrapper for consistent UX
            if (filesToAdd.Count > 0)
            {
                ViewModel.IsBusy = true;

                try
                {
                    foreach (InputListItem fileItem in filesToAdd)
                    {
                        ViewModel.InputListItems.Add(fileItem);
                        ViewModel.AddFile(fileItem.Path, fileItem.Path);
                    }
                }
                finally
                {
                    ViewModel.IsBusy = false;
                }
            }
        }

        private static bool IsImageFile(string extension)
        {
            return SupportedFileExtensions.IsImageFile(extension);
        }
    }
}