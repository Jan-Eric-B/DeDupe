using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum GroupFilterOption
    {
        [Description("All Groups")]
        All,

        [Description("Exact Matches (100%)")]
        ExactMatchesOnly,

        [Description("Similar Only (<100%)")]
        SimilarOnly
    }
}