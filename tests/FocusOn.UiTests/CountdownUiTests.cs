using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace FocusOn.UiTests;

public sealed class CountdownUiTests
{
    private static DayView View(TitleTimerState? timer=null)=>new(new(2026,10,14),DateTimeOffset.UtcNow.AddHours(-1),DateTimeOffset.UtcNow,[],[],"正在记录",null,timer);
    private static void Pump(Window w)
    {
        w.UpdateLayout(); Dispatcher.UIThread.RunJobs(); w.UpdateLayout();
        // A non-activating window may not have committed its first scene yet.
        // Pointer checks require an actual render tick, not only measured bounds.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
    }
    private static void Click(Window w,string name)=>w.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    [AvaloniaFact]
    public async Task Countdown_presets_custom_duration_and_saved_state_share_existing_timer_controls_in_both_languages()
    {
        Localizer.SetLanguage("en");TitleTimerState? state=null;TimeSpan? requested=null;
        var w=new MainWindow(_=>Task.FromResult(View(state)),_=>Task.CompletedTask,(action,_)=>
        {state=action==TimerAction.Stop?null:state! with{IsRunning=action==TimerAction.Resume};return Task.CompletedTask;},
        startCountdown:(title,duration)=>{requested=duration;state=new(title!,TimeSpan.Zero,true,duration,Id:7);return Task.CompletedTask;});
        try
        {
            w.Show();await w.StartRefreshAsync();w.FindControl<ComboBox>("TimerModePicker")!.SelectedIndex=1;
            Assert.True(w.FindControl<StackPanel>("CountdownOptions")!.IsVisible);Assert.Equal("Start countdown",w.FindControl<Button>("StartTimerButton")!.Content);
            w.FindControl<ComboBox>("CountdownPresetPicker")!.SelectedIndex=1;
            Assert.Equal(5,w.FindControl<NumericUpDown>("CountdownMinutesBox")!.Value);
            w.FindControl<NumericUpDown>("CountdownMinutesBox")!.Value=12;
            Assert.Equal(3,w.FindControl<ComboBox>("CountdownPresetPicker")!.SelectedIndex);
            Click(w,"StartTimerButton");await w.RefreshAsync();Assert.Equal(TimeSpan.FromMinutes(12),requested);
            Assert.Equal("Countdown",state!.Title);Assert.Equal("Remaining 12:00",w.FindControl<TextBlock>("TimerElapsedText")!.Text); Assert.Equal(12,w.FindControl<NumericUpDown>("CountdownMinutesBox")!.Value);
            Assert.False(w.FindControl<ComboBox>("TimerModePicker")!.IsEnabled);
            Click(w,"PauseTimerButton");Assert.False(state.IsRunning);Assert.Contains("Paused",w.FindControl<TextBlock>("TimerElapsedText")!.Text);
            w.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex=0;Assert.Contains("剩余",w.FindControl<TextBlock>("TimerElapsedText")!.Text);Assert.Equal("Countdown",w.FindControl<TextBox>("TimerTitleBox")!.Text);
            Click(w,"PauseTimerButton");Assert.True(state.IsRunning);Click(w,"StopTimerButton");Assert.Null(state);Assert.True(w.FindControl<Button>("StartTimerButton")!.IsVisible);
        }
        finally{w.StopRefresh();w.Close();Localizer.SetLanguage("zh-CN");}
    }

    [AvaloniaFact]
    public async Task Completion_releases_inputs_and_does_not_overwrite_the_next_task_title_on_refresh()
    {
        Localizer.SetLanguage("zh-CN");
        var completed=new TitleTimerState("Old task",TimeSpan.FromMinutes(25),false,TimeSpan.FromMinutes(25),true,8);
        var w=new MainWindow(_=>Task.FromResult(View(completed)),_=>Task.CompletedTask,(_,_)=>Task.CompletedTask);
        try
        {
            w.Show();await w.StartRefreshAsync();Assert.False(w.FindControl<TextBox>("TimerTitleBox")!.IsReadOnly);
            w.FindControl<TextBox>("TimerTitleBox")!.Text="Next task";await w.RefreshAsync();w.UpdateTimer(completed);
            Assert.Equal("Next task",w.FindControl<TextBox>("TimerTitleBox")!.Text);
            Assert.False(w.FindControl<Button>("PauseTimerButton")!.IsVisible);Assert.Contains("已完成",w.FindControl<TextBlock>("TimerElapsedText")!.Text);
        }
        finally{w.StopRefresh();w.Close();}
    }

