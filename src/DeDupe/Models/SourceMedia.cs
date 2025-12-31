using DeDupe.Constants;
using DeDupe.Enums;
using System;
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
        /// Metadata for media file.
        /// </summary>
        public MediaMetadata Metadata { get; private set; }

        /// <summary>
        /// Type of media (Image or Video).
        /// </summary>
        public MediaType MediaType { get; }

        /// <summary>
        /// Metadata fully loaded.
        /// </summary>
        public bool IsMetadataLoaded { get; private set; }

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
            MediaType = mediaType;
            Metadata = null!; // Set by create method
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Create SourceMedia for image file.
        /// </summary>
        public static async Task<SourceMedia> CreateImageAsync(string filePath, bool loadFullMetadata = true)
        {
            SourceMedia source = new(filePath, MediaType.Image);

            ImageMetadata metadata = new(filePath, MediaType.Image);
            metadata.LoadBasicFileInfo();

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            source.Metadata = metadata;
            source.IsMetadataLoaded = loadFullMetadata;

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

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            source.Metadata = metadata;
            source.IsMetadataLoaded = loadFullMetadata;

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

            string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

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
        /// Load metadata.
        /// </summary>
        public async Task EnsureMetadataLoadedAsync()
        {
            if (IsMetadataLoaded)
            {
                return;
            }

            if (Metadata is ImageMetadata imageMetadata)
            {
                await imageMetadata.LoadMetadataAsync();
            }
            else if (Metadata is VideoMetadata videoMetadata)
            {
                await videoMetadata.LoadMetadataAsync();
            }

            IsMetadataLoaded = true;
        }

        public override string ToString()
        {
            return $"{MediaType}: {Metadata.FileName}";
        }

        #endregion Methods
    }
}