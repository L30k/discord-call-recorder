# Contribuindo / Contributing

Contribuições são bem-vindas. Antes de alterações grandes, abra uma issue para discutir o objetivo.
Use Windows x64 e SDK .NET 10. Leia [README](README.md) e [English README](README.en.md).

Contributions are welcome. Discuss major changes in an issue first. Use Windows x64
and .NET 10 SDK. Keep changes focused and describe the problem, behavior and validation.

```powershell
dotnet restore tests/Recorder.Tests --locked-mode --configfile NuGet.Config
dotnet run --project tests/Recorder.Tests -c Release --no-restore
```

Os testes padrão não capturam áudio. Integração requer dispositivos reais e deve ser
executada de forma consciente, com autorização dos participantes quando aplicável.
Default tests do not capture audio; integration tests need real devices and explicit local execution.

Textos de interface ficam em `Localization.cs`. Atualize os dois idiomas e preserve
os marcadores de formatação. Não traduza nomes dos dispositivos fornecidos pelo Windows.
UI text belongs in `Localization.cs`; update both languages and preserve format placeholders.

Nunca envie gravações, tokens, configurações pessoais, logs brutos ou identificadores de dispositivos.
Never include recordings, tokens, personal settings, raw logs or device IDs.

Ao enviar uma contribuição original, você concorda em disponibilizá-la sob
PolyForm Noncommercial 1.0.0. Não copie código incompatível. Dependências de terceiros
mantêm suas próprias licenças. By submitting original contributions, you agree to license
them under PolyForm Noncommercial 1.0.0. Third-party components retain their licenses.
