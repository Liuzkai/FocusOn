namespace FocusOn;

public static class MacPermissions
{
    /// <summary>Read-only TCC check: requesting access remains an explicit user action in System Settings.</summary>
    public static bool IsAccessibilityTrusted => OperatingSystem.IsMacOS() && MacNative.AXIsProcessTrusted();
}
