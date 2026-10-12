using System.Collections.Concurrent;

namespace FocusOn;

public abstract class PollingForegroundObserver : IForegroundObserver
{
    private readonly RecordingClock clock;
    private readonly Action<ForegroundObservation?, RecordingTime> observed;
    private readonly Action<RecordingTime, string> failed;
    private readonly Action<RecordingTime, string>? interrupted;
    private readonly ConcurrentQueue<Action<ForegroundObservation?, RecordingTime>> snapshots = new();
    private readonly ManualResetEventSlim stopping = new();
    private Thread? worker;
    private bool disposed;

    protected PollingForegroundObserver(RecordingClock clock, Action<ForegroundObservation?, RecordingTime> observed,
        Action<RecordingTime, string> failed, Action<RecordingTime, string>? interrupted = null)
    {
        this.clock = clock;
        this.observed = observed;
        this.failed = failed;
        this.interrupted = interrupted;
    }

    protected abstract ForegroundObservation? ReadForeground();
    protected virtual void Close() { }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (worker is not null) throw new InvalidOperationException("观测已经启动。");
        worker = new Thread(Run) { IsBackground = true, Name = "FocusOn foreground samples" };
        worker.Start();
    }

    private void Run()
    {
        RecordingTime? previous = null;
        try
        {
            while (!stopping.IsSet)
            {
                var at = clock.Now();
                if (previous is { } before && (at.Utc - before.Utc > TimeSpan.FromSeconds(2)
                    || at.MonotonicTicks - before.MonotonicTicks > TimeSpan.FromSeconds(2).Ticks))
                    interrupted?.Invoke(at, "采样因睡眠或调度停顿中断；空缺不补算，标题计时已暂停，请手动继续。");
                previous = at;
                ForegroundObservation? observation = null;
                try { observation = ReadForeground(); }
                catch (Exception error) { failed(at, error.Message); }
                // Timestamp after native lookup; never assign the preceding observation to a slow read.
                var sampledAt = clock.Now();
                if (sampledAt.Utc - at.Utc > TimeSpan.FromSeconds(2))
                    interrupted?.Invoke(sampledAt, "前台读取停顿；无法确认的范围不补算，标题计时已暂停。");
                observed(observation, sampledAt);
                while (snapshots.TryDequeue(out var snapshot)) snapshot(observation, sampledAt);
                stopping.Wait(TimeSpan.FromMilliseconds(200));
            }
        }
        finally
        {
            Close();
            while (snapshots.TryDequeue(out var snapshot)) snapshot(null, clock.Now());
        }
    }

    public void Poll() => RequestSnapshot(observed);
    public void RequestSnapshot(Action<ForegroundObservation?, RecordingTime> consumer)
    {
        if (disposed || worker is null || !worker.IsAlive) { consumer(null, clock.Now()); return; }
        snapshots.Enqueue(consumer);
    }
    public bool IsStopped => worker is null || !worker.IsAlive;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Set();
        if (worker is not null && Thread.CurrentThread != worker) worker.Join();
        stopping.Dispose();
    }
}
