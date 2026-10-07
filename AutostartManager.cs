using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RedragonBatteryTray;

internal static class AutostartManager
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string RunValueName = "RedragonBatteryTray";
    private const string ShortcutName = "Redragon M913 Battery Tray.lnk";

    internal static bool IsEnabled(string? executable)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return executable is not null && key?.GetValue(RunValueName) is string value &&
               value.Contains(executable, StringComparison.OrdinalIgnoreCase);
    }

    internal static void SetEnabled(bool enabled)
    {
        string? executable = Environment.ProcessPath;
        if (executable is null)
            return;
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (key is null)
            return;
        if (enabled)
            key.SetValue(RunValueName, $"\"{executable}\"");
        else
            key.DeleteValue(RunValueName, false);

        if (!SetStartupShortcut(executable, enabled))
            AppLog.Event($"Startup shortcut {(enabled ? "creation" : "removal")} failed; the Run entry was {(enabled ? "kept" : "removed")}.");
    }

    internal static bool SetStartupShortcut(string executable, bool enabled)
    {
        try
        {
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);
            if (!enabled)
            {
                if (File.Exists(shortcut)) File.Delete(shortcut);
                return true;
            }
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return false;
            object shell = Activator.CreateInstance(shellType)!;
            object? link = null;
            try
            {
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [shortcut]);
                Type linkType = link!.GetType();
                linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, [executable]);
                linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, [Path.GetDirectoryName(executable)!]);
                linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, ["Redragon M913 Battery Tray"]);
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                return File.Exists(shortcut);
            }
            finally
            {
                if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
                if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return false;
        }
    }
}
