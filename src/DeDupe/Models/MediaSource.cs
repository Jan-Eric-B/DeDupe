using DeDupe.Enums;
using System;
using System.IO;

namespace DeDupe.Models
{
    /// <summary>
    /// Base abstract class for all media sources (images, videos)
    /// </summary>
    public abstract class MediaSource : IMediaItem
    {
        /// <summary>
        /// Full path of media file
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// File name of media file
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// Extension of media file
        /// </summary>
        public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();

        /// <summary>
        /// Size of media file in bytes
        /// </summary>
        public long FileSize { get; protected set; }

        /// <summary>
        /// Date the media file was last modified
        /// </summary>
        public DateTime LastModified { get; protected set; }

        /// <summary>
        /// Width of media file in pixels
        /// </summary>
        public int Width { get; protected set; }

        /// <summary>
        /// Height of media file in pixels
        /// </summary>
        public int Height { get; protected set; }

        /// <summary>
        /// Media type (image or video)
        /// </summary>
        public MediaType Type { get; protected set; }

        /// <summary>
        /// Create new instance of MediaSource
        /// </summary>
        /// <param name="filePath">Full path of media file</param>
        protected MediaSource(string filePath)
        {
            FilePath = filePath;
            // Get file info (size and last modified date)
            FileInfo? fileInfo = new(filePath);
            FileSize = fileInfo.Length;
            LastModified = fileInfo.LastWriteTime;
        }

        /// <summary>
        /// If media file exists and is accessible
        /// </summary>
        /// <returns>True if media file exists and is accessible, otherwise false</returns>
        public bool Exists()
        {
            return File.Exists(FilePath);
        }

        /// <summary>
        /// Display name of media source
        /// </summary>
        public virtual string GetDisplayName()
        {
            return FileName;
        }
    }
}