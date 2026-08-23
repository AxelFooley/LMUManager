using System.IO;
using Microsoft.Win32;

namespace LMUManager;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ManagerValueName = "LMUManager";

    public static string StartupFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    public static bool IsAppInStartup(string exePath)
        => IsAppInStartup(exePath, RunKeyPath, StartupFolder);

    /// <summary>Number of startup entries removed (Run values + Startup folder links).</summary>
    public static int RemoveAppFromStartup(string exePath)
        => RemoveAppFromStartup(exePath, RunKeyPath, StartupFolder);

    public static bool IsManagerInStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ManagerValueName) is string;
    }

    public static void SetManagerInStartup(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(ManagerValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ManagerValueName, throwOnMissingValue: false);
        }
    }

    internal static bool IsAppInStartup(string exePath, string runKeyPath, string startupFolder)
    {
        var fileName = Path.GetFileName(exePath);
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
        if (key != null)
        {
            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is string data && CommandReferencesExe(data, fileName))
                    return true;
            }
        }

        if (Directory.Exists(startupFolder))
        {
            var stem = Path.GetFileNameWithoutExtension(exePath);
            foreach (var link in Directory.GetFiles(startupFolder, "*.lnk"))
            {
                if (Path.GetFileNameWithoutExtension(link).Contains(stem, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    internal static int RemoveAppFromStartup(string exePath, string runKeyPath, string startupFolder)
    {
        int removed = 0;
        var fileName = Path.GetFileName(exePath);
        using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true))
        {
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    if (key.GetValue(valueName) is string data && CommandReferencesExe(data, fileName))
                    {
                        key.DeleteValue(valueName, throwOnMissingValue: false);
                        removed++;
                    }
                }
            }
        }

        if (Directory.Exists(startupFolder))
        {
            var stem = Path.GetFileNameWithoutExtension(exePath);
            foreach (var link in Directory.GetFiles(startupFolder, "*.lnk"))
            {
                if (Path.GetFileNameWithoutExtension(link).Contains(stem, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(link); removed++; }
                    catch (IOException) { }
                }
            }
        }

        return removed;
    }

    /// <summary>Matches the exe file name only when it stands alone (path segment), so "pad.exe" never matches "tinypedal.exe".</summary>
    internal static bool CommandReferencesExe(string data, string exeFileName)
    {
        int index = data.IndexOf(exeFileName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;
        if (index == 0)
            return true;

        return data[index - 1] is '"' or '\\' or '/' or ' ' or '=';
    }
}
