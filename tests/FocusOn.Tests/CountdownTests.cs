using Microsoft.Data.Sqlite;
using Xunit;

namespace FocusOn.Tests;

public sealed class CountdownTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 23, 59, 50, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Countdown.Tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "timer.db");
    private static RecordingTime At(double seconds) => new(Start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");
    private static DayView Day(RecordingSession s, int day, double? now = null) => s.GetDay(new(2026,10,day),TimeZoneInfo.Utc, now is { } t ? At(t) : null);

    [Fact]
    public void Late_expiry_atomically_caps_history_at_target_without_stopping_foreground_recording()
    {
        using(var s=new RecordingSession(Database))
        {
            s.Observe(new(1,"editor","Editor","Document"),At(0));
            s.StartCountdown("Focus",TimeSpan.FromSeconds(15),At(0)); s.Checkpoint(At(4));
            var preview=Day(s,10,25); Assert.Equal(TimeSpan.FromSeconds(15),preview.Timer!.Duration);
            Assert.False(preview.Timer.IsCompleted); // reads do not finish/write
            var done=s.AdvanceTimer(At(25))!;
            Assert.True(done.IsCompleted); Assert.False(done.IsRunning); Assert.Equal(TimeSpan.Zero,done.Remaining); Assert.Equal(1,done.Progress);
            Assert.Equal(done,s.AdvanceTimer(At(40)));
            s.Checkpoint(At(40));
            Assert.Equal(TimeSpan.FromSeconds(15),TimeSpan.FromTicks(Day(s,9).Intervals.Concat(Day(s,10).Intervals).Where(x=>x.Kind==IntervalKind.TaskTimer).Sum(x=>x.Duration.Ticks)));
            Assert.Equal(Start.AddSeconds(15),Day(s,10).Intervals.Single(x=>x.Kind==IntervalKind.TaskTimer).EndUtc);
            Assert.Equal(TimeSpan.FromSeconds(30),Day(s,10).Totals.Single().Duration);
            s.ChangeTimer(TimerAction.Start,"New stopwatch",At(41)); Assert.False(s.AdvanceTimer(At(42))!.IsCountdown);
        }
        using var reopened=new RecordingSession(Database);
        Assert.Equal("New stopwatch",reopened.AdvanceTimer(At(100))!.Title);
        Assert.False(reopened.AdvanceTimer(At(100))!.IsRunning);
    }

    [Fact]
    public void Pause_restart_resume_preserve_remaining_and_do_not_count_downtime()
    {
        using(var s=new RecordingSession(Database))
        {
            s.StartCountdown("Break",TimeSpan.FromSeconds(20),At(0)); s.ChangeTimer(TimerAction.Pause,null,At(7));
            Assert.Equal(TimeSpan.FromSeconds(13),s.AdvanceTimer(At(100))!.Remaining);
        }
        using var r=new RecordingSession(Database); var restored=r.AdvanceTimer(At(200))!;
        Assert.False(restored.IsRunning); Assert.Equal(TimeSpan.FromSeconds(20),restored.TargetDuration);
        Assert.Equal(TimeSpan.FromSeconds(13),restored.Remaining);
        r.ChangeTimer(TimerAction.Resume,null,At(200)); var done=r.AdvanceTimer(At(250))!;
        Assert.True(done.IsCompleted); Assert.Equal(TimeSpan.FromSeconds(20),done.Duration);
        Assert.Equal(Start.AddSeconds(213),Day(r,10).Intervals.Single().EndUtc);
    }

    [Fact]
    public void Sleep_and_time_anomaly_pause_without_inventing_countdown_completion()
    {
        using var s=new RecordingSession(Database);
        s.StartCountdown("Focus",TimeSpan.FromSeconds(20),At(0)); s.Checkpoint(At(4));
        s.SetSystemConditions(new(false,true,false,false),null,At(6));
        Assert.False(s.AdvanceTimer(At(100))!.IsRunning); Assert.Equal(TimeSpan.FromSeconds(14),s.AdvanceTimer(At(100))!.Remaining);
        s.ChangeTimer(TimerAction.Resume,null,At(100)); s.Checkpoint(At(104));
        var jumped=s.AdvanceTimer(new(Start.AddHours(1),At(105).MonotonicTicks,"UTC"))!;
        Assert.False(jumped.IsCompleted); Assert.False(jumped.IsRunning); Assert.Equal(TimeSpan.FromSeconds(10),jumped.Duration); Assert.NotNull(Day(s,9).Warning);
    }

    [Fact]
    public void Deleting_cross_day_active_history_keeps_target_pauses_and_never_resurrects_deleted_time()
    {
        using(var s=new RecordingSession(Database))
        {
            s.StartCountdown("Across midnight",TimeSpan.FromSeconds(40),At(0)); s.Checkpoint(At(20));
            s.DeleteDay(new(2026,10,10),TimeZoneInfo.Utc,At(25));
            var paused=s.AdvanceTimer(At(100))!; Assert.False(paused.IsRunning); Assert.Equal(TimeSpan.FromSeconds(30),paused.Remaining);
            Assert.Empty(Day(s,10,100).Intervals);
            s.ChangeTimer(TimerAction.Resume,null,At(100)); Assert.True(s.AdvanceTimer(At(130))!.IsCompleted);
        }
        using var r=new RecordingSession(Database); Assert.Null(r.AdvanceTimer(At(200)));
        Assert.Equal(TimeSpan.FromSeconds(10),Day(r,9).Intervals.Single().Duration);
        var interval=Day(r,10).Intervals.Single(); Assert.Equal(Start.AddSeconds(100),interval.StartUtc); Assert.Equal(TimeSpan.FromSeconds(30),interval.Duration);
    }

    [Fact]
    public void Existing_timer_database_remains_compatible_and_invalid_target_cannot_replace_running_task()
    {
        using(var old=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Database,Pooling=false}.ToString()))
        {
            Directory.CreateDirectory(directory); old.Open(); using var create=old.CreateCommand();
            create.CommandText="CREATE TABLE title_timers(id INTEGER PRIMARY KEY AUTOINCREMENT,title TEXT NOT NULL,finished INTEGER NOT NULL DEFAULT 0); INSERT INTO title_timers(title) VALUES ('Legacy stopwatch');";create.ExecuteNonQuery();
        }
        using var s=new RecordingSession(Database); Assert.Equal("Legacy stopwatch",s.AdvanceTimer(At(0))!.Title); Assert.Null(s.AdvanceTimer(At(0))!.TargetDuration);
        Assert.Throws<InvalidOperationException>(()=>s.StartCountdown("Replace",TimeSpan.FromMinutes(25),At(0)));
        s.ChangeTimer(TimerAction.Stop,null,At(0));
        Assert.Throws<ArgumentException>(()=>s.StartCountdown("Bad",TimeSpan.Zero,At(1)));
        Assert.Throws<ArgumentException>(()=>s.StartCountdown("Bad",TimeSpan.FromHours(25),At(1)));
        Assert.Null(s.AdvanceTimer(At(2)));
    }

    [Fact]
    public void Failed_completion_write_keeps_last_checkpoint_and_does_not_claim_completed()
    {
        using var s=new RecordingSession(Database); s.StartCountdown("Focus",TimeSpan.FromSeconds(10),At(0));s.Checkpoint(At(4));
        using(var writer=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Database,Pooling=false}.ToString()))
        {
            writer.Open();using var locked=writer.BeginTransaction(deferred:false);
            Assert.Throws<SqliteException>(()=>s.AdvanceTimer(At(11)));
            var paused=s.AdvanceTimer(At(12))!;Assert.False(paused.IsCompleted);Assert.False(paused.IsRunning);Assert.Equal(TimeSpan.FromSeconds(6),paused.Remaining);
        }
        s.ChangeTimer(TimerAction.Resume,null,At(20));Assert.True(s.AdvanceTimer(At(30))!.IsCompleted);
    }

    [Fact]
    public void Deleting_part_of_completed_history_dismisses_completion_without_reviving_the_task()
    {
        using var s=new RecordingSession(Database);s.StartCountdown("Finished",TimeSpan.FromSeconds(15),At(0));
        Assert.True(s.AdvanceTimer(At(20))!.IsCompleted);s.DeleteDay(new(2026,10,9),TimeZoneInfo.Utc,At(30));
        Assert.Null(s.AdvanceTimer(At(40)));Assert.Equal(TimeSpan.FromSeconds(5),Day(s,10).Intervals.Single().Duration);
        s.ChangeTimer(TimerAction.Start,"New",At(41));Assert.False(s.AdvanceTimer(At(42))!.IsCountdown);
    }

    public void Dispose(){ if(Directory.Exists(directory))Directory.Delete(directory,true); }
}
