# Privacy

Redragon M913 Battery Tray does not contain networking code and does not send analytics, telemetry, advertising identifiers, battery readings, or device information over the internet.

It reads locally enumerated HID device interfaces whose identifiers match the documented compatibility list. It stores two optional diagnostic files under `%LOCALAPPDATA%\RedragonBatteryTray`:

- `status.txt`: most recent battery samples, product ID, status byte, and HID reply bytes.
- `errors.log`: local exception details only when a read fails.

These files stay on the computer. The standard uninstall removes them; `Uninstall.ps1 -KeepLogs` preserves them for troubleshooting.
