using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum AppTheme
    {
        [Description("System Default")]
        System = 0,

        [Description("Light")]
        Light = 1,

        [Description("Dark")]
        Dark = 2
    }
}