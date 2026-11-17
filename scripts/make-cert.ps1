param(
    [string]$HostIp = $env:HOST_LAN_IP
)

$ErrorActionPreference = 'Stop'

$mkcert = Get-Command mkcert -ErrorAction SilentlyContinue
if (-not $mkcert) {
    throw 'mkcert не найден. Установите: choco install mkcert (или scoop install mkcert).'
}

if ([string]::IsNullOrWhiteSpace($HostIp)) {
    $HostIp = (Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -like '192.168.*' -or $_.IPAddress -like '10.*' } |
        Select-Object -First 1 -ExpandProperty IPAddress)
}

if ([string]::IsNullOrWhiteSpace($HostIp)) {
    throw 'Не удалось определить LAN IP. Укажите: .\scripts\make-cert.ps1 -HostIp 192.168.1.151'
}

$root = Split-Path -Parent $PSScriptRoot
$certDir = Join-Path $root 'certs'
New-Item -ItemType Directory -Force -Path $certDir -PathType Container | Out-Null

Write-Host 'Устанавливаем локальный CA mkcert в доверенные (может потребоваться подтверждение)...'
& $mkcert.Source -install

Write-Host "Выпускаем сертификат для $HostIp, localhost, 127.0.0.1..."
& $mkcert.Source `
    -cert-file (Join-Path $certDir 'mollynvr.pem') `
    -key-file (Join-Path $certDir 'mollynvr-key.pem') `
    $HostIp localhost 127.0.0.1

$caRoot = (& $mkcert.Source -CAROOT).Trim()
Copy-Item (Join-Path $caRoot 'rootCA.pem') (Join-Path $certDir 'rootCA.crt') -Force

Write-Host ''
Write-Host 'Готово. Сертификаты в ./certs (в git не попадают).'
Write-Host "Телефон: установите корневой сертификат .\certs\rootCA.crt (Android: Настройки → Безопасность → Шифрование и учётные данные → Установить сертификат → Сертификат CA)."
Write-Host "Затем откройте https://${HostIp}:9443 и установите приложение из меню Chrome."
