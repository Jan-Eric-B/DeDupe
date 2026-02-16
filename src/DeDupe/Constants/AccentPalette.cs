using Windows.UI;

namespace DeDupe.Constants
{
    public record AccentPalette(Color Base, Color Light1, Color Light2, Color Light3, Color Dark1, Color Dark2, Color Dark3)
    {
        public static AccentPalette Purple { get; } = new(
            Base: Color.FromArgb(255, 140, 99, 234),    // #8C63EA
            Light1: Color.FromArgb(255, 166, 123, 240), // #A67BF0
            Light2: Color.FromArgb(255, 190, 160, 243), // #BEA0F3
            Light3: Color.FromArgb(255, 227, 211, 255), // #E3D3FF
            Dark1: Color.FromArgb(255, 117, 79, 206),   // #754FCE
            Dark2: Color.FromArgb(255, 90, 58, 174),    // #5A3AAE
            Dark3: Color.FromArgb(255, 66, 37, 134)     // #422586
        );
    }
}