using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum InterpolationMethod
    {
        [Description("Nearest Neighbor")]
        NearestNeighbor,

        [Description("Bilinear")]
        Bilinear,

        [Description("Bicubic")]
        Bicubic,

        [Description("Mitchell-Netravali")]
        MitchellNetravali,

        [Description("Lanczos")]
        Lanczos


    }
}