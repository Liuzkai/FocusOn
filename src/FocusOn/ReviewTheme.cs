using Avalonia.Media;
using Avalonia.Controls;

namespace FocusOn;

public static class ReviewTheme
{
    public static bool HighContrast { get; private set; }
    public static IBrush Background => HighContrast ? Brushes.Black : Brush.Parse("#222222");
    public static IBrush Surface => HighContrast ? Brushes.Black : Brush.Parse("#292929");
    public static IBrush Border => HighContrast ? Brushes.White : Brush.Parse("#44464B");
    public static IBrush Foreground => HighContrast ? Brushes.White : Brush.Parse("#E5E5E5");
    public static IBrush Muted => HighContrast ? Brushes.White : Brush.Parse("#A0A2A8");
    public static IBrush Accent => HighContrast ? Brushes.Yellow : Brush.Parse("#78A4EF");
    public static void Apply(IResourceDictionary resources, bool highContrast)
    {
        HighContrast = highContrast;
        resources["ReviewBackground"] = Background; resources["ReviewSurface"] = Surface; resources["ReviewText"] = Foreground;
        resources["ReviewMuted"] = Muted; resources["ReviewAccent"] = Accent;
        resources["ReviewWarningBackground"] = highContrast ? Brushes.Black : Brush.Parse("#393029");
        resources["ReviewWarningText"] = highContrast ? Brushes.Yellow : Brush.Parse("#F2C98C");
        resources["ReviewBadge"] = highContrast ? Brushes.Black : Brush.Parse("#303034");
    }
    private static ApplicationColors applicationColors = new();
    public static void LoadApplicationColors(string directory) => applicationColors = new(directory);
    public static void RegisterApplications(IEnumerable<string?> keys) => applicationColors.Register(keys);
    public static IBrush ApplicationBrush(string? key)
    {
        if (HighContrast) return Foreground;
        return applicationColors.Get(key);
    }
}
