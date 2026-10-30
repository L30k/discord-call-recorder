using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using DiscordCallRecorder;
using NAudio.CoreAudioApi;
using NAudio.Wave;

// Avoid Windows Error Reporting dialogs if an integration assertion/driver call fails.
SetErrorMode(0x0002);
try
{
if (args.Length > 0 && args[0] == "--mic-fixture")
{
    using var enumerator = new MMDeviceEnumerator();
    foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
    using (device)
    {
        Console.WriteLine(device.FriendlyName);
        using var client = device.CreateAudioClient();
        Console.WriteLine($"Native: {client.MixFormat} ({client.MixFormat.GetType()})");
        try { await using var capture = new WasapiRecorderBuilder().WithDevice(device).Build(); capture.StartRecording(); await Task.Delay(500); Console.WriteLine("OK native"); }
        catch (Exception e) { Console.WriteLine(e.Message); }
        try { await using var capture = new WasapiRecorderBuilder().WithDevice(device).WithLowLatency().Build(); capture.StartRecording(); await Task.Delay(500); Console.WriteLine("OK low latency"); }
        catch (Exception e) { Console.WriteLine(e.Message); }
    }
    return;
}
if (args.Length > 0 && args[0] == "--tone")
{
    using var memory = new MemoryStream();
    using (var wav = new WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(memory), new WaveFormat(48000, 16, 1)))
    {
        var data = new byte[96000];
        double frequency = double.Parse(args[1]);
        for (int i = 0; i < 48000; i++) BitConverter.TryWriteBytes(data.AsSpan(i * 2, 2), (short)(Math.Sin(2 * Math.PI * frequency * i / 48000) * 1500));
        wav.Write(data, 0, data.Length);
    }
    memory.Position = 0;
    using var player = new SoundPlayer(memory);
    player.PlayLooping(); await Task.Delay(15000); player.Stop(); return;
}

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var debounce = new Debouncer<string>();
Check(!debounce.Update("call", TimeSpan.Zero), "Entrada aguarda confirmação");
Check(!debounce.Update("call", TimeSpan.FromSeconds(1)), "Pulso curto não inicia");
Check(debounce.Update("call", TimeSpan.FromSeconds(2)) && debounce.Current == "call", "Entrada após 2 segundos");
Check(!debounce.Update(null, TimeSpan.FromSeconds(3)), "Saída aguarda confirmação");
Check(!debounce.Update("call", TimeSpan.FromSeconds(4)) && debounce.Current == "call", "Interrupção breve mantém chamada");
debounce.Update(null, TimeSpan.FromSeconds(5));
Check(debounce.Update(null, TimeSpan.FromSeconds(8)) && debounce.Current is null, "Saída após 3 segundos");
debounce.Reset();
Check(!debounce.Update("new", TimeSpan.FromSeconds(100)), "Retomar exige nova confirmação");
var buffer = new TimelineBuffer();
float[] stereo = [0.4f, 0.6f, -0.4f, -0.6f];
buffer.Add(MemoryMarshal.AsBytes(stereo.AsSpan()), 100, false);
float[] output = new float[104]; buffer.Mix(output, 0);
Check(output.Take(100).All(x => x == 0) && Math.Abs(output[100] - 0.35f) < 0.001 && output[102] == 0, "Silêncio mantém posição temporal");
buffer.Add(MemoryMarshal.AsBytes(stereo.AsSpan()), 200, true);
output = new float[2]; buffer.Mix(output, 200);
Check(output.All(x => x == 0), "Pacote marcado silencioso não reutiliza memória");
buffer.Add(MemoryMarshal.AsBytes(stereo.AsSpan()), 192100, false);
output = new float[2]; buffer.Mix(output, 100);
Check(output.All(x => x == 0), "Buffer circular não repete áudio antigo");
Check(TrayContext.FormatDuration(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(3)) == "25:03:00", "Duração suporta mais de 24 horas");
var gainBuffer = new TimelineBuffer();
gainBuffer.Add(MemoryMarshal.AsBytes(stereo.AsSpan()), 0, false);
var originalVolume = new float[2]; var boostedVolume = new float[2];
gainBuffer.Mix(originalVolume, 0); gainBuffer.Mix(boostedVolume, 0, 1.05f);
Check(Math.Abs(boostedVolume[0] - originalVolume[0] * 1.5f) < 0.00001f, "Ganho do microfone é aplicado independentemente");
var converter = new MicrophoneConverter(new WaveFormat(48000, 16, 1));
short[] pcm = [0, 16384, -16384, 32767, -32768];
var converted = MemoryMarshal.Cast<byte, float>(converter.Convert(MemoryMarshal.AsBytes(pcm.AsSpan()))).ToArray();
Check(converted.Length == 10 && converted[2] == 0.5f && converted[3] == 0.5f && converted[8] == -1f, "Conversão PCM preserva amplitude e sinal");
var oldSettings = JsonSerializer.Deserialize<Settings>("{\"Folder\":\"test\"}")!;
Check(oldSettings.MicrophoneVoiceProcessing && oldSettings.MicrophoneGain == 1.25f, "Configuração anterior recebe os ajustes novos");
Check(oldSettings.Language == Localization.DefaultLanguage, "Configuração anterior usa idioma do sistema");
var languageSettings = JsonSerializer.Deserialize<Settings>("{\"Language\":\"en\"}")!;
Check(languageSettings.Language == "en" && JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(languageSettings))!.Language == "en", "Idioma persiste na configuração");
languageSettings.Language = "unsupported";
Check(languageSettings.Language == "pt-BR", "Idioma inválido usa português");
Localization.Language = "en";
Check(Localization.Text("Pausar") == "Pause" && Localization.Text("Idioma") == "Language", "Menu em inglês");
Check(Localization.Format("Duração {0} • {1:F1} MB.", "00:01:00", 1.5) == "Duration 00:01:00 • 1.5 MB.", "Notificação em inglês formata números");
Localization.Language = "pt-BR";
Check(Localization.Text("Pausar") == "Pausar" && Localization.Format("Duração {0} • {1:F1} MB.", "00:01:00", 1.5).Contains("1,5"), "Retorno ao português");
foreach (var entry in Localization.English)
{
    Localization.Language = "en";
    Check(!string.IsNullOrWhiteSpace(Localization.Format(entry.Key, "value", 1.0)), "Tradução válida: " + entry.Key);
}
Localization.Language = "pt-BR";

