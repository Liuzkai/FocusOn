using Avalonia;

namespace FocusOn;

public static class DesktopRendering
{
    public static AppBuilder Configure(AppBuilder builder) => OperatingSystem.IsMacOS()
        // The modest timeline needs no GPU. Software avoids Metal shader timeouts
        // on virtual Intel Macs and uses the same native Cocoa window/menu backend.
        ? builder.With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software], AppSandboxEnabled = false })
        : builder;
}
