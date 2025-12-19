using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Models;
using DeDupe.Models.Input;
using DeDupe.Services;
using Microsoft.UI.Xaml;
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
        private ObservableCollection<InputListItem> _inputListItems = [];

        // Track which files came from which source (for removal)
        private readonly Dictionary<string, HashSet<string>> _filePathToSourcesMap = new(StringComparer.OrdinalIgnoreCase);

        // Loaded SourceMedia objects
        private readonly Dictionary<string, SourceMedia> _loadedSourceMedia = new(StringComparer.OrdinalIgnoreCase);

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

        public ObservableCollection<InputListItem> InputListItems
        {
            get => _inputListItems;
            set
            {
                if (SetProperty(ref _inputListItems, value))
                {
                    OnPropertyChanged(nameof(IsMediaListEmpty));
                }
            }
        }

        public int TotalFileCount => _appStateService.SourceCount;

        public Visibility IsMediaListEmpty => InputListItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        #endregion Properties

        #region Constructor

        public FileInputViewModel(IAppStateService appStateService) : base(0)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

            Title = "File Input";
            Status = "No files added";
            InputListItems.CollectionChanged += MediaPathItems_CollectionChanged;
            IsBusy = false;

            _appStateService.SourceMediaChanged += OnSourceMediaChanged;
        }

        #endregion Constructor

        #region Event Handlers

        private void OnSourceMediaChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(TotalFileCount));
            UpdateStatus();
            UpdateCompletionStatus();
        }

        private void MediaPathItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsMediaListEmpty));

            if (e.NewItems != null)
            {
                foreach (InputListItem item in e.NewItems)
                {
                    item.PropertyChanged += MediaPathItem_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (InputListItem item in e.OldItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }

            UpdateCompletionStatus();
        }

        private async void MediaPathItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputListItem.IncludeSubdirectories)
                && sender is InputListItem item
                && item.IsFolder)
            {
                await ProcessFolderAsync(item);
            }
        }

        #endregion Event Handlers

        #region Commands

        [RelayCommand]
        private async Task AddFolder()
        {
            FolderPicker folderPicker = new()
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
            if (folder != null && !InputListItems.Any(item => string.Equals(item.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                // Folder has subdirectories
                IReadOnlyList<StorageFolder> subfolders = await folder.GetFoldersAsync();
                bool hasSubdirectories = subfolders.Count > 0;

                // Create new input list item for folder
                InputListItem inputListItem = new()
                {
                    Path = folder.Path,
                    IsFolder = true,
                    HasSubdirectories = hasSubdirectories,
                    IncludeSubdirectories = hasSubdirectories
                };

                InputListItems.Add(inputListItem);

                await ProcessFolderAsync(inputListItem);
            }
        }

        [RelayCommand]
        private async Task AddFiles()
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

            IReadOnlyList<StorageFile> files = await fileOpenPicker.PickMultipleFilesAsync();

            if (files != null && files.Count > 0)
            {
                IsBusy = true;
                UpdateStatus();

                foreach (StorageFile file in files)
                {
                    if (IsImageFile(file.FileType) && !InputListItems.Any(item => string.Equals(item.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        InputListItem inputListItem = new()
                        {
                            Path = file.Path,
                            IsFolder = false,
                            HasSubdirectories = false,
                            IncludeSubdirectories = false
                        };

                        InputListItems.Add(inputListItem);

                        // Add to unique file paths
                        await AddFileAsync(file.Path, file.Path);
                    }
                }

                IsBusy = false;
                UpdateStatus();
            }
        }

        [RelayCommand]
        private void RemoveItem(InputListItem item)
        {
            if (item != null)
            {
                InputListItems.Remove(item);
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
                Status = "No files added";
            }
            else
            {
                Status = $"{TotalFileCount} file{(TotalFileCount == 1 ? "" : "s")} added";
            }
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = TotalFileCount > 0;
            OnPropertyChanged(nameof(IsMediaListEmpty));
        }

        /// <summary>
        /// Add file and load its SourceMedia asynchronously.
        /// </summary>
        public async Task AddFileAsync(string filePath, string sourcePath)
        {
            string normalizedPath = filePath.ToLowerInvariant();

            // Track source
            if (!_filePathToSourcesMap.TryGetValue(normalizedPath, out HashSet<string>? sources))
            {
                sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _filePathToSourcesMap[normalizedPath] = sources;
            }
            sources.Add(sourcePath);

            // Load SourceMedia if not loaded
            if (!_loadedSourceMedia.ContainsKey(normalizedPath))
            {
                try
                {
                    SourceMedia? sourceMedia = await SourceMedia.CreateAsync(filePath, loadFullMetadata: true);
                    if (sourceMedia != null)
                    {
                        _loadedSourceMedia[normalizedPath] = sourceMedia;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load source media for {filePath}: {ex.Message}");
                }
            }

            UpdateAppStateSourceMedia();
        }

        public static bool IsImageFile(string extension)
        {
            return MediaFileExtensions.IsImageFile(extension);
        }

        public async Task ProcessFolderAsync(InputListItem folderItem)
        {
            if (!folderItem.IsFolder)
                return;

            IsBusy = true;
            UpdateStatus();

            try
            {
                // Remove existing files from folder source
                RemoveSource(folderItem.Path);

                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderItem.Path);
                if (folder != null)
                {
                    QueryOptions queryOptions = new(CommonFileQuery.DefaultQuery, MediaFileExtensions.SupportedImageExtensions)
                    {
                        FolderDepth = folderItem.IncludeSubdirectories ? FolderDepth.Deep : FolderDepth.Shallow
                    };

                    StorageFileQueryResult query = folder.CreateFileQueryWithOptions(queryOptions);
                    IReadOnlyList<StorageFile> files = await query.GetFilesAsync();

                    foreach (StorageFile file in files)
                    {
                        await AddFileAsync(file.Path, folderItem.Path);
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

        private void RemoveSource(string sourcePath)
        {
            List<string> pathsToRemove = [];

            foreach (KeyValuePair<string, HashSet<string>> entry in _filePathToSourcesMap.ToList())
            {
                entry.Value.Remove(sourcePath);

                if (entry.Value.Count == 0)
                {
                    _filePathToSourcesMap.Remove(entry.Key);
                    _loadedSourceMedia.Remove(entry.Key);
                    pathsToRemove.Add(entry.Key);
                }
            }

            if (pathsToRemove.Count > 0)
            {
                UpdateAppStateSourceMedia();
            }
        }

        /// <summary>
        /// Update AppStateService with current SourceMedia collection.
        /// </summary>
        private void UpdateAppStateSourceMedia()
        {
            List<SourceMedia> allSources = [];

            foreach (string path in _filePathToSourcesMap.Keys)
            {
                if (_loadedSourceMedia.TryGetValue(path, out SourceMedia? source))
                {
                    allSources.Add(source);
                }
            }

            _appStateService.SetSourceMedia(allSources);
        }

        #endregion Methods

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appStateService.SourceMediaChanged -= OnSourceMediaChanged;
                InputListItems.CollectionChanged -= MediaPathItems_CollectionChanged;

                foreach (InputListItem item in InputListItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }
            base.Dispose(disposing);
        }

        #endregion Cleanup
    }
}