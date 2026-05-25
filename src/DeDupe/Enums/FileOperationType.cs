using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum FileOperationType
    {
        [Description("Move")]
        Move,

        [Description("Copy")]
        Copy,

        [Description("Extract")]
        Extract
    }
}