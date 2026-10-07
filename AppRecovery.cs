using System.Diagnostics;

namespace RedragonBatteryTray;

internal static class AppRecovery
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedragonBatteryTray");
    private static readonly string InstanceFile = Path.Combine(Folder, "instance.txt");
    private static EventWaitHandle? _stop;
    private static EventWaitHandle? _ready;
    private static Process? _guardian;
    private static string? _token;

    internal static bool StopRequested => _stop?.WaitOne(0) == true;

    internal static void Start()
    {
        Directory.CreateDirectory(Folder);
        _token = Guid.NewGuid().ToString("N");
        _stop = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(_token));
        _ready = new EventWaitHandle(false, EventResetMode.AutoReset, ReadyName(_token));
        File.WriteAllText(InstanceFile, $"{Environment.ProcessId}\n{_token}\n");
        EnsureRunning();
    }

    internal static void EnsureRunning()
    {
        if (_stop is null || _token is null || StopRequested)
            return;
        if (_guardian is not null && !_guardian.HasExited)
            return;
        try
        {
            _guardian?.Dispose();
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("--guardian");
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            info.ArgumentList.Add(_token);
            info.ArgumentList.Add("--independent");
            _guardian = Process.Start(info);
            AppLog.Event($"Recovery guard started PID={_guardian?.Id} for main PID={Environment.ProcessId}.");
            if (_ready?.WaitOne(2_000) != true)
                AppLog.Event("Recovery guard readiness was not confirmed; next health check will retry if it exited.");
        }
        catch (Exception exception) { AppLog.Write(exception); }
    }

    internal static void Stop(string reason)
    {
        AppLog.Event($"Intentional stop: {reason}; PID={Environment.ProcessId}.");
        _stop?.Set();
    }

    internal static void Dispose()
    {
        _guardian?.Dispose();
        _stop?.Dispose();
        _ready?.Dispose();
    }

    internal static void RequestStop()
    {
        try
        {
            string[] lines = File.ReadAllLines(InstanceFile);
            if (lines.Length < 2 || !int.TryParse(lines[0], out int id) || !Guid.TryParseExact(lines[1], "N", out _))
                return;
            using Process process = Process.GetProcessById(id);
            if (!string.Equals(process.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                return;
            using EventWaitHandle stop = EventWaitHandle.OpenExisting(EventName(lines[1]));
            stop.Set();
            AppLog.Event($"External graceful stop requested for PID={id}.");
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or WaitHandleCannotBeOpenedException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            AppLog.Event($"No active instance to stop: {exception.Message}");
        }
    }

    internal static bool TryRunGuardian(string[] args)
    {
        if (args.Length != 3 || args[0] != "--guardian" || !int.TryParse(args[1], out int id) ||
            !Guid.TryParseExact(args[2], "N", out _))
            return false;
        using var mutex = new Mutex(true, $"Local\\RedragonM913Guardian-{args[2]}", out bool first);
        if (!first)
            return true;
        try
        {
            using EventWaitHandle stop = EventWaitHandle.OpenExisting(EventName(args[2]));
            using Process parent = Process.GetProcessById(id);
            // Keep an OS process handle before it exits; PID lookup alone does
            // not retain exit information after forced termination.
            _ = parent.Handle;
            if (!string.Equals(parent.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                return true;
            using EventWaitHandle ready = EventWaitHandle.OpenExisting(ReadyName(args[2]));
            ready.Set();
            AppLog.Event($"Guard monitoring PID={id}; InJob={IndependentProcess.IsInJob()}; JobLimits=0x{IndependentProcess.CurrentJobLimits():X}.");
            while (!stop.WaitOne(500) && !parent.HasExited) { }
            if (stop.WaitOne(0))
            {
                AppLog.Event($"Guard exits after intentional stop of PID={id}.");
                return true;
            }
            string exitCode;
            try { exitCode = parent.ExitCode.ToString(); }
            catch (InvalidOperationException) { exitCode = "unavailable"; }
            AppLog.Event($"Unexpected main exit PID={id}; ExitCode={exitCode}.");
            if (!stop.WaitOne(2_000) && AllowRestart())
            {
                var restart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                restart.ArgumentList.Add("--independent");
                Process.Start(restart)?.Dispose();
                AppLog.Event("Guard relaunched the tray application.");
            }
        }
        catch (Exception exception) { AppLog.Write(exception); }
        return true;
    }

    private static bool AllowRestart()
    {
        string state = Path.Combine(Folder, "recovery-history.txt");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var recent = new List<DateTimeOffset>();
        if (File.Exists(state))
            foreach (string line in File.ReadAllLines(state))
                if (DateTimeOffset.TryParse(line, out DateTimeOffset time) && time > now.AddMinutes(-10))
                    recent.Add(time);
        if (recent.Count >= 5)
        {
            AppLog.Event("Recovery paused: five unexpected exits within ten minutes; avoiding a restart loop.");
            return false;
        }
        recent.Add(now);
        File.WriteAllLines(state, recent.Select(time => time.ToString("O")));
        return true;
    }

    private static string EventName(string token) => $"Local\\RedragonM913Stop-{token}";
    private static string ReadyName(string token) => $"Local\\RedragonM913Ready-{token}";
}
