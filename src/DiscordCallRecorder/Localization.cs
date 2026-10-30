using System.Globalization;

namespace DiscordCallRecorder;

internal static class Localization
{
    private static string language = "pt-BR";
    internal static string Language
    {
        get => language;
        set => language = Normalize(value);
    }
    internal static string Normalize(string? value) => value == "en" ? "en" : "pt-BR";
    internal static string DefaultLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? "pt-BR" : "en";
    internal static CultureInfo Culture => CultureInfo.GetCultureInfo(Language);
    internal static readonly Dictionary<string, string> English = new()
    {
        ["Abrir gravações"] = "Open recordings",
        ["Alterar local de armazenamento"] = "Change storage folder",
        ["Microfone"] = "Microphone",
        ["Iniciar com o Windows"] = "Start with Windows",
        ["Mostrar notificações"] = "Show notifications",
        ["Sair"] = "Exit",
        ["Idioma"] = "Language",
        ["Gravação iniciada"] = "Recording started",
        ["Chamada do Discord detectada."] = "Discord call detected.",
        ["Gravação salva"] = "Recording saved",
        ["Duração {0} • {1:F1} MB."] = "Duration {0} • {1:F1} MB.",
        ["Mesmo microfone usado pelo Discord"] = "Same microphone as Discord",
        ["Tratamento de voz (Windows)"] = "Voice processing (Windows)",
        ["Microfone atualizado"] = "Microphone updated",
        ["O tratamento será aplicado na próxima gravação. O resultado depende do dispositivo."] = "Processing will apply to the next recording. Results depend on the device.",
        ["Volume do microfone"] = "Microphone volume",
        [" (padrão)"] = " (default)",
        ["O volume será aplicado na próxima gravação."] = "The volume will apply to the next recording.",
        ["A escolha será usada na próxima gravação."] = "The selection will apply to the next recording.",
        ["Falha — tentando novamente"] = "Error — retrying",
        ["Não foi possível gravar"] = "Unable to record",
        ["Pausado"] = "Paused",
        ["Gravando • {0}"] = "Recording • {0}",
        ["Aguardando"] = "Waiting",
        ["Retomar"] = "Resume",
        ["Pausar"] = "Pause",
        ["Escolha onde deseja salvar suas gravações."] = "Choose where to save your recordings.",
        ["Confirmar"] = "Confirm",
        ["Escolher pasta"] = "Choose folder",
        ["Não foi possível usar essa pasta.\n"] = "Unable to use this folder.\n",
        ["Não foi possível salvar a configuração: "] = "Unable to save settings: ",
        ["A captura isolada do Discord requer Windows build 20348 ou superior (Windows 11 recomendado)."] = "Isolated Discord capture requires Windows build 20348 or later (Windows 11 recommended).",
        ["O codificador tools/ffmpeg.exe não foi encontrado."] = "The tools/ffmpeg.exe encoder was not found.",
        ["O dispositivo de áudio interrompeu a captura."] = "The audio device stopped capturing.",
        ["A gravação não conseguiu acompanhar o disco. O arquivo parcial foi preservado."] = "Recording could not keep up with the disk. The partial file was preserved.",
        ["Gravação interrompida. Arquivo parcial: {0}"] = "Recording interrupted. Partial file: {0}",
        ["Codificador: {0}"] = "Encoder: {0}",
        ["Formato do microfone não suportado: {0}"] = "Unsupported microphone format: {0}",
        ["Pacote de áudio excedeu o buffer de um segundo."] = "An audio packet exceeded the one-second buffer."
    };
    internal static string Text(string key) => Language == "en" && English.TryGetValue(key, out var value) ? value : key;
    internal static string Format(string key, params object[] args) => string.Format(Culture, Text(key), args);
    internal static string Key(string text) => English.FirstOrDefault(pair => pair.Value == text).Key ?? text;
}
