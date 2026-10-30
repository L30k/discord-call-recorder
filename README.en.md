# Discord Call Recorder

English · [Português (Brasil)](README.md)

A Windows tray application that automatically records Discord process audio mixed
with your chosen microphone into a local mono Opus file. Built with C#/.NET 10;
no Discord bot, token, account integration, runtime network access or app telemetry.

**Source available for noncommercial use under PolyForm Noncommercial 1.0.0.**
You may study, modify and share the original code for the purposes allowed by
[LICENSE](LICENSE). Commercial use requires separate permission from the copyright
holder. This is not an OSI-approved open-source license. Third-party components
retain their licenses; see [NOTICE](NOTICE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
This independent project is not affiliated with or endorsed by Discord.

## Requirements and build

- Windows x64 build 20348 or later; Windows 11 recommended.
- Desktop Discord, Discord PTB or Discord Canary. Browser Discord is not supported.
- Desktop-app microphone permission, a working capture device and a writable folder.
- For building: .NET 10 x64 SDK, Git, and a Windows x64 FFmpeg build with `libopus`.

This initial repository contains source only, with no executable release.

```powershell
git clone https://github.com/L30k/discord-call-recorder.git
cd discord-call-recorder
# Place a trusted FFmpeg executable at tools/ffmpeg.exe (see tools/README.md).
.\scripts\build.ps1 -Test
```

Get the SDK from https://dotnet.microsoft.com/download/dotnet/10.0 and FFmpeg through
https://ffmpeg.org/download.html. See [tools/README.md](tools/README.md) for licensing.
The script restores locked dependencies and creates `artifacts/Release`.
An optional `-DotnetPath C:\path\dotnet.exe` selects an SDK executable.
Run `artifacts/Release/DiscordCallRecorder.exe`; keep the entire Release folder together.
The published build is self-contained and does not require the .NET runtime or admin rights.

## Use and language

Choose **Português (Brasil)** or **English** and an output folder during first setup.
Change the language any time using **Idioma / Language** in the tray menu. The change
applies immediately without interrupting recording, and persists across restarts.
The initial default is Portuguese on Portuguese-language systems, English elsewhere.
Device names and native Windows/FFmpeg errors may retain their original language.

Tray colors: blue = waiting, red = recording, yellow = paused/error.

- **Pause / Resume**: pause finalizes the current file and disables detection; the preference persists.
- **Open recordings / Change storage folder**: a folder change applies to future recordings.
- **Microphone**: use the same device as Discord or choose another active device.
- **Voice processing (Windows)**: requests communications processing; available noise/echo/gain
  effects depend on the driver. It is not Discord Krisp and does not guarantee echo removal.
- **Microphone volume**: 50–300%, default 125%; applies to the next recording without
  changing Windows or Discord device gain. Higher gain can amplify noise or distort audio.
- **Start with Windows**: per-user startup, enabled at first setup. Toggle it off/on after moving the app.
- **Show notifications**: controls silent start/save/error notifications.
- **Exit**: finalizes the current recording and closes the application.

To uninstall, disable startup, exit, and remove the program folder. Settings and recordings
are stored separately. Settings/logs: `%LOCALAPPDATA%\DiscordCallRecorder`.
The settings file includes folder, microphone, gain, processing, notifications, pause and language.
Logs rotate at approximately 1 MB plus one previous file and do not contain conversation audio.

## Recording behavior and privacy

Obtain the required authorization from participants before recording. **Muting inside Discord
may not mute the recorder's independent microphone capture.** Hardware mute that stops the
device signal is respected. A headset is recommended: speakers may leak acoustically into the mic.

The app checks active capture sessions once per second and matches Discord executable names.
Start confirmation takes 2 seconds; stop confirmation takes 3 seconds. The initial confirmation
period is not recorded. Detection follows microphone use, so Discord's microphone test can also
trigger recording. Only one Discord process tree is followed at a time.

WASAPI process-loopback capture includes the selected Discord process tree rather than global
Windows output. All sounds from that process, including notifications, may be included.
Participants are not separated. Timestamp-aligned fixed buffers keep memory bounded;
the microphone is resampled to 48 kHz and mixed to mono with a base gain of 0.7 per source.
No speaker monitoring, transcription or custom echo cancellation is provided.

FFmpeg runs only during recording, receiving PCM through a pipe and writing Ogg Opus:
mono, 48 kHz, nominal 48 kbps VBR (about 22 MB/hour, content-dependent).
Files use year/month/day folders, with Portuguese month names in both interface languages:

```text
2026/09 - Setembro/21/18-42_20-17 [01h35m].opus
```

During capture the suffix is `_gravando.opus`. Existing names get numeric suffixes.
Calls crossing midnight stay under their start date; there is no periodic segmentation.

On stable device/process changes, the current file ends and a new one may start.
Capture, encoder or disk errors preserve partial output and retry after 30 seconds.
Slow disks stop recording instead of allowing unbounded buffers. On normal exit,
the encoder flushes before renaming the file. Power loss/forced termination can leave
an unfinished `_gravando` file; already-written Ogg pages may be readable, but recovery
is not guaranteed, lost segments are not stitched together, and interrupted files are not auto-renamed.

## Tests and diagnostics

Default regression tests do not access audio devices or require FFmpeg:

```powershell
dotnet restore tests/Recorder.Tests --locked-mode --configfile NuGet.Config
dotnet run --project tests/Recorder.Tests -c Release --no-restore
```

The Windows GitHub Actions workflow builds and runs these checks. Optional hardware integration
tests play two synthetic tones and capture microphone input, testing process isolation and Opus output:

```powershell
dotnet run --project tests/Recorder.Tests -c Release -- --integration "$PWD\artifacts\test-audio"
.\artifacts\Release\DiscordCallRecorder.exe --diagnose "$PWD\artifacts\diagnostics.json"
```

Build with FFmpeg first for integration testing. Diagnostics enumerate devices and Discord sessions
without recording or changing settings; they contain device identifiers and should be redacted.
Manual validation remains necessary for real calls, mute, pause/resume, unrelated app audio,
headset reconnects and startup at login. Synthetic tests do not replace real-device checks.

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md),
[CHANGELOG.md](CHANGELOG.md), and the [Portuguese technical documentation](README.md).
Never submit recordings, credentials, personal settings or raw diagnostic device IDs.
