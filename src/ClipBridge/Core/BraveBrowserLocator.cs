using System.IO;
using Microsoft.Win32;

namespace ClipBridge.Core;

/// <summary>
/// Finds the installed stable Brave browser rather than switching to Edge or
/// controlling the user's existing Brave profile.
/// </summary>
public static class BraveBrowserLocator
{
    public static string FindRequired()
    {
        string? custom = Environment.GetEnvironmentVariable("CLIPBRIDGE_BRAVE_PATH");
        if (IsBraveExecutable(custom)) return Path.GetFullPath(custom!);

        string?[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        ];

        foreach (string? root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            string path = Path.Combine(root, "BraveSoftware", "Brave-Browser",
                "Application", "brave.exe");
            if (IsBraveExecutable(path)) return path;
        }

        string[] appPaths =
        [
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\brave.exe",
            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\brave.exe"
        ];
        foreach (string key in appPaths)
        {
            try
            {
                string? registered = Registry.GetValue(key, "", null) as string;
                if (IsBraveExecutable(registered)) return Path.GetFullPath(registered!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Some installations do not grant registry access.
            }
        }

        throw new FileNotFoundException(
            "Brave Browser was not found on this PC. Install Brave from https://brave.com/download/ " +
            "and restart ClipBridge. If Brave is installed in a custom folder, set " +
            "CLIPBRIDGE_BRAVE_PATH to the full path of brave.exe. " +
            "ClipBridge will not silently open Edge instead.");
    }

    private static bool IsBraveExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            return Path.IsPathFullyQualified(path) &&
                   string.Equals(Path.GetFileName(path), "brave.exe",
                       StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(path);
        }
        catch (ArgumentException) { return false; }
    }
}
