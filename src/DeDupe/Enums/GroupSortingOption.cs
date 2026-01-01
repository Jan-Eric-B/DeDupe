using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum GroupSortingOption
    {
        [Description("Similarity")]
        Similarity,

        [Description("Image Count")]
        ImageCount,

        [Description("Name")]
        Name
    }
}