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
        /// File name without path.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSize { get; private set; }

        /// <summary>
        /// Last modified date.
        /// </summary>
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// Metadata for media file.
        /// </summary>
        public MediaMetadata Metadata { get; private set; }

        /// <summary>
        /// Type of media (Image or Video).
        /// </summary>
        public MediaType MediaType { get; }

        /// <summary>
        /// Whether dimensions have been loaded.
        /// </summary>
        public bool AreDimensionsLoaded { get; private set; }

        /// <summary>
        /// Whether full metadata has been loaded.
        /// </summary>
        public bool IsFullMetadataLoaded { get; private set; }

        #endregion Properties

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

        private SourceMedia(string filePath, MediaType mediaType)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            MediaType = mediaType;
            Metadata = null!; // Set by create method
        }

        #endregion Constructors

        #region Methods

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

            if (SupportedFileExtensions.IsImageFile(extension))
            {
                mediaType = MediaType.Image;
            }
            else if (SupportedFileExtensions.IsVideoFile(extension))
            {
                mediaType = MediaType.Video;
            }
            else
            {
                throw new ArgumentException($"Unsupported file type: {extension}", nameof(filePath));
            }

            SourceMedia source = new(filePath, mediaType);

            // Load file system info
            try
            {
                FileInfo info = new(filePath);
                source.FileSize = info.Length;
                source.LastModified = info.LastWriteTime;
            }
            catch
            {
                // Not exist or inaccessible
                source.FileSize = 0;
                source.LastModified = DateTime.MinValue;
            }

            // Create metadata shell
            if (mediaType == MediaType.Image)
            {
                source.Metadata = new ImageMetadata(filePath, mediaType);
            }
            else
            {
                source.Metadata = new VideoMetadata(filePath, mediaType);
            }

            return source;
        }

        /// <summary>
        /// Create SourceMedia for image file.
        /// </summary>
        public static async Task<SourceMedia> CreateImageAsync(string filePath, bool loadFullMetadata = true)
        {
            SourceMedia source = new(filePath, MediaType.Image);

            ImageMetadata metadata = new(filePath, MediaType.Image);
            metadata.LoadBasicFileInfo();

            source.FileSize = metadata.FileSize;
            source.LastModified = metadata.LastModifiedDate;

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
                source.AreDimensionsLoaded = true;
                source.IsFullMetadataLoaded = true;
            }

            source.Metadata = metadata;
            return source;
        }

        /// <summary>
        /// Create SourceMedia for video file.
        /// </summary>
        public static async Task<SourceMedia> CreateVideoAsync(string filePath, bool loadFullMetadata = true)
        {
            SourceMedia source = new(filePath, MediaType.Video);

            VideoMetadata metadata = new(filePath, MediaType.Video);
            metadata.LoadBasicFileInfo();

            source.FileSize = metadata.FileSize;
            source.LastModified = metadata.LastModifiedDate;

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
                source.AreDimensionsLoaded = true;
                source.IsFullMetadataLoaded = true;
            }

            source.Metadata = metadata;
            return source;
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

                AreDimensionsLoaded = true;
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

                AreDimensionsLoaded = true;
                IsFullMetadataLoaded = true;
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

        #endregion Methods
    }
}