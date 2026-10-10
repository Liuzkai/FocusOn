using System.Security.Cryptography;
using System.Text;

namespace FocusOn;

public sealed class SingleInstance : IDisposable
{
    private readonly EventWaitHandle activation;
    private readonly Mutex writer;
    private RegisteredWaitHandle? listener;
    private volatile bool disposed;

    public SingleInstance(string dataDirectory)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory)).ToUpperInvariant();
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory)));
        var options = new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = false };
        // Create the event first so a duplicate can signal while startup is still loading.
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, "FocusOn-activate-" + identity, options);
        try
        {
            writer = new Mutex(false, "FocusOn-writer-" + identity, options);
            try { IsPrimary = writer.WaitOne(0); }
            catch (AbandonedMutexException) { IsPrimary = true; }
            catch { writer.Dispose(); throw; }
        }
        catch { activation.Dispose(); throw; }
    }

    public bool IsPrimary { get; }

    public void Listen(Action onActivate)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsPrimary) throw new InvalidOperationException("Only the recording instance can receive activation requests.");
        if (listener is not null) throw new InvalidOperationException("Activation listening has already started.");
        listener = ThreadPool.RegisterWaitForSingleObject(activation,
            (_, _) => { if (!disposed) onActivate(); }, null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void SignalExisting()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        activation.Set();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        listener?.Unregister(null);
        // Mutex ownership belongs to the startup thread, which must also dispose this instance.
        if (IsPrimary) writer.ReleaseMutex();
        writer.Dispose();
        activation.Dispose();
    }
}
