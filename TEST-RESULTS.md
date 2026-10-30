# Validation / Validação

Validated on 2026-10-29 with .NET SDK 10.0.401 on Windows x64.

- `scripts/build.ps1 -Test`: successful self-contained Windows x64 publish and 59 passing checks.
- Coverage includes call debouncing, timestamp buffers, microphone conversion/gain,
  compatibility with older settings, persistent language selection, invalid-language fallback,
  Portuguese/English messages and formatted notification placeholders.
- Build output includes the project license and notice alongside documentation.
- Git excludes generated files, SDK/package caches, FFmpeg executables, recordings,
  personal settings and diagnostic output.

No real calls or microphone recordings were made during this repository-preparation validation.
Real Discord calls, device reconnection, startup at login and visual UI behavior still
need manual verification on the target machine. See the README for optional audio integration tests.

The GitHub Actions workflow repeats the build and default checks on Windows for pushes and pull requests.
