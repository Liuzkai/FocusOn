using Microsoft.Data.Sqlite;
using Xunit;

namespace FocusOn.Tests;

public sealed class TitleTimerTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 23, 59, 50, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "timer.db");
    private static RecordingTime At(double seconds) => new(Start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");
    private static DayView Read(RecordingSession session, int day, double? now = null) => session.GetDay(new(2026, 10, day), TimeZoneInfo.Utc, now is double n ? At(n) : null);

    [Fact]
    public void Pause_resume_stop_preserve_named_history_without_changing_usage_totals()
    {
        using var session = new RecordingSession(Database);
        session.Observe(new(1, "editor", "编辑器", "文档"), At(0));
        session.ChangeTimer(TimerAction.Start, "  写提案  ", At(0));
        session.Checkpoint(At(3));
        var live = Read(session, 9, 5);
        var savedId = Assert.Single(live.Intervals, x => x.Kind == IntervalKind.TaskTimer).Id;
        Assert.Equal("写提案", live.Timer!.Title);
        Assert.Equal(TimeSpan.FromSeconds(5), live.Timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(5), Assert.Single(live.Totals).Duration);
        session.ChangeTimer(TimerAction.Pause, null, At(5));
        Assert.False(Read(session, 9, 8).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(5), Read(session, 9, 8).Timer!.Duration);
        session.ChangeTimer(TimerAction.Resume, null, At(8));
        session.ChangeTimer(TimerAction.Stop, null, At(9));
        session.Finish(At(9));
        var result = Read(session, 9);
        Assert.Null(result.Timer);
        var intervals = result.Intervals.Where(x => x.Kind == IntervalKind.TaskTimer).ToArray();
        Assert.Equal(2, intervals.Length);
        Assert.Equal(savedId, intervals[0].Id);
        Assert.Equal(TimeSpan.FromSeconds(6), TimeSpan.FromTicks(intervals.Sum(x => x.Duration.Ticks)));
        Assert.Equal(TimeSpan.FromSeconds(9), Assert.Single(result.Totals).Duration);
        Assert.All(intervals, x => Assert.Equal("写提案", x.WindowTitle));
    }

    [Fact]
    public void Restart_restores_paused_at_last_checkpoint_and_never_counts_closed_time()
    {
        using (var session = new RecordingSession(Database))
        {
            session.ChangeTimer(TimerAction.Start, "阅读", At(0));
            session.Checkpoint(At(4));
            Assert.Equal(TimeSpan.FromSeconds(8), Read(session, 9, 8).Timer!.Duration);
        }
        using var reopened = new RecordingSession(Database);
        Assert.False(Read(reopened, 10, 100).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(4), Read(reopened, 10, 100).Timer!.Duration);
        Assert.Empty(Read(reopened, 10, 100).Intervals);
        reopened.ChangeTimer(TimerAction.Resume, null, At(100));
        reopened.ChangeTimer(TimerAction.Stop, null, At(103));
        Assert.Equal(TimeSpan.FromSeconds(3), Assert.Single(Read(reopened, 10).Intervals).Duration);
    }

    [Fact]
    public void Lock_and_manual_recording_pause_keep_timer_running_but_sleep_pauses_until_manual_resume()
    {
        using var session = new RecordingSession(Database);
        session.ChangeTimer(TimerAction.Start, "任务", At(0));
        session.SetManualPaused(true, null, At(2));
        session.SetSystemConditions(new(false, false, true, false), null, At(3));
        Assert.True(Read(session, 9, 4).Timer!.IsRunning);
        session.SetSystemConditions(new(false, true, true, false), null, At(5));
        Assert.False(Read(session, 9, 6).Timer!.IsRunning);
        session.SetSystemConditions(new(false, false, false, false), null, At(7));
        Assert.False(Read(session, 9, 8).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(5), Read(session, 9, 8).Timer!.Duration);
    }

    [Fact]
    public void Midnight_and_delete_preserve_other_day_and_never_resurrect_deleted_timer()
    {
        using (var session = new RecordingSession(Database))
        {
            session.ChangeTimer(TimerAction.Start, "跨日", At(0));
            session.Checkpoint(At(20));
            Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(Read(session, 9).Intervals).Duration);
            Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(Read(session, 10).Intervals).Duration);
            session.DeleteDay(new(2026, 10, 10), TimeZoneInfo.Utc, At(20));
            session.Checkpoint(At(25));
            Assert.Empty(Read(session, 10, 25).Intervals);
            Assert.False(Read(session, 10, 25).Timer!.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(10), Read(session, 10, 25).Timer!.Duration);
            session.ChangeTimer(TimerAction.Resume, null, At(25));
            session.ChangeTimer(TimerAction.Stop, null, At(30));
        }
        using var reopened = new RecordingSession(Database);
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(Read(reopened, 9).Intervals).Duration);
        var remaining = Assert.Single(Read(reopened, 10).Intervals);
        Assert.Equal(Start.AddSeconds(25), remaining.StartUtc);
        Assert.Equal(TimeSpan.FromSeconds(5), remaining.Duration);
    }

    [Fact]
    public void Deletion_does_not_reassign_identically_titled_history_to_active_timer()
    {
        using var session = new RecordingSession(Database);
        session.ChangeTimer(TimerAction.Start, "重复标题", At(0));
        session.ChangeTimer(TimerAction.Stop, null, At(20));
        session.ChangeTimer(TimerAction.Start, "重复标题", At(30));
        session.ChangeTimer(TimerAction.Pause, null, At(35));
        session.DeleteDay(new(2026, 10, 9), TimeZoneInfo.Utc, At(35));
        Assert.Equal(TimeSpan.FromSeconds(5), Read(session, 10).Timer!.Duration);
        Assert.Equal(TimeSpan.FromSeconds(15), TimeSpan.FromTicks(Read(session, 10).Intervals.Sum(x => x.Duration.Ticks)));
    }

    [Fact]
    public void Clock_jump_freezes_timer_at_saved_endpoint_with_warning()
    {
        using var session = new RecordingSession(Database);
        session.ChangeTimer(TimerAction.Start, "任务", At(0));
        session.Checkpoint(At(3));
        session.Checkpoint(new(Start.AddHours(1), TimeSpan.FromSeconds(4).Ticks, "UTC"));
        var day = Read(session, 9);
        Assert.False(day.Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(3), day.Timer.Duration);
        Assert.NotNull(day.Warning);
    }

    [Fact]
    public void Only_one_timer_can_run_and_blank_titles_are_rejected()
    {
        using var session = new RecordingSession(Database);
        Assert.Throws<ArgumentException>(() => session.ChangeTimer(TimerAction.Start, "  ", At(0)));
        session.ChangeTimer(TimerAction.Start, "任务", At(0));
        Assert.Throws<InvalidOperationException>(() => session.ChangeTimer(TimerAction.Start, "另一个", At(1)));
        session.Finish(At(3));
        Assert.False(Read(session, 9).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(3), Read(session, 9).Timer!.Duration);
    }

    [Fact]
    public void Deleting_yesterday_does_not_pause_today_timer_when_foreground_spans_midnight()
    {
        using var session = new RecordingSession(Database);
        session.Observe(new(1, "editor", "编辑器", "跨日窗口"), At(0));
        session.ChangeTimer(TimerAction.Start, "今天的任务", At(20));
        session.Checkpoint(At(25));
        session.DeleteDay(new(2026, 10, 9), TimeZoneInfo.Utc, At(30));
        Assert.True(Read(session, 10, 30).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(10), Read(session, 10, 30).Timer!.Duration);
        session.Checkpoint(At(35));
        Assert.True(Read(session, 10, 40).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(20), Read(session, 10, 40).Timer!.Duration);
    }

    [Fact]
    public void Repeated_day_deletion_keeps_original_tick_allocation_on_other_days_after_reopen()
    {
        var finish = At(172820) with { MonotonicTicks = TimeSpan.FromSeconds(172820).Ticks + 116207 };
        RecordedInterval preserved;
        using (var session = new RecordingSession(Database))
        {
            session.ChangeTimer(TimerAction.Start, "多日计时", At(0));
            session.ChangeTimer(TimerAction.Stop, null, finish);
            preserved = Assert.Single(Read(session, 12).Intervals);
            session.DeleteDay(new(2026, 10, 10), TimeZoneInfo.Utc, finish);
            Assert.Equal(preserved.Duration, Assert.Single(Read(session, 12).Intervals).Duration);
            session.DeleteDay(new(2026, 10, 11), TimeZoneInfo.Utc, finish);
            Assert.Equal(preserved.Duration, Assert.Single(Read(session, 12).Intervals).Duration);
        }
        using var reopened = new RecordingSession(Database);
        Assert.Equal(preserved.Duration, Assert.Single(Read(reopened, 12).Intervals).Duration);
        Assert.Empty(Read(reopened, 10).Intervals);
        Assert.Empty(Read(reopened, 11).Intervals);
    }

    [Fact]
    public void Failed_day_delete_preserves_both_timer_and_foreground_history()
    {
        using var session = new RecordingSession(Database);
        session.Observe(new(1, "editor", "编辑器", "窗口"), At(0));
        session.ChangeTimer(TimerAction.Start, "任务", At(0));
        session.ChangeTimer(TimerAction.Stop, null, At(5));
        session.Finish(At(5));
        var saved = Read(session, 9);
        using (var writer = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString()))
        {
            writer.Open();
            using var writeLock = writer.BeginTransaction(deferred: false);
            Assert.Throws<SqliteException>(() => session.DeleteDay(new(2026, 10, 9), TimeZoneInfo.Utc, At(6)));
            Assert.Equal(saved.Intervals, Read(session, 9).Intervals);
        }
        session.DeleteDay(new(2026, 10, 9), TimeZoneInfo.Utc, At(7));
        Assert.Empty(Read(session, 9).Intervals);
        using var reopened = new RecordingSession(Database);
        Assert.Empty(Read(reopened, 9).Intervals);
    }

    [Fact]
    public void Sleep_save_failure_pauses_timer_at_checkpoint_and_wake_never_counts_sleep()
    {
        using var session = new RecordingSession(Database);
        session.ChangeTimer(TimerAction.Start, "任务", At(0));
        session.Checkpoint(At(3));
        using (var writer = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString()))
        {
            writer.Open();
            using var writeLock = writer.BeginTransaction(deferred: false);
            Assert.Throws<SqliteException>(() => session.SetSystemConditions(new(false, true, false, false), null, At(5)));
            var failed = Read(session, 9, 6);
            Assert.False(failed.Timer!.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(3), failed.Timer.Duration);
            Assert.Contains("标题计时保存失败", failed.Warning);
        }
        session.SetSystemConditions(new(false, false, false, false), new(1, "editor", "编辑器", "唤醒"), At(20));
        session.Checkpoint(At(25));
        Assert.False(Read(session, 10, 30).Timer!.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(3), Read(session, 10, 30).Timer!.Duration);
        session.ChangeTimer(TimerAction.Resume, null, At(30));
        session.ChangeTimer(TimerAction.Stop, null, At(32));
        Assert.Equal(TimeSpan.FromSeconds(2), Assert.Single(Read(session, 10).Intervals, x => x.Kind == IntervalKind.TaskTimer).Duration);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
