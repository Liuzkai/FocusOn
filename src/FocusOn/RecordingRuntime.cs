using System.Collections.Concurrent;

namespace FocusOn;

public sealed class RecordingRuntime : IDisposable
{
    private readonly BlockingCollection<Action<RecordingSession>> commands = new();
    private readonly RecordingClock clock;
    private readonly Thread worker;
    private readonly TaskCompletionSource initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile string? failure;
    private volatile string? observationFailure;
    private long observationFailureAt;
    private volatile bool stopped;

    public RecordingRuntime(string databasePath, RecordingClock clock)
    {
        this.clock = clock;
        worker = new Thread(() => Run(databasePath)) { IsBackground = true, Name = "FocusOn recording" };
        worker.Start();
    }

    public Task Initialized => initialized.Task;

    public void MarkUnreliable(RecordingTime at, string reason) => Submit(session =>
    {
        session.MarkUnreliable(at, reason);
        observationFailure = reason;
        observationFailureAt = at.MonotonicTicks;
    });

    public void Observe(ForegroundObservation? observation, RecordingTime at)
    {
        Submit(session =>
        {
            session.Observe(observation, at);
            session.Checkpoint(at);
            failure = null;
            if (observation is not null && at.MonotonicTicks >= observationFailureAt) observationFailure = null;
        });
    }

    public Task SetSystemConditions(RecordingConditions conditions, ForegroundObservation? observation, RecordingTime at)
    {
        return Query(session =>
        {
            session.SetSystemConditions(conditions, observation, at);
            session.Checkpoint(at);
            failure = null;
            return true;
        });
    }

    public Task SetManualPaused(bool paused, ForegroundObservation? observation, RecordingTime at) => Query(session =>
    {
        session.SetManualPaused(paused, observation, at);
        session.Checkpoint(at);
        failure = null;
        return true;
    });

    public Task<DayView> ReadDayAsync(DateOnly day) => Query(session =>
    {
        var view = session.GetDay(day, TimeZoneInfo.Local, clock.Now());
        var warning = failure ?? observationFailure;
        return warning is null ? view : view with { Status = "记录异常", Warning = warning };
    });

    public Task ChangeTimerAsync(TimerAction action, string? title = null) => Query(session =>
    {
        session.ChangeTimer(action, title, clock.Now());
        return true;
    });

    public Task DeleteDayAsync(DateOnly day) => Query(session =>
    {
        session.DeleteDay(day, TimeZoneInfo.Local, clock.Now());
        return true;
    });

    public Task<bool> ResumeAfterDeleteAsync(ForegroundObservation? observation, RecordingConditions system, RecordingTime at) => Query(session =>
    {
        session.SetSystemConditions(system, observation, at);
        var resumed = session.ResumeAfterDelete(observation, at);
        if (resumed) failure = null;
        return resumed;
    });

    public Task<string?> FinishAsync(RecordingTime at) => Query(session =>
    {
        session.Finish(at);
        return failure ?? observationFailure ?? session.GetDay(DateOnly.FromDateTime(at.Utc.LocalDateTime), TimeZoneInfo.Local).Warning;
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
