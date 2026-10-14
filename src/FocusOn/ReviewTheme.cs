using Avalonia.Media;
using Avalonia.Controls;

namespace FocusOn;

public static class ReviewTheme
{
    public static string Theme { get; private set; } = "dark";
    public static bool Light => Theme == "light";
    public static bool HighContrast { get; private set; }
    public static IBrush Background => HighContrast ? Brushes.Black : Brush.Parse(Light ? "#F2F4F8" : "#222222");
    public static IBrush Surface => HighContrast ? Brushes.Black : Brush.Parse(Light ? "#FFFFFF" : "#292929");
    public static IBrush Border => HighContrast ? Brushes.White : Brush.Parse(Light ? "#CDD3DE" : "#44464B");
    public static IBrush Foreground => HighContrast ? Brushes.White : Brush.Parse(Light ? "#202A3A" : "#E5E5E5");
    public static IBrush Muted => HighContrast ? Brushes.White : Brush.Parse(Light ? "#596476" : "#A0A2A8");
    public static IBrush Accent => HighContrast ? Brushes.Yellow : Brush.Parse(Light ? "#245DB6" : "#78A4EF");
    public static void Apply(IResourceDictionary resources, bool highContrast, string? theme = null)
    {
        if (theme is not null) Theme = theme;
        HighContrast = highContrast;
        resources["ReviewBackground"] = Background; resources["ReviewSurface"] = Surface; resources["ReviewText"] = Foreground;
        resources["ReviewMuted"] = Muted; resources["ReviewAccent"] = Accent;
        resources["ReviewWarningBackground"] = highContrast ? Brushes.Black : Brush.Parse(Light ? "#FFF3DB" : "#393029");
        resources["ReviewWarningText"] = highContrast ? Brushes.Yellow : Brush.Parse(Light ? "#805016" : "#F2C98C");
        resources["ReviewBadge"] = highContrast ? Brushes.Black : Brush.Parse(Light ? "#E9EDF4" : "#303034");
    }
    private static ApplicationColors applicationColors = new();
    public static void LoadApplicationColors(string directory) => applicationColors = new(directory);
    public static void RegisterApplications(IEnumerable<string?> keys) => applicationColors.Register(keys);
    public static IBrush ApplicationBrush(string? key)
    {
        if (HighContrast) return Foreground;
        var brush = applicationColors.Get(key);
        if (!Light || brush is not SolidColorBrush solid) return brush;
        var hsl = solid.Color.ToHsl();
        var color = HslColor.FromHsl(hsl.H, hsl.S, .28).ToRgb();
        return new SolidColorBrush(color);
    }
}
