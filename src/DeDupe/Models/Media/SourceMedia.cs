using DeDupe.Constants;
using DeDupe.Enums;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DeDupe.Models.Media
{
    /// <summary>
    /// Represents an source file (image or video).
    /// </summary>
    public class SourceMedia
    {
        private SourceMedia(string filePath, MediaType mediaType, MediaMetadata metadata)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            FilePath = filePath;
            MediaType = mediaType;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
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

        public static SourceMedia CreateLightweight(string filePath)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            MediaType mediaType;
            MediaMetadata metadata;

            if (SupportedFileExtensions.IsImageFile(extension))
            {
                mediaType = MediaType.Image;
                metadata = new ImageMetadata(filePath, mediaType);
            }
            else if (SupportedFileExtensions.IsVideoFile(extension))
            {
                mediaType = MediaType.Video;
                metadata = new VideoMetadata(filePath, mediaType);
            }
            else
            {
                throw new ArgumentException($"Unsupported file type: {extension}", nameof(filePath));
            }

            // Load basic file system info
            metadata.LoadBasicFileInfo();

            return new SourceMedia(filePath, mediaType, metadata);
        }

        public static async Task<SourceMedia> CreateImageAsync(string filePath, bool loadFullMetadata = true)
        {
            ImageMetadata metadata = new(filePath, MediaType.Image);
            metadata.LoadBasicFileInfo();

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            return new SourceMedia(filePath, MediaType.Image, metadata);
        }

        public static async Task<SourceMedia> CreateVideoAsync(string filePath, bool loadFullMetadata = true)
        {
            VideoMetadata metadata = new(filePath, MediaType.Video);
            metadata.LoadBasicFileInfo();

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            return new SourceMedia(filePath, MediaType.Video, metadata);
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
                Debug.WriteLine($"Failed to load dimensions for {FilePath}: {ex.Message}");
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
                Debug.WriteLine($"Failed to load metadata for {FilePath}: {ex.Message}");
            }
        }

        #endregion Loading
    }
}