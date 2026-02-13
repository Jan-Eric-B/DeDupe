using DeDupe.Constants;
using DeDupe.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DeDupe.Models.Media
{
    /// <summary>
    /// Represents an source file (image or video).
    /// </summary>
    public partial class SourceMedia
    {
        private readonly ILogger _logger;

        private SourceMedia(string filePath, MediaType mediaType, MediaMetadata metadata, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            FilePath = filePath;
            MediaType = mediaType;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _logger = logger ?? NullLogger.Instance;
        }

        public Guid Id { get; } = Guid.NewGuid();

        public string FilePath { get; }

        /// <summary>
        /// Type of media (Image or Video).
        /// </summary>
        public MediaType MediaType { get; }

        public MediaMetadata Metadata { get; }

        public string FileName => Metadata.FileName;

        public long FileSize => Metadata.FileSize;

        public DateTime LastModified => Metadata.LastModifiedDate;

        #region Image

        public ImageMetadata? AsImage => Metadata as ImageMetadata;

        public bool IsImage => MediaType == MediaType.Image;

        #endregion Image

        #region Video

        public VideoMetadata? AsVideo => Metadata as VideoMetadata;

        public bool IsVideo => MediaType == MediaType.Video;

        #endregion Video

        #region Loading

        /// <summary>
        /// Whether dimensions have been loaded.
        /// </summary>
        public bool AreDimensionsLoaded => Metadata switch
        {
            ImageMetadata img => img.AreDimensionsLoaded,
            VideoMetadata vid => vid.AreDimensionsLoaded,
            _ => false
        };

        /// <summary>
        /// Whether full metadata has been loaded.
        /// </summary>
        public bool IsFullMetadataLoaded => Metadata switch
        {
            ImageMetadata img => img.IsFullMetadataLoaded,
            VideoMetadata vid => vid.IsFullMetadataLoaded,
            _ => false
        };

        public static SourceMedia CreateLightweight(string filePath, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            MediaType mediaType;
            MediaMetadata metadata;

            if (SupportedFileExtensions.IsImageFile(extension))
            {
                mediaType = MediaType.Image;
                metadata = new ImageMetadata(filePath, mediaType, logger);
            }
            else if (SupportedFileExtensions.IsVideoFile(extension))
            {
                mediaType = MediaType.Video;
                metadata = new VideoMetadata(filePath, mediaType, logger);
            }
            else
            {
                throw new ArgumentException($"Unsupported file type: {extension}", nameof(filePath));
            }

            // Load basic file system info
            metadata.LoadBasicFileInfo();

            return new SourceMedia(filePath, mediaType, metadata, logger);
        }

        public static async Task<SourceMedia> CreateImageAsync(string filePath, bool loadFullMetadata = true, ILogger? logger = null)
        {
            ImageMetadata metadata = new(filePath, MediaType.Image, logger);
            metadata.LoadBasicFileInfo();

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            return new SourceMedia(filePath, MediaType.Image, metadata, logger);
        }

        public static async Task<SourceMedia> CreateVideoAsync(string filePath, bool loadFullMetadata = true, ILogger? logger = null)
        {
            VideoMetadata metadata = new(filePath, MediaType.Video, logger);
            metadata.LoadBasicFileInfo();

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            return new SourceMedia(filePath, MediaType.Video, metadata, logger);
        }

        /// <summary>
        /// Load dimensions.
        /// </summary>
        public async Task EnsureDimensionsLoadedAsync()
        {
            if (AreDimensionsLoaded)
                return;

            try
            {
                if (Metadata is ImageMetadata imageMetadata)
                {
                    await imageMetadata.LoadDimensionsOnlyAsync();
                }
                else if (Metadata is VideoMetadata videoMetadata)
                {
                    await videoMetadata.LoadDimensionsOnlyAsync();
                }
            }
            catch (Exception ex)
            {
                LogDimensionLoadFailed(FilePath, ex);
            }
        }

        /// <summary>
        /// Load full metadata.
        /// </summary>
        public async Task EnsureFullMetadataLoadedAsync()
        {
            if (IsFullMetadataLoaded)
            {
                return;
            }

            try
            {
                if (Metadata is ImageMetadata imageMetadata)
                {
                    await imageMetadata.LoadMetadataAsync();
                }
                else if (Metadata is VideoMetadata videoMetadata)
                {
                    await videoMetadata.LoadMetadataAsync();
                }
            }
            catch (Exception ex)
            {
                LogFullMetadataLoadFailed(FilePath, ex);
            }
        }

        #endregion Loading

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "Dimension load failed for {FilePath}")]
        private partial void LogDimensionLoadFailed(string filePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Full metadata load failed for {FilePath}")]
        private partial void LogFullMetadataLoadFailed(string filePath, Exception ex);

        #endregion Logging
    }
}