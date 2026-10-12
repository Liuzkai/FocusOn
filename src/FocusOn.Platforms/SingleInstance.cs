using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;

namespace FocusOn;

/// <summary>A persistent exclusive file handle owns recording; user-only IPC restores the window.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly FileStream? writer;
    private readonly string pipeName;
    private readonly CancellationTokenSource stopping = new();
    private readonly object gate = new();
    private readonly Task? server;
    private Action? activated;
    private bool pending;
    private bool disposed;

    public SingleInstance(string dataDirectory)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        directory = CanonicalDirectory(directory);
        var identityPath = OperatingSystem.IsWindows() ? directory.ToUpperInvariant() : directory;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + "\0" + identityPath)));
        pipeName = "FocusOn-" + identity;
        try
        {
            writer = new FileStream(Path.Combine(directory, ".focuson.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            IsPrimary = true;
        }
        catch (IOException) { IsPrimary = false; }
        if (IsPrimary)
        {
            try { server = ListenAsync(CreatePipe()); }
            catch { writer!.Dispose(); throw; }
        }
    }

    public bool IsPrimary { get; }

    private static string CanonicalDirectory(string directory)
    {
        if (OperatingSystem.IsWindows()) return directory;
        var resolved = RealPath(directory, 0);
        if (resolved != 0)
        {
            try { directory = Marshal.PtrToStringUTF8(resolved) ?? directory; }
            finally { Free(resolved); }
        }
        if (OperatingSystem.IsMacOS())
        {
            // Darwin returns the actual volume spelling, including on case-insensitive APFS.
            var fd = Open(directory, 0);
            if (fd >= 0)
            {
                try
                {
                    var buffer = new byte[1024];
                    if (GetPath(fd, 50, buffer) == 0) directory = Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0));
                }
                finally { Close(fd); }
            }
        }
        return directory;
    }

    [DllImport("libc", EntryPoint = "realpath")] private static extern nint RealPath(string path, nint buffer);
    [DllImport("libc", EntryPoint = "free")] private static extern void Free(nint pointer);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "open")] private static extern int Open(string path, int flags);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "fcntl")] private static extern int GetPath(int fd, int command, byte[] path);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "close")] private static extern int Close(int fd);

    private NamedPipeServerStream CreatePipe() => new(pipeName, PipeDirection.In, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private async Task ListenAsync(NamedPipeServerStream pipe)
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                using (pipe)
                {
                    await pipe.WaitForConnectionAsync(stopping.Token).ConfigureAwait(false);
                    var message = new byte[1];
                    if (await pipe.ReadAsync(message, stopping.Token).ConfigureAwait(false) == 1 && message[0] == 1)
                    {
                        Action? callback;
                        lock (gate)
                        {
                            callback = activated;
                            if (callback is null) pending = true;
                        }
                        callback?.Invoke();
                    }
                }
                if (!stopping.IsCancellationRequested) pipe = CreatePipe();
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (stopping.IsCancellationRequested) { }
        finally { pipe.Dispose(); }
    }

    public void Listen(Action onActivate)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!IsPrimary) throw new InvalidOperationException("Only the recording instance can receive activation requests.");
            if (activated is not null) throw new InvalidOperationException("Activation listening has already started.");
            activated = onActivate;
            if (!pending) return;
            pending = false;
        }
        onActivate();
    }

    public void SignalExisting()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
        client.Connect(3000);
        client.WriteByte(1);
        client.Flush();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Cancel();
        try { server?.GetAwaiter().GetResult(); }
        finally { writer?.Dispose(); stopping.Dispose(); }
        // Never unlink the lock file: replacing the inode could allow two simultaneous writers.
    }
}
