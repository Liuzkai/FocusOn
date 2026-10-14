using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FocusOn;

public partial class TimerCompanion : Window
{
    private readonly Action restore, hide;
    private PixelPoint? press;
    private PixelPoint origin;
    private bool dragged;
    public TimerCompanion() : this(() => { }, () => { }) { }
    public TimerCompanion(Action restore, Action hide)
    {
        this.restore=restore;this.hide=hide; InitializeComponent();
        HideButton.Click += (_,_)=>this.hide();
        AddHandler(PointerPressedEvent, (_,e)=>
        {
            if (new Rect(HideButton.Bounds.Size).Contains(e.GetPosition(HideButton)) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            press=this.PointToScreen(e.GetPosition(this));origin=Position;dragged=false;e.Pointer.Capture(Surface);e.Handled=true;
        }, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, (_,e)=>
        {
            if(press is not { } start) return;
            var at=this.PointToScreen(e.GetPosition(this));var delta=at-start;
            if(Math.Abs(delta.X)+Math.Abs(delta.Y)>4) dragged=true;
            if(dragged)Position=new(origin.X+delta.X,origin.Y+delta.Y);
        }, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_,e)=>
        {
            if(press is null)return;press=null;e.Pointer.Capture(null);e.Handled=true;
            KeepOnScreen();if(!dragged)this.restore();
        }, RoutingStrategies.Tunnel);
        Surface.PointerCaptureLost += (_,_)=>press=null;
        KeyDown += (_,e)=> { if(e.Key is Key.Enter or Key.Space){this.restore();e.Handled=true;} };
        Closing += (_,e)=> { if(!disposing){e.Cancel=true;this.hide();} };
    }
    private bool disposing;
    public void Update(TitleTimerState state)
    {
        TimeText.Text=TimerPresentation.Time(state);
        ProgressText.IsVisible=state.IsCountdown; ProgressText.Text=state.IsCountdown?$"{Math.Floor(state.Progress*100):0}%":string.Empty;
        var mode=Localizer.Get(state.IsCompleted?"倒计时完成":!state.IsRunning?"已暂停":state.IsCountdown?"倒计时":"计时");
        Caption.Text=mode+" · "+state.Title;
        Dial.State=state;Dial.InvalidateVisual();
        Title="FocusOn · "+TimerPresentation.Summary(state);
        ToolTip.SetTip(Surface,state.Title+"\n"+TimerPresentation.Summary(state)+"\n"+Localizer.Get("点击打开 FocusOn，拖动移动"));
        AutomationProperties.SetName(Surface,state.Title+" · "+TimerPresentation.Summary(state));Surface.Focusable=true;
        AutomationProperties.SetName(HideButton,Localizer.Get("隐藏计时组件"));
    }
    public void PlaceNearPanel(Window main)
    {
        var screen=Screens.ScreenFromWindow(main)??Screens.Primary;
        if(screen is null)return;
        var area=screen.WorkingArea;var scaling=screen.Scaling;
        Position=new(area.Right-(int)(Width*scaling)-12,OperatingSystem.IsMacOS()?area.Y+12:area.Bottom-(int)(Height*scaling)-12);
    }
    public void KeepOnScreen()
    {
        if(press is not null)return;
        var screen=Screens.ScreenFromPoint(Position)??Screens.Primary;if(screen is null)return;
        Position=Clamp(Position,screen.WorkingArea,(int)(Width*screen.Scaling),(int)(Height*screen.Scaling));
    }
    public static PixelPoint Clamp(PixelPoint position,PixelRect area,int width,int height)=>new(
        Math.Clamp(position.X,area.X,Math.Max(area.X,area.Right-width)),Math.Clamp(position.Y,area.Y,Math.Max(area.Y,area.Bottom-height)));
    public void DisposeWindow(){disposing=true;Close();}
}

public sealed class TimerCompanionController(Window main, Action restore) : IDisposable
{
    public TimerCompanion? Window { get; private set; }
    private bool enabled=true,disposed,hidden;
    private long? task;
    private TitleTimerState? state;
    public void SetEnabled(bool value){if(value==enabled)return;enabled=value;if(value)hidden=false;Update(state);}
    public void Update(TitleTimerState? value)
    {
        if(disposed)return;state=value;
        if(value?.Id!=task){task=value?.Id;hidden=false;}
        if(!enabled||hidden||value is null){Window?.Hide();return;}
        if(Window is null){Window=new(restore,()=>{hidden=true;Window?.Hide();});Window.PlaceNearPanel(main);}
        Window.Update(value);Window.KeepOnScreen();if(!Window.IsVisible)Window.Show();
    }
    public void Dispose(){if(disposed)return;disposed=true;Window?.DisposeWindow();Window=null;}
}
