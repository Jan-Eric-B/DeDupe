using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using WinRT.Interop;

namespace DeDupe.ViewModels.Pages
{
    public partial class FileInputViewModel : PageViewModelBase
    {
        private bool _includeVideoFiles;

        public bool IncludeVideoFiles
        {
            get => _includeVideoFiles;
            set
            {
                if (_includeVideoFiles != value)
                {
                    _includeVideoFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        // Visible collection
        private ObservableCollection<MediaPathItem> _mediaPathItems = [];

        public ObservableCollection<MediaPathItem> MediaPathItems
        {
            get => _mediaPathItems;
            set
            {
                if (SetProperty(ref _mediaPathItems, value))
                {
                    OnPropertyChanged(nameof(IsMediaListEmpty));
                }
            }
        }

        // Internal collection
        private readonly Dictionary<string, HashSet<string>> _filePathSources = new(StringComparer.OrdinalIgnoreCase);

        private int _totalFileCount;

        public int TotalFileCount
        {
            get => _totalFileCount;
            set
            {
                SetProperty(ref _totalFileCount, value);
                OnPropertyChanged(nameof(FileCountText));
            }
        }

        private string _fileCountText = "No files selected";

        public string FileCountText
        {
            get => _fileCountText;
            set => SetProperty(ref _fileCountText, value);
        }

        public Microsoft.UI.Xaml.Visibility IsMediaListEmpty => MediaPathItems.Count == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public FileInputViewModel() : base(0)
        {
            Title = "File Input";
            MediaPathItems.CollectionChanged += MediaPathItems_CollectionChanged;
            TotalFileCount = 0;
            IsBusy = false;
        }

        [RelayCommand]
        private async Task SelectFolder()
        {
            FolderPicker? folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            // Initialize folder picker
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(folderPicker, windowHandle);

            // Show folder picker
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

            // Folder is already in list
            if (folder != null && !MediaPathItems.Any(item => string.Equals(item.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                // Folder has subdirectories
                IReadOnlyList<StorageFolder>? subfolders = await folder.GetFoldersAsync();
                bool hasSubdirectories = subfolders.Count > 0;

                // Create new media path item for folder
                MediaPathItem? mediaPathItem = new()
                {
                    Path = folder.Path,
                    IsFolder = true,
                    HasSubdirectories = hasSubdirectories,
                    IncludeSubdirectories = hasSubdirectories
                };

                MediaPathItems.Add(mediaPathItem);

                // Process folder files
                await ProcessFolderAsync(mediaPathItem);
            }
        }

        [RelayCommand]
        private async Task SelectFiles()
        {
            FileOpenPicker fileOpenPicker = new()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            foreach (string ext in MediaFileExtensions.SupportedImageExtensions)
            {
                fileOpenPicker.FileTypeFilter.Add(ext);
            }

            // Initialize file picker
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);

            IReadOnlyList<StorageFile>? files = await fileOpenPicker.PickMultipleFilesAsync();

            if (files != null && files.Count > 0)
            {
                IsBusy = true;
                UpdateFileCountText();

                foreach (StorageFile file in files)
                {
                    // Check if file is already in list and its extension is supported
                    if (IsImageFile(file.FileType) && !MediaPathItems.Any(item => string.Equals(item.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Create new media path item for the file
                        MediaPathItem? mediaPathItem = new()
                        {
                            Path = file.Path,
                            IsFolder = false,
                            HasSubdirectories = false,
                            IncludeSubdirectories = false
                        };

                        MediaPathItems.Add(mediaPathItem);

                        // Add to unique file paths
                        AddFilePath(file.Path, file.Path);
                    }
                }

                IsBusy = false;
                UpdateFileCountText();
            }
        }

        [RelayCommand]
        private void RemoveItem(MediaPathItem item)
        {
            if (item != null)
            {
                MediaPathItems.Remove(item);
                RemoveFilesFromSource(item.Path);
            }
        }

        private void UpdateFileCountText()
        {
            if (IsBusy)
            {
                FileCountText = "Processing files...";
            }
            else if (TotalFileCount == 0)
            {
                FileCountText = "No files selected";
            }
            else
            {
                FileCountText = $"{TotalFileCount} file{(TotalFileCount == 1 ? "" : "s")} selected";
            }
        }

        private void MediaPathItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsMediaListEmpty));

            // Subscribe
            if (e.NewItems != null)
            {
                foreach (MediaPathItem item in e.NewItems)
                {
                    item.PropertyChanged += MediaPathItem_PropertyChanged;
                }
            }

            // Unsubscribe
            if (e.OldItems != null)
            {
                foreach (MediaPathItem item in e.OldItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }

            UpdateCompletionStatus();
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = MediaPathItems.Count > 0;
            OnPropertyChanged(nameof(IsMediaListEmpty));
        }

        private async void MediaPathItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaPathItem.IncludeSubdirectories) && sender is MediaPathItem item && item.IsFolder)
            {
                await ProcessFolderAsync(item);
            }
        }

        public void AddFilePath(string filePath, string sourcePath)
        {
            string normalizedPath = filePath.ToLowerInvariant();

            // Track source of file path
            if (!_filePathSources.TryGetValue(normalizedPath, out HashSet<string>? sources))
            {
                sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _filePathSources[normalizedPath] = sources;
            }

            sources.Add(sourcePath);

            TotalFileCount = _filePathSources.Count;
            UpdateFileCountText();
        }

        public static bool IsImageFile(string extension)
        {
            return MediaFileExtensions.IsImageFile(extension);
        }

        // Process all files in folder
        public async Task ProcessFolderAsync(MediaPathItem folderItem)
        {
            if (folderItem.IsFolder)
            {
                IsBusy = true;
                UpdateFileCountText();

                try
                {
                    // Remove already existing files
                    RemoveFilesFromSource(folderItem.Path);

                    // Add matching files
                    StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderItem.Path);
                    if (folder != null)
                    {
                        // Get supported files
                        QueryOptions queryOptions = new(CommonFileQuery.DefaultQuery, MediaFileExtensions.SupportedImageExtensions);

                        if (folderItem.IncludeSubdirectories)
                        {
                            queryOptions.FolderDepth = FolderDepth.Deep;
                        }
                        else
                        {
                            queryOptions.FolderDepth = FolderDepth.Shallow;
                        }

                        StorageFileQueryResult query = folder.CreateFileQueryWithOptions(queryOptions);
                        IReadOnlyList<StorageFile> files = await query.GetFilesAsync();

                        foreach (StorageFile file in files)
                        {
                            AddFilePath(file.Path, folderItem.Path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing folder {folderItem.Path}: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                    UpdateFileCountText();
                }
            }
            else
            {
                return;
            }
        }

        // Removes source and any files from only that source
        private void RemoveFilesFromSource(string sourcePath)
        {
            // Get all files from that source
            foreach (KeyValuePair<string, HashSet<string>> filePathEntry in _filePathSources.ToList())
            {
                filePathEntry.Value.Remove(sourcePath);

                if (filePathEntry.Value.Count == 0)
                {
                    _filePathSources.Remove(filePathEntry.Key);
                }
            }

            TotalFileCount = _filePathSources.Count;
            UpdateFileCountText();
        }
    }
}