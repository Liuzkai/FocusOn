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

    public RecordingSession(string databasePath)
    {
        var path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
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
    }

    public void Observe(ForegroundObservation? observation, RecordingTime at)
    {
        var kind = !conditions.Available ? IntervalKind.Unrecorded :
            conditions.ManualPaused ? IntervalKind.ManualPause :
            conditions.Sleeping ? IntervalKind.Sleep :
            conditions.Locked ? IntervalKind.Locked :
            conditions.DisplayOff ? IntervalKind.DisplayOff :
            observation is null ? IntervalKind.Unrecorded : IntervalKind.Usage;
        if (current is not null && current.Kind == kind &&
            (kind != IntervalKind.Usage || (observation?.WindowId == currentObservation?.WindowId &&
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

    public void Checkpoint(RecordingTime at) => SaveCurrent(at);

    public void SetConditions(RecordingConditions conditions, ForegroundObservation? currentObservation, RecordingTime at)
    {
        this.conditions = conditions;
        Observe(currentObservation, at);
    }

    public void SetManualPaused(bool paused, ForegroundObservation? currentObservation, RecordingTime at) =>
        SetConditions(conditions with { ManualPaused = paused }, currentObservation, at);

    public void SetSystemConditions(RecordingConditions system, ForegroundObservation? currentObservation, RecordingTime at) =>
        SetConditions(system with { ManualPaused = conditions.ManualPaused }, currentObservation, at);

    public void Finish(RecordingTime at)
    {
        SaveCurrent(at);
        current = null;
        currentObservation = null;
    }

    public DayView GetDay(DateOnly day, TimeZoneInfo zone, RecordingTime? liveNow = null)
    {
        var (start, end) = DailyProjection.Bounds(day, zone);
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

        if (current is not null && liveNow is RecordingTime at)
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

        intervals = intervals.Select(interval => DailyProjection.Clip(interval, start, end))
            .OfType<RecordedInterval>().ToList();
        var totals = intervals.Where(interval => interval.Kind == IntervalKind.Usage)
            .GroupBy(interval => interval.AppKey!)
            .Select(group => new AppTotal(group.Key, group.First().AppName!,
                TimeSpan.FromTicks(group.Sum(interval => interval.Duration.Ticks))))
            .OrderByDescending(total => total.Duration).ThenBy(total => total.AppKey, StringComparer.Ordinal)
            .ToArray();
        return new DayView(day, start, end, intervals, totals,
            current is null ? "已停止" : current.Kind switch
            {
                IntervalKind.ManualPause => "手动暂停",
                IntervalKind.Sleep => "睡眠",
                IntervalKind.Locked => "锁屏",
                IntervalKind.DisplayOff => "屏幕已关闭",
                IntervalKind.Unrecorded => "未记录",
                _ => "正在记录"
            }, null);
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
    }

    public void Dispose() => connection.Dispose();
}
