# Release status: 1.7.0 pilot

This package is suitable for public pilot testing within the verified scope below. Version 1.7.0 adds a local control panel; it is not described as universally compatible because only one physical M913 hardware identity and one Windows installation were available for direct validation.

## Verified on physical hardware

- Windows 11 x64 build 26200.
- Redragon M913 wireless receiver `VID_25A7 / PID_FA07`.
- Five consecutive samples per reading and automatic one-minute refresh.
- Coexistence with the original Redragon application; the vendor application may be closed.
- Tray number rendered with Segoe UI Variable Text Regular without non-uniform stretching, clear padding inside a slightly taller frame, persistent notification-area identity, Arabic interface on the test machine, and saved Arabic/English selection.
- Self-contained launch with .NET discovery pointed at a nonexistent path.
- Built with .NET SDK 10.0.401 and bundled runtime 10.0.12 from the verified official SDK archive.
- In-place upgrade while running, registered uninstall entry, autostart registration, and single-instance enforcement.
- Private kill-on-close job reproduction: 1.5.3 terminated; 1.6.0 escaped the test job and survived its closure. Final main/guard ran with desktop job limits `0x1800` (no kill-on-close), not the host's `0x2800`/`0x2000` limits.
- Recovery after forced main termination, replacement of a terminated guard, and re-registration after explicit icon deletion without restarting Explorer.
- Graceful stop leaves zero main/guard processes and does not restart during a five-second observation.
- After a verified Windows update restart left the Run entry present but the app absent, 1.6.2 added and verified a second per-user Startup-folder path. The shortcut stopped the app cleanly and then relaunched one main plus one guard with a registered tray icon.
- Source ZIP rebuild produces an identical executable SHA-256 hash.
- Strict build with zero compiler/analyzer warnings, package checksums, and local Microsoft Defender scan when recorded in the external verification report.

## Implemented but not physically verified

- `VID_25A7 / PID_FA08` alternate device identity.
- The flashing critical warning at 20% or lower; both visual frames and timer logic are implemented, but the physical battery was not at a critical level during testing.
- Windows 10 Enterprise/LTSC and other Windows 11 builds.
- Other M913 firmware or hardware revisions.
- Actual Windows reboot/logoff behavior, uninstall behavior for this new guarded version, and recovery under every possible third-party process policy.

## Known publication limitation

The 1.7.0 binaries are intentionally unsigned. Windows can therefore show an unknown-publisher or SmartScreen reputation warning. Release archives include SHA-256 checksums. Packaging locally does not itself publish a GitHub release.

Recovery is bounded to five restarts in ten minutes. Killing both processes together, preventing process creation, or repeatedly crashing the application can require a manual start. The guard is not a Windows service and does not bypass deliberate Exit or session ending. The Startup shortcut was launched manually in testing; another physical reboot was not performed.
