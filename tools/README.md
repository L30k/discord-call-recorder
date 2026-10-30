# FFmpeg

O repositório contém somente código e documentação; `ffmpeg.exe` não é versionado.
Para executar uma compilação local, obtenha um build Windows x64 de fonte confiável,
com suporte a `libopus`, a partir de https://ffmpeg.org/download.html.
Coloque o executável em `tools/ffmpeg.exe` antes de usar `scripts/build.ps1`.

This repository does not bundle FFmpeg. Obtain a trusted Windows x64 build with
`libopus` support from the official FFmpeg download links and place it here as
`ffmpeg.exe` before running the publish script.

Confira `ffmpeg.exe -version` e `ffmpeg.exe -buildconf`. A licença depende das opções
do build; a cópia `FFmpeg-LICENSE.txt` refere-se ao build GPLv3 usado no desenvolvimento.
Se usar outro build, atualize a licença e os avisos correspondentes.

Before distributing executable packages, satisfy the exact FFmpeg build's license,
including corresponding-source requirements where applicable. The application's
noncommercial restriction does not apply to FFmpeg or other third-party components.
This initial GitHub publication distributes source only, with no executable release.
