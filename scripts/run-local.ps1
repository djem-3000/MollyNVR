[CmdletBinding()]
param(
    [int]$Port = 9080,
    [int]$WebRtcPort = 8555,
    [string]$RtspListen = '127.0.0.1:8554',
    [string]$DataDirectory = '',
    [switch]$NoBuild,
    [switch]$SkipUiBuild
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $root 'data'
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
$DataDirectory = (Resolve-Path -LiteralPath $DataDirectory).Path

$go2rtc = Join-Path $root 'tools\go2rtc.exe'
if (-not (Test-Path -LiteralPath $go2rtc)) {
    throw "Не найден go2rtc: $go2rtc. Положите бинарник в tools\go2rtc.exe."
}

$busyPort = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
if ($busyPort) {
    throw "Порт $Port уже занят. Остановите контейнер (docker compose stop) или укажите другой порт: .\scripts\run-local.ps1 -Port 9081"
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Storage__DataDirectory = $DataDirectory
$env:Go2Rtc__BinaryPath = $go2rtc
$env:Go2Rtc__WebRtcListen = ":$WebRtcPort"
$env:Go2Rtc__RtspListen = $RtspListen
$env:Kestrel__Endpoints__Http__Url = "http://0.0.0.0:$Port"

$uiDist = Join-Path $root 'ui\dist'
$env:ASPNETCORE_WEBROOT = $uiDist

$reolinkBridge = Join-Path $root 'tools\neolink\neolink.net.dll'
$bridgeProcess = $null
if (Test-Path -LiteralPath $reolinkBridge) {
    $env:Reolink__ApiBaseUrl = 'http://127.0.0.1:8655'
    $env:Reolink__RtspBaseUrl = 'rtsp://127.0.0.1:8654'
    $env:Reolink__ConfigPath = Join-Path $DataDirectory 'reolink\config.json'
}

Write-Host "MollyNVR (локальный запуск)" -ForegroundColor Cyan
Write-Host "  UI/API:   http://127.0.0.1:$Port"
Write-Host "  UI:       $uiDist"
Write-Host "  Данные:   $DataDirectory"
Write-Host "  go2rtc:   $go2rtc (WebRTC :$WebRtcPort)"
if (Test-Path -LiteralPath $reolinkBridge) {
    Write-Host "  Reolink:  $reolinkBridge (RTSP :8654, API :8655)"
}
else {
    Write-Host "  Reolink:  мост не найден ($reolinkBridge) — модуль Reolink недоступен." -ForegroundColor Yellow
}
Write-Host "  Multicast-поиск доступен, пока приложение запущено на хосте." -ForegroundColor DarkGray
Write-Host ""
Write-Host "Если контейнер mollynvr запущен, остановите его: docker compose stop" -ForegroundColor Yellow
Write-Host ""

$arguments = @('run', '--project', 'src/MollyNvr.Api')
if ($NoBuild) {
    $arguments += '--no-build'
}

Push-Location $root
try {
    if (-not $SkipUiBuild) {
        npm --prefix ui run build
        if ($LASTEXITCODE -ne 0) {
            throw "Сборка UI завершилась с ошибкой (npm exit code $LASTEXITCODE)."
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $uiDist 'index.html'))) {
        throw "Не найдена UI-сборка: $uiDist\index.html. Запустите без -SkipUiBuild."
    }

    if (Test-Path -LiteralPath $reolinkBridge) {
        $bridgeProcess = Start-Process pwsh -ArgumentList @(
            '-NoProfile',
            '-File', (Join-Path $root 'scripts\run-reolink-bridge.ps1'),
            '-AssemblyPath', $reolinkBridge,
            '-ConfigPath', $env:Reolink__ConfigPath
        ) -PassThru -WindowStyle Hidden
    }

    dotnet @arguments
}
finally {
    if ($bridgeProcess -and -not $bridgeProcess.HasExited) {
        taskkill /PID $bridgeProcess.Id /T /F 2>$null | Out-Null
    }

    Pop-Location
}
