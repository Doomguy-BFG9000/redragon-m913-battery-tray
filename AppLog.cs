namespace RedragonBatteryTray;

internal static class AppLog
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RedragonBatteryTray");

    internal static void Event(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "lifecycle.log");
            using var mutex = new Mutex(false, @"Local\RedragonM913LifecycleLog");
            bool acquired;
            try { acquired = mutex.WaitOne(2_000); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return;
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                    File.Move(path, path + ".old", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} PID={Environment.ProcessId} {message}\n");
            }
            finally { mutex.ReleaseMutex(); }
        }
        catch { /* Diagnostics must not terminate the app. */ }
    }

    internal static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "errors.log"),
                $"{DateTimeOffset.Now:O} {exception}\n\n");
        }
        catch
        {
            // Logging must never terminate the tray utility.
        }
    }

    internal static void WriteStatus(StableBatteryReading stableReading, int displayedPercent)
    {
        try
        {
            BatteryReading reading = stableReading.Reading;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "status.txt"),
                $"Updated={DateTimeOffset.Now:O}\n" +
                $"DisplayedPercent={displayedPercent}\n" +
                $"SampledPercent={reading.Percent}\n" +
                $"RawMedian={reading.RawLevel}\n" +
                $"RawSamples={string.Join(',', stableReading.RawSamples)}\n" +
                $"Status={reading.Status}\n" +
                $"ProductId={reading.ProductId}\n" +
                $"Report={Convert.ToHexString(reading.Report)}\n");
        }
        catch
        {
            // Status persistence is diagnostic only.
        }
    }
}
