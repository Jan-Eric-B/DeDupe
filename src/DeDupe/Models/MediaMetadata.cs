using DeDupe.Enums;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace DeDupe.Models
{
    /// <summary>
    /// Base metadata class for all media types (images and videos).
    /// </summary>
    public partial class MediaMetadata : INotifyPropertyChanged
    {
        #region File Properties

        /// <summary>
        /// Full file path
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// File name with extension
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// File name without extension
        /// </summary>
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

        /// <summary>
        /// File extension (lowercase, with dot)
        /// </summary>
        public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();

        /// <summary>
        /// File extension (uppercase, without dot)
        /// </summary>
        public string ExtensionDisplay => Extension.TrimStart('.').ToUpperInvariant();

        /// <summary>
        /// File directory
        /// </summary>
        public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; protected set; }

        /// <summary>
        /// Formatted file size
        /// </summary>
        public string FormattedFileSize => FormatFileSize(FileSize);

        /// <summary>
        /// File creation date
        /// </summary>
        public DateTime CreatedDate { get; protected set; }

        /// <summary>
        /// File last modified date
        /// </summary>
        public DateTime LastModifiedDate { get; protected set; }

        /// <summary>
        /// File last accessed date
        /// </summary>
        public DateTime LastAccessedDate { get; protected set; }

        #endregion File Properties

        #region Media Properties

        /// <summary>
        /// Type of media (Image, Video, VideoFrame)
        /// </summary>
        public MediaType MediaType { get; protected set; }

        /// <summary>
        /// Width in pixels
        /// </summary>
        public int Width { get; protected set; }

        /// <summary>
        /// Height in pixels
        /// </summary>
        public int Height { get; protected set; }

        /// <summary>
        /// Formatted resolution
        /// </summary>
        public string Resolution => $"{Width} × {Height}";

        /// <summary>
        /// Total pixel count
        /// </summary>
        public long PixelCount => (long)Width * Height;

        /// <summary>
        /// Megapixel count
        /// </summary>
        public double Megapixels => PixelCount / 1_000_000.0;

        /// <summary>
        /// Formatted megapixel
        /// </summary>
        public string FormattedMegapixels => $"{Megapixels:F1} MP";

        /// <summary>
        /// Aspect ratio
        /// </summary>
        public double AspectRatio => Height > 0 ? (double)Width / Height : 0;

        /// <summary>
        /// Orientation based on dimensions
        /// </summary>
        public MediaOrientation Orientation
        {
            get
            {
                if (Width > Height)
                {
                    return MediaOrientation.Landscape;
                }

                if (Height > Width)
                {
                    return MediaOrientation.Portrait;
                }

                return MediaOrientation.Square;
            }
        }

        #endregion Media Properties

        #region Display Helpers

        /// <summary>
        /// Gets a display-friendly name for this item
        /// </summary>
        public virtual string GetDisplayName() => FileName;

        #endregion Display Helpers

        #region Constructor

        /// <summary>
        /// Create metadata with basic file information.
        /// </summary>
        public MediaMetadata(string filePath, MediaType mediaType)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            MediaType = mediaType;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Loads basic file information
        /// </summary>
        public virtual void LoadBasicFileInfo()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                FileInfo fileInfo = new(FilePath);
                FileSize = fileInfo.Length;
                CreatedDate = fileInfo.CreationTime;
                LastModifiedDate = fileInfo.LastWriteTime;
                LastAccessedDate = fileInfo.LastAccessTime;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MediaMetadata.LoadBasicFileInfo error for {FilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Format file size
        /// </summary>
        protected static string FormatFileSize(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            if (suffixIndex == 0)
            {
                return $"{size:N0} {suffixes[suffixIndex]}";
            }
            else
            {
                return $"{size:N1} {suffixes[suffixIndex]}";
            }
        }

        public override string ToString()
        {
            return $"{FileName} ({Resolution}, {FormattedFileSize})";
        }

        #endregion Methods

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}