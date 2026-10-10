using Microsoft.Data.Sqlite;
using Xunit;

namespace FocusOn.Tests;

public sealed class DeleteDayTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateOnly DeletedDay = new(2026, 10, 9);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");

    [Fact]
    public void Deleting_a_historical_day_preserves_both_cross_day_parts_and_exact_duration_after_reopen()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "跨三日标题"), At(0));
            session.Finish(At(86402, 1));
            session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(86403, 1));

            var deleted = session.GetDay(DeletedDay, TimeZoneInfo.Utc);
            Assert.Empty(deleted.Intervals);
            Assert.Empty(deleted.Totals);
            Assert.Null(deleted.Warning);
        }

        using var reopened = new RecordingSession(DatabasePath);
        var before = reopened.GetDay(DeletedDay.AddDays(-1), TimeZoneInfo.Utc);
        var after = reopened.GetDay(DeletedDay.AddDays(1), TimeZoneInfo.Utc);
        AssertPart(Assert.Single(before.Intervals), Start, Start.AddSeconds(1), TimeSpan.FromSeconds(1));
        AssertPart(Assert.Single(after.Intervals), Start.AddSeconds(86401), Start.AddSeconds(86402), TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(before.Totals).Duration);
        Assert.Equal(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1), Assert.Single(after.Totals).Duration);
        Assert.Empty(reopened.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
    }

    [Fact]
    public void Deleting_today_waits_for_completion_and_queued_foreground_facts_cannot_restore_deleted_titles()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "已删除的私人标题"), At(10));
            session.Checkpoint(At(20));
            session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30));
            session.Observe(new(1, "editor", "编辑器", "删除期间排队的旧标题"), At(35));
            session.Checkpoint(At(40));
            Assert.Empty(session.GetDay(DeletedDay, TimeZoneInfo.Utc, At(45)).Intervals);

            session.ResumeAfterDelete(new(2, "browser", "浏览器", "删除完成后的实际窗口"), At(50));
            session.Observe(new(1, "editor", "编辑器", "恢复后晚到的旧标题"), At(49));
            session.Observe(new(1, "editor", "编辑器", "边界上晚到的旧标题"), At(50));
            session.Checkpoint(At(60));
            session.Finish(At(70));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var day = reopened.GetDay(DeletedDay, TimeZoneInfo.Utc);
        var interval = Assert.Single(day.Intervals);
        Assert.Equal(Start.AddSeconds(50), interval.StartUtc);
        Assert.Equal(Start.AddSeconds(70), interval.EndUtc);
        Assert.Equal(TimeSpan.FromSeconds(20), interval.Duration);
        Assert.Equal("删除完成后的实际窗口", interval.WindowTitle);
        Assert.Equal("browser", Assert.Single(day.Totals).AppKey);
        Assert.Null(day.Warning);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reopen_does_not_recreate_deleted_today_or_the_wait_for_resume_as_a_failure_gap(bool keepsAnotherDay)
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            if (keepsAnotherDay)
            {
                session.Observe(new(1, "editor", "编辑器", "前一日仍保留"), At(0));
                session.Finish(At(1));
            }
            session.Observe(new(2, "browser", "浏览器", "被删除的标题"), At(10));
            session.Checkpoint(At(20));
            session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30));
            session.ResumeAfterDelete(new(3, "editor", "编辑器", "尚未保存的新事实"), At(50));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var restart = new RecordingTime(Start.AddSeconds(60), 0, "UTC");
        var missing = Assert.Single(reopened.GetDay(DeletedDay, TimeZoneInfo.Utc, restart).Intervals);
        Assert.Equal(IntervalKind.Unrecorded, missing.Kind);
        Assert.Equal(Start.AddSeconds(50), missing.StartUtc);
        Assert.Equal(Start.AddSeconds(60), missing.EndUtc);

        reopened.Observe(new(4, "browser", "浏览器", "真正重启后的前台"), restart);
        reopened.Finish(new(Start.AddSeconds(70), TimeSpan.FromSeconds(10).Ticks, "UTC"));
        var day = reopened.GetDay(DeletedDay, TimeZoneInfo.Utc);
        Assert.Equal(2, day.Intervals.Count);
        Assert.Equal(Start.AddSeconds(50), day.Intervals[0].StartUtc);
        Assert.Equal("真正重启后的前台", day.Intervals[1].WindowTitle);
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(day.Totals).Duration);
        if (keepsAnotherDay)
            Assert.Equal("前一日仍保留", Assert.Single(reopened.GetDay(DeletedDay.AddDays(-1), TimeZoneInfo.Utc).Intervals).WindowTitle);
        Assert.Null(day.Warning);
    }

    [Fact]
    public void An_old_recovery_gap_keeps_other_days_but_excludes_the_deleted_historical_day()
    {
        using (var original = new RecordingSession(DatabasePath))
        {
            original.Observe(new(1, "editor", "编辑器", "最后可信事实"), At(0));
            original.Checkpoint(At(1));
        }

        var deleted = DeletedDay.AddDays(1);
        using (var recovered = new RecordingSession(DatabasePath))
        {
            recovered.DeleteDay(deleted, TimeZoneInfo.Utc, At(172805));
            var before = Assert.Single(recovered.GetDay(DeletedDay, TimeZoneInfo.Utc, At(172806)).Intervals);
            Assert.Equal(IntervalKind.Unrecorded, before.Kind);
            Assert.Equal(TimeSpan.FromDays(1), before.Duration);
            Assert.Empty(recovered.GetDay(deleted, TimeZoneInfo.Utc, At(172806)).Intervals);
            recovered.Observe(new(2, "browser", "浏览器", "缺口之后实际前台"), At(172806));
            recovered.Finish(At(172816));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var preservedGap = Assert.Single(reopened.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
        Assert.Equal(TimeSpan.FromDays(1), preservedGap.Duration);
        Assert.Null(preservedGap.AppKey);
        Assert.Empty(reopened.GetDay(deleted, TimeZoneInfo.Utc, new(Start.AddSeconds(172826), 0, "UTC")).Intervals);
        var after = reopened.GetDay(deleted.AddDays(1), TimeZoneInfo.Utc);
        Assert.Equal(2, after.Intervals.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), after.Intervals[0].Duration);
        Assert.Equal("缺口之后实际前台", after.Intervals[1].WindowTitle);
        Assert.Null(after.Warning);
    }

    [Theory]
    [InlineData(IntervalKind.ManualPause)]
    [InlineData(IntervalKind.Sleep)]
    [InlineData(IntervalKind.Locked)]
    [InlineData(IntervalKind.DisplayOff)]
    [InlineData(IntervalKind.Unrecorded)]
    public void Deleting_today_preserves_each_stop_condition_and_resumes_only_after_it_is_cleared(IntervalKind kind)
    {
        using var session = new RecordingSession(DatabasePath);
        var conditions = new RecordingConditions(kind == IntervalKind.ManualPause, kind == IntervalKind.Sleep,
            kind == IntervalKind.Locked, kind == IntervalKind.DisplayOff, kind != IntervalKind.Unrecorded);
        session.SetConditions(conditions, new(1, "editor", "编辑器", "停止前标题"), At(10));
        session.Checkpoint(At(20));
        session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30));
        session.ResumeAfterDelete(new(2, "browser", "浏览器", "删除后的实际窗口"), At(50));
        session.Checkpoint(At(60));

        var stopped = session.GetDay(DeletedDay, TimeZoneInfo.Utc, At(70));
        Assert.Equal(kind, Assert.Single(stopped.Intervals).Kind);
        Assert.Equal(Start.AddSeconds(50), stopped.Intervals[0].StartUtc);
        Assert.Empty(stopped.Totals);
        Assert.Null(stopped.Intervals[0].WindowTitle);
        if (kind == IntervalKind.ManualPause)
            session.SetManualPaused(false, new(3, "browser", "浏览器", "主动继续后的窗口"), At(80));
        else
            session.SetSystemConditions(new(false, false, false, false), new(3, "browser", "浏览器", "系统条件恢复后的窗口"), At(80));
        session.Finish(At(90));
        var resumed = session.GetDay(DeletedDay, TimeZoneInfo.Utc);
        Assert.Equal(2, resumed.Intervals.Count);
        Assert.Equal(kind, resumed.Intervals[0].Kind);
        Assert.Equal(IntervalKind.Usage, resumed.Intervals[1].Kind);
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(resumed.Totals).Duration);
        Assert.Null(resumed.Warning);
    }

    [Fact]
    public void Deleting_an_unrelated_historical_day_does_not_interrupt_the_current_segment()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "被删除的历史标题"), At(0));
        session.Finish(At(1));
        session.Observe(new(2, "browser", "浏览器", "保持的当前标题"), At(10));
        session.Checkpoint(At(20));
        session.DeleteDay(DeletedDay.AddDays(-1), TimeZoneInfo.Utc, At(30));
        session.ResumeAfterDelete(new(3, "editor", "编辑器", "不应造成额外切段的读取"), At(50));
        session.Checkpoint(At(60));
        session.Finish(At(70));

        Assert.Empty(session.GetDay(DeletedDay.AddDays(-1), TimeZoneInfo.Utc).Intervals);
        var interval = Assert.Single(session.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
        Assert.Equal(Start.AddSeconds(10), interval.StartUtc);
        Assert.Equal(TimeSpan.FromSeconds(60), interval.Duration);
        Assert.Equal("保持的当前标题", interval.WindowTitle);
    }

    [Fact]
    public void Real_delete_transaction_failure_preserves_history_and_does_not_create_a_deletion_hole()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "不能被失败操作删掉"), At(10));
        session.Finish(At(20));
        var saved = Assert.Single(session.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
        using (var writer = OpenWriter())
        using (var writeLock = writer.BeginTransaction(deferred: false))
        {
            Assert.Throws<SqliteException>(() => session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30)));
            var failed = session.GetDay(DeletedDay, TimeZoneInfo.Utc);
            Assert.Equal(saved, Assert.Single(failed.Intervals));
            Assert.Contains("无法删除记录", failed.Warning);
            Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(failed.Totals).Duration);
        }

        session.Observe(new(2, "browser", "浏览器", "失败后的实际前台"), At(40));
        session.Finish(At(50));
        var recovered = session.GetDay(DeletedDay, TimeZoneInfo.Utc);
        Assert.Equal(3, recovered.Intervals.Count);
        Assert.Equal(saved, recovered.Intervals[0]);
        Assert.Equal(IntervalKind.Unrecorded, recovered.Intervals[1].Kind);
        Assert.Equal(Start.AddSeconds(20), recovered.Intervals[1].StartUtc);
        Assert.Equal(Start.AddSeconds(40), recovered.Intervals[1].EndUtc);
        Assert.Null(recovered.Warning);
    }

    [Fact]
    public void Real_resume_write_failure_keeps_the_deletion_and_fresh_facts_can_recover_with_new_conditions()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "已经删除的私人标题"), At(10));
            session.Checkpoint(At(20));
            session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30));
            using (var writer = OpenWriter())
            using (var writeLock = writer.BeginTransaction(deferred: false))
            {
                Assert.Throws<SqliteException>(() => session.ResumeAfterDelete(new(2, "browser", "浏览器", "失败的恢复事实"), At(50)));
                session.Observe(null, At(55));
                session.Checkpoint(At(56));
                var failed = session.GetDay(DeletedDay, TimeZoneInfo.Utc, At(57));
                Assert.Equal("记录异常", failed.Status);
                Assert.Contains("无法保存删除后的记录边界", failed.Warning);
                Assert.Empty(failed.Totals);
                var gap = Assert.Single(failed.Intervals);
                Assert.Equal(IntervalKind.Unrecorded, gap.Kind);
                Assert.Equal(Start.AddSeconds(30), gap.StartUtc);
                Assert.Null(gap.WindowTitle);
                Assert.True(gap.IsCurrent);
            }

            session.SetManualPaused(true, null, At(58));
            session.Observe(new(3, "browser", "浏览器", "真正新鲜的恢复事实"), At(60));
            session.Checkpoint(At(70));
            Assert.Equal("手动暂停", session.GetDay(DeletedDay, TimeZoneInfo.Utc, At(75)).Status);
            session.SetManualPaused(false, new(4, "browser", "浏览器", "主动继续后的窗口"), At(80));
            session.Finish(At(90));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var day = reopened.GetDay(DeletedDay, TimeZoneInfo.Utc);
        Assert.Equal(3, day.Intervals.Count);
        Assert.Equal(IntervalKind.Unrecorded, day.Intervals[0].Kind);
        Assert.Equal(Start.AddSeconds(30), day.Intervals[0].StartUtc);
        Assert.Equal(IntervalKind.ManualPause, day.Intervals[1].Kind);
        Assert.Equal(Start.AddSeconds(60), day.Intervals[1].StartUtc);
        Assert.Equal("主动继续后的窗口", day.Intervals[2].WindowTitle);
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(day.Totals).Duration);
        Assert.Null(day.Warning);
    }

    [Fact]
    public void A_stale_delete_boundary_is_rejected_without_changing_saved_history()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "最新可信范围"), At(10));
        session.Checkpoint(At(40));
        var saved = Assert.Single(session.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
        Assert.Throws<ArgumentException>(() => session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30)));
        Assert.Equal(saved, Assert.Single(session.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals));
        session.Finish(At(50));
        Assert.Equal(TimeSpan.FromSeconds(40), Assert.Single(session.GetDay(DeletedDay, TimeZoneInfo.Utc).Totals).Duration);
    }

    [Fact]
    public void Historical_cross_day_splitting_cannot_move_the_reopen_boundary_before_later_saved_activity()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "旧跨日片段"), At(0));
            session.Finish(At(86402));
            session.Observe(new(2, "browser", "浏览器", "后来已经可信保存的活动"), At(86403));
            session.Finish(At(86423));
            session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(86424));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var restart = new RecordingTime(Start.AddSeconds(86433), 0, "UTC");
        var day = reopened.GetDay(DeletedDay.AddDays(1), TimeZoneInfo.Utc, restart);
        var gap = Assert.Single(day.Intervals, interval => interval.Kind == IntervalKind.Unrecorded);
        Assert.Equal(Start.AddSeconds(86423), gap.StartUtc);
        Assert.Equal(TimeSpan.FromSeconds(10), gap.Duration);
        Assert.Equal(TimeSpan.FromSeconds(20), day.Totals.Single(total => total.AppKey == "browser").Duration);
        reopened.Observe(new(3, "browser", "浏览器", "重开后的新前台"), restart);
        reopened.Finish(new(Start.AddSeconds(86443), TimeSpan.FromSeconds(10).Ticks, "UTC"));
        var saved = reopened.GetDay(DeletedDay.AddDays(1), TimeZoneInfo.Utc);
        Assert.Equal(4, saved.Intervals.Count);
        Assert.Equal("旧跨日片段", saved.Intervals[0].WindowTitle);
        Assert.Equal("后来已经可信保存的活动", saved.Intervals[1].WindowTitle);
        Assert.Equal(IntervalKind.Unrecorded, saved.Intervals[2].Kind);
        Assert.Equal("重开后的新前台", saved.Intervals[3].WindowTitle);
        Assert.Empty(reopened.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_newer_control_or_foreground_fact_rejects_an_old_resume_snapshot_and_a_fresh_snapshot_can_finish(bool newerForeground)
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "删除之前的 A"), At(10));
        session.Checkpoint(At(20));
        session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, At(30));
        if (newerForeground)
            session.Observe(new(2, "browser", "浏览器", "已经切到 B 的较新事实"), At(45));
        else
            session.SetSystemConditions(new(false, false, true, false), null, At(45));

        Assert.False(session.ResumeAfterDelete(new(1, "editor", "编辑器", "较早读取的 A 快照"), At(40)));
        Assert.Empty(session.GetDay(DeletedDay, TimeZoneInfo.Utc, At(50)).Intervals);
        Assert.True(session.ResumeAfterDelete(new(2, "browser", "浏览器", "重新读取到的 B"), At(55)));
        session.Finish(At(65));
        var resumed = session.GetDay(DeletedDay, TimeZoneInfo.Utc);
        var interval = Assert.Single(resumed.Intervals);
        Assert.Equal(Start.AddSeconds(55), interval.StartUtc);
        Assert.Equal(newerForeground ? IntervalKind.Usage : IntervalKind.Locked, interval.Kind);
        if (newerForeground)
            Assert.Equal("重新读取到的 B", interval.WindowTitle);
        else
        {
            Assert.Empty(resumed.Totals);
            session.SetSystemConditions(new(false, false, false, false), new(2, "browser", "浏览器", "解锁后实际的 B"), At(70));
            session.Finish(At(80));
            Assert.Equal("解锁后实际的 B", session.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals[1].WindowTitle);
        }
        Assert.Null(resumed.Warning);
    }

    [Fact]
    public void Deleting_the_middle_of_a_long_segment_preserves_other_days_to_the_original_tick()
    {
        var origin = new DateTimeOffset(2026, 10, 8, 23, 59, 58, TimeSpan.Zero);
        var end = origin.AddSeconds(259231);
        var keptDay = new DateOnly(2026, 10, 11);
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "原始分配基准"), new(origin, 0, "UTC"));
            session.Finish(new(end, TimeSpan.FromSeconds(259231).Ticks + 116207, "UTC"));
            Assert.Equal(TimeSpan.FromTicks(864000038732), Assert.Single(session.GetDay(keptDay, TimeZoneInfo.Utc).Intervals).Duration);
            session.DeleteDay(new(2026, 10, 10), TimeZoneInfo.Utc,
                new(end.AddSeconds(1), TimeSpan.FromSeconds(259232).Ticks + 116207, "UTC"));
            Assert.Equal(TimeSpan.FromTicks(864000038732), Assert.Single(session.GetDay(keptDay, TimeZoneInfo.Utc).Intervals).Duration);
        }

        using var reopened = new RecordingSession(DatabasePath);
        Assert.Equal(TimeSpan.FromTicks(864000038732), Assert.Single(reopened.GetDay(keptDay, TimeZoneInfo.Utc).Totals).Duration);
        reopened.DeleteDay(new(2026, 10, 12), TimeZoneInfo.Utc, new(end.AddSeconds(2), 0, "UTC"));
        Assert.Equal(TimeSpan.FromTicks(864000038732), Assert.Single(reopened.GetDay(keptDay, TimeZoneInfo.Utc).Intervals).Duration);
        Assert.Empty(reopened.GetDay(new(2026, 10, 10), TimeZoneInfo.Utc).Intervals);
        Assert.Empty(reopened.GetDay(new(2026, 10, 12), TimeZoneInfo.Utc).Intervals);
    }

    [Fact]
    public void A_committed_v1_database_can_be_opened_queried_deleted_and_reopened_without_recreating_history()
    {
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "recording-v1.db"), DatabasePath);
        var origin = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        using (var session = new RecordingSession(DatabasePath))
        {
            var legacy = session.GetDay(DeletedDay, TimeZoneInfo.Utc);
            Assert.Equal(2, legacy.Intervals.Count);
            Assert.Equal("v1 历史标题", legacy.Intervals[0].WindowTitle);
            Assert.Equal(IntervalKind.Sleep, legacy.Intervals[1].Kind);
            Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(legacy.Totals).Duration);
            session.DeleteDay(DeletedDay, TimeZoneInfo.Utc, new(origin, 0, "UTC"));
            Assert.Empty(session.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
            session.Observe(new(2, "browser", "浏览器", "升级后的新活动"), new(origin.AddSeconds(10), TimeSpan.FromSeconds(10).Ticks, "UTC"));
            session.Finish(new(origin.AddSeconds(20), TimeSpan.FromSeconds(20).Ticks, "UTC"));
        }

        using var reopened = new RecordingSession(DatabasePath);
        Assert.Empty(reopened.GetDay(DeletedDay, TimeZoneInfo.Utc).Intervals);
        var day = reopened.GetDay(DeletedDay.AddDays(1), TimeZoneInfo.Utc);
        Assert.Equal("升级后的新活动", day.Intervals.Last().WindowTitle);
        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(day.Totals).Duration);
        Assert.Null(day.Warning);
    }

    private SqliteConnection OpenWriter()
    {
        var writer = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString());
        writer.Open();
        return writer;
    }

    private static RecordingTime At(int seconds, long extraMonotonicTicks = 0) =>
        new(Start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks + extraMonotonicTicks, "UTC");

    private static void AssertPart(RecordedInterval interval, DateTimeOffset start, DateTimeOffset end, TimeSpan duration)
    {
        Assert.Equal(IntervalKind.Usage, interval.Kind);
        Assert.Equal(start, interval.StartUtc);
        Assert.Equal(end, interval.EndUtc);
        Assert.Equal(duration, interval.Duration);
        Assert.Equal("跨三日标题", interval.WindowTitle);
        Assert.False(interval.IsCurrent);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
