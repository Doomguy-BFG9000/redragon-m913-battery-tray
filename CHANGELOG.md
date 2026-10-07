# Changelog

## 1.7.1 - 2026-10-07

- Added a one-time, bilingual setup prompt and a permanent tray-menu action that open the official Windows taskbar page for keeping the battery percentage visible beside the clock.
- Retained the app's fixed icon GUID so Windows preserves the user's visibility choice across app updates.
- Avoided unsupported registry manipulation: Microsoft documents that only the user can promote an icon from the overflow area.

## 1.7.0 - 2026-10-07

- Added a polished control panel with a large, high-contrast battery reading, clear health state, last-update time, manual refresh, Windows startup control, and a direct link to the Redragon software.
- Double-clicking the tray icon now opens the control panel; the original right-click tray menu remains available for quick actions.
- Kept the release self-contained and offline: the new interface uses the built-in Windows Forms stack with no network service or external telemetry.

## 1.6.2 - 2026-10-03

- Added a current-user Startup-folder shortcut alongside the existing Run registry entry.
- The installer verifies the shortcut exists; the app's startup toggle creates or removes both startup paths.
- Replaced a blocked Scheduled Tasks fallback with the per-user shortcut, which needs no elevated permission.

## 1.6.0 - 2026-09-17

- Launch through the same-user desktop shell outside the host's packaged runtime and kill-on-close jobs; use native job breakaway as a fallback.
- Added a small recovery guard for unexpected exits, with a five-restarts-per-ten-minutes limit.
- Deliberate Exit, language changes, updates/uninstall and Windows session ending suppress recovery.
- Check tray registration every ten seconds and re-add it if Windows loses it.
- Added bounded local lifecycle logging and graceful shutdown for updates/uninstall.

## 1.5.3 - 2026-09-12

- Preserved the typeface proportions instead of stretching digits independently on each axis.
- Added clear internal padding around the percentage and made the battery frame slightly taller.
- Removed the artificial dark glyph stroke so the number follows the system typeface more faithfully.

## 1.5.2 - 2026-09-12

- Matched the tray percentage typeface to the Windows 11 system typography by using Segoe UI Variable Text Regular.

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
