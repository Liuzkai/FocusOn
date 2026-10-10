using System.Windows;
using System.Windows.Media;

namespace FocusOn;

public static class ReviewTheme
{
    private static readonly Brush DarkBackground = Brush(0x22, 0x22, 0x22);
    private static readonly Brush DarkSurface = Brush(0x29, 0x29, 0x29);
    private static readonly Brush DarkHover = Brush(0x36, 0x36, 0x36);
    private static readonly Brush DarkBorder = Brush(0x3A, 0x3A, 0x3A);
    private static readonly Brush LightText = Brush(0xE5, 0xE5, 0xE5);
    private static readonly Brush MutedText = Brush(0xA0, 0xA2, 0xA8);
    private static readonly Brush BlueAccent = Brush(0x78, 0xA4, 0xEF);
    public static Brush Background => SystemParameters.HighContrast ? SystemColors.WindowBrush : DarkBackground;
    public static Brush Surface => SystemParameters.HighContrast ? SystemColors.WindowBrush : DarkSurface;
    public static Brush Hover => SystemParameters.HighContrast ? SystemColors.WindowBrush : DarkHover;
    public static Brush Border => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : DarkBorder;
    public static Brush Foreground => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : LightText;
    public static Brush Muted => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MutedText;
    public static Brush Accent => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : BlueAccent;

    public static void Apply(ResourceDictionary resources)
    {
        resources["ReviewBackground"] = Background;
        resources["ReviewSurface"] = Surface;
        resources["ReviewHover"] = Hover;
        resources["ReviewBorder"] = Border;
        resources["ReviewText"] = Foreground;
        resources["ReviewMuted"] = Muted;
        resources["ReviewAccent"] = Accent;
    }

    public static SolidColorBrush Brush(byte red, byte green, byte blue, byte alpha = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }

    public static Brush BadgeBackground(Brush color) => SystemParameters.HighContrast ? SystemColors.WindowBrush
        : color is SolidColorBrush solid ? Brush(solid.Color.R, solid.Color.G, solid.Color.B, 42) : Surface;
}
