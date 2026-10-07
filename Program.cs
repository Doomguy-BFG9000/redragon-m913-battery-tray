using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace RedragonBatteryTray;

internal static class Program
{
    private const string MutexName = @"Local\RedragonM913BatteryTray";

    [STAThread]
    private static void Main(string[] args)
    {
        if (TryRunProbe(args))
            return;

        bool independentAttempted = args.Contains("--independent");
        args = args.Where(arg => arg != "--independent").ToArray();
        if (args.Contains("--desktop-bootstrap"))
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string arg in args.Where(arg => arg != "--desktop-bootstrap")) info.ArgumentList.Add(arg);
            info.ArgumentList.Add("--independent");
            Process.Start(info)?.Dispose();
            return;
        }
        if (IndependentProcess.TryRelaunch(args, independentAttempted))
            return;
        if (args.Length == 1 && args[0] == "--request-stop")
        {
            AppRecovery.RequestStop();
            return;
        }
        if (args.Length == 1 && args[0] == "--set-autostart")
        {
            AutostartManager.SetEnabled(true);
            return;
        }
        if (AppRecovery.TryRunGuardian(args))
            return;

        WaitForPreviousInstance(args);

        using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
            return;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => AppLog.Write(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AppLog.Write(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled error"));
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLog.Write(eventArgs.Exception);
            eventArgs.SetObserved();
        };
        SessionEndingEventHandler sessionEnding = (_, _) => AppRecovery.Stop("Windows session ending");
        SystemEvents.SessionEnding += sessionEnding;
        AppLog.Event($"Main started version={Application.ProductVersion}; InJob={IndependentProcess.IsInJob()}; JobLimits=0x{IndependentProcess.CurrentJobLimits():X}; Packaged={IndependentProcess.HasPackageIdentity()}.");
        try
        {
            AppRecovery.Start();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception exception) { AppLog.Write(exception); }
        finally
        {
            AppLog.Event("Main message loop ended.");
            SystemEvents.SessionEnding -= sessionEnding;
            AppRecovery.Dispose();
        }
    }

    private static void WaitForPreviousInstance(string[] args)
    {
        if (args.Length != 2 ||
            !args[0].Equals("--restart-after", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(args[1], out int processId))
            return;

        try
        {
            using Process process = Process.GetProcessById(processId);
            process.WaitForExit(5_000);
        }
        catch (ArgumentException)
        {
            // The previous process already exited.
        }
    }

    private static bool TryRunProbe(string[] args)
    {
        if (args.Length != 2)
            return false;

        if (args[0].Equals("--stabilizer-self-test-file", StringComparison.OrdinalIgnoreCase))
        {
            var stabilizer = new BatteryStabilizer(confirmationsRequired: 2);
            int[] input = [70, 60, 70, 60, 60, 70, 70];
            int[] expected = [70, 70, 70, 70, 60, 60, 70];
            int[] actual = input.Select(stabilizer.Update).ToArray();
            bool passed = actual.SequenceEqual(expected);
            File.WriteAllText(args[1],
                $"{(passed ? "PASS" : "FAIL")}\nInput={string.Join(',', input)}\n" +
                $"Expected={string.Join(',', expected)}\nActual={string.Join(',', actual)}\n",
                Encoding.UTF8);
            return true;
        }

        if (args[0].Equals("--icon-preview-file", StringComparison.OrdinalIgnoreCase))
        {
            using var preview = new Bitmap(900, 240);
            using Graphics graphics = Graphics.FromImage(preview);
            graphics.Clear(Color.FromArgb(31, 32, 35));
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            int[] levels = [60, 40, 20, 20];
            bool[] bright = [true, true, true, false];
            string[] labels = ["60% - good", "40% - low", "20% - alert on", "20% - alert off"];
            using var labelFont = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var labelBrush = new SolidBrush(Color.White);
            for (int index = 0; index < levels.Length; index++)
            {
                int x = 28 + (index * 218);
                using Bitmap icon = TrayIconFactory.Render(levels[index], bright[index]);
                graphics.DrawImage(icon, new Rectangle(x + 25, 22, 140, 140));
                graphics.DrawString(labels[index], labelFont, labelBrush, x, 178);
            }
            preview.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            return true;
        }

        bool stableProbe = args[0].Equals("--stable-probe-file", StringComparison.OrdinalIgnoreCase);
        if (!stableProbe && !args[0].Equals("--probe-file", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            StableBatteryReading? stable = stableProbe ? M913BatteryReader.ReadStable() : null;
            BatteryReading reading = stable?.Reading ?? M913BatteryReader.Read();
            File.WriteAllText(args[1],
                $"OK\nPercent={reading.Percent}\nRaw={reading.RawLevel}\nStatus={reading.Status}\n" +
                $"ProductId={reading.ProductId}\nReport={Convert.ToHexString(reading.Report)}\n" +
                (stable is null ? string.Empty : $"RawSamples={string.Join(',', stable.RawSamples)}\n"),
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            File.WriteAllText(args[1], $"ERROR\n{ex.GetType().Name}: {ex.Message}\n", Encoding.UTF8);
        }

        return true;
    }
}
