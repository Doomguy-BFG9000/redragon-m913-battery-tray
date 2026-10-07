using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Principal;

namespace RedragonBatteryTray;

internal static class IndependentProcess
{
    internal static bool IsInJob()
    {
        using Process current = Process.GetCurrentProcess();
        return IsProcessInJob(current.Handle, IntPtr.Zero, out bool inJob) && inJob;
    }

    internal static bool TryRelaunch(string[] args, bool alreadyAttempted)
    {
        bool packaged = HasPackageIdentity();
        if (!IsInJob() && !packaged)
            return false;
        uint? limits = CurrentJobLimits();
        if (alreadyAttempted)
        {
            if (packaged || limits is null || (limits & 0x2000) != 0)
                AppLog.Event("Launch still has host package identity or kill-on-close job limits; independent launch was not guaranteed.");
            return false;
        }

        string? executable = Environment.ProcessPath;
        if (executable is null)
            return false;
        var command = new StringBuilder(Quote(executable));
        foreach (string arg in args.Append("--independent"))
            command.Append(' ').Append(Quote(arg));
        // An outer job may forbid breakaway. Use this user's desktop shell as
        // the parent instead, so the app belongs to the desktop's lifetime.
        var desktopCommand = new StringBuilder(command.ToString()).Append(" --desktop-bootstrap");
        if (TryDesktopLaunch(executable, desktopCommand, out ProcessInfo desktopInfo))
        {
            CloseHandle(desktopInfo.Thread);
            CloseHandle(desktopInfo.Process);
            AppLog.Event($"Desktop-independent launch created PID={desktopInfo.ProcessId}; launcher exits.");
            return true;
        }
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                0x01000000, IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out ProcessInfo info))
        {
            AppLog.Event($"Independent launch failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            return false;
        }

        CloseHandle(info.Thread);
        CloseHandle(info.Process);
        AppLog.Event($"Independent launch created PID={info.ProcessId}; launcher exits.");
        return true;
    }

    private static bool TryDesktopLaunch(string executable, StringBuilder command, out ProcessInfo info)
    {
        info = default;
        IntPtr shell = GetShellWindow();
        if (shell == IntPtr.Zero) return false;
        GetWindowThreadProcessId(shell, out uint id);
        IntPtr parent = OpenProcess(0x0080 | 0x1000, false, id);
        if (parent == IntPtr.Zero) return false;
        IntPtr attributes = IntPtr.Zero, value = IntPtr.Zero, token = IntPtr.Zero, policy = IntPtr.Zero;
        bool initialized = false;
        try
        {
            using Process desktop = Process.GetProcessById((int)id);
            using Process current = Process.GetCurrentProcess();
            if (desktop.SessionId != current.SessionId ||
                !string.Equals(desktop.MainModule?.FileName,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    StringComparison.OrdinalIgnoreCase) || !OpenProcessToken(parent, 0x0008, out token))
                return false;
            using var desktopIdentity = new WindowsIdentity(token);
            using WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
            if (desktopIdentity.User != currentIdentity.User) return false;
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            attributes = Marshal.AllocHGlobal(checked((int)size));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size)) return false;
            initialized = true;
            value = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(value, parent);
            if (!UpdateProcThreadAttribute(attributes, 0, 0x00020000, value, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                return false;
            policy = Marshal.AllocHGlobal(sizeof(uint));
            Marshal.WriteInt32(policy, 1); // DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE
            if (!UpdateProcThreadAttribute(attributes, 0, 0x00020012, policy, sizeof(uint), IntPtr.Zero, IntPtr.Zero))
                return false;
            var startup = new ExtendedStartupInfo
            {
                Startup = new StartupInfo { Size = Marshal.SizeOf<ExtendedStartupInfo>() },
                Attributes = attributes
            };
            bool created = CreateDesktopProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                0x00080000, IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out info);
            AppLog.Event(created ? $"Launch uses same-user desktop shell PID={id}." :
                $"Desktop-independent launch failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            return created;
        }
        catch (Exception exception) { AppLog.Event($"Desktop launch unavailable: {exception.Message}"); return false; }
        finally
        {
            if (initialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (value != IntPtr.Zero) Marshal.FreeHGlobal(value);
            if (policy != IntPtr.Zero) Marshal.FreeHGlobal(policy);
            if (token != IntPtr.Zero) CloseHandle(token);
            CloseHandle(parent);
        }
    }

    internal static uint? CurrentJobLimits()
    {
        return QueryInformationJobObject(IntPtr.Zero, 2, out BasicLimits limits,
            (uint)Marshal.SizeOf<BasicLimits>(), IntPtr.Zero) ? limits.Flags : null;
    }

    internal static bool HasPackageIdentity()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != 15700; // APPMODEL_ERROR_NO_PACKAGE
    }

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? (slashes * 2) + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size;
        internal string? Reserved, Desktop, Title;
        internal uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        internal ushort ShowWindow, ReservedSize;
        internal IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        internal IntPtr Process, Thread;
        internal uint ProcessId, ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedStartupInfo { internal StartupInfo Startup; internal IntPtr Attributes; }

    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint id);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute,
        IntPtr value, nuint size, IntPtr previousValue, IntPtr returnedSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateDesktopProcess(string application, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint flags, IntPtr environment, string? directory, ref ExtendedStartupInfo startup, out ProcessInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        internal long ProcessTime, JobTime;
        internal uint Flags;
        internal UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint Priority, Scheduling;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(IntPtr job, int informationClass,
        out BasicLimits limits, uint length, IntPtr returnedLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint length, IntPtr name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool inJob);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint flags, IntPtr environment, string? directory, ref StartupInfo startup, out ProcessInfo info);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
