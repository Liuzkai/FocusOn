using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace FocusOn.UiTests;

public sealed class SettingsPageTests
{
    private static void Click(MainWindow w, string name) => w.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static DayView View()
    {
        var date = DateOnly.FromDateTime(DateTime.Today); var (start,end) = DailyProjection.Bounds(date,TimeZoneInfo.Local);
        return new(date,start,end,[new(1,IntervalKind.Usage,start.AddHours(9),start.AddHours(10),TimeSpan.FromHours(1),"editor","User editor","Private title")],[new("editor","User editor",TimeSpan.FromHours(1))],"正在记录",null);
    }
    [AvaloniaFact]
    public async Task Settings_navigation_saves_language_and_theme_without_losing_review_selection_or_zoom()
    {
        var directory = Path.Combine(Path.GetTempPath(),"FocusOn-settings-ui-"+Guid.NewGuid().ToString("N")); Localizer.SetLanguage("zh-CN");
        var w = new MainWindow(_=>Task.FromResult(View()),_=>Task.CompletedTask,(_,_)=>Task.CompletedTask,l=>LanguagePreference.Save(directory,l),()=>View().StartUtc.AddHours(12));
        try
        {
            w.ConfigureSettings(directory,false,new(),v=>v.Save(directory),_=>Task.CompletedTask,_=>Task.CompletedTask);
            w.Show(); await w.StartRefreshAsync(); w.FindControl<ComboBox>("RangePicker")!.SelectedIndex=3; await w.RefreshAsync(); w.UpdateLayout();
            var grid=w.FindControl<DataGrid>("LogGrid")!; var selected=grid.ItemsSource.Cast<IntervalRow>().First(row=>row.Title=="Private title"); grid.SelectedItem=selected;
            var timeline=w.FindControl<TimelineControl>("TimelineView")!; timeline.ZoomIn(); var start=timeline.VisibleStart;
            Click(w,"SettingsPageButton"); Assert.True(w.FindControl<ScrollViewer>("SettingsPane")!.IsVisible); Assert.False(w.FindControl<Grid>("ReviewPane")!.IsVisible);
            w.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex=1; Assert.Equal("en",LanguagePreference.Load(directory));
            w.FindControl<ComboBox>("ThemePicker")!.SelectedIndex=1; Assert.Equal("light",AppPreferences.Load(directory).Theme);
            Assert.Equal(ThemeVariant.Light,Application.Current!.RequestedThemeVariant); Assert.Equal(Color.Parse("#202A3A"),((ISolidColorBrush)ReviewTheme.Foreground).Color);
            Assert.Contains(ReleaseUpdates.CurrentVersion.ToString(3),w.FindControl<TextBlock>("VersionText")!.Text);
            w.FindControl<CheckBox>("StartupCheckBox")!.IsChecked=false; Assert.False(AppPreferences.Load(directory).StartAtLogin);
            Click(w,"StatisticsPageButton"); Click(w,"ReviewPageButton"); Assert.Same(selected,grid.SelectedItem); Assert.Equal(start,timeline.VisibleStart);
            Assert.Equal("Private title", ((IntervalRow)selected!).Title);
        }
        finally { w.ApplySelectedTheme("dark"); w.StopRefresh(); w.Close(); Localizer.SetLanguage("zh-CN"); if(Directory.Exists(directory))Directory.Delete(directory,true); }
    }
    [AvaloniaFact]
    public void Failed_setting_save_rolls_back_theme_and_isolation_disables_startup_control()
    {
        var w=new MainWindow(_=>Task.FromResult(View()),_=>Task.CompletedTask,(_,_)=>Task.CompletedTask);
        try
        {
            w.ConfigureSettings(Path.GetTempPath(),true,new(),_=>throw new IOException("denied"),_=>Task.CompletedTask,_=>Task.CompletedTask); w.Show(); Click(w,"SettingsPageButton");
            Assert.False(w.FindControl<CheckBox>("StartupCheckBox")!.IsEnabled);
            w.FindControl<ComboBox>("ThemePicker")!.SelectedIndex=1;
            Assert.Equal(0,w.FindControl<ComboBox>("ThemePicker")!.SelectedIndex); Assert.Equal("dark",ReviewTheme.Theme);
            Assert.Contains("denied",w.FindControl<TextBlock>("SettingsError")!.Text);
            ReviewTheme.Apply(Application.Current!.Resources,true,"light"); w.ApplySelectedTheme("light"); Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant); Assert.Equal(Brushes.White,ReviewTheme.Foreground); Assert.Equal(Brushes.Black,ReviewTheme.Surface);
            Assert.True(w.FindControl<Button>("ExitButton")!.IsVisible);
        }
        finally { ReviewTheme.Apply(Application.Current!.Resources,false,"dark"); w.ApplySelectedTheme("dark"); w.StopRefresh(); w.Close(); }
    }
}
