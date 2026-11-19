param(
    [string]$HostIp = $env:HOST_LAN_IP
)

$ErrorActionPreference = 'Stop'

$mkcert = Get-Command mkcert -ErrorAction SilentlyContinue
if (-not $mkcert) {
        throw 'mkcert not found. Install it: choco install mkcert (or scoop install mkcert).'
}

if ([string]::IsNullOrWhiteSpace($HostIp)) {
    $HostIp = (Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -like '192.168.*' -or $_.IPAddress -like '10.*' } |
        Select-Object -First 1 -ExpandProperty IPAddress)
}

if ([string]::IsNullOrWhiteSpace($HostIp)) {
        throw 'Could not determine the LAN IP. Specify it: .\scripts\make-cert.ps1 -HostIp 192.168.1.151'
}

$root = Split-Path -Parent $PSScriptRoot
$certDir = Join-Path $root 'certs'
New-Item -ItemType Directory -Force -Path $certDir -PathType Container | Out-Null

Write-Host 'Installing the local mkcert CA into the trusted store (confirmation may be required)...'
& $mkcert.Source -install

Write-Host "Issuing a certificate for $HostIp, localhost, 127.0.0.1..."
& $mkcert.Source `
    -cert-file (Join-Path $certDir 'mollynvr.pem') `
    -key-file (Join-Path $certDir 'mollynvr-key.pem') `
    $HostIp localhost 127.0.0.1

$caRoot = (& $mkcert.Source -CAROOT).Trim()
Copy-Item (Join-Path $caRoot 'rootCA.pem') (Join-Path $certDir 'rootCA.crt') -Force

Write-Host ''
Write-Host 'Done. Certificates are in ./certs (not committed to git).'
Write-Host "Phone: install the root certificate .\certs\rootCA.crt (Android: Settings -> Security -> Encryption & credentials -> Install a certificate -> CA certificate)."
Write-Host "Then open https://${HostIp}:9443 and install the app from the Chrome menu."
