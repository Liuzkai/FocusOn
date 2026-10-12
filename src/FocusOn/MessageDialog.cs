using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace FocusOn;

public static class MessageDialog
{
    public static async Task<bool> ShowAsync(Window owner, string message, bool confirm = false)
    {
        var dialog = new Window { Title = confirm ? "确认删除日期" : "FocusOn", Width = 500, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var accept = new Button { Content = confirm ? "删除" : "确定", Name = "AcceptButton" };
        var cancel = new Button { Content = "取消", Name = "CancelButton", IsVisible = confirm };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel); buttons.Children.Add(accept);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(buttons); dialog.Content = panel;
        accept.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Opened += (_, _) => { if (confirm) cancel.Focus(); else accept.Focus(); };
        return await dialog.ShowDialog<bool>(owner);
    }
}