if (args.Contains("--integration"))
{
    var directory = Path.GetFullPath(args.Last()); Directory.CreateDirectory(directory);
    using var target = StartTone("440"); using var other = StartTone("1000");
    try
    {
        await Task.Delay(1200);
        using var enumerator = new MMDeviceEnumerator();
        int micArgument = Array.IndexOf(args, "--microphone");
        using var mic = micArgument >= 0
            ? enumerator.GetDevice(args[micArgument + 1])
            : enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        Console.WriteLine($"Microfone em teste: {mic.FriendlyName}");
        await using (var capture = await new WasapiRecorderBuilder().WithProcessLoopback((uint)target.Id)
            .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)).BuildAsync())
        {
            var captured = new List<float>(48000 * 5);
            capture.DataAvailable += (data, flags, _, _) =>
            {
                if (((int)flags & 2) != 0) return;
                var floats = MemoryMarshal.Cast<byte, float>(data);
                lock (captured) for (int i = 0; i < floats.Length && captured.Count < 48000 * 5; i += 2) captured.Add(floats[i]);
            };
            capture.StartRecording(); await Task.Delay(3000); await capture.DisposeAsync();
            float[] wave; lock (captured) wave = captured.ToArray();
            double wanted = Magnitude(wave, 440), unwanted = Magnitude(wave, 1000);
            File.WriteAllText(Path.Combine(directory, "isolation.json"), JsonSerializer.Serialize(new { Samples = wave.Length, Target440Hz = wanted, Excluded1000Hz = unwanted }));
            Check(wave.Length > 48000 && wanted > 0.0001, "WASAPI captura áudio do processo alvo");
            Check(unwanted < wanted * 0.03, "Áudio do outro processo excluído (>30 dB)");
        }
        var settings = new Settings { Folder = directory, MicrophoneId = mic.ID };
        var recording = await Recording.Start(new(target.Id, target.StartTime.ToUniversalTime().Ticks, mic.ID), settings);
        await Task.Delay(3100);
        var result = await recording.Finish();
        Check(File.Exists(result.Path) && new FileInfo(result.Path).Length > 1000 && !result.Path.Contains("_gravando"), "Microfone + processo codificados e finalizados em Opus");
        File.WriteAllText(Path.Combine(directory, "recording-path.txt"), result.Path);
        Check(CallDetector.Find() is null || Process.GetProcessById(CallDetector.Find()!.ProcessId).ProcessName.StartsWith("Discord"), "Detector não confunde o próprio microfone com Discord");
    }
    finally { if (!target.HasExited) target.Kill(true); if (!other.HasExited) other.Kill(true); }
}
Console.WriteLine($"{passed} verificações passaram.");
}
catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }

[DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);

static Process StartTone(string frequency)
{
    var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add("--tone"); info.ArgumentList.Add(frequency);
    return Process.Start(info)!;
}
static double Magnitude(float[] samples, double frequency)
{
    double real = 0, imaginary = 0;
    for (int i = 0; i < samples.Length; i++) { double angle = 2 * Math.PI * frequency * i / 48000; real += samples[i] * Math.Cos(angle); imaginary += samples[i] * Math.Sin(angle); }
    return Math.Sqrt(real * real + imaginary * imaginary) / Math.Max(1, samples.Length);
}
