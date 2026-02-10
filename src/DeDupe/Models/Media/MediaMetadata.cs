using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Enums;
using System;
using System.Diagnostics;
using System.IO;

namespace DeDupe.Models.Media
{
    /// <summary>
    /// Base metadata class for all media types (images and videos).
    /// </summary>
    public partial class MediaMetadata : ObservableObject
    {
        public MediaMetadata(string filePath, MediaType mediaType)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            FilePath = filePath;
            MediaType = mediaType;
        }

        #region Loading

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
                Debug.WriteLine($"MediaMetadata.LoadBasicFileInfo error for {FilePath}: {ex.Message}");
            }
        }

        #endregion Loading

        #region File

        public string FilePath { get; }

        public string FileName => Path.GetFileName(FilePath);

        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

        /// <summary>
        /// File extension (lowercase, with dot)
        /// </summary>
        public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();

        /// <summary>
        /// File extension (uppercase, without dot)
        /// </summary>
        public string ExtensionDisplay => Extension.TrimStart('.').ToUpperInvariant();

        public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

        public long FileSize { get; protected set; }

        public string FormattedFileSize => FormatFileSize(FileSize);

        public DateTime CreatedDate { get; protected set; }

        public DateTime LastModifiedDate { get; protected set; }

        public DateTime LastAccessedDate { get; protected set; }

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

        #endregion File

        #region Media

        /// <summary>
        /// Type of media (Image, Video, VideoFrame)
        /// </summary>
        public MediaType MediaType { get; protected set; }

        public int Width { get; protected set; }

        public int Height { get; protected set; }

        public string Resolution => $"{Width} × {Height}";

        /// <summary>
        /// Total pixel count
        /// </summary>
        public long PixelCount => (long)Width * Height;

        public double Megapixels => PixelCount / 1_000_000.0;

        public string FormattedMegapixels => $"{Megapixels:F1} MP";

        public double AspectRatio => Height > 0 ? (double)Width / Height : 0;

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

        #endregion Media
    }
}