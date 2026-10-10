using Microsoft.Data.Sqlite;

namespace FocusOn;

// Called only on the recording worker; shares its connection and deletion transaction.
internal sealed class TitleTimerStore
{
    private readonly SqliteConnection connection;
    private long? taskId;
    private string? title;
    private long? segmentId;
    private RecordingTime? segmentStart;
    private RecordingTime? trustedEnd;
    public string? Warning { get; private set; }

    public TitleTimerStore(SqliteConnection connection)
    {
        this.connection = connection;
        using var schema = connection.CreateCommand();
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS title_timers (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, finished INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS timer_segments (id INTEGER PRIMARY KEY AUTOINCREMENT, task_id INTEGER NOT NULL,
                start_utc INTEGER NOT NULL, end_utc INTEGER NOT NULL, duration_ticks INTEGER NOT NULL,
                basis_start INTEGER NOT NULL, basis_end INTEGER NOT NULL, basis_ticks INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS timer_segments_task ON timer_segments(task_id);
            CREATE INDEX IF NOT EXISTS timer_segments_day ON timer_segments(end_utc,start_utc);
            SELECT id, title FROM title_timers WHERE finished = 0 ORDER BY id DESC LIMIT 1;
            """;
        using var reader = schema.ExecuteReader();
        if (reader.Read()) { taskId = reader.GetInt64(0); title = reader.GetString(1); }
        // The last successfully saved endpoint is history; reopening never extends it.
    }

    public void Change(TimerAction action, string? newTitle, RecordingTime at)
    {
        if (action == TimerAction.Start)
        {
            if (taskId is not null) throw new InvalidOperationException("请先停止当前计时。");
            var text = newTitle?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > 200) throw new ArgumentException("请输入 1 至 200 字的计时标题。");
            using var transaction = connection.BeginTransaction();
            using var create = connection.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = "INSERT INTO title_timers(title) VALUES ($title); SELECT last_insert_rowid();";
            create.Parameters.AddWithValue("$title", text);
            var id = (long)create.ExecuteScalar()!;
            var segment = CreateSegment(id, at, transaction);
            transaction.Commit();
            taskId = id;
            title = text;
            segmentId = segment;
            segmentStart = trustedEnd = at;
            Warning = null;
            return;
        }
        if (taskId is null) throw new InvalidOperationException("当前没有标题计时。");
        if (action == TimerAction.Resume)
        {
            if (segmentStart is null)
            {
                segmentId = CreateSegment(taskId.Value, at);
                segmentStart = trustedEnd = at;
                Warning = null;
            }
            return;
        }
        Pause(at);
        if (action == TimerAction.Stop)
        {
            using var finish = connection.CreateCommand();
            finish.CommandText = "UPDATE title_timers SET finished = 1 WHERE id = $id;";
            finish.Parameters.AddWithValue("$id", taskId.Value);
            finish.ExecuteNonQuery();
            taskId = null;
            title = null;
        }
    }

    private long CreateSegment(long owner, RecordingTime at, SqliteTransaction? transaction = null)
    {
        using var create = connection.CreateCommand();
        create.Transaction = transaction;
        create.CommandText = """
            INSERT INTO timer_segments(task_id,start_utc,end_utc,duration_ticks,basis_start,basis_end,basis_ticks)
            VALUES ($task,$at,$at,0,$at,$at,0); SELECT last_insert_rowid();
            """;
        create.Parameters.AddWithValue("$task", owner);
        create.Parameters.AddWithValue("$at", at.Utc.UtcTicks);
        return (long)create.ExecuteScalar()!;
    }

    private bool Reliable(RecordingTime at) => trustedEnd is not RecordingTime previous ||
        (at.MonotonicTicks >= previous.MonotonicTicks && at.Utc >= previous.Utc && at.TimeZoneId == previous.TimeZoneId &&
         Math.Abs((at.Utc - previous.Utc).Ticks - (at.MonotonicTicks - previous.MonotonicTicks)) <= TimeSpan.FromSeconds(2).Ticks);

    public void Checkpoint(RecordingTime at)
    {
        if (segmentStart is not RecordingTime start || at.MonotonicTicks < start.MonotonicTicks) return;
        if (trustedEnd is RecordingTime previous && at.MonotonicTicks < previous.MonotonicTicks) return;
        if (!Reliable(at))
        {
            segmentStart = trustedEnd = null;
            segmentId = null;
            Warning = "标题计时因系统时间或时区变化已暂停；仅保留已保存的时间，请手动继续。";
            return;
        }
        var duration = at.MonotonicTicks - start.MonotonicTicks;
        if (duration == 0 || at.Utc <= start.Utc) return;
        using var save = connection.CreateCommand();
        save.CommandText = "UPDATE timer_segments SET end_utc=$end, duration_ticks=$duration, basis_end=$end, basis_ticks=$duration WHERE id=$id;";
        save.Parameters.AddWithValue("$end", at.Utc.UtcTicks);
        save.Parameters.AddWithValue("$duration", duration);
        save.Parameters.AddWithValue("$id", (object?)segmentId ?? DBNull.Value);
        try { save.ExecuteNonQuery(); }
        catch (SqliteException error)
        {
            segmentStart = trustedEnd = null;
            segmentId = null;
            Warning = $"标题计时保存失败，已暂停并保留成功保存的时间：{error.Message}";
            throw;
        }
        trustedEnd = at;
    }

    public void Pause(RecordingTime at)
    {
        try { Checkpoint(at); }
        finally
        {
            segmentStart = trustedEnd = null;
            segmentId = null;
        }
    }

    private RecordedInterval? Live(RecordingTime? now)
    {
        if (segmentStart is not RecordingTime start || now is not RecordingTime at || !Reliable(at) ||
            at.MonotonicTicks <= start.MonotonicTicks || at.Utc <= start.Utc) return null;
        return new(-(segmentId ?? long.MaxValue), IntervalKind.TaskTimer, start.Utc.ToUniversalTime(), at.Utc.ToUniversalTime(),
            TimeSpan.FromTicks(at.MonotonicTicks - start.MonotonicTicks), null, "标题计时", title, true);
    }

    public TitleTimerState? State(RecordingTime? now)
    {
        if (taskId is not long id) return null;
        using var sum = connection.CreateCommand();
        sum.CommandText = "SELECT COALESCE(SUM(duration_ticks),0) FROM timer_segments WHERE task_id=$id;";
        sum.Parameters.AddWithValue("$id", id);
        var ticks = (long)sum.ExecuteScalar()!;
        if (Live(now) is { } live)
        {
            if (segmentId is long saved)
            {
                using var existing = connection.CreateCommand();
                existing.CommandText = "SELECT duration_ticks FROM timer_segments WHERE id=$id;";
                existing.Parameters.AddWithValue("$id", saved);
                ticks -= (long)existing.ExecuteScalar()!;
            }
            ticks += live.Duration.Ticks;
        }
        return new(title!, TimeSpan.FromTicks(ticks), segmentStart is not null);
    }

    public List<RecordedInterval> Read(DateTimeOffset start, DateTimeOffset end, RecordingTime? now = null, SqliteTransaction? transaction = null, bool clip = true)
    {
        using var query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = """
            SELECT s.id,s.start_utc,s.end_utc,s.duration_ticks,t.title,s.basis_start,s.basis_end,s.basis_ticks
            FROM timer_segments s JOIN title_timers t ON t.id=s.task_id
            WHERE s.end_utc > $start AND s.start_utc < $end AND s.duration_ticks > 0 ORDER BY s.start_utc,s.id;
            """;
        query.Parameters.AddWithValue("$start", start.UtcTicks);
        query.Parameters.AddWithValue("$end", end.UtcTicks);
        var result = new List<RecordedInterval>();
        using (var reader = query.ExecuteReader())
            while (reader.Read())
            {
                var interval = new RecordedInterval(-reader.GetInt64(0), IntervalKind.TaskTimer, new(reader.GetInt64(1), TimeSpan.Zero),
                    new(reader.GetInt64(2), TimeSpan.Zero), TimeSpan.FromTicks(reader.GetInt64(3)), null, "标题计时", reader.GetString(4));
                var clipped = clip ? DailyProjection.Clip(interval, start, end) : interval;
                if (clipped is not null)
                    result.Add(clipped with { Duration = TimeSpan.FromTicks(Allocated(reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7), clipped.StartUtc.UtcTicks, clipped.EndUtc.UtcTicks)) });
            }
        if (Live(now) is { } live)
        {
            result.RemoveAll(x => x.Id == live.Id);
            result.Add(live);
        }
        return result;
    }

    private static long Allocated(long basisStart, long basisEnd, long ticks, long start, long end) =>
        (long)((Int128)ticks * (end - basisStart) / (basisEnd - basisStart)) -
        (long)((Int128)ticks * (start - basisStart) / (basisEnd - basisStart));

    public void PrepareDelete(DateTimeOffset start, DateTimeOffset end, RecordingTime at)
    {
        if (segmentStart is RecordingTime running && running.Utc < end && at.Utc >= start) Pause(at);
    }

    public void Delete(DateTimeOffset start, DateTimeOffset end, SqliteTransaction transaction)
    {
        // Read full segments, then use their original allocation basis for each retained part.
        var overlapping = Read(start, end, transaction: transaction, clip: false);
        foreach (var interval in overlapping)
        {
            using var owner = connection.CreateCommand();
            owner.Transaction = transaction;
            owner.CommandText = "SELECT task_id,basis_start,basis_end,basis_ticks FROM timer_segments WHERE id=$id;";
            owner.Parameters.AddWithValue("$id", -interval.Id);
            long ownerId, basisStart, basisEnd, basisTicks;
            using (var reader = owner.ExecuteReader())
            {
                reader.Read();
                ownerId = reader.GetInt64(0); basisStart = reader.GetInt64(1); basisEnd = reader.GetInt64(2); basisTicks = reader.GetInt64(3);
            }
            using var remove = connection.CreateCommand();
            remove.Transaction = transaction;
            remove.CommandText = "DELETE FROM timer_segments WHERE id=$id;";
            remove.Parameters.AddWithValue("$id", -interval.Id);
            remove.ExecuteNonQuery();
            var before = DailyProjection.Clip(interval, interval.StartUtc, start);
            var after = DailyProjection.Clip(interval, end, interval.EndUtc);
            foreach (var part in new[] { before, after }.OfType<RecordedInterval>())
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO timer_segments(task_id,start_utc,end_utc,duration_ticks,basis_start,basis_end,basis_ticks)
                    VALUES ($task,$start,$end,$duration,$basisStart,$basisEnd,$basisTicks);
                    """;
                insert.Parameters.AddWithValue("$task", ownerId);
                insert.Parameters.AddWithValue("$start", part.StartUtc.UtcTicks);
                insert.Parameters.AddWithValue("$end", part.EndUtc.UtcTicks);
                insert.Parameters.AddWithValue("$duration", Allocated(basisStart, basisEnd, basisTicks, part.StartUtc.UtcTicks, part.EndUtc.UtcTicks));
                insert.Parameters.AddWithValue("$basisStart", basisStart);
                insert.Parameters.AddWithValue("$basisEnd", basisEnd);
                insert.Parameters.AddWithValue("$basisTicks", basisTicks);
                insert.ExecuteNonQuery();
            }
        }
        using var cleanup = connection.CreateCommand();
        cleanup.Transaction = transaction;
        cleanup.CommandText = "DELETE FROM title_timers WHERE finished=1 AND NOT EXISTS (SELECT 1 FROM timer_segments WHERE task_id=title_timers.id AND duration_ticks>0);";
        cleanup.ExecuteNonQuery();
    }
}
