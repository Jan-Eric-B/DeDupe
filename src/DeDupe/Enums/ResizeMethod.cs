using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum ResizeMethod
    {
        [Description("Padding")]
        Padding,

        [Description("Stretch")]
        Stretch,

        [Description("Crop")]
        Crop
    }
}