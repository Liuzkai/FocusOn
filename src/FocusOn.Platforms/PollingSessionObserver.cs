namespace FocusOn;

public abstract class PollingSessionObserver(Action<SessionState> changed) : ISessionObserver
{
    private readonly ManualResetEventSlim stopping = new();
    private readonly object gate = new();
    private SessionState state = new(false, false, false, false);
    private Thread? worker;
    private bool disposed;
    protected abstract SessionState ReadState();
    protected virtual void Close() { }
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (worker is not null) throw new InvalidOperationException("会话观测已经启动。");
        worker = new Thread(() =>
        {
            try
            {
                while (!stopping.IsSet)
                {
                    SessionState next;
                    try { next = ReadState(); }
                    catch (Exception error) { next = new(false, false, false, false, "无法确认会话状态：" + error.Message); }
                    bool different;
                    lock (gate) { different = next != state; state = next; }
                    if (different) changed(next);
                    stopping.Wait(TimeSpan.FromMilliseconds(500));
                }
            }
            finally { Close(); }
        }) { IsBackground = true, Name = "FocusOn session samples" };
        worker.Start();
    }
    public SessionState ReadCurrentState() { lock (gate) return state; }
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
