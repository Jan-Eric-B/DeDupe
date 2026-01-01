using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum SelectionStrategy
    {
        [Description("Keep Highest Resolution (Selects lower resolution items)")]
        KeepHighestResolution,

        [Description("Keep Lowest Resolution (Selects higher resolution items)")]
        KeepLowestResolution,

        [Description("Keep Newest (Selects older items)")]
        KeepNewest,

        [Description("Keep Oldest (Selects newer items)")]
        KeepOldest,

        [Description("Keep Largest File Size (Selects smaller files)")]
        KeepLargestFileSize,

        [Description("Keep Smallest File Size (Selects larger files)")]
        KeepSmallestFileSize,

        [Description("Keep All (deselects all)")]
        KeepAll,

        [Description("Keep None (selects all)")]
        KeepNone
    }
}