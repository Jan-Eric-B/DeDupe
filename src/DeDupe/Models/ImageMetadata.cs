using DeDupe.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace DeDupe.Models
{
    /// <summary>
    /// Extended metadata (EXIF) for image files.
    /// </summary>
    public partial class ImageMetadata : MediaMetadata
    {
        #region Loading State

        /// <summary>
        /// Whether dimensions have been loaded.
        /// </summary>
        public bool AreDimensionsLoaded { get; private set; }

        /// <summary>
        /// Whether full EXIF metadata has been loaded.
        /// </summary>
        public bool IsFullMetadataLoaded { get; private set; }

        #endregion Loading State

        #region Image Properties

        /// <summary>
        /// Horizontal DPI
        /// </summary>
        public double DpiX { get; protected set; }

        /// <summary>
        /// Vertical DPI
        /// </summary>
        public double DpiY { get; protected set; }

        /// <summary>
        /// Bit depth
        /// </summary>
        public int BitsPerPixel { get; protected set; }

        /// <summary>
        /// Pixel format name
        /// </summary>
        public string PixelFormat { get; protected set; } = string.Empty;

        /// <summary>
        /// Has alpha channel
        /// </summary>
        public bool HasAlphaChannel { get; protected set; }

        /// <summary>
        /// Color space
        /// </summary>
        public string ColorSpace { get; protected set; } = string.Empty;

        #endregion Image Properties

        #region EXIF Properties

        /// <summary>
        /// Date photo was taken
        /// </summary>
        public DateTime? DateTaken { get; protected set; }

        /// <summary>
        /// Camera manufacturer
        /// </summary>
        public string? CameraMake { get; protected set; }

        /// <summary>
        /// Camera model
        /// </summary>
        public string? CameraModel { get; protected set; }

        /// <summary>
        /// Camera info (Make + Model)
        /// </summary>
        public string CameraInfo
        {
            get
            {
                if (string.IsNullOrEmpty(CameraMake) && string.IsNullOrEmpty(CameraModel))
                {
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(CameraMake))
                {
                    return CameraModel ?? string.Empty;
                }

                return string.IsNullOrEmpty(CameraModel) ? CameraMake : $"{CameraMake} {CameraModel}";
            }
        }

        /// <summary>
        /// Lens
        /// </summary>
        public string? LensModel { get; protected set; }

        /// <summary>
        /// Focal length in mm
        /// </summary>
        public double? FocalLength { get; protected set; }

        /// <summary>
        /// Formatted focal length
        /// </summary>
        public string FormattedFocalLength => FocalLength.HasValue ? $"{FocalLength:F0}mm" : string.Empty;

        /// <summary>
        /// F-stop / aperture
        /// </summary>
        public double? FNumber { get; protected set; }

        /// <summary>
        /// Formatted aperture
        /// </summary>
        public string FormattedAperture => FNumber.HasValue ? $"f/{FNumber:F1}" : string.Empty;

        /// <summary>
        /// Exposure time
        /// </summary>
        public double? ExposureTime { get; protected set; }

        /// <summary>
        /// Formatted exposure time
        /// </summary>
        public string FormattedExposureTime
        {
            get
            {
                if (!ExposureTime.HasValue)
                {
                    return string.Empty;
                }
                else
                {
                    return ExposureTime.Value >= 1 ? $"{ExposureTime.Value:F1}s" : $"1/{(int)(1 / ExposureTime.Value)}s";
                }
            }
        }

        /// <summary>
        /// ISO
        /// </summary>
        public int? IsoSpeed { get; protected set; }

        /// <summary>
        /// Formatted ISO
        /// </summary>
        public string FormattedIso => IsoSpeed.HasValue ? $"ISO {IsoSpeed}" : string.Empty;

        /// <summary>
        /// Exposure bias
        /// </summary>
        public double? ExposureBias { get; protected set; }

        /// <summary>
        /// Flash mode
        /// </summary>
        public string? FlashMode { get; protected set; }

        /// <summary>
        /// Flash was fired
        /// </summary>
        public bool? FlashFired { get; protected set; }

        /// <summary>
        /// White balance mode
        /// </summary>
        public string? WhiteBalance { get; protected set; }

        /// <summary>
        /// EXIF orientation value (1-8)
        /// </summary>
        public int ExifOrientation { get; protected set; } = 1;

        /// <summary>
        /// Image title from metadata
        /// </summary>
        public string? Title { get; protected set; }

        /// <summary>
        /// Image description
        /// </summary>
        public string? Description { get; protected set; }

        /// <summary>
        /// Copyright
        /// </summary>
        public string? Copyright { get; protected set; }

        /// <summary>
        /// Used software
        /// </summary>
        public string? Software { get; protected set; }

        /// <summary>
        /// Author
        /// </summary>
        public string? Author { get; protected set; }

        #endregion EXIF Properties

        #region GPS Properties

        /// <summary>
        /// GPS latitude
        /// </summary>
        public double? GpsLatitude { get; protected set; }

        /// <summary>
        /// GPS longitude
        /// </summary>
        public double? GpsLongitude { get; protected set; }

        /// <summary>
        /// GPS altitude in meters
        /// </summary>
        public double? GpsAltitude { get; protected set; }

        /// <summary>
        /// GPS coordinates are available
        /// </summary>
        public bool HasGpsCoordinates => GpsLatitude.HasValue && GpsLongitude.HasValue;

        /// <summary>
        /// Formatted GPS coordinates
        /// </summary>
        public string FormattedGpsCoordinates
        {
            get
            {
                if (!HasGpsCoordinates)
                {
                    return string.Empty;
                }
                else
                {
                    return $"{GpsLatitude:F6}, {GpsLongitude:F6}";
                }
            }
        }

        #endregion GPS Properties

        #region Constructors

        /// <summary>
        /// Create image metadata from file path.
        /// </summary>
        protected internal ImageMetadata(string filePath, MediaType mediaType) : base(filePath, mediaType)
        {
            MediaType = MediaType.Image;
        }

        #endregion Constructors

        #region Factory Methods

        /// <summary>
        /// Create and load image metadata.
        /// </summary>
        public static async Task<ImageMetadata> CreateAsync(string filePath, bool loadFullMetadata = true)
        {
            ImageMetadata metadata = new(filePath, MediaType.Image);
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
        /// Load only dimensions (width & height).
        /// </summary>
        public async Task LoadDimensionsOnlyAsync()
        {
            if (AreDimensionsLoaded)
                return;

            try
            {
                // Simple header reading first
                if (await TryLoadDimensionsFromHeaderAsync())
                {
                    AreDimensionsLoaded = true;
                    return;
                }

                // Fall back to Windows Imaging
                await LoadDimensionsViaWindowsImagingAsync();
                AreDimensionsLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageMetadata.LoadDimensionsOnlyAsync error for {FilePath}: {ex.Message}");
            }
        }

        private async Task<bool> TryLoadDimensionsFromHeaderAsync()
        {
            try
            {
                string extension = Extension.ToLowerInvariant();

                await Task.Run(() =>
                {
                    using FileStream fs = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
                    using BinaryReader reader = new(fs);

                    switch (extension)
                    {
                        case ".jpg":
                        case ".jpeg":
                            ReadJpegDimensions(reader);
                            break;

                        case ".png":
                            ReadPngDimensions(reader);
                            break;

                        case ".gif":
                            ReadGifDimensions(reader);
                            break;

                        case ".bmp":
                            ReadBmpDimensions(reader);
                            break;

                        default:
                            // Unknown format
                            throw new NotSupportedException();
                    }
                });

                return Width > 0 && Height > 0;
            }
            catch
            {
                return false;
            }
        }

        private void ReadJpegDimensions(BinaryReader reader)
        {
            if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
            {
                throw new InvalidDataException("Not a valid JPEG");
            }

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte marker1 = reader.ReadByte();
                if (marker1 != 0xFF)
                {
                    continue;
                }

                byte marker2 = reader.ReadByte();

                // Start of Frame markers
                if (marker2 >= 0xC0 && marker2 <= 0xCF && marker2 != 0xC4 && marker2 != 0xC8 && marker2 != 0xCC)
                {
                    reader.ReadInt16(); // Skip length
                    reader.ReadByte();  // Skip precision
                    Height = ReadBigEndianInt16(reader);
                    Width = ReadBigEndianInt16(reader);
                    return;
                }

                // Skip other segments
                if (marker2 != 0xD0 && marker2 != 0xD9 && marker2 != 0x01)
                {
                    int length = ReadBigEndianInt16(reader);
                    reader.BaseStream.Seek(length - 2, SeekOrigin.Current);
                }
            }
        }

        private void ReadPngDimensions(BinaryReader reader)
        {
            reader.BaseStream.Seek(8, SeekOrigin.Begin);

            // IHDR chunk
            reader.ReadInt32(); // Length
            uint chunkType = reader.ReadUInt32();

            if (chunkType == 0x52444849) // "IHDR"
            {
                Width = ReadBigEndianInt32(reader);
                Height = ReadBigEndianInt32(reader);
            }
        }

        private void ReadGifDimensions(BinaryReader reader)
        {
            reader.BaseStream.Seek(6, SeekOrigin.Begin);
            Width = reader.ReadInt16();
            Height = reader.ReadInt16();
        }

        private void ReadBmpDimensions(BinaryReader reader)
        {
            reader.BaseStream.Seek(18, SeekOrigin.Begin);
            Width = reader.ReadInt32();
            Height = Math.Abs(reader.ReadInt32());
        }

        private static int ReadBigEndianInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            return (bytes[0] << 8) | bytes[1];
        }

        private static int ReadBigEndianInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        /// <summary>
        /// Load dimensions using Windows Imaging.
        /// </summary>
        private async Task LoadDimensionsViaWindowsImagingAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(FilePath);
                using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                Width = (int)decoder.PixelWidth;
                Height = (int)decoder.PixelHeight;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadDimensionsViaWindowsImagingAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load full metadata.
        /// </summary>
        public async Task LoadMetadataAsync()
        {
            if (IsFullMetadataLoaded)
            {
                return;
            }

            LoadBasicFileInfo();
            await LoadImagePropertiesAsync();
            await LoadExifDataAsync();

            AreDimensionsLoaded = true;
            IsFullMetadataLoaded = true;
        }

        /// <summary>
        /// Load basic image properties
        /// </summary>
        public async Task LoadImagePropertiesAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(FilePath);
                using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                // Dimensions
                Width = (int)decoder.PixelWidth;
                Height = (int)decoder.PixelHeight;

                // DPI
                DpiX = decoder.DpiX;
                DpiY = decoder.DpiY;

                // Pixel format
                PixelFormat = decoder.BitmapPixelFormat.ToString();
                HasAlphaChannel = decoder.BitmapAlphaMode != BitmapAlphaMode.Ignore;

                // Calculate bits from pixel based on format
                BitsPerPixel = decoder.BitmapPixelFormat switch
                {
                    BitmapPixelFormat.Gray8 => 8,
                    BitmapPixelFormat.Gray16 => 16,
                    BitmapPixelFormat.Bgra8 or BitmapPixelFormat.Rgba8 => 32,
                    BitmapPixelFormat.Rgba16 => 64,
                    _ => 24
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageMetadata.LoadImagePropertiesAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load EXIF metadata from image
        /// </summary>
        public async Task LoadExifDataAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(FilePath);
                ImageProperties imageProps = await file.Properties.GetImagePropertiesAsync();

                // Basic image properties
                if (Width == 0)
                {
                    Width = (int)imageProps.Width;
                }

                if (Height == 0)
                {
                    Height = (int)imageProps.Height;
                }

                DateTaken = imageProps.DateTaken.Year > 1 ? imageProps.DateTaken.DateTime : null;
                Title = string.IsNullOrWhiteSpace(imageProps.Title) ? null : imageProps.Title;

                // Camera info
                CameraMake = string.IsNullOrWhiteSpace(imageProps.CameraManufacturer) ? null : imageProps.CameraManufacturer;
                CameraModel = string.IsNullOrWhiteSpace(imageProps.CameraModel) ? null : imageProps.CameraModel;

                // GPS coordinates
                if (imageProps.Latitude.HasValue && imageProps.Longitude.HasValue)
                {
                    GpsLatitude = imageProps.Latitude;
                    GpsLongitude = imageProps.Longitude;
                }

                // Extended properties
                await LoadExifDataPropertiesAsync(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageMetadata.LoadExifDataAsync warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Load more EXIF properties
        /// </summary>
        private async Task LoadExifDataPropertiesAsync(StorageFile file)
        {
            try
            {
                // Request specific EXIF properties
                string[] propertyNames =
                [
                    "System.Photo.FNumber",
                    "System.Photo.ExposureTime",
                    "System.Photo.ISOSpeed",
                    "System.Photo.FocalLength",
                    "System.Photo.ExposureBias",
                    "System.Photo.Flash",
                    "System.Photo.WhiteBalance",
                    "System.Photo.Orientation",
                    "System.Photo.LensManufacturer",
                    "System.Photo.LensModel",
                    "System.Image.Copyright",
                    "System.ApplicationName",
                    "System.Author",
                    "System.Comment",
                    "System.GPS.Altitude"
                ];

                IDictionary<string, object> props = await file.Properties.RetrievePropertiesAsync(propertyNames);

                if (props.TryGetValue("System.Photo.FNumber", out object? fNumber) && fNumber != null)
                    FNumber = Convert.ToDouble(fNumber);

                if (props.TryGetValue("System.Photo.ExposureTime", out object? exposure) && exposure != null)
                    ExposureTime = Convert.ToDouble(exposure);

                if (props.TryGetValue("System.Photo.ISOSpeed", out object? iso) && iso != null)
                    IsoSpeed = Convert.ToInt32(iso);

                if (props.TryGetValue("System.Photo.FocalLength", out object? focal) && focal != null)
                    FocalLength = Convert.ToDouble(focal);

                if (props.TryGetValue("System.Photo.ExposureBias", out object? bias) && bias != null)
                    ExposureBias = Convert.ToDouble(bias);

                if (props.TryGetValue("System.Photo.Flash", out object? flash) && flash != null)
                {
                    int flashValue = Convert.ToInt32(flash);
                    FlashFired = (flashValue & 1) == 1;
                    FlashMode = GetFlashModeDescription(flashValue);
                }

                if (props.TryGetValue("System.Photo.WhiteBalance", out object? wb) && wb != null)
                    WhiteBalance = Convert.ToInt32(wb) == 0 ? "Auto" : "Manual";

                if (props.TryGetValue("System.Photo.Orientation", out object? orientation) && orientation != null)
                    ExifOrientation = Convert.ToInt32(orientation);

                if (props.TryGetValue("System.Photo.LensModel", out object? lens) && lens != null)
                    LensModel = lens.ToString();

                if (props.TryGetValue("System.Image.Copyright", out object? copyright) && copyright != null)
                    Copyright = copyright.ToString();

                if (props.TryGetValue("System.ApplicationName", out object? software) && software != null)
                    Software = software.ToString();

                if (props.TryGetValue("System.Author", out object? author) && author is string[] authors && authors.Length > 0)
                    Author = string.Join(", ", authors);

                if (props.TryGetValue("System.Comment", out object? comment) && comment != null)
                    Description = comment.ToString();

                if (props.TryGetValue("System.GPS.Altitude", out object? altitude) && altitude != null)
                    GpsAltitude = Convert.ToDouble(altitude);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageMetadata.LoadExtendedExifPropertiesAsync warning: {ex.Message}");
            }
        }

        private static string GetFlashModeDescription(int flashValue)
        {
            // EXIF flash values
            return flashValue switch
            {
                0x00 => "No Flash",
                0x01 => "Flash Fired",
                0x05 => "Flash Fired, Strobe Return Light Not Detected",
                0x07 => "Flash Fired, Strobe Return Light Detected",
                0x08 => "On, Did Not Fire",
                0x09 => "Flash Fired, Compulsory",
                0x0D => "Flash Fired, Compulsory, Return Light Not Detected",
                0x0F => "Flash Fired, Compulsory, Return Light Detected",
                0x10 => "Off, Did Not Fire",
                0x14 => "Off, Did Not Fire, Return Light Not Detected",
                0x18 => "Auto, Did Not Fire",
                0x19 => "Flash Fired, Auto",
                0x1D => "Flash Fired, Auto, Return Light Not Detected",
                0x1F => "Flash Fired, Auto, Return Light Detected",
                0x20 => "No Flash Function",
                0x30 => "Off, No Flash Function",
                0x41 => "Flash Fired, Red-Eye Reduction",
                0x45 => "Flash Fired, Red-Eye Reduction, Return Light Not Detected",
                0x47 => "Flash Fired, Red-Eye Reduction, Return Light Detected",
                0x49 => "Flash Fired, Compulsory, Red-Eye Reduction",
                0x4D => "Flash Fired, Compulsory, Red-Eye Reduction, Return Light Not Detected",
                0x4F => "Flash Fired, Compulsory, Red-Eye Reduction, Return Light Detected",
                0x50 => "Off, Red-Eye Reduction",
                0x58 => "Auto, Did Not Fire, Red-Eye Reduction",
                0x59 => "Flash Fired, Auto, Red-Eye Reduction",
                0x5D => "Flash Fired, Auto, Red-Eye Reduction, Return Light Not Detected",
                0x5F => "Flash Fired, Auto, Red-Eye Reduction, Return Light Detected",
                _ => $"Unknown ({flashValue})"
            };
        }

        #endregion Methods
    }
}