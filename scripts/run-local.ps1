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
        throw "go2rtc not found: $go2rtc. Put the binary into tools\go2rtc.exe."
}

$busyPort = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
if ($busyPort) {
        throw "Port $Port is already in use. Stop the container (docker compose stop) or specify another port: .\scripts\run-local.ps1 -Port 9081"
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

Write-Host "MollyNVR (local run)" -ForegroundColor Cyan
Write-Host "  UI/API:   http://127.0.0.1:$Port"
Write-Host "  UI:       $uiDist"
Write-Host "  Data:     $DataDirectory"
Write-Host "  go2rtc:   $go2rtc (WebRTC :$WebRtcPort)"
if (Test-Path -LiteralPath $reolinkBridge) {
    Write-Host "  Reolink:  $reolinkBridge (RTSP :8654, API :8655)"
}
else {
        Write-Host "  Reolink:  bridge not found ($reolinkBridge) — the Reolink module is unavailable." -ForegroundColor Yellow
}
Write-Host "  Multicast discovery is available while the app runs on the host." -ForegroundColor DarkGray
Write-Host ""
Write-Host "If the mollynvr container is running, stop it: docker compose stop" -ForegroundColor Yellow
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
                        throw "UI build failed (npm exit code $LASTEXITCODE)."
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $uiDist 'index.html'))) {
                throw "UI build not found: $uiDist\index.html. Run without -SkipUiBuild."
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
