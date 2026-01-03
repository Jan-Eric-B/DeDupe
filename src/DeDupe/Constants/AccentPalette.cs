using Windows.UI;

namespace DeDupe.Constants
{
    public record AccentPalette(Color Base, Color Light1, Color Light2, Color Light3, Color Dark1, Color Dark2, Color Dark3)
    {
        public static AccentPalette Purple { get; } = new(
            Base: Color.FromArgb(255, 139, 92, 246),
            Light1: Color.FromArgb(255, 167, 139, 250),
            Light2: Color.FromArgb(255, 196, 181, 253),
            Light3: Color.FromArgb(255, 221, 214, 254),
            Dark1: Color.FromArgb(255, 124, 58, 237),
            Dark2: Color.FromArgb(255, 109, 40, 217),
            Dark3: Color.FromArgb(255, 91, 33, 182)
        );
    }
}