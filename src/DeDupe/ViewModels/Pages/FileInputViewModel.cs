using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Models;
using DeDupe.Services;
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
        #region Fields

        private readonly IAppStateService _appStateService;

        private bool _includeVideoFiles;

        private ObservableCollection<SourcePathItem> _sourcePathItems = [];

        private readonly Dictionary<string, HashSet<string>> _filePathSources = new(StringComparer.OrdinalIgnoreCase);

        #endregion Fields

        #region Properties

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
        public ObservableCollection<SourcePathItem> SourcePathItems
        {
            get => _sourcePathItems;
            set
            {
                if (SetProperty(ref _sourcePathItems, value))
                {
                    OnPropertyChanged(nameof(IsMediaListEmpty));
                }
            }
        }

        public int TotalFileCount => _appStateService.FileCount;

        public Microsoft.UI.Xaml.Visibility IsMediaListEmpty => SourcePathItems.Count == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        #endregion Properties

        #region Constructor

        public FileInputViewModel(IAppStateService appStateService) : base(0)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

            Title = "File Input";
            Status = "No files selected";
            SourcePathItems.CollectionChanged += MediaPathItems_CollectionChanged;
            IsBusy = false;

            _appStateService.FilePathsChanged += OnFilePathsChanged;
        }

        #endregion Constructor

        #region Event Handlers

        private void OnFilePathsChanged(object? sender, EventArgs e)
        {
            // Update UI
            OnPropertyChanged(nameof(TotalFileCount));
            UpdateStatus();
            UpdateCompletionStatus();
        }

        private void MediaPathItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsMediaListEmpty));

            // Subscribe
            if (e.NewItems != null)
            {
                foreach (SourcePathItem item in e.NewItems)
                {
                    item.PropertyChanged += MediaPathItem_PropertyChanged;
                }
            }

            // Unsubscribe
            if (e.OldItems != null)
            {
                foreach (SourcePathItem item in e.OldItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }

            UpdateCompletionStatus();
        }

        private async void MediaPathItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SourcePathItem.IncludeSubdirectories) && sender is SourcePathItem item && item.IsFolder)
            {
                await ProcessFolderAsync(item);
            }
        }

        #endregion Event Handlers

        #region Commands

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
            if (folder != null && !SourcePathItems.Any(item => string.Equals(item.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                // Folder has subdirectories
                IReadOnlyList<StorageFolder>? subfolders = await folder.GetFoldersAsync();
                bool hasSubdirectories = subfolders.Count > 0;

                // Create new media path item for folder
                SourcePathItem? mediaPathItem = new()
                {
                    Path = folder.Path,
                    IsFolder = true,
                    HasSubdirectories = hasSubdirectories,
                    IncludeSubdirectories = hasSubdirectories
                };

                SourcePathItems.Add(mediaPathItem);

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
                UpdateStatus();
                foreach (StorageFile file in files)
                {
                    // Check if file is already in list and its extension is supported
                    if (IsImageFile(file.FileType) && !SourcePathItems.Any(item => string.Equals(item.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Create new media path item for the file
                        SourcePathItem? mediaPathItem = new()
                        {
                            Path = file.Path,
                            IsFolder = false,
                            HasSubdirectories = false,
                            IncludeSubdirectories = false
                        };

                        SourcePathItems.Add(mediaPathItem);

                        // Add to unique file paths
                        AddFilePath(file.Path, file.Path);
                    }
                }

                IsBusy = false;
                UpdateStatus();
            }
        }

        [RelayCommand]
        private void RemoveItem(SourcePathItem item)
        {
            if (item != null)
            {
                SourcePathItems.Remove(item);
                RemoveSource(item.Path);
            }
        }

        #endregion Commands

        #region Methods

        private void UpdateStatus()
        {
            if (IsBusy)
            {
                Status = "Processing files...";
            }
            else if (TotalFileCount == 0)
            {
                Status = "No files selected";
            }
            else
            {
                Status = $"{TotalFileCount} file{(TotalFileCount == 1 ? "" : "s")} selected";
            }
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = SourcePathItems.Count > 0;
            OnPropertyChanged(nameof(IsMediaListEmpty));
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

            UpdateSharedStateFilePaths();
        }

        public static bool IsImageFile(string extension)
        {
            return MediaFileExtensions.IsImageFile(extension);
        }

        // Process all files in folder
        public async Task ProcessFolderAsync(SourcePathItem folderItem)
        {
            if (!folderItem.IsFolder)
            {
                return;
            }

            IsBusy = true;
            UpdateStatus();

            try
            {
                // Remove existing files from folder
                RemoveSource(folderItem.Path);

                // Add matching files from folder
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderItem.Path);
                if (folder != null)
                {
                    // Get supported files in this folder
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
                UpdateStatus();
            }
        }

        // Removes source and any files from only that source
        private void RemoveSource(string sourcePath)
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

            // Update shared state
            UpdateSharedStateFilePaths();
        }

        // Update shared state service with current file paths
        private void UpdateSharedStateFilePaths()
        {
            List<string>? filePaths = [.. _filePathSources.Keys];
            _appStateService.SetFilePaths(filePaths);
        }

        #endregion Methods

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events
                _appStateService.FilePathsChanged -= OnFilePathsChanged;
                SourcePathItems.CollectionChanged -= MediaPathItems_CollectionChanged;

                // Unsubscribe from PropertyChanged for each item in MediaPathItems
                foreach (SourcePathItem item in SourcePathItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }
            base.Dispose(disposing);
        }

        #endregion Cleanup
    }
}