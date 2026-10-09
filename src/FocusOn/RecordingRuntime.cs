using System.Collections.Concurrent;

namespace FocusOn;

public sealed class RecordingRuntime : IDisposable
{
    private readonly BlockingCollection<Action<RecordingSession>> commands = new();
    private readonly RecordingClock clock;
    private readonly Thread worker;
    private readonly TaskCompletionSource initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile string? failure;
    private volatile bool stopped;

    public RecordingRuntime(string databasePath, RecordingClock clock)
    {
        this.clock = clock;
        worker = new Thread(() => Run(databasePath)) { IsBackground = true, Name = "FocusOn recording" };
        worker.Start();
    }

    public Task Initialized => initialized.Task;

    public void Observe(ForegroundObservation? observation, RecordingTime at)
    {
        Submit(session =>
        {
            session.Observe(observation, at);
            session.Checkpoint(at);
        });
    }

    public Task SetConditions(RecordingConditions conditions, ForegroundObservation? observation, RecordingTime at)
    {
        return Query(session =>
        {
            session.SetConditions(conditions, observation, at);
            session.Checkpoint(at);
            return true;
        });
    }

    public Task<DayView> ReadDayAsync(DateOnly day) => Query(session =>
    {
        var view = session.GetDay(day, TimeZoneInfo.Local, clock.Now());
        return failure is null ? view : view with { Status = "记录异常", Warning = failure };
    });

    public Task FinishAsync() => Query(session =>
    {
        session.Finish(clock.Now());
        return true;
    });

    private void Run(string databasePath)
    {
        try
        {
            using var session = new RecordingSession(databasePath);
            initialized.SetResult();
            foreach (var command in commands.GetConsumingEnumerable()) command(session);
        }
        catch (Exception error)
        {
            failure = error.Message;
            initialized.TrySetException(error);
        }
    }

    private void Submit(Action<RecordingSession> command)
    {
        if (stopped) return;
        commands.Add(session =>
        {
            try { command(session); }
            catch (Exception error) { failure = error.Message; }
        });
    }

    private Task<T> Query<T>(Func<RecordingSession, T> query)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (initialized.Task.IsFaulted)
        {
            result.SetException(initialized.Task.Exception!.InnerExceptions);
            return result.Task;
        }
        if (stopped)
        {
            result.SetException(new ObjectDisposedException(nameof(RecordingRuntime)));
            return result.Task;
        }
        commands.Add(session =>
        {
            try { result.SetResult(query(session)); }
            catch (Exception error) { failure = error.Message; result.SetException(error); }
        });
        return result.Task;
    }

    public void Dispose()
    {
        if (stopped) return;
        stopped = true;
        commands.CompleteAdding();
        worker.Join();
        commands.Dispose();
    }
}
