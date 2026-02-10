using DeDupe.Constants;
using DeDupe.Models.Input;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.Views.Pages
{
    public sealed partial class ConfigurationPage : Page
    {
        private ConfigurationViewModel ViewModel { get; }

        public ConfigurationPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ConfigurationViewModel>();
            DataContext = ViewModel;
        }

        #region Item Loading

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

            (List<InputListItem>? folders, List<InputListItem>? files) = await CollectDroppedItemsAsync(items);

            await ProcessDroppedFoldersAsync(folders);
            ProcessDroppedFiles(files);
        }

        private async Task<(List<InputListItem> Folders, List<InputListItem> Files)> CollectDroppedItemsAsync(IReadOnlyList<IStorageItem> items)
        {
            List<InputListItem> folders = [];
            List<InputListItem> files = [];

            foreach (IStorageItem item in items)
            {
                if (IsAlreadyInInputList(item.Path))
                {
                    continue;
                }

                if (item is StorageFolder folder)
                {
                    InputListItem? folderItem = await CreateFolderItemAsync(folder);
                    if (folderItem is not null)
                    {
                        folders.Add(folderItem);
                    }
                }
                else if (item is StorageFile file && SupportedFileExtensions.IsImageFile(file.FileType.ToLowerInvariant()))
                {
                    files.Add(new InputListItem
                    {
                        Path = file.Path,
                        IsFolder = false,
                        HasSubdirectories = false,
                        IncludeSubdirectories = false
                    });
                }
            }

            return (folders, files);
        }

        private bool IsAlreadyInInputList(string path)
        {
            return ViewModel.InputListItems.Any(existingItem => existingItem.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<InputListItem?> CreateFolderItemAsync(StorageFolder folder)
        {
            try
            {
                // Check if folder has subdirectories
                IReadOnlyList<StorageFolder> subfolders = await folder.GetFoldersAsync();
                bool hasSubdirectories = subfolders.Count > 0;

                return new InputListItem
                {
                    Path = folder.Path,
                    IsFolder = true,
                    HasSubdirectories = hasSubdirectories,
                    IncludeSubdirectories = hasSubdirectories
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing folder '{folder.Path}': {ex.Message}");
                return null;
            }
        }

        private async Task ProcessDroppedFoldersAsync(List<InputListItem> folders)
        {
            foreach (InputListItem folderItem in folders)
            {
                ViewModel.InputListItems.Add(folderItem);
                await ViewModel.ProcessFolderAsync(folderItem);
            }
        }

        private void ProcessDroppedFiles(List<InputListItem> files)
        {
            if (files.Count == 0)
            {
                return;
            }

            ViewModel.IsBusy = true;

            try
            {
                foreach (InputListItem fileItem in files)
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

        #endregion Item Loading
    }
}