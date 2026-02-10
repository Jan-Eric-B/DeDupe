using DeDupe.Enums;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace DeDupe.Models.Media
{
    /// <summary>
    /// Extended metadata (EXIF) for image files.
    /// </summary>
    public partial class ImageMetadata : MediaMetadata
    {
        protected internal ImageMetadata(string filePath, MediaType mediaType) : base(filePath, mediaType)
        {
            MediaType = MediaType.Image;
        }

        #region Loading

        public bool AreDimensionsLoaded { get; private set; }

        public bool IsFullMetadataLoaded { get; private set; }

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

        public async Task LoadDimensionsOnlyAsync()
        {
            if (AreDimensionsLoaded)
                return;

            try
            {
                // Simple header reading first
                if (await LoadDimensionsAsync())
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

        private async Task<bool> LoadDimensionsAsync()
        {
            try
            {
                ImageInfo info = await Image.IdentifyAsync(FilePath);
                if (info != null)
                {
                    Width = info.Width;
                    Height = info.Height;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

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

        #endregion Loading

        #region Image Properties

        public double DpiX { get; protected set; }

        public double DpiY { get; protected set; }

        public int BitsPerPixel { get; protected set; }

        public string PixelFormat { get; protected set; } = string.Empty;

        public bool HasAlphaChannel { get; protected set; }

        public string ColorSpace { get; protected set; } = string.Empty;

        #endregion Image Properties

        #region EXIF Properties

        public DateTime? DateTaken { get; protected set; }

        public string? CameraMake { get; protected set; }

        public string? CameraModel { get; protected set; }

        public string? LensModel { get; protected set; }

        public double? FocalLength { get; protected set; }

        public string FormattedFocalLength => FocalLength.HasValue ? $"{FocalLength:F0}mm" : string.Empty;

        /// <summary>
        /// F-stop / aperture
        /// </summary>
        public double? FNumber { get; protected set; }

        public string FormattedAperture => FNumber.HasValue ? $"f/{FNumber:F1}" : string.Empty;

        public double? ExposureTime { get; protected set; }

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

        public int? IsoSpeed { get; protected set; }

        public string FormattedIso => IsoSpeed.HasValue ? $"ISO {IsoSpeed}" : string.Empty;

        public double? ExposureBias { get; protected set; }

        public string? FlashMode { get; protected set; }

        public bool? FlashFired { get; protected set; }

        public string? WhiteBalance { get; protected set; }

        public int ExifOrientation { get; protected set; } = 1;

        public string? Title { get; protected set; }

        public string? Description { get; protected set; }

        public string? Copyright { get; protected set; }

        public string? Software { get; protected set; }

        public string? Author { get; protected set; }

        #endregion EXIF Properties

        #region GPS Properties

        public double? GpsLatitude { get; protected set; }

        public double? GpsLongitude { get; protected set; }

        public double? GpsAltitude { get; protected set; }

        public bool HasGpsCoordinates => GpsLatitude.HasValue && GpsLongitude.HasValue;

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
    }
}