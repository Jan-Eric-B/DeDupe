namespace DeDupe.Services
{
    public static class FileService
    {
        /// <summary>
        /// Format file size to human readable string
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB"];
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return suffixIndex == 0
                ? $"{size:N0} {suffixes[suffixIndex]}"
                : $"{size:N1} {suffixes[suffixIndex]}";
        }
    }
}