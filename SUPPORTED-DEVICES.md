# Supported devices

| USB identity | Connection | Support level | Evidence |
| --- | --- | --- | --- |
| `VID_25A7 / PID_FA07` | M913 wireless receiver | Verified | Repeated battery reads and coexistence with the original Redragon software on Windows 11 build 26200. |
| `VID_25A7 / PID_FA08` | Alternate M913 identity | Experimental | Detection and protocol path are implemented, but no active matching device was available for hardware validation. |

The verified FA07 device uses `MI_01&COL07` for the feature command and `MI_01&COL05` for the reply. A valid reply is 17 bytes, uses report IDs `09 04`, and has an 8-bit sum of `0x55`. The battery level observed at byte index 6 is expressed in 10% steps.

No claim is made for other product IDs, cloned devices, firmware revisions, macOS, Linux, 32-bit Windows, or Windows on ARM. Reports from those environments should be treated as new compatibility work, not assumed support.