    [AvaloniaFact]
    public void Companion_reuses_one_window_hides_per_task_restores_on_click_and_disposes_without_stopping_the_timer()
    {
        Localizer.SetLanguage("zh-CN");
        var main=new Window();main.Show();var opened=0;using var owner=new TimerCompanionController(main,()=>opened++);
        try
        {
            var state=new TitleTimerState("User title",TimeSpan.FromSeconds(20),true,TimeSpan.FromMinutes(1),Id:9);
            owner.Update(state);var widget=owner.Window!;Assert.True(widget.IsVisible);Assert.False(widget.ShowInTaskbar);Assert.False(widget.ShowActivated);
            Assert.Equal("00:40",widget.FindControl<TextBlock>("TimeText")!.Text); Assert.Equal("33%",widget.FindControl<TextBlock>("ProgressText")!.Text);
            Pump(widget); Assert.True(widget.InputHitTest(new Point(100,35)) is not null, $"Hit test missing {widget.Bounds} / {widget.FindControl<Border>("Surface")!.Bounds}"); widget.MouseMove(new Point(100,35)); widget.MouseDown(new Point(100,35),MouseButton.Left);widget.MouseUp(new Point(100,35),MouseButton.Left);Assert.Equal(1,opened);
            Click(widget,"HideButton");Assert.False(widget.IsVisible);owner.SetEnabled(true);owner.Update(state with {Duration=TimeSpan.FromSeconds(30)});Assert.False(widget.IsVisible);
            owner.Update(state with {Id=10,IsRunning=false});Assert.Same(widget,owner.Window);Assert.True(widget.IsVisible);Assert.Contains("已暂停",widget.FindControl<TextBlock>("Caption")!.Text);
            owner.SetEnabled(false);Assert.False(widget.IsVisible);owner.SetEnabled(true);Assert.True(widget.IsVisible);
            owner.Update(null);Assert.False(widget.IsVisible);owner.Dispose();Assert.Null(owner.Window);
        }
        finally{main.Close();}
    }

    [AvaloniaFact]
    public void Companion_drag_does_not_restore_and_screen_clamping_handles_removed_monitors()
    {
        Localizer.SetLanguage("zh-CN");
        var resources=Application.Current!.Resources; var hadSurface=resources.TryGetResource("ReviewSurface", null, out var oldSurface);
        resources.Remove("ReviewSurface"); // Cold startup: hit targets must not depend on a resolved theme brush.
        var opened=0;var w=new TimerCompanion(()=>opened++,()=>{});try
        {
            w.Update(new("Drag",TimeSpan.Zero,true));w.Show();Pump(w);Assert.True(w.InputHitTest(new Point(100,30)) is not null, $"Hit test missing {w.Bounds} / {w.FindControl<Border>("Surface")!.Bounds}");var start=w.Position;
            w.MouseDown(new Point(100,30),MouseButton.Left);w.MouseMove(new Point(125,40));w.MouseUp(new Point(125,40),MouseButton.Left);
            Assert.Equal(0,opened);Assert.NotEqual(start,w.Position);
            Assert.Equal(new PixelPoint(780,500),TimerCompanion.Clamp(new(4000,3000),new(0,0,1040,580),260,80));
            Assert.Equal(new PixelPoint(-1920,0),TimerCompanion.Clamp(new(-4000,-3000),new(-1920,0,1920,1080),260,80));
        }
        finally{w.DisposeWindow();if(hadSurface)resources["ReviewSurface"]=oldSurface;}
    }

    [AvaloniaFact]
    public void Saved_widget_switch_changes_preference_without_modifying_timer_state()
    {
        var saved=new AppPreferences();var w=new MainWindow(_=>Task.FromResult(View()),_=>Task.CompletedTask,(_,_)=>Task.CompletedTask);
        try
        {
            w.ConfigureSettings(Path.GetTempPath(),true,saved,next=>saved=next,_=>Task.CompletedTask,_=>Task.CompletedTask);w.Show();Click(w,"SettingsPageButton");
            w.FindControl<CheckBox>("TimerWidgetCheckBox")!.IsChecked=false;Assert.False(saved.TimerWidget);
            w.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex=1;Assert.Equal("Show compact timer",w.FindControl<CheckBox>("TimerWidgetCheckBox")!.Content);
        }
        finally{w.StopRefresh();w.Close();Localizer.SetLanguage("zh-CN");}
    }
}
