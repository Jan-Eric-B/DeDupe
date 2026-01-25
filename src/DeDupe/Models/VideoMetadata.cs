using DeDupe.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace DeDupe.Models
{
    /// <summary>
    /// Extended metadata for video files.
    /// </summary>
    public partial class VideoMetadata : MediaMetadata
    {
        #region Loading State

        /// <summary>
        /// Whether dimensions have been loaded.
        /// </summary>
        public bool AreDimensionsLoaded { get; private set; }

        /// <summary>
        /// Whether full metadata has been loaded.
        /// </summary>
        public bool IsFullMetadataLoaded { get; private set; }

        #endregion Loading State

        #region Video Properties

        /// <summary>
        /// Video duration
        /// </summary>
        public TimeSpan Duration { get; protected set; }

        /// <summary>
        /// Formatted duration
        /// </summary>
        public string FormattedDuration
        {
            get
            {
                if (Duration.TotalHours >= 1)
                {
                    return Duration.ToString(@"h\:mm\:ss");
                }

                return Duration.ToString(@"m\:ss");
            }
        }

        /// <summary>
        /// Frame rate
        /// </summary>
        public double FrameRate { get; protected set; }

        /// <summary>
        /// Total frame count
        /// </summary>
        public long TotalFrames => (long)(Duration.TotalSeconds * FrameRate);

        /// <summary>
        /// Video bitrate
        /// </summary>
        public uint VideoBitrate { get; protected set; }

        /// <summary>
        /// Formatted video bitrate
        /// </summary>
        public string FormattedVideoBitrate => FormatBitrate(VideoBitrate);

        /// <summary>
        /// Audio bitrate
        /// </summary>
        public uint AudioBitrate { get; protected set; }

        /// <summary>
        /// Formatted audio bitrate
        /// </summary>
        public string FormattedAudioBitrate => FormatBitrate(AudioBitrate);

        /// <summary>
        /// Total bitrate (video + audio)
        /// </summary>
        public uint TotalBitrate { get; protected set; }

        /// <summary>
        /// Video codec
        /// </summary>
        public string VideoCodec { get; protected set; } = string.Empty;

        /// <summary>
        /// Audio codec
        /// </summary>
        public string AudioCodec { get; protected set; } = string.Empty;

        /// <summary>
        /// Container format
        /// </summary>
        public string ContainerFormat { get; protected set; } = string.Empty;

        #endregion Video Properties

        #region Audio Properties

        /// <summary>
        /// Whether video has audio track
        /// </summary>
        public bool HasAudio { get; protected set; }

        /// <summary>
        /// Audio sample rate
        /// </summary>
        public uint AudioSampleRate { get; protected set; }

        /// <summary>
        /// Audio channel count
        /// </summary>
        public uint AudioChannels { get; protected set; }

        /// <summary>
        /// Formatted audio channels
        /// </summary>
        public string FormattedAudioChannels
        {
            get
            {
                return AudioChannels switch
                {
                    0 => "No Audio",
                    1 => "Mono",
                    2 => "Stereo",
                    3 => "2.1 Surround",
                    4 => "Quadraphonic",
                    5 => "4.1 Surround",
                    6 => "5.1 Surround",
                    7 => "6.1 Surround",
                    8 => "7.1 Surround",
                    10 => "7.1.2 Surround",
                    12 => "9.1.2 Surround",
                    _ => $"{AudioChannels} channels"
                };
            }
        }

        #endregion Audio Properties

        #region Recording Metadata

        /// <summary>
        /// Date the video was recorded
        /// </summary>
        public DateTime? DateRecorded { get; protected set; }

        /// <summary>
        /// Video title
        /// </summary>
        public string? Title { get; protected set; }

        /// <summary>
        /// Video description
        /// </summary>
        public string? Description { get; protected set; }

        /// <summary>
        /// Director/creator
        /// </summary>
        public string? Director { get; protected set; }

        /// <summary>
        /// Copyright information
        /// </summary>
        public string? Copyright { get; protected set; }

        /// <summary>
        /// GPS latitude
        /// </summary>
        public double? GpsLatitude { get; protected set; }

        /// <summary>
        /// GPS longitude
        /// </summary>
        public double? GpsLongitude { get; protected set; }

        /// <summary>
        /// Whether GPS coordinates are available
        /// </summary>
        public bool HasGpsCoordinates => GpsLatitude.HasValue && GpsLongitude.HasValue;

        #endregion Recording Metadata

        #region Constructor

        /// <summary>
        /// Create video metadata from file path.
        /// Protected internal to allow inheritance by VideoSource.
        /// </summary>
        protected internal VideoMetadata(string filePath, MediaType mediaType) : base(filePath, mediaType)
        {
            MediaType = MediaType.Video;
        }

        #endregion Constructor

        #region Factory Methods

        /// <summary>
        /// Create and load video metadata.
        /// </summary>
        public static async Task<VideoMetadata> CreateAsync(string filePath, bool loadFullMetadata = true)
        {
            VideoMetadata metadata = new(filePath, MediaType.Video);
            metadata.LoadBasicFileInfo();

            if (loadFullMetadata)
            {
                await metadata.LoadMetadataAsync();
            }

            return metadata;
        }

        #endregion Factory Methods

        #region Methods

        /// <summary>
        /// Load only dimensions & duration.
        /// </summary>
        public async Task LoadDimensionsOnlyAsync()
        {
            if (AreDimensionsLoaded)
                return;

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(FilePath);
                VideoProperties videoProps = await file.Properties.GetVideoPropertiesAsync();

                // Dimensions
                Width = (int)videoProps.Width;
                Height = (int)videoProps.Height;

                // Duration
                Duration = videoProps.Duration;

                AreDimensionsLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoMetadata.LoadDimensionsOnlyAsync error for {FilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Load all video metadata
        /// </summary>
        public async Task LoadMetadataAsync()
        {
            if (IsFullMetadataLoaded)
                return;

            LoadBasicFileInfo();
            await LoadVideoPropertiesAsync();

            AreDimensionsLoaded = true;
            IsFullMetadataLoaded = true;
        }

        /// <summary>
        /// Load video properties from file
        /// </summary>
        public async Task LoadVideoPropertiesAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(FilePath);
                VideoProperties videoProps = await file.Properties.GetVideoPropertiesAsync();

                // Dimensions
                Width = (int)videoProps.Width;
                Height = (int)videoProps.Height;

                // Duration
                Duration = videoProps.Duration;

                // Bitrate
                TotalBitrate = videoProps.Bitrate;

                // Title and metadata
                Title = string.IsNullOrWhiteSpace(videoProps.Title) ? null : videoProps.Title;

                // GPS
                if (videoProps.Latitude.HasValue && videoProps.Longitude.HasValue)
                {
                    GpsLatitude = videoProps.Latitude;
                    GpsLongitude = videoProps.Longitude;
                }

                // Extended properties
                await LoadExtendedVideoPropertiesAsync(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoMetadata.LoadVideoPropertiesAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads extended video properties
        /// </summary>
        private async Task LoadExtendedVideoPropertiesAsync(StorageFile file)
        {
            try
            {
                string[] propertyNames =
                [
                    "System.Video.FrameRate",
                    "System.Video.EncodingBitrate",
                    "System.Audio.EncodingBitrate",
                    "System.Audio.SampleRate",
                    "System.Audio.ChannelCount",
                    "System.Video.Compression",
                    "System.Audio.Compression",
                    "System.Media.DateEncoded",
                    "System.Media.DateReleased",
                    "System.Media.Director",
                    "System.Copyright",
                    "System.Comment"
                ];

                IDictionary<string, object> props = await file.Properties.RetrievePropertiesAsync(propertyNames);

                if (props.TryGetValue("System.Video.FrameRate", out object? frameRate) && frameRate != null)
                    FrameRate = Convert.ToDouble(frameRate) / 1000.0; // Milliframes per second

                if (props.TryGetValue("System.Video.EncodingBitrate", out object? videoBitrate) && videoBitrate != null)
                    VideoBitrate = Convert.ToUInt32(videoBitrate);

                if (props.TryGetValue("System.Audio.EncodingBitrate", out object? audioBitrate) && audioBitrate != null)
                {
                    AudioBitrate = Convert.ToUInt32(audioBitrate);
                    HasAudio = AudioBitrate > 0;
                }

                if (props.TryGetValue("System.Audio.SampleRate", out object? sampleRate) && sampleRate != null)
                    AudioSampleRate = Convert.ToUInt32(sampleRate);

                if (props.TryGetValue("System.Audio.ChannelCount", out object? channels) && channels != null)
                    AudioChannels = Convert.ToUInt32(channels);

                if (props.TryGetValue("System.Video.Compression", out object? videoCodec) && videoCodec != null)
                    VideoCodec = videoCodec.ToString() ?? string.Empty;

                if (props.TryGetValue("System.Audio.Compression", out object? audioCodec) && audioCodec != null)
                    AudioCodec = audioCodec.ToString() ?? string.Empty;

                if (props.TryGetValue("System.Media.DateEncoded", out object? dateEncoded) && dateEncoded is DateTime encoded)
                    DateRecorded = encoded;

                if (props.TryGetValue("System.Media.Director", out object? director) && director is string[] directors && directors.Length > 0)
                    Director = string.Join(", ", directors);

                if (props.TryGetValue("System.Copyright", out object? copyright) && copyright != null)
                    Copyright = copyright.ToString();

                if (props.TryGetValue("System.Comment", out object? comment) && comment != null)
                    Description = comment.ToString();

                // Determine container format from extension
                ContainerFormat = Extension.ToUpperInvariant().TrimStart('.');
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoMetadata.LoadExtendedVideoPropertiesAsync warning: {ex.Message}");
            }
        }

        #endregion Methods

        #region Helpers

        private static string FormatBitrate(uint bitsPerSecond)
        {
            if (bitsPerSecond == 0) return string.Empty;

            double kbps = bitsPerSecond / 1000.0;
            if (kbps < 1000)
                return $"{kbps:F0} kbps";

            double mbps = kbps / 1000.0;
            return $"{mbps:F1} Mbps";
        }

        #endregion Helpers
    }
}