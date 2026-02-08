using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum OutputFormat
    {
        [Description("PNG")]
        PNG,

        [Description("JPEG")]
        JPEG,

        [Description("BMP")]
        BMP,

        [Description("TIFF")]
        TIFF,

        [Description("WebP")]
        WebP
    }
}