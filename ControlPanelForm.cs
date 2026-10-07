using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace RedragonBatteryTray;

/// <summary>
/// The visible home for the tray utility. It deliberately uses only WinForms so the
/// release stays small, offline, and dependable on a clean Windows installation.
/// </summary>
internal sealed class ControlPanelForm : Form
{
    private static readonly Color WindowColor = Color.FromArgb(24, 27, 32);
    private static readonly Color SurfaceColor = Color.FromArgb(35, 40, 47);
    private static readonly Color MutedText = Color.FromArgb(181, 190, 201);
    private readonly Func<int?> _getPercent;
    private readonly Func<bool> _isRefreshing;
    private readonly Func<Task> _refresh;
    private readonly CheckBox _autostart;
    private readonly Label _headline;
    private readonly Label _detail;
    private readonly Label _lastChecked;
    private readonly DashboardBatteryMeter _meter;
    private readonly Button _refreshButton;
    private bool _closing;

    internal ControlPanelForm(
        Func<int?> getPercent,
        Func<bool> isRefreshing,
        Func<Task> refresh,
        Func<bool> getAutostart,
        Action<bool> setAutostart,
        Action openRedragon)
    {
        _getPercent = getPercent;
        _isRefreshing = isRefreshing;
        _refresh = refresh;

        Text = AppText.AppTitle;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = WindowColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        ClientSize = new Size(500, 474);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowIcon = true;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = AppText.IsArabic ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = AppText.IsArabic;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 24),
            BackColor = WindowColor,
            ColumnCount = 1,
            RowCount = 5,
            RightToLeft = RightToLeft
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = NewLabel(AppText.IsArabic ? "بطارية فأرة Redragon M913" : "Redragon M913 battery", 19f, FontStyle.Bold, Color.White);
        title.Margin = new Padding(0, 0, 0, 3);
        _headline = NewLabel(AppText.IsArabic ? "جارٍ قراءة البطارية…" : "Reading battery…", 11f, FontStyle.Regular, MutedText);
        _headline.Margin = new Padding(0, 0, 0, 20);

