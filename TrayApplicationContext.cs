using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RedragonBatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NativeTrayIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _criticalAlertTimer;
    private readonly System.Windows.Forms.Timer _healthTimer;
    private int _healthTicks;
    private bool _exiting;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _startWithWindowsItem;
    private readonly BatteryStabilizer _stabilizer = new(confirmationsRequired: 2);
    private ControlPanelForm? _controlPanel;
    private bool _refreshing;
    private bool _criticalAlertBright = true;
    private int? _lastPercent;

    internal TrayApplicationContext()
    {
        _statusItem = new ToolStripMenuItem(AppText.Reading) { Enabled = false };
        var controlPanelItem = new ToolStripMenuItem(AppText.IsArabic ? "لوحة التحكم…" : "Open control panel…");
        controlPanelItem.Click += (_, _) => ShowControlPanel();
        var refreshItem = new ToolStripMenuItem(AppText.RefreshNow);
        refreshItem.Click += async (_, _) => await RefreshAsync();

        var openRedragonItem = new ToolStripMenuItem(AppText.OpenRedragon);
        openRedragonItem.Click += (_, _) => OpenRedragon();

        _startWithWindowsItem = new ToolStripMenuItem(AppText.StartWithWindows)
        {
            CheckOnClick = true,
            Checked = AutostartManager.IsEnabled(Environment.ProcessPath)
        };
        _startWithWindowsItem.CheckedChanged += (_, _) =>
            AutostartManager.SetEnabled(_startWithWindowsItem.Checked);

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
        exitItem.Click += (_, _) => RequestExit("user Exit menu");

        var menu = new ContextMenuStrip { RightToLeft = AppText.MenuDirection };
        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            controlPanelItem,
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
            ShowControlPanel);

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

        _healthTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
        _healthTimer.Tick += (_, _) =>
        {
            if (AppRecovery.StopRequested) { RequestExit("external stop request"); return; }
            if (++_healthTicks % 10 == 0)
            {
                AppRecovery.EnsureRunning();
                _notifyIcon.EnsurePresent();
            }
        };
        _healthTimer.Start();
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
        RequestExit("language restart");
    }

    private void RequestExit(string reason)
    {
        AppRecovery.Stop(reason);
        ExitThread();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || _exiting)
            return;
        _refreshing = true;

        try
        {
            StableBatteryReading stableReading = await Task.Run(() => M913BatteryReader.ReadStable());
            if (_exiting) return;
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
            UpdateControlPanel();
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            if (_exiting) return;
            _lastPercent = null;
            _criticalAlertTimer.Stop();
            _notifyIcon.Update(
                TrayIconFactory.Create(null),
                AppText.UnavailableTray);
            _statusItem.Text = AppText.UnavailableStatus;
            UpdateControlPanel();
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

    private void ShowControlPanel()
    {
        if (_exiting)
            return;

        if (_controlPanel is { IsDisposed: false })
        {
            _controlPanel.BringToFront();
            _controlPanel.Activate();
            return;
        }

        _controlPanel = new ControlPanelForm(
            getPercent: () => _lastPercent,
            isRefreshing: () => _refreshing,
            refresh: RefreshAsync,
            getAutostart: () => _startWithWindowsItem.Checked,
            setAutostart: enabled => _startWithWindowsItem.Checked = enabled,
            openRedragon: OpenRedragon);
        _controlPanel.FormClosed += (_, _) => _controlPanel = null;
        _controlPanel.Show();
        _controlPanel.Activate();
        UpdateControlPanel();
    }

    private void UpdateControlPanel()
    {
        if (_controlPanel is { IsDisposed: false })
            _controlPanel.UpdateReading(_lastPercent, _refreshing);
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

    protected override void ExitThreadCore()
    {
        if (_exiting) return;
        _exiting = true;
        _healthTimer.Stop();
        _healthTimer.Dispose();
        _timer.Stop();
        _timer.Dispose();
        _criticalAlertTimer.Stop();
        _criticalAlertTimer.Dispose();
        _controlPanel?.Close();
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

        RectangleF body = new(1.75f, 1.75f, 57f, 60.5f);
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
        graphics.FillRoundedRectangle(terminal, new RectangleF(58f, 20.5f, 5.5f, 23f), 2f);

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
            >= 3 => new RectangleF(5f, 7f, 50f, 50f),
            2 => new RectangleF(5.75f, 4.75f, 48.5f, 54.5f),
            _ => new RectangleF(6f, 4.5f, 48f, 55f)
        };
        float scale = Math.Min(target.Width / glyphBounds.Width, target.Height / glyphBounds.Height);
        float scaledWidth = glyphBounds.Width * scale;
        float scaledHeight = glyphBounds.Height * scale;
        var fitted = new RectangleF(
            target.Left + ((target.Width - scaledWidth) / 2f),
            target.Top + ((target.Height - scaledHeight) / 2f),
            scaledWidth,
            scaledHeight);
        PointF[] destination =
        [
            new(fitted.Left, fitted.Top),
            new(fitted.Right, fitted.Top),
            new(fitted.Left, fitted.Bottom)
        ];
        using var transform = new Matrix(glyphBounds, destination);
        textPath.Transform(transform);

        using var textBrush = new SolidBrush(Color.White);
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
