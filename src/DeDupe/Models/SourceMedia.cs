using DeDupe.Constants;
using DeDupe.Enums;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DeDupe.Models
{
    /// <summary>
    /// Represents an original source file (image or video).
    /// </summary>
    public class SourceMedia
    {
        #region Properties

        /// <summary>
        /// Unique identifier for this source.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Full path to original file.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Type of media (Image or Video).
        /// </summary>
        public MediaType MediaType { get; }

        /// <summary>
        /// Metadata for media file (owns all file/media data).
        /// </summary>
        public MediaMetadata Metadata { get; }

        /// <summary>
        /// File name without path.
        /// </summary>
        public string FileName => Metadata.FileName;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSize => Metadata.FileSize;

        /// <summary>
        /// Last modified date.
        /// </summary>
        public DateTime LastModified => Metadata.LastModifiedDate;

        #endregion Properties

        #region Loading State (delegated to typed metadata)

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

        #endregion Loading State (delegated to typed metadata)

        #region Type-Specific Access

        /// <summary>
        /// Get as ImageMetadata.
        /// </summary>
        public ImageMetadata? AsImage => Metadata as ImageMetadata;

        /// <summary>
        /// Get as VideoMetadata.
        /// </summary>
        public VideoMetadata? AsVideo => Metadata as VideoMetadata;

        /// <summary>
        /// Is an image file.
        /// </summary>
        public bool IsImage => MediaType == MediaType.Image;

        /// <summary>
        /// Is a video file.
        /// </summary>
        public bool IsVideo => MediaType == MediaType.Video;

        #endregion Type-Specific Access

        #region Constructors

        private SourceMedia(string filePath, MediaType mediaType, MediaMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            FilePath = filePath;
            MediaType = mediaType;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        #endregion Constructors

        #region Factory Methods

        /// <summary>
        /// Create SourceMedia with basic file info.
        /// </summary>
        public static SourceMedia CreateLightweight(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

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

        /// <summary>
        /// Create SourceMedia for image file.
        /// </summary>
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

        /// <summary>
        /// Create SourceMedia for video file.
        /// </summary>
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
        /// Create SourceMedia and detect type from file extension.
        /// </summary>
        public static async Task<SourceMedia?> CreateAsync(string filePath, bool loadFullMetadata = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (SupportedFileExtensions.IsImageFile(extension))
            {
                return await CreateImageAsync(filePath, loadFullMetadata);
            }
            else if (SupportedFileExtensions.IsVideoFile(extension))
            {
                return await CreateVideoAsync(filePath, loadFullMetadata);
            }

            return null;
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
                System.Diagnostics.Debug.WriteLine($"Failed to load dimensions for {FilePath}: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Failed to load metadata for {FilePath}: {ex.Message}");
            }
        }

        public override string ToString()
        {
            return $"{MediaType}: {FileName}";
        }

        #endregion Factory Methods
    }
}