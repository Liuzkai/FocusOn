using Microsoft.Data.Sqlite;
using Xunit;

namespace FocusOn.Tests;

public sealed class RecoveryTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = DateOnly.FromDateTime(Start.Date);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reopen_keeps_the_checkpoint_and_marks_the_unsaved_tail_and_downtime_unrecorded(bool paused)
    {
        using (var original = new RecordingSession(DatabasePath))
        {
            var editor = new ForegroundObservation(1, "editor", "编辑器", "未保存尾段");
            if (paused)
                original.SetManualPaused(true, editor, At(0));
            else
                original.Observe(editor, At(0));
            original.Checkpoint(At(10));
            Assert.Equal(TimeSpan.FromSeconds(20), Assert.Single(original.GetDay(Date, TimeZoneInfo.Utc, At(20)).Intervals).Duration);
        }

        using var reopened = new RecordingSession(DatabasePath);
        reopened.Observe(new(2, "browser", "浏览器", "重启后的新网页"), new(Start.AddSeconds(30), 0, "UTC"));
        reopened.Finish(new(Start.AddSeconds(40), TimeSpan.FromSeconds(10).Ticks, "UTC"));

        var day = reopened.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval => AssertInterval(interval, paused ? IntervalKind.ManualPause : IntervalKind.Usage, 0, 10),
            interval =>
            {
                AssertInterval(interval, IntervalKind.Unrecorded, 10, 30);
                Assert.Null(interval.AppKey);
                Assert.Null(interval.AppName);
                Assert.Null(interval.WindowTitle);
            },
            interval =>
            {
                AssertInterval(interval, IntervalKind.Usage, 30, 40);
                Assert.Equal("browser", interval.AppKey);
                Assert.Equal("重启后的新网页", interval.WindowTitle);
            });
        Assert.Equal(TimeSpan.FromSeconds(10), day.Totals.Single(total => total.AppKey == "browser").Duration);
        Assert.Equal(paused ? 1 : 2, day.Totals.Count);
    }

    [Fact]
    public void Real_write_lock_keeps_committed_history_and_recovery_does_not_extend_the_old_app()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "已保存历史"), At(0));
        session.Checkpoint(At(10));
        using (var writer = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString()))
        {
            writer.Open();
            using var writeLock = writer.BeginTransaction(deferred: false);
            Assert.Throws<SqliteException>(() => session.Observe(new(2, "browser", "浏览器", "失败的切换"), At(20)));

            var duringFailure = session.GetDay(Date, TimeZoneInfo.Utc, At(25));
            Assert.Equal("记录异常", duringFailure.Status);
            Assert.False(string.IsNullOrWhiteSpace(duringFailure.Warning));
            Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(duringFailure.Totals).Duration);
            Assert.Collection(duringFailure.Intervals,
                interval => AssertInterval(interval, IntervalKind.Usage, 0, 10),
                interval =>
                {
                    Assert.Equal(IntervalKind.Unrecorded, interval.Kind);
                    Assert.Equal(Start.AddSeconds(10), interval.StartUtc);
                    Assert.Equal(Start.AddSeconds(25), interval.EndUtc);
                    Assert.Null(interval.AppKey);
                    Assert.Null(interval.WindowTitle);
                });
            var gapId = duringFailure.Intervals[1].Id;
            Assert.Throws<SqliteException>(() => session.Observe(new(2, "browser", "浏览器", "仍受写锁限制"), At(26)));
            var retry = session.GetDay(Date, TimeZoneInfo.Utc, At(27));
            Assert.Equal(gapId, retry.Intervals.Single(interval => interval.Kind == IntervalKind.Unrecorded).Id);

            session.Observe(null, At(28));
            session.Checkpoint(At(28));
            var nullPoll = session.GetDay(Date, TimeZoneInfo.Utc, At(28));
            Assert.Equal("记录异常", nullPoll.Status);
            Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(nullPoll.Totals).Duration);
            Assert.Equal(gapId, nullPoll.Intervals.Single(interval => interval.Kind == IntervalKind.Unrecorded).Id);
            Assert.Throws<SqliteException>(() => session.Finish(At(29)));
            var failedFinish = session.GetDay(Date, TimeZoneInfo.Utc, At(29));
            Assert.Equal("记录异常", failedFinish.Status);
            Assert.Equal(gapId, failedFinish.Intervals.Single(interval => interval.Kind == IntervalKind.Unrecorded).Id);
        }

        session.Observe(new(3, "browser", "浏览器", "故障后新鲜观测"), At(30));
        session.Finish(At(40));
        var recovered = session.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Null(recovered.Warning);
        Assert.Collection(recovered.Intervals,
            interval => AssertInterval(interval, IntervalKind.Usage, 0, 10),
            interval => AssertInterval(interval, IntervalKind.Unrecorded, 10, 30),
            interval =>
            {
                AssertInterval(interval, IntervalKind.Usage, 30, 40);
                Assert.Equal("故障后新鲜观测", interval.WindowTitle);
            });
        Assert.All(recovered.Totals, total => Assert.Equal(TimeSpan.FromSeconds(10), total.Duration));
    }

    [Fact]
    public void Late_foreground_metadata_and_its_checkpoint_cannot_override_a_newer_stop()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "开始"), At(0));
        session.Checkpoint(At(4));
        session.SetSystemConditions(new(false, false, true, false), null, At(10));
        session.Observe(new(2, "browser", "浏览器", "跨线程晚到的旧事实"), At(5));
        session.Checkpoint(At(5));
        Assert.Equal("锁屏", session.GetDay(Date, TimeZoneInfo.Utc, At(15)).Status);
        session.SetSystemConditions(new(false, false, false, false), new(3, "browser", "浏览器", "恢复时实际前台"), At(20));
        session.Finish(At(30));

        var day = session.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Null(day.Warning);
        Assert.Collection(day.Intervals,
            interval => AssertInterval(interval, IntervalKind.Usage, 0, 10),
            interval => AssertInterval(interval, IntervalKind.Locked, 10, 20),
            interval => AssertInterval(interval, IntervalKind.Usage, 20, 30));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Known_collection_failure_discards_the_tail_and_waits_for_a_fresh_fact(bool paused)
    {
        using var session = new RecordingSession(DatabasePath);
        var editor = new ForegroundObservation(1, "editor", "编辑器", "可信检查点");
        if (paused)
            session.SetManualPaused(true, editor, At(0));
        else
            session.Observe(editor, At(0));
        session.Checkpoint(At(10));
        Assert.Equal(TimeSpan.FromSeconds(20), Assert.Single(session.GetDay(Date, TimeZoneInfo.Utc, At(20)).Intervals).Duration);

        session.MarkUnreliable(At(25), "前台观测线程失效");
        session.Observe(new(2, "browser", "浏览器", "失效前旧快照"), At(24));
        session.Observe(new(2, "browser", "浏览器", "同时间的失效快照"), At(25));
        session.Checkpoint(At(35));
        var failed = session.GetDay(Date, TimeZoneInfo.Utc, At(35));
        Assert.Equal("记录异常", failed.Status);
        Assert.Contains("前台观测线程失效", failed.Warning);
        Assert.Collection(failed.Intervals,
            interval => AssertInterval(interval, paused ? IntervalKind.ManualPause : IntervalKind.Usage, 0, 10),
            interval =>
            {
                Assert.Equal(IntervalKind.Unrecorded, interval.Kind);
                Assert.Equal(Start.AddSeconds(10), interval.StartUtc);
                Assert.Equal(TimeSpan.FromSeconds(25), interval.Duration);
                Assert.Null(interval.WindowTitle);
            });

        var fresh = new ForegroundObservation(3, "browser", "浏览器", "恢复实际前台");
        if (paused)
            session.SetManualPaused(false, fresh, At(40));
        else
            session.Observe(fresh, At(40));
        session.Finish(At(50));
        var recovered = session.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Null(recovered.Warning);
        Assert.Collection(recovered.Intervals,
            interval => AssertInterval(interval, paused ? IntervalKind.ManualPause : IntervalKind.Usage, 0, 10),
            interval => AssertInterval(interval, IntervalKind.Unrecorded, 10, 40),
            interval => AssertInterval(interval, IntervalKind.Usage, 40, 50));
    }

    [Theory]
    [InlineData(IntervalKind.Usage)]
    [InlineData(IntervalKind.ManualPause)]
    [InlineData(IntervalKind.Sleep)]
    public void Null_poll_after_failure_cannot_restore_the_old_usage_or_stop_reason(IntervalKind oldKind)
    {
        using var session = new RecordingSession(DatabasePath);
        var editor = new ForegroundObservation(1, "editor", "编辑器", "失效前事实");
        if (oldKind == IntervalKind.ManualPause)
            session.SetManualPaused(true, editor, At(0));
        else if (oldKind == IntervalKind.Sleep)
            session.SetSystemConditions(new(false, true, false, false), null, At(0));
        else
            session.Observe(editor, At(0));
        session.Checkpoint(At(10));
        session.MarkUnreliable(At(25), "采集线程不可用");
        session.Observe(null, At(30));
        session.Checkpoint(At(35));

        var failed = session.GetDay(Date, TimeZoneInfo.Utc, At(40));
        Assert.Equal("记录异常", failed.Status);
        Assert.Contains("采集线程不可用", failed.Warning);
        Assert.Collection(failed.Intervals,
            interval => AssertInterval(interval, oldKind, 0, 10),
            interval =>
            {
                Assert.Equal(IntervalKind.Unrecorded, interval.Kind);
                Assert.Equal(Start.AddSeconds(10), interval.StartUtc);
                Assert.Equal(Start.AddSeconds(40), interval.EndUtc);
                Assert.Null(interval.AppKey);
                Assert.Null(interval.AppName);
                Assert.Null(interval.WindowTitle);
            });

        var fresh = new ForegroundObservation(2, "browser", "浏览器", "新的可信窗口");
        if (oldKind == IntervalKind.ManualPause)
            session.SetManualPaused(false, fresh, At(50));
        else if (oldKind == IntervalKind.Sleep)
            session.SetSystemConditions(new(false, false, false, false), fresh, At(50));
        else
            session.Observe(fresh, At(50));
        session.Finish(At(60));

        var recovered = session.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Null(recovered.Warning);
        Assert.Collection(recovered.Intervals,
            interval => AssertInterval(interval, oldKind, 0, 10),
            interval => AssertInterval(interval, IntervalKind.Unrecorded, 10, 50),
            interval => AssertInterval(interval, IntervalKind.Usage, 50, 60));
    }

    [Fact]
    public void Explicit_finish_saves_a_known_failure_as_unrecorded_without_restoring_old_pause()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.SetManualPaused(true, new(1, "editor", "编辑器", "退出前事实"), At(0));
            session.Checkpoint(At(10));
            session.MarkUnreliable(At(25), "采集线程不可用");
            session.Observe(null, At(30));
            session.Checkpoint(At(35));
            session.Finish(At(40));
            Assert.Contains("采集线程不可用", session.GetDay(Date, TimeZoneInfo.Utc).Warning);
        }

        using var reopened = new RecordingSession(DatabasePath);
        var day = reopened.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval => AssertInterval(interval, IntervalKind.ManualPause, 0, 10),
            interval =>
            {
                AssertInterval(interval, IntervalKind.Unrecorded, 10, 40);
                Assert.Null(interval.AppKey);
                Assert.Null(interval.AppName);
                Assert.Null(interval.WindowTitle);
            });
        Assert.Empty(day.Totals);
    }

    [Theory]
    [InlineData(3600, "UTC")]
    [InlineData(-60, "UTC")]
    [InlineData(0, "changed-zone")]
    public void Clock_or_zone_change_warns_and_does_not_rewrite_committed_history(int utcShiftSeconds, string zoneId)
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "原始历史"), At(0));
        session.Checkpoint(At(10));
        var saved = Assert.Single(session.GetDay(Date, TimeZoneInfo.Utc).Intervals);

        var changed = new RecordingTime(Start.AddSeconds(20 + utcShiftSeconds), TimeSpan.FromSeconds(20).Ticks, zoneId);
        session.Checkpoint(changed);
        var after = session.GetDay(Date, TimeZoneInfo.Utc, changed with
        {
            Utc = changed.Utc.AddSeconds(5),
            MonotonicTicks = TimeSpan.FromSeconds(25).Ticks
        });
        Assert.False(string.IsNullOrWhiteSpace(after.Warning));
        Assert.Equal(saved, after.Intervals.Single(interval => interval.Id == saved.Id));
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(after.Totals).Duration);
    }

    [Theory]
    [InlineData(3600, "UTC")]
    [InlineData(-60, "UTC")]
    [InlineData(0, "changed-zone")]
    public void Live_query_with_a_changed_clock_shows_only_committed_history_and_a_warning(int utcShiftSeconds, string zoneId)
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "可信历史"), At(0));
        session.Checkpoint(At(10));
        var saved = Assert.Single(session.GetDay(Date, TimeZoneInfo.Utc).Intervals);

        var changedNow = new RecordingTime(Start.AddSeconds(20 + utcShiftSeconds), TimeSpan.FromSeconds(20).Ticks, zoneId);
        var preview = session.GetDay(Date, TimeZoneInfo.Utc, changedNow);
        Assert.False(string.IsNullOrWhiteSpace(preview.Warning));
        Assert.Equal(saved, Assert.Single(preview.Intervals));
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(preview.Totals).Duration);
    }

    [Fact]
    public void Live_query_does_not_advance_the_input_fence_past_a_captured_foreground_fact()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "初始前台"), At(0));
        session.Checkpoint(At(10));
        Assert.Equal(TimeSpan.FromSeconds(20), Assert.Single(session.GetDay(Date, TimeZoneInfo.Utc, At(20)).Intervals).Duration);
        session.Observe(new(2, "browser", "浏览器", "早于刷新时刻已捕获"), At(15));
        session.Finish(At(25));

        Assert.Collection(session.GetDay(Date, TimeZoneInfo.Utc).Intervals,
            interval => AssertInterval(interval, IntervalKind.Usage, 0, 15),
            interval =>
            {
                AssertInterval(interval, IntervalKind.Usage, 15, 25);
                Assert.Equal("browser", interval.AppKey);
            });
    }

    private static RecordingTime At(int seconds) => new(Start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");

    private static void AssertInterval(RecordedInterval interval, IntervalKind kind, int start, int end)
    {
        Assert.Equal(kind, interval.Kind);
        Assert.Equal(Start.AddSeconds(start), interval.StartUtc);
        Assert.Equal(Start.AddSeconds(end), interval.EndUtc);
        Assert.Equal(TimeSpan.FromSeconds(end - start), interval.Duration);
        Assert.False(interval.IsCurrent);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
