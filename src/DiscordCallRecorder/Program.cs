using System.Text.Json;
using NAudio.CoreAudioApi;

namespace DiscordCallRecorder;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--diagnose")
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = new List<object>();
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                using (device) devices.Add(new { device.ID, device.FriendlyName });
                File.WriteAllText(args[1], JsonSerializer.Serialize(new { Windows = Environment.OSVersion.VersionString, Devices = devices, ActiveDiscord = CallDetector.Find() }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { File.WriteAllText(args[1], e.ToString()); Environment.ExitCode = 1; }
            return;
        }
        ApplicationConfiguration.Initialize();
        var settings = Settings.Load();
        Localization.Language = settings.Language;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            MessageBox.Show(Localization.Text("A captura isolada do Discord requer Windows build 20348 ou superior (Windows 11 recomendado)."), "Discord Recorder"); return;
        }
        using var mutex = new Mutex(true, @"Local\DiscordCallRecorder", out var first);
        if (!first) return;
        Application.ThreadException += (_, e) => { Log.Write(e.Exception); MessageBox.Show(e.Exception.Message, "Discord Recorder"); };
        if (string.IsNullOrWhiteSpace(settings.Folder))
        {
            using var setup = new SetupForm();
            if (setup.ShowDialog() != DialogResult.OK) return;
            settings.Folder = setup.SelectedFolder;
            settings.Language = setup.SelectedLanguage;
            try { settings.Save(); Settings.SetStartup(true); }
            catch (Exception e) { MessageBox.Show(Localization.Text("Não foi possível salvar a configuração: ") + e.Message); return; }
        }
        Application.Run(new TrayContext(settings));
    }
}
