using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.Models.Input
{
    /// <summary>
    /// Represents a file or folder path in the input list.
    /// </summary>
    public partial class InputListItem : ObservableObject
    {
        [ObservableProperty]
        public partial string Path { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IconGlyph))]
        public partial bool IsFolder { get; set; }

        [ObservableProperty]
        public partial bool HasSubdirectories { get; set; }

        [ObservableProperty]
        public partial bool IncludeSubdirectories { get; set; }

        public string IconGlyph => IsFolder ? "\uE8B7" : "\uEB9F";
    }
}