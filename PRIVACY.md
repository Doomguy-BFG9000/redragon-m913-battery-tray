# Privacy

Redragon M913 Battery Tray does not contain networking code and does not send analytics, telemetry, advertising identifiers, battery readings, or device information over the internet.

It reads locally enumerated HID device interfaces whose identifiers match the documented compatibility list. It stores local status and recovery files under `%LOCALAPPDATA%\RedragonBatteryTray`:

- `status.txt`: most recent battery samples, product ID, status byte, and HID reply bytes.
- `errors.log`: local exception details when an operation fails.
- `lifecycle.log` and `lifecycle.log.old`: startup, stop, tray restoration and recovery events; each lifecycle file is limited to approximately 1 MiB.
- `instance.txt`: current process ID and a random local stop-event token.
- `recovery-history.txt`: recent unexpected-exit timestamps used to prevent a restart loop.

A small second process watches the tray process and restarts it after an unexpected exit. It exits on deliberate Exit, update/uninstall, or Windows session ending. No remote monitoring is used.

The app can open the official Windows taskbar Settings page so the user can choose to keep the battery icon visible. It does not edit Windows notification-area preferences itself. A small local marker records that the one-time visibility explanation has been shown.

These files stay on the computer. The standard uninstall removes them; `Uninstall.ps1 -KeepLogs` preserves them for troubleshooting.
