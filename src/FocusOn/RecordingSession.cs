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
        lastInterval.CommandText = "SELECT end_utc FROM intervals ORDER BY id DESC LIMIT 1;";
        if (lastInterval.ExecuteScalar() is long endUtc)
            committedEnd = gapStart = new DateTimeOffset(endUtc, TimeSpan.Zero);
    }

    public void Observe(ForegroundObservation? observation, RecordingTime at)
    {
        if (!AcceptTime(at)) return;
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

    private void SaveGap(RecordingTime at)
    {
        if (gapStart is DateTimeOffset missingFrom && at.Utc > missingFrom)
        {
            current = new RecordedInterval(nextId, IntervalKind.Unrecorded, missingFrom,
                missingFrom, TimeSpan.Zero, null, null, null, true);
            currentMonotonicStart = at.MonotonicTicks - (at.Utc - missingFrom).Ticks;
            SaveCurrent(at);
            nextId++;
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
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, start_utc, end_utc, duration_ticks, app_key, app_name, window_title
            FROM intervals
            WHERE end_utc > $start AND start_utc < $end AND duration_ticks > 0
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$start", start.UtcTicks);
        command.Parameters.AddWithValue("$end", end.UtcTicks);
        var intervals = new List<RecordedInterval>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                intervals.Add(new RecordedInterval(reader.GetInt64(0), (IntervalKind)reader.GetInt32(1),
                    new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero),
                    new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero),
                    TimeSpan.FromTicks(reader.GetInt64(4)),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }

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
            intervals.Add(new RecordedInterval(nextId, IntervalKind.Unrecorded, missingFrom,
                missingTo.Utc.ToUniversalTime(), missingTo.Utc - missingFrom, null, null, null, true));
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
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO intervals (id, kind, start_utc, end_utc, duration_ticks, app_key, app_name, window_title)
            VALUES ($id, $kind, $start, $end, $duration, $key, $name, $title)
            ON CONFLICT(id) DO UPDATE SET end_utc = excluded.end_utc, duration_ticks = excluded.duration_ticks;
            """;
            command.Parameters.AddWithValue("$id", current.Id);
            command.Parameters.AddWithValue("$kind", (int)current.Kind);
            command.Parameters.AddWithValue("$start", current.StartUtc.UtcTicks);
            command.Parameters.AddWithValue("$end", at.Utc.UtcTicks);
            command.Parameters.AddWithValue("$duration", durationTicks);
            command.Parameters.AddWithValue("$key", (object?)current.AppKey ?? DBNull.Value);
            command.Parameters.AddWithValue("$name", (object?)current.AppName ?? DBNull.Value);
            command.Parameters.AddWithValue("$title", (object?)current.WindowTitle ?? DBNull.Value);
            command.ExecuteNonQuery();
            transaction.Commit();
            committedEnd = at.Utc.ToUniversalTime();
        }
        catch (SqliteException error)
        {
            MarkUnreliable(at, $"无法保存记录：{error.Message}");
            throw;
        }
    }

    private bool AcceptTime(RecordingTime at)
    {
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
