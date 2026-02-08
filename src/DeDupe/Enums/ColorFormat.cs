using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum ColorFormat
    {
        [Description("RGB 8-bit")]
        RGB8,

        [Description("BGR 8-bit")]
        BGR8,

        [Description("Grayscale 8-bit")]
        Gray8,

        [Description("RGB 16-bit")]
        RGB16,

        [Description("BGR 16-bit")]
        BGR16
    }
}