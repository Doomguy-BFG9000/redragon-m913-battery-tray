# Redragon M913 Battery Tray

An independent Windows utility that reads a compatible Redragon M913 mouse over HID and shows its battery percentage in the notification area.

[اقرأ بالعربية](README-ar.md)

## Download

Download the current tested package from [GitHub Releases](https://github.com/abhhjhh28-svg/redragon-m913-battery-tray/releases). Extract the ZIP, then run `Install.cmd`.

## Features

- Large, high-contrast battery number in the system tray.
- Green outline at 50–100%, amber at 21–49%, and a flashing red warning at 0–20%.
- Five-sample median plus two-refresh confirmation to reduce unstable readings.
- Refreshes every minute and starts with Windows.
- Arabic interface on Arabic Windows; English on other system languages, with a saved manual language selector.
- No network access, analytics, advertisements, or vendor software dependency.

## Requirements

- 64-bit Windows 11, or a currently supported 64-bit Windows 10 Enterprise/LTSC release (version 1809 or newer).
- A compatible Redragon M913 receiver/device. See [SUPPORTED-DEVICES.md](SUPPORTED-DEVICES.md).
- No separate .NET installation is required by the `win-x64` release.

## Install

1. Extract the entire ZIP archive.
2. Double-click `Install.cmd`.
3. The battery icon appears in the notification area. Windows may initially place a new icon in the overflow menu; use Taskbar settings to keep it visible.

The installer is per-user, requires no administrator rights, installs under `%LOCALAPPDATA%\Programs\RedragonM913BatteryTray`, enables startup, and registers an uninstall entry in Windows Settings.

## Use

- Left-click: show the current percentage in a notification.
- Double-click: open the original Redragon software if it is installed in its default location.
- Right-click: refresh, toggle startup, switch Arabic/English, open Redragon, view About, or exit.

The original Redragon program does not need to remain open. If the mouse is sleeping, the reading can temporarily become unavailable and returns after the mouse wakes.

## Uninstall

Use **Settings > Apps > Installed apps > Redragon M913 Battery Tray**, or run `Uninstall.cmd` from the extracted package. Uninstall removes the installed app and its local diagnostic files by default.

## Diagnostics and privacy

Local status and error files are stored in `%LOCALAPPDATA%\RedragonBatteryTray`. The app does not make network requests. See [PRIVACY.md](PRIVACY.md).

## Integrity and signing

Release archives include `SHA256SUMS.txt` and a separate ZIP checksum. Version 1.5.3 is an unsigned open-source community release, so Windows can display an unknown-publisher or SmartScreen reputation warning. Verify the published SHA-256 checksum if Windows shows a warning.

This project is unofficial and is not affiliated with or endorsed by Redragon. See [NOTICE.md](NOTICE.md).
