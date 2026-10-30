using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace DiscordCallRecorder;

internal sealed class TrayContext : ApplicationContext
{
    private readonly Settings settings;
    private readonly QuietTray tray;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Debouncer<CallTarget> debounce = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ToolStripMenuItem status = new() { Enabled = false };
    private readonly ToolStripMenuItem pause = new();
    private readonly Icon waitingIcon = MakeIcon(Color.FromArgb(95, 147, 190));
    private readonly Icon recordingIcon = MakeIcon(Color.FromArgb(232, 73, 84));
    private readonly Icon pausedIcon = MakeIcon(Color.FromArgb(226, 176, 62));
    private Recording? recording;
    private bool busy, closing;
    private TimeSpan retryAfter;
    private string? fault;
    private readonly Form dispatcher = new();

    public TrayContext(Settings settings)
    {
        this.settings = settings;
        _ = dispatcher.Handle;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Discord Recorder") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(status);
        menu.Items.Add(pause);
        pause.Click += async (_, _) =>
        {
            settings.Paused = !settings.Paused; Save(); debounce.Reset();
            if (!busy) { busy = true; try { if (settings.Paused) await Stop(); } finally { busy = false; } }
            UpdateStatus();
        };
        menu.Items.Add(Localization.Text("Abrir gravações"), null, (_, _) =>
        {
            try { Directory.CreateDirectory(settings.Folder); Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { settings.Folder }, UseShellExecute = true }); }
            catch (Exception e) { Report(e); }
        });
        menu.Items.Add(Localization.Text("Alterar local de armazenamento"), null, (_, _) =>
        {
            using var setup = new SetupForm(settings.Folder);
            if (setup.ShowDialog() == DialogResult.OK) { settings.Folder = setup.SelectedFolder; settings.Language = setup.SelectedLanguage; Save(); retryAfter = TimeSpan.Zero; }
        });
        var microphones = new ToolStripMenuItem(Localization.Text("Microfone"));
        microphones.DropDownOpening += (_, _) => LoadMicrophones(microphones);
        menu.Items.Add(microphones);
        var startup = new ToolStripMenuItem(Localization.Text("Iniciar com o Windows")) { Checked = Settings.StartupEnabled };
        startup.Click += (_, _) => { try { Settings.SetStartup(!startup.Checked); startup.Checked = Settings.StartupEnabled; } catch (Exception e) { Report(e); } };
        menu.Items.Add(startup);
        var notifications = new ToolStripMenuItem(Localization.Text("Mostrar notificações")) { Checked = settings.Notifications };
        notifications.Click += (_, _) => { settings.Notifications = !settings.Notifications; notifications.Checked = settings.Notifications; Save(); };
        menu.Items.Add(notifications); menu.Items.Add(new ToolStripSeparator());
        var languages = new ToolStripMenuItem(Localization.Text("Idioma"));
        foreach (var (code, label) in new[] { ("pt-BR", "Português (Brasil)"), ("en", "English") })
        {
            var item = new ToolStripMenuItem(label) { Checked = settings.Language == code };
            item.Click += (_, _) =>
            {
                settings.Language = code; Localization.Language = code; Save();
                RefreshMenu(menu.Items); UpdateStatus();
            };
            languages.DropDownItems.Add(item);
        }
        languages.DropDownOpening += (_, _) =>
        {
            ((ToolStripMenuItem)languages.DropDownItems[0]).Checked = settings.Language == "pt-BR";
            ((ToolStripMenuItem)languages.DropDownItems[1]).Checked = settings.Language == "en";
        };
        menu.Items.Add(languages);
        menu.Items.Add(Localization.Text("Sair"), null, async (_, _) => await Shutdown());
        foreach (ToolStripItem item in menu.Items)
            if (item != status && item != pause) item.Tag = Localization.Key(item.Text ?? "");
        menu.Opening += (_, _) => { RefreshMenu(menu.Items); UpdateStatus(); };
        tray = new QuietTray(menu, waitingIcon);
        timer.Tick += async (_, _) => await Tick();
        SystemEvents.SessionEnding += SessionEnding;
        timer.Start(); UpdateStatus();
    }
    private async Task Tick()
    {
        UpdateStatus();
        if (busy || closing) return;
        busy = true;
        try
        {
            if (settings.Paused) { await Stop(); return; }
            if (recording?.Error is not null) { await Stop(); retryAfter = clock.Elapsed + TimeSpan.FromSeconds(30); debounce.Reset(); return; }
            if (clock.Elapsed < retryAfter) return;
            var observed = await Task.Run(CallDetector.Find);
            if (settings.Paused || closing) return;
            debounce.Update(observed, clock.Elapsed);
            var target = debounce.Current;
            if (recording is not null && !Equals(recording.Target, target)) await Stop();
            if (target is not null && recording is null)
            {
                recording = await Recording.Start(target, settings);
                if (closing || settings.Paused) { await Stop(); return; }
                fault = null;
                Notify(Localization.Text("Gravação iniciada"), Localization.Text("Chamada do Discord detectada."));
            }
        }
        catch (Exception e)
        {
            await Stop(); Report(e); debounce.Reset(); retryAfter = clock.Elapsed + TimeSpan.FromSeconds(30);
        }
        finally { busy = false; UpdateStatus(); }
    }
    private async Task Stop()
    {
        var current = recording; recording = null;
        if (current is null) return;
        try
        {
            var result = await current.Finish();
            var size = new FileInfo(result.Path).Length / 1_000_000.0;
            Notify(Localization.Text("Gravação salva"), Localization.Format("Duração {0} • {1:F1} MB.", FormatDuration(result.Duration), size));
            Log.Write($"Salvo: {result.Path}");
        }
        catch (Exception e) { Report(e); }
    }
    private void LoadMicrophones(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        AddMicrophone(menu, Localization.Text("Mesmo microfone usado pelo Discord"), null);
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            using (device) AddMicrophone(menu, device.FriendlyName, device.ID);
        }
        catch (Exception e) { Report(e); }
        menu.DropDownItems.Add(new ToolStripSeparator());
        var voice = new ToolStripMenuItem(Localization.Text("Tratamento de voz (Windows)")) { Checked = settings.MicrophoneVoiceProcessing };
        voice.Click += (_, _) =>
        {
            settings.MicrophoneVoiceProcessing = !settings.MicrophoneVoiceProcessing;
            Save(); Notify(Localization.Text("Microfone atualizado"), Localization.Text("O tratamento será aplicado na próxima gravação. O resultado depende do dispositivo."));
        };
        menu.DropDownItems.Add(voice);
        var volume = new ToolStripMenuItem(Localization.Text("Volume do microfone"));
        foreach (float gain in Enumerable.Range(2, 11).Select(step => step * 0.25f))
        {
            var item = new ToolStripMenuItem($"{gain * 100:0}%" + (gain == 1.25f ? Localization.Text(" (padrão)") : ""))
                { Checked = Math.Abs(settings.MicrophoneGain - gain) < 0.01f };
            item.Click += (_, _) => { settings.MicrophoneGain = gain; Save(); Notify(Localization.Text("Microfone atualizado"), Localization.Text("O volume será aplicado na próxima gravação.")); };
            volume.DropDownItems.Add(item);
        }
        menu.DropDownItems.Add(volume);
    }
    private void AddMicrophone(ToolStripMenuItem menu, string label, string? id)
    {
        var item = new ToolStripMenuItem(label) { Checked = settings.MicrophoneId == id };
        item.Click += (_, _) => { settings.MicrophoneId = id; Save(); Notify(Localization.Text("Microfone atualizado"), Localization.Text("A escolha será usada na próxima gravação.")); };
        menu.DropDownItems.Add(item);
    }
    private void Save() { try { settings.Save(); } catch (Exception e) { Report(e); } }
    private void Report(Exception e) { Log.Write(e); fault = "Falha — tentando novamente"; Notify(Localization.Text("Não foi possível gravar"), e.Message); }
    private static void RefreshMenu(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
            if (item.Tag is string key) item.Text = Localization.Text(key);
    }
    private void UpdateStatus()
    {
        var text = settings.Paused ? Localization.Text("Pausado") : recording is not null ? Localization.Format("Gravando • {0}", FormatDuration(recording.Duration)) : Localization.Text(fault ?? "Aguardando");
        status.Text = text; pause.Text = settings.Paused ? Localization.Text("Retomar") : Localization.Text("Pausar");
        tray.Text = "Discord Recorder\n" + text;
        tray.Icon = settings.Paused || fault is not null ? pausedIcon : recording is not null ? recordingIcon : waitingIcon;
    }
    private void Notify(string title, string message)
    {
        if (!settings.Notifications || closing) return;
        tray.Notify(title, message);
    }
    private async Task Shutdown()
    {
        if (closing) return;
        closing = true; timer.Stop();
        while (busy) await Task.Delay(50);
        await Stop();
        SystemEvents.SessionEnding -= SessionEnding;
        tray.Dispose(); timer.Dispose(); dispatcher.Dispose();
        waitingIcon.Dispose(); recordingIcon.Dispose(); pausedIcon.Dispose();
        ExitThread();
    }
    private void SessionEnding(object? sender, SessionEndingEventArgs e)
    {
        // WM_QUERYENDSESSION is synchronous and Windows limits the time available.
        try
        {
            dispatcher.Invoke(() =>
            {
                closing = true; timer.Stop();
                var current = recording; recording = null;
                if (current is not null)
                    Task.Run(async () => { try { await current.Finish(); } catch (Exception ex) { Log.Write(ex); } }).Wait(TimeSpan.FromSeconds(4));
            });
        }
        catch (Exception ex) { Log.Write(ex); }
    }
    internal static string FormatDuration(TimeSpan duration) => $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    private static Icon MakeIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(color))
        { graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; graphics.FillEllipse(brush, 4, 4, 24, 24); graphics.DrawEllipse(Pens.White, 9, 9, 14, 14); }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
}
