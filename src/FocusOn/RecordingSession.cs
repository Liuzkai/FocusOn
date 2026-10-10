using Microsoft.Data.Sqlite;

namespace FocusOn;

public sealed class RecordingSession : IDisposable
{
    private readonly SqliteConnection connection;
    private RecordedInterval? current;
    private ForegroundObservation? currentObservation;
    private long currentMonotonicStart;
    private long nextId;
    private RecordingConditions conditions = new(false, false, false, false);
    private DateTimeOffset? gapStart;
    private DateTimeOffset? committedEnd;
    private string? saveFailure;
    private RecordingTime? lastAccepted;
    private string? timeWarning;
    private long? unreliableThrough;
    private bool awaitingDeleteResume;
    private long? deleteFence;
    private (DateTimeOffset Start, DateTimeOffset End)? pendingDeleteDay;

    public RecordingSession(string databasePath)
    {
        var path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS intervals (
                id INTEGER PRIMARY KEY,
                kind INTEGER NOT NULL,
                start_utc INTEGER NOT NULL,
                end_utc INTEGER NOT NULL,
                duration_ticks INTEGER NOT NULL,
                app_key TEXT,
                app_name TEXT,
                window_title TEXT
            );
            CREATE TABLE IF NOT EXISTS deleted_ranges (
                start_utc INTEGER NOT NULL,
                end_utc INTEGER NOT NULL,
                PRIMARY KEY (start_utc, end_utc)
            );
            CREATE TABLE IF NOT EXISTS interval_allocations (
                id INTEGER PRIMARY KEY,
                start_utc INTEGER NOT NULL,
                end_utc INTEGER NOT NULL,
                duration_ticks INTEGER NOT NULL
            );
            SELECT COALESCE(MAX(id), 0) + 1 FROM intervals;
            """;
        using var reader = command.ExecuteReader();
        // PRAGMA produces the first result; the identity query follows it.
        do
        {
            if (reader.FieldCount == 1 && reader.GetName(0) != "journal_mode" && reader.Read())
                nextId = reader.GetInt64(0);
        } while (reader.NextResult());
        using var lastInterval = connection.CreateCommand();
        lastInterval.CommandText = "SELECT MAX(end_utc) FROM intervals;";
        if (lastInterval.ExecuteScalar() is long endUtc)
            committedEnd = gapStart = new DateTimeOffset(endUtc, TimeSpan.Zero);
        else
        {
            lastInterval.CommandText = "SELECT MAX(end_utc) FROM deleted_ranges;";
            if (lastInterval.ExecuteScalar() is long deletedEnd)
                committedEnd = gapStart = new DateTimeOffset(deletedEnd, TimeSpan.Zero);
        }
    }

    public void Observe(ForegroundObservation? observation, RecordingTime at)
    {
        if (!AcceptTime(at)) return;
        if (awaitingDeleteResume) return;
        if (saveFailure is not null && (observation is null ||
            (unreliableThrough is long cutoff && at.MonotonicTicks <= cutoff)))
            return;
        if (current is null && gapStart is not null)
        {
            SaveGap(at);
            saveFailure = null;
            unreliableThrough = null;
        }
        var kind = !conditions.Available ? IntervalKind.Unrecorded :
            conditions.ManualPaused ? IntervalKind.ManualPause :
            conditions.Sleeping ? IntervalKind.Sleep :
            conditions.Locked ? IntervalKind.Locked :
            conditions.DisplayOff ? IntervalKind.DisplayOff :
            observation is null ? IntervalKind.Unrecorded : IntervalKind.Usage;
        if (current is not null && current.Kind == kind &&
            (kind != IntervalKind.Usage || (observation?.WindowId == currentObservation?.WindowId &&
            observation?.ProcessId == currentObservation?.ProcessId &&
            observation?.WindowTitle == currentObservation?.WindowTitle)))
        {
            currentObservation = observation;
            return;
        }

        SaveCurrent(at);
        currentObservation = observation;
        currentMonotonicStart = at.MonotonicTicks;
        var usage = kind == IntervalKind.Usage;
        var knownIdentity = !string.IsNullOrWhiteSpace(observation?.AppKey);
        current = new RecordedInterval(nextId++, kind,
            at.Utc.ToUniversalTime(), at.Utc.ToUniversalTime(), TimeSpan.Zero,
            !usage ? null : knownIdentity ? observation!.AppKey : "unknown",
            !usage ? null : knownIdentity && !string.IsNullOrWhiteSpace(observation!.AppName) ? observation.AppName : "未知软件",
            !usage ? null : string.IsNullOrWhiteSpace(observation!.WindowTitle) ? "标题不可用" : observation.WindowTitle,
            true);
    }

    public void Checkpoint(RecordingTime at)
    {
        if (awaitingDeleteResume) return;
        if (AcceptTime(at)) SaveCurrent(at);
    }

    public void SetConditions(RecordingConditions conditions, ForegroundObservation? currentObservation, RecordingTime at)
    {
        if (!AcceptTime(at)) return;
        this.conditions = conditions;
        Observe(currentObservation, at);
    }

    public void SetManualPaused(bool paused, ForegroundObservation? currentObservation, RecordingTime at) =>
        SetConditions(conditions with { ManualPaused = paused }, currentObservation, at);

    public void SetSystemConditions(RecordingConditions system, ForegroundObservation? currentObservation, RecordingTime at) =>
        SetConditions(system with { ManualPaused = conditions.ManualPaused }, currentObservation, at);

    public void MarkUnreliable(RecordingTime at, string reason)
    {
        if (!AcceptTime(at)) return;
        DiscardTail(at, reason);
    }

    private void DiscardTail(RecordingTime at, string reason)
    {
        gapStart = committedEnd ?? current?.StartUtc ?? gapStart ?? at.Utc.ToUniversalTime();
        current = null;
        currentObservation = null;
        saveFailure = reason;
        unreliableThrough = at.MonotonicTicks;
    }

    public void Finish(RecordingTime at)
    {
        if (!AcceptTime(at)) return;
        if (current is null && gapStart is not null)
            SaveGap(at);
        else
            SaveCurrent(at);
        current = null;
        currentObservation = null;
    }

    public void DeleteDay(DateOnly day, TimeZoneInfo zone, RecordingTime stopAt)
    {
        if ((lastAccepted is RecordingTime previous && stopAt.MonotonicTicks < previous.MonotonicTicks) ||
            (deleteFence is long cutoff && stopAt.MonotonicTicks <= cutoff))
            throw new ArgumentException("删除边界不能早于最新记录控制事实。", nameof(stopAt));
        var (start, end) = DailyProjection.Bounds(day, zone);
        var stopsCurrent = current is not null && current.StartUtc < end && stopAt.Utc > start;
        if (stopsCurrent) Finish(stopAt);
        try
        {
            using var transaction = connection.BeginTransaction();
            var intervals = ReadIntervals(start, end, transaction);
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM interval_allocations WHERE id IN
                    (SELECT id FROM intervals WHERE end_utc > $start AND start_utc < $end);
                DELETE FROM intervals WHERE end_utc > $start AND start_utc < $end;
                """;
            delete.Parameters.AddWithValue("$start", start.UtcTicks);
            delete.Parameters.AddWithValue("$end", end.UtcTicks);
            delete.ExecuteNonQuery();
            var allocatedIds = 0;
            foreach (var stored in intervals)
            {
                var before = ClipStored(stored, stored.Interval.StartUtc, start);
                var after = ClipStored(stored, end, stored.Interval.EndUtc);
                if (before is not null) WriteInterval(before, transaction, stored);
                if (after is not null)
                    WriteInterval(before is null ? after : after with { Id = nextId + allocatedIds++ }, transaction, stored);
            }
            WriteDeletedRange(start, end < stopAt.Utc ? end : stopAt.Utc, transaction);
            transaction.Commit();
            nextId += allocatedIds;
            if (stopsCurrent)
            {
                current = null;
                currentObservation = null;
                gapStart = null;
                awaitingDeleteResume = true;
                pendingDeleteDay = (start, end);
            }
        }
        catch (SqliteException error)
        {
            MarkUnreliable(stopAt, $"无法删除记录：{error.Message}");
            throw;
        }
    }

    public bool ResumeAfterDelete(ForegroundObservation? currentObservation, RecordingTime completedAt)
    {
        if (!awaitingDeleteResume) return true;
        if (!AcceptTime(completedAt)) return false;
        try
        {
            if (pendingDeleteDay is { } range)
            {
                using var transaction = connection.BeginTransaction();
                WriteDeletedRange(range.Start, range.End < completedAt.Utc ? range.End : completedAt.Utc, transaction);
                transaction.Commit();
            }
        }
        catch (SqliteException error)
        {
            awaitingDeleteResume = false;
            pendingDeleteDay = null;
            MarkUnreliable(completedAt, $"无法保存删除后的记录边界：{error.Message}");
            deleteFence = completedAt.MonotonicTicks;
            throw;
        }
        awaitingDeleteResume = false;
        pendingDeleteDay = null;
        Observe(currentObservation, completedAt);
        deleteFence = completedAt.MonotonicTicks;
        return true;
    }

    private void SaveGap(RecordingTime at)
    {
        if (gapStart is DateTimeOffset missingFrom && at.Utc > missingFrom)
        {
            try
            {
                using var transaction = connection.BeginTransaction();
                var gap = new RecordedInterval(nextId, IntervalKind.Unrecorded, missingFrom,
                    at.Utc.ToUniversalTime(), at.Utc - missingFrom, null, null, null);
                var parts = SubtractDeletedRanges(gap, transaction);
                for (var index = 0; index < parts.Count; index++)
                    WriteInterval(parts[index] with { Id = nextId + index }, transaction);
                transaction.Commit();
                nextId += parts.Count;
                committedEnd = at.Utc.ToUniversalTime();
            }
            catch (SqliteException error)
            {
                MarkUnreliable(at, $"无法保存记录：{error.Message}");
                throw;
            }
        }
        gapStart = null;
        current = null;
    }

    public DayView GetDay(DateOnly day, TimeZoneInfo zone, RecordingTime? liveNow = null)
    {
        var (start, end) = DailyProjection.Bounds(day, zone);
        var previewWarning = liveNow is RecordingTime previewAt ? TimeChangeWarning(previewAt) : null;
        if (previewWarning is not null) timeWarning = previewWarning;
        var showLive = previewWarning is null && (liveNow is not RecordingTime suppliedNow ||
            lastAccepted is not RecordingTime accepted || suppliedNow.MonotonicTicks >= accepted.MonotonicTicks);
        var intervals = ReadIntervals(start, end).Select(stored => ClipStored(stored, start, end))
            .OfType<RecordedInterval>().ToList();

        if (showLive && current is not null && liveNow is RecordingTime at)
        {
            var durationTicks = at.MonotonicTicks - currentMonotonicStart;
            if (durationTicks < 0 || at.Utc < current.StartUtc)
                throw new ArgumentException("回看时刻不能早于当前片段。", nameof(liveNow));
            if (durationTicks > 0 && at.Utc > start && current.StartUtc < end)
            {
                intervals.RemoveAll(interval => interval.Id == current.Id);
                intervals.Add(current with { EndUtc = at.Utc.ToUniversalTime(), Duration = TimeSpan.FromTicks(durationTicks) });
            }
        }
        else if (showLive && gapStart is DateTimeOffset missingFrom && liveNow is RecordingTime missingTo && missingTo.Utc > missingFrom)
        {
            var gap = new RecordedInterval(nextId, IntervalKind.Unrecorded, missingFrom,
                missingTo.Utc.ToUniversalTime(), missingTo.Utc - missingFrom, null, null, null, true);
            intervals.AddRange(SubtractDeletedRanges(gap).Select((part, index) => part with { Id = nextId + index }));
        }

        intervals = intervals.Select(interval => DailyProjection.Clip(interval, start, end))
            .OfType<RecordedInterval>().ToList();
        var totals = intervals.Where(interval => interval.Kind == IntervalKind.Usage)
            .GroupBy(interval => interval.AppKey!)
            .Select(group => new AppTotal(group.Key, group.First().AppName!,
                TimeSpan.FromTicks(group.Sum(interval => interval.Duration.Ticks))))
            .OrderByDescending(total => total.Duration).ThenBy(total => total.AppKey, StringComparer.Ordinal)
            .ToArray();
        return new DayView(day, start, end, intervals, totals,
            previewWarning is not null || saveFailure is not null ? "记录异常" : current is null ? "已停止" : current.Kind switch
            {
                IntervalKind.ManualPause => "手动暂停",
                IntervalKind.Sleep => "睡眠",
                IntervalKind.Locked => "锁屏",
                IntervalKind.DisplayOff => "屏幕已关闭",
                IntervalKind.Unrecorded => "未记录",
                _ => "正在记录"
            }, saveFailure ?? timeWarning);
    }

    private List<StoredInterval> ReadIntervals(DateTimeOffset start, DateTimeOffset end, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT i.id, i.kind, i.start_utc, i.end_utc, i.duration_ticks, i.app_key, i.app_name, i.window_title,
                   a.start_utc, a.end_utc, a.duration_ticks
            FROM intervals i LEFT JOIN interval_allocations a ON a.id = i.id
            WHERE i.end_utc > $start AND i.start_utc < $end AND i.duration_ticks > 0
            ORDER BY i.start_utc, i.id;
            """;
        command.Parameters.AddWithValue("$start", start.UtcTicks);
        command.Parameters.AddWithValue("$end", end.UtcTicks);
        var intervals = new List<StoredInterval>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var interval = new RecordedInterval(reader.GetInt64(0), (IntervalKind)reader.GetInt32(1),
                new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero),
                new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero),
                TimeSpan.FromTicks(reader.GetInt64(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));
            intervals.Add(reader.IsDBNull(8)
                ? new(interval, interval.StartUtc, interval.EndUtc, interval.Duration.Ticks)
                : new(interval, new(reader.GetInt64(8), TimeSpan.Zero), new(reader.GetInt64(9), TimeSpan.Zero), reader.GetInt64(10)));
        }
        return intervals;
    }

    private static RecordedInterval? ClipStored(StoredInterval stored, DateTimeOffset start, DateTimeOffset end)
    {
        var clipped = DailyProjection.Clip(stored.Interval, start, end);
        if (clipped is null) return null;
        var wallTicks = (stored.AllocationEnd - stored.AllocationStart).Ticks;
        var from = (long)((Int128)stored.AllocationTicks * (clipped.StartUtc - stored.AllocationStart).Ticks / wallTicks);
        var to = (long)((Int128)stored.AllocationTicks * (clipped.EndUtc - stored.AllocationStart).Ticks / wallTicks);
        return clipped with { Duration = TimeSpan.FromTicks(to - from) };
    }

    private List<RecordedInterval> SubtractDeletedRanges(RecordedInterval interval, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT start_utc, end_utc FROM deleted_ranges
            WHERE end_utc > $start AND start_utc < $end
            ORDER BY start_utc, end_utc;
            """;
        command.Parameters.AddWithValue("$start", interval.StartUtc.UtcTicks);
        command.Parameters.AddWithValue("$end", interval.EndUtc.UtcTicks);
        var parts = new List<RecordedInterval>();
        var cursor = interval.StartUtc;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var deletedStart = new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero);
            var deletedEnd = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
            var before = DailyProjection.Clip(interval, cursor, deletedStart);
            if (before is not null) parts.Add(before);
            if (deletedEnd > cursor) cursor = deletedEnd;
            if (cursor >= interval.EndUtc) break;
        }
        var after = DailyProjection.Clip(interval, cursor, interval.EndUtc);
        if (after is not null) parts.Add(after with { IsCurrent = interval.IsCurrent });
        return parts;
    }

    private void WriteDeletedRange(DateTimeOffset start, DateTimeOffset end, SqliteTransaction transaction)
    {
        if (end <= start) return;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO deleted_ranges (start_utc, end_utc) VALUES ($start, $end);";
        command.Parameters.AddWithValue("$start", start.UtcTicks);
        command.Parameters.AddWithValue("$end", end.UtcTicks);
        command.ExecuteNonQuery();
    }

    private void SaveCurrent(RecordingTime at)
    {
        if (current is null)
            return;
        var durationTicks = at.MonotonicTicks - currentMonotonicStart;
        if (durationTicks < 0 || at.Utc < current.StartUtc)
            throw new ArgumentException("记录时刻必须按时间顺序输入。", nameof(at));
        if (durationTicks == 0)
            return;

        try
        {
            using var transaction = connection.BeginTransaction();
            WriteInterval(current with { EndUtc = at.Utc.ToUniversalTime(), Duration = TimeSpan.FromTicks(durationTicks) }, transaction);
            transaction.Commit();
            committedEnd = at.Utc.ToUniversalTime();
        }
        catch (SqliteException error)
        {
            MarkUnreliable(at, $"无法保存记录：{error.Message}");
            throw;
        }
    }

    private void WriteInterval(RecordedInterval interval, SqliteTransaction transaction, StoredInterval? allocation = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO intervals (id, kind, start_utc, end_utc, duration_ticks, app_key, app_name, window_title)
            VALUES ($id, $kind, $start, $end, $duration, $key, $name, $title)
            ON CONFLICT(id) DO UPDATE SET end_utc = excluded.end_utc, duration_ticks = excluded.duration_ticks;
            """;
        command.Parameters.AddWithValue("$id", interval.Id);
        command.Parameters.AddWithValue("$kind", (int)interval.Kind);
        command.Parameters.AddWithValue("$start", interval.StartUtc.UtcTicks);
        command.Parameters.AddWithValue("$end", interval.EndUtc.UtcTicks);
        command.Parameters.AddWithValue("$duration", interval.Duration.Ticks);
        command.Parameters.AddWithValue("$key", (object?)interval.AppKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", (object?)interval.AppName ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)interval.WindowTitle ?? DBNull.Value);
        command.ExecuteNonQuery();
        using var basis = connection.CreateCommand();
        basis.Transaction = transaction;
        basis.Parameters.AddWithValue("$id", interval.Id);
        if (allocation is null)
            basis.CommandText = "DELETE FROM interval_allocations WHERE id = $id;";
        else
        {
            basis.CommandText = """
                INSERT INTO interval_allocations (id, start_utc, end_utc, duration_ticks)
                VALUES ($id, $start, $end, $duration)
                ON CONFLICT(id) DO UPDATE SET start_utc = excluded.start_utc,
                    end_utc = excluded.end_utc, duration_ticks = excluded.duration_ticks;
                """;
            basis.Parameters.AddWithValue("$start", allocation.AllocationStart.UtcTicks);
            basis.Parameters.AddWithValue("$end", allocation.AllocationEnd.UtcTicks);
            basis.Parameters.AddWithValue("$duration", allocation.AllocationTicks);
        }
        basis.ExecuteNonQuery();
    }

    private sealed record StoredInterval(RecordedInterval Interval, DateTimeOffset AllocationStart,
        DateTimeOffset AllocationEnd, long AllocationTicks);

    private bool AcceptTime(RecordingTime at)
    {
        if (deleteFence is long cutoff && at.MonotonicTicks <= cutoff)
            return false;
        if (lastAccepted is RecordingTime previous && at.MonotonicTicks < previous.MonotonicTicks)
            return false;
        var warning = TimeChangeWarning(at);
        lastAccepted = at;
        if (warning is not null)
        {
            timeWarning = warning;
            DiscardTail(at, warning);
        }
        return true;
    }

    private string? TimeChangeWarning(RecordingTime at)
    {
        if (lastAccepted is not RecordingTime previous || at.MonotonicTicks < previous.MonotonicTicks)
            return null;
        if (at.TimeZoneId != previous.TimeZoneId)
            return "时区已变化；已保存历史未改写，异常后的日期映射尚未验证。";
        var utcTicks = (at.Utc - previous.Utc).Ticks;
        var monotonicTicks = at.MonotonicTicks - previous.MonotonicTicks;
        if (utcTicks < 0 || Math.Abs(utcTicks - monotonicTicks) > TimeSpan.FromSeconds(2).Ticks)
            return "系统时间已跳变；已保存历史未改写，异常后的日期映射尚未验证。";
        return null;
    }

    public void Dispose() => connection.Dispose();
}
