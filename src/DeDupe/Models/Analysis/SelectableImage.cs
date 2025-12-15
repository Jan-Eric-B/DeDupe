using DeDupe.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Wrapper for ExtractedFeatures that adds selection state for UI binding
    /// </summary>
    public partial class SelectableImage : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Underlying extracted features
        /// </summary>
        public ExtractedFeatures Features { get; }

        /// <summary>
        /// Image is selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Event raised when selection state changes
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Path to original image file
        /// </summary>
        public string FilePath => Features.OriginalFilePath;

        /// <summary>
        /// Filename
        /// </summary>
        public string DisplayName => Features.DisplayName;

        /// <summary>
        /// File name without path
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// Directory path
        /// </summary>
        public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// Formatted file size string
        /// </summary>
        public string FormattedFileSize => FileService.FormatFileSize(FileSize);

        /// <summary>
        /// Image width
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Image height
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Resolution string
        /// </summary>
        public string Resolution => $"{Width} × {Height}";

        /// <summary>
        /// File extension
        /// </summary>
        public string Extension => Path.GetExtension(FilePath).ToUpperInvariant().TrimStart('.');

        /// <summary>
        /// Create selectable wrapper for given features
        /// </summary>
        public SelectableImage(ExtractedFeatures features)
        {
            Features = features ?? throw new ArgumentNullException(nameof(features));

            // Get file info
            if (File.Exists(FilePath))
            {
                FileInfo fileInfo = new(FilePath);
                FileSize = fileInfo.Length;
            }

            // Get dimensions from ProcessedMedia
            Width = features.ProcessedMedia.OriginalItem.Width;
            Height = features.ProcessedMedia.OriginalItem.Height;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}