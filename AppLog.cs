namespace RedragonBatteryTray;

internal static class AppLog
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RedragonBatteryTray");

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
