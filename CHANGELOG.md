# Changelog

## 1.5.1 - 2026-09-12

- Reduced the tray percentage's font weight and shadow outline while retaining the large high-contrast number.
- Prepared the project for an unsigned public GitHub release with published SHA-256 checksums.

## 1.5.0 - 2026-09-12

- Added a self-contained `win-x64` release that does not require a separate .NET installation.
- Pinned release builds to .NET SDK 10.0.401 and runtime 10.0.12, the current September 2026 security release.
- Added automatic Arabic/English UI selection.
- Added a saved manual Arabic/English language selector.
- Added per-user install/uninstall registration and double-click command wrappers.
- Added app metadata, icon, license, privacy, security, compatibility, and integrity files.
- Ensured pending HID reads complete cancellation before native buffers are released.
- Preserved the large high-contrast number and color/critical-alert behavior from 1.4.0.

## 1.4.0 - 2026-09-12

- Enlarged the tray number to fill most of the available icon area.

## 1.3.0 - 2026-09-12

- Replaced the solid fill with a colored outline.
- Added amber low-battery state and flashing red critical state.

## 1.2.0 - 2026-09-12

- Added five-sample smoothing and two-refresh change confirmation.
- Added a stable notification-area GUID so Windows can retain icon visibility.
