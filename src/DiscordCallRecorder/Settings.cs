using System.Text.Json;
using Microsoft.Win32;

namespace DiscordCallRecorder;

internal sealed class Settings
{
    public string Folder { get; set; } = "";
    public string? MicrophoneId { get; set; }
    public bool MicrophoneVoiceProcessing { get; set; } = true;
    public float MicrophoneGain { get; set; } = 1.25f;
    public bool Notifications { get; set; } = true;
    public bool Paused { get; set; }
    private string language = Localization.DefaultLanguage;
    public string Language { get => language; set => language = Localization.Normalize(value); }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiscordCallRecorder");
    private static string FilePath => Path.Combine(DataDirectory, "settings.json");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (Exception e) { Log.Write(e); return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    public static bool StartupEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("DiscordCallRecorder") != null; }
    }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("DiscordCallRecorder", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("DiscordCallRecorder", false);
    }
}

internal static class Log
{
    private static readonly object Gate = new();
    public static void Write(object message)
    {
        lock (Gate) try
        {
            Directory.CreateDirectory(Settings.DataDirectory);
            var path = Path.Combine(Settings.DataDirectory, "recorder.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { /* Logging must never prevent finalizing a recording. */ }
    }
}
