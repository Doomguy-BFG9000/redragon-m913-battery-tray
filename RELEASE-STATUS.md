# Release status: 1.5.2 public pilot

This package is suitable for public pilot testing within the verified scope below. It is not described as universally compatible because only one physical M913 hardware identity and one Windows installation were available for direct validation.

## Verified on physical hardware

- Windows 11 x64 build 26200.
- Redragon M913 wireless receiver `VID_25A7 / PID_FA07`.
- Five consecutive samples per reading and automatic one-minute refresh.
- Coexistence with the original Redragon application; the vendor application may be closed.
- Large tray number rendered with Segoe UI Variable Text Regular to match Windows 11 system typography, persistent notification-area identity, Arabic interface on the test machine, and saved Arabic/English selection.
- Self-contained launch with .NET discovery pointed at a nonexistent path.
- Built with .NET SDK 10.0.401 and bundled runtime 10.0.12 from the verified official SDK archive.
- Fresh install, in-place upgrade while running, registered uninstall, removal from the installed location, autostart registration, and single-instance enforcement.
- Strict build with zero compiler/analyzer warnings, package checksums, and local Microsoft Defender scan when recorded in the external verification report.

## Implemented but not physically verified

- `VID_25A7 / PID_FA08` alternate device identity.
- The flashing critical warning at 20% or lower; both visual frames and timer logic are implemented, but the physical battery was not at a critical level during testing.
- Windows 10 Enterprise/LTSC and other Windows 11 builds.
- Other M913 firmware or hardware revisions.

## Known publication limitation

The 1.5.2 binaries are intentionally published as an unsigned open-source community release. Windows can therefore show an unknown-publisher or SmartScreen reputation warning. Release archives include SHA-256 checksums so downloads can be checked against the GitHub release.
