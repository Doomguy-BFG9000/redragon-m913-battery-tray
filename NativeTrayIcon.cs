using System.Runtime.InteropServices;

namespace RedragonBatteryTray;

internal sealed class NativeTrayIcon : IDisposable
{
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetFocus = 0x00000003;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifInfo = 0x00000010;
    private const uint NifGuid = 0x00000020;
    private const uint NifRealtime = 0x00000040;
    private const uint NifShowTip = 0x00000080;
    private const uint NotifyIconVersion4 = 4;
    private const int WmApp = 0x8000;
    private const int CallbackMessage = WmApp + 71;
    private const int WmContextMenu = 0x007B;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDoubleClick = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int NinSelect = 0x0400;
    private const int NinKeySelect = 0x0401;
    private static readonly Guid IconGuid = new("564AA102-122F-4C9F-9100-3035E07857B7");

    private readonly TrayMessageWindow _window;
    private readonly ContextMenuStrip _menu;
    private readonly Action _leftClick;
    private readonly Action _doubleClick;
    private Icon _icon;
    private string _text;
    private bool _added;
    private bool _disposed;

    internal NativeTrayIcon(
        Icon icon,
        string text,
        ContextMenuStrip menu,
        Action leftClick,
        Action doubleClick)
    {
        _icon = icon;
        _text = text;
        _menu = menu;
        _leftClick = leftClick;
        _doubleClick = doubleClick;
        _window = new TrayMessageWindow(this);
        Add();
    }

    internal void Update(Icon icon, string text)
    {
        if (_disposed)
        {
            icon.Dispose();
            return;
        }

        Icon previous = _icon;
        _icon = icon;
        _text = text;
        EnsurePresent();
        previous.Dispose();
    }

    internal void EnsurePresent()
    {
        if (_disposed) return;
        var data = CreateData(NifIcon | NifTip | NifGuid | NifShowTip);
        if (Shell_NotifyIcon(NimModify, ref data)) return;
        _added = false;
        Add();
        AppLog.Event(_added ? "Tray icon restored after lost registration." : "Tray registration failed; retrying on next health check.");
    }

    internal void ShowBalloon(string title, string message, ToolTipIcon icon)
    {
        if (_disposed)
            return;

        var data = CreateData(NifInfo | NifGuid | NifRealtime);
        data.InfoTitle = title;
        data.Info = message;
        data.InfoFlags = icon switch
        {
            ToolTipIcon.Warning => 0x00000002,
            ToolTipIcon.Error => 0x00000003,
            ToolTipIcon.Info => 0x00000001,
            _ => 0x00000000
        };
        Shell_NotifyIcon(NimModify, ref data);
    }

    private void Add()
    {
        var data = CreateData(NifMessage | NifIcon | NifTip | NifGuid | NifShowTip);
        _added = Shell_NotifyIcon(NimAdd, ref data);
        if (!_added)
            _added = Shell_NotifyIcon(NimModify, ref data);
        if (_added)
        {
            data.VersionOrTimeout = NotifyIconVersion4;
            Shell_NotifyIcon(NimSetVersion, ref data);
        }
    }

    private NotifyIconData CreateData(uint flags) => new()
    {
        Size = Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _window.Handle,
        Id = 1,
        Flags = flags,
        CallbackMessage = CallbackMessage,
        IconHandle = _icon.Handle,
        Tip = Truncate(_text, 127),
        Info = string.Empty,
        InfoTitle = string.Empty,
        IconGuid = IconGuid
    };

    private void HandleMessage(int eventMessage)
    {
        switch (eventMessage)
        {
            case WmContextMenu:
            case WmRButtonUp:
                _menu.Show(Cursor.Position);
                _menu.Closed += ReturnFocusAfterMenu;
                break;
            case WmLButtonDoubleClick:
                _doubleClick();
                break;
            case WmLButtonUp:
            case NinSelect:
            case NinKeySelect:
                _leftClick();
                break;
        }
    }

    private void ReturnFocusAfterMenu(object? sender, ToolStripDropDownClosedEventArgs eventArgs)
    {
        _menu.Closed -= ReturnFocusAfterMenu;
        var data = CreateData(NifGuid);
        Shell_NotifyIcon(NimSetFocus, ref data);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_added)
        {
            var data = CreateData(NifGuid);
            Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
        }

        _window.DestroyHandle();
        _icon.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint VersionOrTimeout;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid IconGuid;
        public IntPtr BalloonIconHandle;
    }

    private sealed class TrayMessageWindow : NativeWindow
    {
        private readonly NativeTrayIcon _owner;
        private readonly int _taskbarCreatedMessage;

        internal TrayMessageWindow(NativeTrayIcon owner)
        {
            _owner = owner;
            _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
            CreateHandle(new CreateParams { Caption = "Redragon M913 Battery Tray Messages" });
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == CallbackMessage)
                _owner.HandleMessage(unchecked((ushort)message.LParam.ToInt64()));
            else if (message.Msg == _taskbarCreatedMessage)
                _owner.Add();

            base.WndProc(ref message);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);
}
