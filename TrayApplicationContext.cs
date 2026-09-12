using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RedragonBatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "RedragonBatteryTray";
    private readonly NativeTrayIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _criticalAlertTimer;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _startWithWindowsItem;
    private readonly BatteryStabilizer _stabilizer = new(confirmationsRequired: 2);
    private bool _refreshing;
    private bool _criticalAlertBright = true;
    private int? _lastPercent;

    internal TrayApplicationContext()
    {
        _statusItem = new ToolStripMenuItem(AppText.Reading) { Enabled = false };
        var refreshItem = new ToolStripMenuItem(AppText.RefreshNow);
        refreshItem.Click += async (_, _) => await RefreshAsync();

        var openRedragonItem = new ToolStripMenuItem(AppText.OpenRedragon);
        openRedragonItem.Click += (_, _) => OpenRedragon();

        _startWithWindowsItem = new ToolStripMenuItem(AppText.StartWithWindows)
        {
            CheckOnClick = true,
            Checked = IsAutoStartEnabled()
        };
        _startWithWindowsItem.CheckedChanged += (_, _) =>
            SetAutoStart(_startWithWindowsItem.Checked);

        var aboutItem = new ToolStripMenuItem(AppText.About);
        aboutItem.Click += (_, _) => MessageBox.Show(
            AppText.AboutText,
            AppText.AppTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            AppText.MessageOptions);

        var languageItem = new ToolStripMenuItem(AppText.Language);
        var arabicItem = new ToolStripMenuItem(AppText.Arabic)
        {
            Checked = AppText.IsArabic
        };
        var englishItem = new ToolStripMenuItem(AppText.English)
        {
            Checked = !AppText.IsArabic
        };
        arabicItem.Click += (_, _) => ChangeLanguage("ar");
        englishItem.Click += (_, _) => ChangeLanguage("en");
        languageItem.DropDownItems.AddRange([arabicItem, englishItem]);

        var exitItem = new ToolStripMenuItem(AppText.Exit);
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip { RightToLeft = AppText.MenuDirection };
        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            refreshItem,
            openRedragonItem,
            _startWithWindowsItem,
            languageItem,
            new ToolStripSeparator(),
            aboutItem,
            exitItem
        });

        _notifyIcon = new NativeTrayIcon(
            TrayIconFactory.Create(null),
            AppText.ReadingTray,
            menu,
            ShowCurrentReading,
            OpenRedragon);

        _timer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _criticalAlertTimer = new System.Windows.Forms.Timer { Interval = 650 };
        _criticalAlertTimer.Tick += (_, _) =>
        {
            if (_lastPercent is not int percent || percent > 20)
            {
                _criticalAlertTimer.Stop();
                return;
            }

            _criticalAlertBright = !_criticalAlertBright;
            UpdateTrayVisual(percent);
        };

        _ = RefreshAsync();
    }

    private void ChangeLanguage(string language)
    {
        if ((language == "ar") == AppText.IsArabic)
            return;

        AppText.SetLanguage(language);
        string? executable = Environment.ProcessPath;
        if (executable is not null)
        {
            Process.Start(new ProcessStartInfo(executable, $"--restart-after {Environment.ProcessId}")
            {
                UseShellExecute = true
            });
        }
        ExitThread();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;

        try
        {
            StableBatteryReading stableReading = await Task.Run(() => M913BatteryReader.ReadStable());
            int displayedPercent = _stabilizer.Update(stableReading.Reading.Percent);
            AppLog.WriteStatus(stableReading, displayedPercent);
            _lastPercent = displayedPercent;
            _criticalAlertBright = true;
            UpdateTrayVisual(displayedPercent);
            if (displayedPercent <= 20)
                _criticalAlertTimer.Start();
            else
                _criticalAlertTimer.Stop();
            _statusItem.Text = AppText.BatteryStatus(displayedPercent);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            _lastPercent = null;
            _criticalAlertTimer.Stop();
            _notifyIcon.Update(
                TrayIconFactory.Create(null),
                AppText.UnavailableTray);
            _statusItem.Text = AppText.UnavailableStatus;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateTrayVisual(int percent)
    {
        _notifyIcon.Update(
            TrayIconFactory.Create(percent, _criticalAlertBright),
            AppText.TrayPercent(percent));
    }

    private void ShowCurrentReading()
    {
        string message = _lastPercent is int percent
            ? AppText.NotificationPercent(percent)
            : AppText.ReadFailed;
        _notifyIcon.ShowBalloon(
            AppText.AppTitle,
            message,
            _lastPercent is null ? ToolTipIcon.Warning : ToolTipIcon.Info);
    }

    private static void OpenRedragon()
    {
        const string path = @"C:\Program Files (x86)\Redragon M913\OemDrv.exe";
        if (!File.Exists(path))
        {
            MessageBox.Show(AppText.MainSoftwareMissing, AppText.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                AppText.MessageOptions);
            return;
        }

        Process.Start(new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = true
        });
    }

    private static bool IsAutoStartEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        string? processPath = Environment.ProcessPath;
        return processPath is not null && key?.GetValue(RunValueName) is string value &&
               value.Contains(processPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetAutoStart(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (key is null)
            return;
        if (enabled)
            key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunValueName, false);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _criticalAlertTimer.Stop();
        _criticalAlertTimer.Dispose();
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}

internal sealed class BatteryStabilizer
{
    private readonly int _confirmationsRequired;
    private int? _displayedPercent;
    private int? _candidatePercent;
    private int _candidateConfirmations;

    internal BatteryStabilizer(int confirmationsRequired)
    {
        if (confirmationsRequired < 1)
            throw new ArgumentOutOfRangeException(nameof(confirmationsRequired));
        _confirmationsRequired = confirmationsRequired;
    }

    internal int Update(int sampledPercent)
    {
        if (_displayedPercent is null)
        {
            _displayedPercent = sampledPercent;
            ResetCandidate();
            return sampledPercent;
        }

        if (sampledPercent == _displayedPercent)
        {
            ResetCandidate();
            return _displayedPercent.Value;
        }

        if (_candidatePercent == sampledPercent)
            _candidateConfirmations++;
        else
        {
            _candidatePercent = sampledPercent;
            _candidateConfirmations = 1;
        }

        if (_candidateConfirmations >= _confirmationsRequired)
        {
            _displayedPercent = sampledPercent;
            ResetCandidate();
        }

        return _displayedPercent.Value;
    }

    private void ResetCandidate()
    {
        _candidatePercent = null;
        _candidateConfirmations = 0;
    }
}

internal static class TrayIconFactory
{
    private const int CanvasSize = 64;

    internal static Icon Create(int? percent, bool criticalAlertBright = true)
    {
        using Bitmap bitmap = Render(percent, criticalAlertBright);
        IntPtr iconHandle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(iconHandle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    internal static Bitmap Render(int? percent, bool criticalAlertBright = true)
    {
        var bitmap = new Bitmap(CanvasSize, CanvasSize);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        Color accent = percent switch
        {
            null => Color.FromArgb(185, 190, 198),
            <= 20 when criticalAlertBright => Color.FromArgb(255, 48, 48),
            <= 20 => Color.FromArgb(128, 12, 12),
            < 50 => Color.FromArgb(255, 178, 36),
            _ => Color.FromArgb(42, 220, 105)
        };

        RectangleF body = new(1.75f, 2.25f, 57f, 59f);
        using var background = new SolidBrush(
            percent is <= 20 && criticalAlertBright
                ? Color.FromArgb(235, 112, 0, 0)
                : Color.FromArgb(225, 22, 24, 28));
        using var outline = new Pen(accent, 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        graphics.FillRoundedRectangle(background, body, 8f);
        graphics.DrawRoundedRectangle(outline, body, 8f);
        using var terminal = new SolidBrush(accent);
        graphics.FillRoundedRectangle(terminal, new RectangleF(58f, 21f, 5f, 22f), 2f);

        string text = percent is int value ? value.ToString() : "--";
        using var textPath = new GraphicsPath();
        using var fontFamily = new FontFamily("Segoe UI Variable Text");
        using var typographic = (StringFormat)StringFormat.GenericTypographic.Clone();
        textPath.AddString(
            text,
            fontFamily,
            (int)FontStyle.Regular,
            64f,
            PointF.Empty,
            typographic);

        RectangleF glyphBounds = textPath.GetBounds();
        RectangleF target = text.Length switch
        {
            >= 3 => new RectangleF(4.5f, 9f, 51.5f, 46f),
            2 => new RectangleF(4.5f, 6f, 51.5f, 51f),
            _ => new RectangleF(5f, 5f, 50f, 53f)
        };
        PointF[] destination =
        [
            new(target.Left, target.Top),
            new(target.Right, target.Top),
            new(target.Left, target.Bottom)
        ];
        using var transform = new Matrix(glyphBounds, destination);
        textPath.Transform(transform);

        using var glyphOutline = new Pen(Color.FromArgb(175, 0, 0, 0), 1.1f)
        {
            LineJoin = LineJoin.Round
        };
        using var textBrush = new SolidBrush(Color.White);
        graphics.DrawPath(glyphOutline, textPath);
        graphics.FillPath(textBrush, textPath);
        return bitmap;
    }

    private static void FillRoundedRectangle(
        this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        float diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(
        this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        float diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);
}
