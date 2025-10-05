using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeDupe.Models
{
    /// <summary>
    /// Holds File and Folder paths in the media selection list
    /// </summary>
    public partial class SourcePathItem : INotifyPropertyChanged
    {
        /// <summary>
        /// Full path of file or folder
        /// </summary>
        private string _path = string.Empty;

        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Is folder or file
        /// </summary>
        private bool _isFolder;

        public bool IsFolder
        {
            get => _isFolder;
            set
            {
                if (_isFolder != value)
                {
                    _isFolder = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IconGlyph));
                }
            }
        }

        /// <summary>
        /// Folder has subdirectories
        /// </summary>
        private bool _hasSubdirectories;

        public bool HasSubdirectories
        {
            get => _hasSubdirectories;
            set
            {
                if (_hasSubdirectories != value)
                {
                    _hasSubdirectories = value;
                    OnPropertyChanged();
                    // Only show checkbox if it has subdirectories
                    OnPropertyChanged(nameof(ShowSubdirectoriesCheckbox));
                }
            }
        }

        /// <summary>
        /// Include subdirectories
        /// </summary>
        private bool _includeSubdirectories;

        public bool IncludeSubdirectories
        {
            get => _includeSubdirectories;
            set
            {
                if (_includeSubdirectories != value)
                {
                    _includeSubdirectories = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Show subdirectories checkbox
        /// </summary>
        public bool ShowSubdirectoriesCheckbox => IsFolder && HasSubdirectories;

        /// <summary>
        /// Gets icon glyph
        /// </summary>
        public string IconGlyph => IsFolder ? "\uE8B7" : "\uEB9F";

        #region INotifyPropertyChanged

        /// <summary>
        /// Property changed event
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)

        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}