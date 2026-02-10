using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace DeDupe.Services
{
    /// <summary>
    /// Validates and sanitizes folder names for Windows compatibility.
    /// </summary>
    public static partial class FolderNameValidationService
    {
        private static readonly char[] InvalidCharacters = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

        private static readonly string[] InvalidNames =
        [
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ];

        private const int MaxFolderNameLength = 255;

        /// <summary>
        /// Validates specified folder name.
        /// </summary>
        /// <remarks>
        /// Must not exceed maximum allowed length, contain invalid or control characters, end with a period or space,
        /// or match any reserved names.
        /// </remarks>
        public static bool Validate(string? name)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(name);

            string trimmedName = name.Trim();

            // Length
            if (trimmedName.Length > MaxFolderNameLength)
            {
                return false;
            }

            // Invalid characters
            char[] foundInvalidChars = [.. trimmedName.Where(c => InvalidCharacters.Contains(c)).Distinct()];

            if (foundInvalidChars.Length > 0)
            {
                return false;
            }

            // Control characters (ASCII 0-31)
            if (trimmedName.Any(c => c < 32))
            {
                return false;
            }

            // Check if name ends with period or space
            if (trimmedName.EndsWith('.') || trimmedName.EndsWith(' '))
            {
                return false;
            }

            // Invalid names (case-insensitive)
            if (InvalidNames.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sanitizes specified folder or file name by removing or replacing invalid characters.
        /// </summary>
        public static string? Sanitize(string? name, char replacement = '_')
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(name);

            string sanitized = name.Trim();

            // Invalid characters
            foreach (char invalidChar in InvalidCharacters)
            {
                sanitized = sanitized.Replace(invalidChar, replacement);
            }

            // Control characters
            sanitized = ControlCharacterRegex().Replace(sanitized, string.Empty);

            // Trailing periods and spaces
            sanitized = sanitized.TrimEnd('.', ' ');

            // Invalid names
            if (InvalidNames.Contains(sanitized, StringComparer.OrdinalIgnoreCase))
            {
                sanitized = $"{sanitized}_";
            }

            // Too long
            if (sanitized.Length > MaxFolderNameLength)
            {
                sanitized = sanitized[..MaxFolderNameLength].TrimEnd('.', ' ');
            }

            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        // Precompiled regex for control characters (ASCII 0-31)
        [GeneratedRegex(@"[\x00-\x1F]")]
        private static partial Regex ControlCharacterRegex();
    }
}