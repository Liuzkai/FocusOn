using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace FocusOn.UiTests;

public sealed class WindowResidencyTests
{
    [AvaloniaFact]
    public void Close_hides_same_window_with_tray_and_loss_restores_it_before_explicit_exit()
    {
        var window = new Window(); var closed = false; var usable = false;
        window.Closed += (_, _) => closed = true;
        using var residency = new WindowResidency(window, value => usable = value);
        window.Show(); residency.SetTrayAvailable(true); window.Close();
        Assert.True(usable); Assert.False(window.IsVisible); Assert.False(closed);
        residency.Restore(); Assert.True(window.IsVisible);
        window.Close(); residency.SetTrayAvailable(false);
        Assert.False(usable); Assert.True(window.IsVisible); Assert.False(closed);
        window.Close(); Assert.True(window.IsVisible); Assert.False(closed);
        residency.BeginExit(); window.Close(); Assert.True(closed);
    }

    [AvaloniaFact]
    public void Missing_tray_keeps_window_recoverable_and_explanation_follows_saved_language()
    {
        Localizer.SetLanguage("zh-CN");
        var window = new MainWindow();
        using var residency = new WindowResidency(window, window.SetTrayAvailable);
        try
        {
            window.Show();
            residency.SetTrayAvailable(false); window.Close();
            Assert.True(window.IsVisible);
            var notice = window.FindControl<TextBlock>("ResidencyText")!;
            Assert.True(notice.IsVisible); Assert.StartsWith("系统托盘不可用", notice.Text);
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 1;
            Assert.StartsWith("The system tray is unavailable", notice.Text);
            residency.SetTrayAvailable(true); Assert.False(notice.IsVisible);
        }
        finally { residency.BeginExit(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }
}
