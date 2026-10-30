using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DiscordCallRecorder;

internal sealed class Recording : IAsyncDisposable
{
    private WasapiRecorder? microphone;
    private WasapiRecorder? discord;
    private MMDevice? device;
    private readonly TimelineBuffer micBuffer = new(), discordBuffer = new();
    private Process? encoder;
    private string encoderError = "";
    private Task? writer;
    private long origin;
    private long stopAt = -1;
    private bool stopping;
    private float microphoneGain;
    private readonly string partialPath;
    public DateTimeOffset Started { get; } = DateTimeOffset.Now;
    public CallTarget Target { get; }
    public Exception? Error { get; private set; }
    public TimeSpan Duration => TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - origin) / (double)Stopwatch.Frequency);
    private Recording(CallTarget target, string folder)
    {
        Target = target;
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var month = culture.TextInfo.ToTitleCase(Started.ToString("MMMM", culture));
        var directory = Path.Combine(folder, Started.ToString("yyyy"), $"{Started:MM} - {month}", Started.ToString("dd"));
        Directory.CreateDirectory(directory);
        partialPath = UniquePath(directory, $"{Started:HH-mm}_gravando", ".opus");
        // Reserve the name and check write permission before opening any capture stream.
        using var reserve = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    }
    public static async Task<Recording> Start(CallTarget target, Settings settings)
    {
        var recording = new Recording(target, settings.Folder);
        try { await recording.Initialize(settings); return recording; }
        catch { await recording.DisposeAsync(); throw; }
    }
    private async Task Initialize(Settings settings)
    {
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
        if (!File.Exists(ffmpeg)) throw new FileNotFoundException(Localization.Text("O codificador tools/ffmpeg.exe não foi encontrado."));
        using var enumerator = new MMDeviceEnumerator();
        device = enumerator.GetDevice(settings.MicrophoneId ?? Target.DeviceId);
        microphoneGain = float.IsFinite(settings.MicrophoneGain) ? Math.Clamp(settings.MicrophoneGain, 0.5f, 3f) : 1.25f;
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        var micBuilder = new WasapiRecorderBuilder().WithDevice(device).WithBufferLength(100);
        if (settings.MicrophoneVoiceProcessing) micBuilder.WithCommunicationsMode();
        microphone = micBuilder.Build();
        Log.Write($"Microfone: {device.FriendlyName}; tratamento Windows: {settings.MicrophoneVoiceProcessing}; ganho: {microphoneGain:0.0}x");
        discord = await new WasapiRecorderBuilder().WithProcessLoopback((uint)Target.ProcessId)
            .WithFormat(format).WithBufferLength(100).BuildAsync();
        var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "f32le", "-ar", "48000", "-ac", "1", "-i", "pipe:0", "-c:a", "libopus", "-b:a", "48k", "-vbr", "on", "-application", "voip", "-compression_level", "5", "-frame_duration", "20", "-page_duration", "1000000", "-flush_packets", "1", "-f", "opus", partialPath }) info.ArgumentList.Add(arg);
        encoder = new Process { StartInfo = info };
        encoder.ErrorDataReceived += (_, e) => { if (e.Data is not null) encoderError = e.Data.Length > 2000 ? e.Data[..2000] : e.Data; };
        encoder.Start();
        encoder.BeginErrorReadLine();
        origin = Stopwatch.GetTimestamp();
        var converter = new MicrophoneConverter(microphone.WaveFormat);
        microphone.DataAvailable += (data, flags, position, qpc) =>
        {
            try
            {
                // Missing/silent microphone packets already map to zeros on the timeline.
                if (((int)flags & 2) == 0) Receive(micBuffer, converter.Convert(data), (int)flags, qpc);
            }
            catch (Exception e) { Error = e; }
        };
        discord.DataAvailable += (data, flags, position, qpc) => Receive(discordBuffer, data, (int)flags, qpc);
        microphone.RecordingStopped += CaptureStopped;
        discord.RecordingStopped += CaptureStopped;
        microphone.StartRecording();
        discord.StartRecording();
        writer = Task.Run(WriteAudio);
    }
    private void CaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (!stopping) Error = e.Exception ?? new IOException(Localization.Text("O dispositivo de áudio interrompeu a captura."));
    }
    private void Receive(TimelineBuffer buffer, ReadOnlySpan<byte> data, int flags, long qpc)
    {
        // WASAPI QPC timestamps are expressed in 100 ns units, not raw QPC ticks.
        var seconds = qpc / 10_000_000.0 - origin / (double)Stopwatch.Frequency;
        if ((flags & 4) != 0 || qpc == 0) seconds = Duration.TotalSeconds - data.Length / (48000.0 * 8);
        buffer.Add(data, (long)Math.Round(seconds * 48000), (flags & 2) != 0);
    }
    private async Task WriteAudio()
    {
        long cursor = 0;
        var samples = new float[960];
        var bytes = new byte[960 * 4];
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
            while (true)
            {
                var end = Interlocked.Read(ref stopAt);
                long ready = end >= 0 ? end : (long)((Duration.TotalSeconds - 0.25) * 48000);
                if (ready - cursor > 48000 * 3) throw new IOException(Localization.Text("A gravação não conseguiu acompanhar o disco. O arquivo parcial foi preservado."));
                while (cursor < ready && (ready - cursor >= samples.Length || end >= 0))
                {
                    int count = (int)Math.Min(samples.Length, ready - cursor);
                    Array.Clear(samples);
                    micBuffer.Mix(samples.AsSpan(0, count), cursor, 0.7f * microphoneGain);
                    discordBuffer.Mix(samples.AsSpan(0, count), cursor);
                    for (int i = 0; i < count; i++) samples[i] = Math.Clamp(samples[i], -1f, 1f);
                    Buffer.BlockCopy(samples, 0, bytes, 0, count * 4);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await encoder!.StandardInput.BaseStream.WriteAsync(bytes.AsMemory(0, count * 4), timeout.Token);
                    cursor += count;
                }
                if (end >= 0 && cursor >= end) break;
                await timer.WaitForNextTickAsync();
            }
        }
        catch (Exception e) { Error = e; Log.Write(e); }
        finally { try { encoder?.StandardInput.Close(); } catch (Exception e) { Error ??= e; } }
    }
    public async Task<(string Path, TimeSpan Duration)> Finish()
    {
        var duration = Duration;
        await DisposeAsync();
        if (Error is not null) throw new IOException(Localization.Format("Gravação interrompida. Arquivo parcial: {0}", partialPath), Error);
        var end = Started + duration;
        var name = $"{Started:HH-mm}_{end:HH-mm} [{(int)duration.TotalHours:00}h{duration.Minutes:00}m]";
        var final = UniquePath(Path.GetDirectoryName(partialPath)!, name, ".opus");
        File.Move(partialPath, final);
        return (final, duration);
    }
    public async ValueTask DisposeAsync()
    {
        if (stopping) return;
        stopping = true;
        Interlocked.Exchange(ref stopAt, Math.Max(0, (long)(Duration.TotalSeconds * 48000)));
        foreach (var capture in new IAsyncDisposable?[] { microphone, discord })
            if (capture is not null) try { await capture.DisposeAsync(); } catch (Exception e) { Error ??= e; }
        device?.Dispose();
        if (writer is not null) await writer;
        if (encoder is not null)
        {
            try
            {
                encoder.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await encoder.WaitForExitAsync(timeout.Token);
                if (encoder.ExitCode != 0) Error ??= new IOException(Localization.Format("Codificador: {0}", encoderError));
            }
            catch (Exception e) { Error ??= e; try { encoder.Kill(true); } catch { } }
            finally { encoder.Dispose(); }
        }
    }
    internal static string UniquePath(string directory, string name, string extension)
    {
        var path = Path.Combine(directory, name + extension);
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(directory, $"{name} ({i}){extension}");
        return path;
    }
}
