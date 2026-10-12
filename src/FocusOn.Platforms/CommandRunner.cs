using System.Diagnostics;

namespace FocusOn;

internal static class CommandRunner
{
    public static string Run(string command, params string[] arguments)
    {
        var start = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"无法启动 {command}。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(1500))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"{command} 响应超时。");
        }
        var text = output.GetAwaiter().GetResult();
        var detail = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{command}：{detail.Trim()}");
        return text.Trim();
    }
}
