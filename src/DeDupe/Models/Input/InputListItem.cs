using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.Models.Input
{
    /// <summary>
    /// Represents a file or folder path in the input list.
    /// </summary>
    public partial class InputListItem : ObservableObject
    {
        /// <summary>
        /// Full path of file or folder.
        /// </summary>
        [ObservableProperty]
        public partial string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether this item is a folder (true) or file (false).
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IconGlyph))]
        [NotifyPropertyChangedFor(nameof(ShowSubdirectoriesCheckbox))]
        public partial bool IsFolder { get; set; }

        /// <summary>
        /// Whether the folder contains subdirectories.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowSubdirectoriesCheckbox))]
        public partial bool HasSubdirectories { get; set; }

        /// <summary>
        /// Whether to include files from subdirectories when scanning.
        /// </summary>
        [ObservableProperty]
        public partial bool IncludeSubdirectories { get; set; }

        /// <summary>
        /// Whether to show the subdirectories checkbox in the UI.
        /// </summary>
        public bool ShowSubdirectoriesCheckbox => IsFolder && HasSubdirectories;

        /// <summary>
        /// Icon glyph for displaying in the UI.
        /// </summary>
        public string IconGlyph => IsFolder ? "\uE8B7" : "\uEB9F";
    }
}