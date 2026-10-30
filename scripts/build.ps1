param([switch]$Test, [string]$DotnetPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
    $dotnet = if ($DotnetPath) { $DotnetPath } elseif (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
    if (!(Test-Path 'tools\ffmpeg.exe')) { throw 'Baixe o FFmpeg conforme README e coloque tools\ffmpeg.exe.' }
    & $dotnet restore src\DiscordCallRecorder -r win-x64 --locked-mode --configfile NuGet.Config
    if ($LASTEXITCODE) { throw 'Falha no restore' }
    & $dotnet publish src\DiscordCallRecorder -c Release -r win-x64 --self-contained true --no-restore -o artifacts\Release
    if ($LASTEXITCODE) { throw 'Falha na compilação' }
    if ($Test) {
        & $dotnet restore tests\Recorder.Tests --locked-mode --configfile NuGet.Config
        if ($LASTEXITCODE) { throw 'Falha no restore dos testes' }
        & $dotnet run --project tests\Recorder.Tests -c Release --no-restore
        if ($LASTEXITCODE) { throw 'Falha nos testes' }
    }
} finally { Pop-Location }
