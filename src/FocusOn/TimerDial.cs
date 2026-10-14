using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FocusOn;

public sealed class TimerDial : Control
{
    public TitleTimerState? State { get; set; }
    public bool Template { get; set; }
    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2); var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 3;
        if (radius <= 0) return;
        var foreground = Template ? Brushes.White : State is { IsCompleted: true } ? ReviewTheme.Accent : State is { IsRunning: false } ? ReviewTheme.Muted : ReviewTheme.Accent;
        context.DrawEllipse(null, new Pen(Template ? Brushes.White : ReviewTheme.Border, 2), center, radius, radius);
        if (State is { IsCountdown: true } timer)
        {
            var sweep = timer.Progress;
            if (sweep >= .99999) context.DrawEllipse(null, new Pen(foreground, 3), center, radius, radius);
            else if (sweep > 0)
            {
                var angle = sweep * Math.Tau - Math.PI / 2;
                var path = new StreamGeometry(); using(var geometry=path.Open())
                {
                    geometry.BeginFigure(new(center.X,center.Y-radius),false);
                    geometry.ArcTo(new(center.X+radius*Math.Cos(angle),center.Y+radius*Math.Sin(angle)),new(radius,radius),0,sweep>.5,SweepDirection.Clockwise);
                    geometry.EndFigure(false);
                }
                context.DrawGeometry(null,new Pen(foreground,3,lineCap:PenLineCap.Round),path);
            }
            context.DrawEllipse(foreground,null,center,2,2);
        }
        else
        {
            // A clock hand, not an invented stopwatch completion percentage.
            var angle = (State?.Duration.TotalMinutes ?? 0) / 60 * Math.Tau - Math.PI / 2;
            context.DrawLine(new Pen(foreground,2,lineCap:PenLineCap.Round),center,new(center.X+radius*.65*Math.Cos(angle),center.Y+radius*.65*Math.Sin(angle)));
            context.DrawLine(new Pen(foreground,2),center,new(center.X+radius*.35,center.Y));
        }
    }
}
