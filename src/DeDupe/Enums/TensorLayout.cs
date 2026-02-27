using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum TensorLayout
    {
        [Description("NCHW (Channels First)")]
        NCHW,

        [Description("NHWC (Channels Last)")]
        NHWC
    }
}