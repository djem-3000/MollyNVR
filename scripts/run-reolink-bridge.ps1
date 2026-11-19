[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AssemblyPath,

    [Parameter(Mandatory)]
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'

$configDirectory = Split-Path -Parent $ConfigPath
if ($configDirectory -and -not (Test-Path -LiteralPath $configDirectory)) {
    New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    '{ "webui": false, "cameras": [] }' | Set-Content -LiteralPath $ConfigPath -Encoding utf8NoBOM
}

# neolink.net reads the configuration only at startup, so when it changes
# the process is restarted — just like the container entrypoint.
while ($true) {
    $stdout = Join-Path $configDirectory 'bridge.log'
    $stderr = Join-Path $configDirectory 'bridge.error.log'

    $process = Start-Process dotnet -ArgumentList @($AssemblyPath, '--config', $ConfigPath) `
        -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr

    $hash = (Get-FileHash -LiteralPath $ConfigPath -ErrorAction SilentlyContinue).Hash

    while (-not $process.HasExited) {
        Start-Sleep -Seconds 2

        $updated = (Get-FileHash -LiteralPath $ConfigPath -ErrorAction SilentlyContinue).Hash
        if ($updated -and $updated -ne $hash) {
            $hash = $updated
            try {
                $process.Kill($true)
            }
            catch {
                                # the process has already exited
            }

            break
        }
    }

    Start-Sleep -Seconds 1
}
