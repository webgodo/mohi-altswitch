using System.Text;
using Microsoft.Win32;

namespace MohiAltSwitch;

/// <summary>The few user-adjustable options, stored as plain key=value lines in the user's local app data.</summary>
internal sealed class Settings
{
    public bool ShowPreview { get; set; } = true;
    public int PreviewDelayMs { get; set; } = 200;

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MohiAltSwitch", "settings.ini");

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line[..eq].Trim(), value = line[(eq + 1)..].Trim();
                if (key == "ShowPreview" && bool.TryParse(value, out bool show)) s.ShowPreview = show;
                if (key == "PreviewDelayMs" && int.TryParse(value, out int ms)) s.PreviewDelayMs = Math.Clamp(ms, 0, 2000);
            }
        }
        catch
        {
            // first run or unreadable file: defaults
        }
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, $"ShowPreview={ShowPreview}\nPreviewDelayMs={PreviewDelayMs}\n", Encoding.UTF8);
        }
        catch
        {
            // a settings file we can't write must never take the app down
        }
    }
}

/// <summary>Start-with-Windows for the unpackaged build (the Store package declares a startup task instead).</summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MohiAltSwitch";

    /// <summary>True when running from an MSIX package (the Store build).</summary>
    public static bool IsPackaged
    {
        get
        {
            uint length = 0;
            return Native.GetCurrentPackageFullName(ref length, null) != Native.APPMODEL_ERROR_NO_PACKAGE;
        }
    }

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(ValueName, $"\"{exe}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