        var card = new Panel
        {
            BackColor = SurfaceColor,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(24, 20, 24, 18)
        };
        card.Paint += DrawCardBorder;
        _meter = new DashboardBatteryMeter { Dock = DockStyle.Top, Height = 105 };
        _detail = NewLabel(string.Empty, 11f, FontStyle.Regular, MutedText);
        _detail.Dock = DockStyle.Top;
        _detail.Height = 27;
        _detail.Margin = new Padding(0, 12, 0, 0);
        _lastChecked = NewLabel(string.Empty, 9f, FontStyle.Regular, Color.FromArgb(138, 149, 163));
        _lastChecked.Dock = DockStyle.Bottom;
        _lastChecked.Height = 24;
        card.Controls.Add(_lastChecked);
        card.Controls.Add(_detail);
        card.Controls.Add(_meter);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = AppText.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            Margin = new Padding(0, 20, 0, 14),
            WrapContents = false
        };
        _refreshButton = NewButton(AppText.IsArabic ? "تحديث الآن" : "Refresh now", Color.FromArgb(37, 172, 94));
        _refreshButton.Click += async (_, _) => await RefreshFromPanelAsync();
        var vendorButton = NewButton(AppText.IsArabic ? "فتح برنامج Redragon" : "Open Redragon software", Color.FromArgb(66, 75, 87));
        vendorButton.Click += (_, _) => openRedragon();
        actions.Controls.Add(_refreshButton);
        actions.Controls.Add(vendorButton);

        _autostart = new CheckBox
        {
            Text = AppText.IsArabic ? "تشغيل تلقائي مع Windows" : "Start automatically with Windows",
            AutoSize = true,
            Checked = getAutostart(),
            ForeColor = Color.FromArgb(226, 232, 240),
            BackColor = WindowColor,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 0),
            RightToLeft = RightToLeft
        };
        _autostart.CheckedChanged += (_, _) =>
        {
            if (!_closing)
                setAutostart(_autostart.Checked);
        };

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(_headline, 0, 1);
        root.Controls.Add(card, 0, 2);
        root.Controls.Add(actions, 0, 3);
        root.Controls.Add(_autostart, 0, 4);
        Controls.Add(root);
    }

    internal void UpdateReading(int? percent, bool refreshing)
    {
        if (IsDisposed)
            return;

        bool arabic = AppText.IsArabic;
        _meter.Percent = percent;
        _meter.Refresh();
        _refreshButton.Enabled = !refreshing;
        _refreshButton.Text = refreshing
            ? (arabic ? "جارٍ التحديث…" : "Refreshing…")
            : (arabic ? "تحديث الآن" : "Refresh now");

        if (percent is null)
        {
            _headline.Text = arabic ? "لم تتوفر قراءة الآن" : "No battery reading available";
            _headline.ForeColor = MutedText;
            _detail.Text = arabic ? "شغّل الفأرة أو افحص اتصالها ثم حدّث القراءة." : "Turn on the mouse or check its connection, then refresh.";
            _lastChecked.Text = string.Empty;
            return;
        }

        (string label, Color color) = percent.Value switch
        {
            <= 20 => (arabic ? "البطارية حرجة — اشحن الفأرة الآن" : "Battery critical — charge the mouse now", Color.FromArgb(255, 76, 76)),
            < 50 => (arabic ? "البطارية منخفضة" : "Battery low", Color.FromArgb(255, 184, 43)),
            _ => (arabic ? "البطارية بحالة جيدة" : "Battery in good condition", Color.FromArgb(59, 214, 113))
        };
        _headline.Text = label;
        _headline.ForeColor = color;
        _detail.Text = arabic
            ? "النسبة المعروضة مستقرة لتجنب القفزات اللحظية."
            : "The displayed value is stabilized to avoid momentary jumps.";
        _lastChecked.Text = arabic
            ? $"آخر تحديث: {DateTime.Now:t}"
            : $"Last updated: {DateTime.Now:t}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        base.OnFormClosing(e);
    }

    private async Task RefreshFromPanelAsync()
    {
        UpdateReading(_getPercent(), true);
        await _refresh();
        UpdateReading(_getPercent(), _isRefreshing());
    }

    private static Label NewLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = color,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, style, GraphicsUnit.Point),
        TextAlign = ContentAlignment.MiddleLeft,
        RightToLeft = AppText.IsArabic ? RightToLeft.Yes : RightToLeft.No
    };

    private static Button NewButton(string text, Color color) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(172, 42),
        Margin = new Padding(0, 0, 10, 0),
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = ControlPaint.Light(color, 0.08f), MouseDownBackColor = ControlPaint.Dark(color, 0.10f) },
        BackColor = color,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
        Cursor = Cursors.Hand
    };

    private static void DrawCardBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
            return;
        using var pen = new Pen(Color.FromArgb(65, 75, 88), 1f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
    }
}

internal sealed class DashboardBatteryMeter : Control
{
    private int? _percent;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int? Percent
    {
        get => _percent;
        set => _percent = value;
    }

    public DashboardBatteryMeter()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int bodyWidth = Math.Max(170, Width - 56);
        var body = new Rectangle(0, 24, bodyWidth, 64);
        var cap = new Rectangle(body.Right + 3, 43, 12, 26);
        Color accent = _percent switch
        {
            null => Color.FromArgb(154, 164, 178),
            <= 20 => Color.FromArgb(255, 76, 76),
            < 50 => Color.FromArgb(255, 184, 43),
            _ => Color.FromArgb(59, 214, 113)
        };

        using var outline = new Pen(accent, 3f);
        using var capBrush = new SolidBrush(accent);
        e.Graphics.DrawRectangle(outline, body);
        e.Graphics.FillRectangle(capBrush, cap);

        string text = _percent is int value ? $"{value}%" : "—";
        using var font = new Font("Segoe UI", 32f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(244, 247, 250));
        SizeF textSize = e.Graphics.MeasureString(text, font);
        e.Graphics.DrawString(text, font, brush,
            body.Left + ((body.Width - textSize.Width) / 2f),
            body.Top + ((body.Height - textSize.Height) / 2f) - 2f);
    }
}
