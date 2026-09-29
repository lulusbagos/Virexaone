param(
    [string]$SettingsPath = (Join-Path $PSScriptRoot 'appsettings.Local.json'),
    [switch]$RotateKey,
    [switch]$ShowKey
)

$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath($SettingsPath)
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Private settings file not found: $path"
}

$settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
if (-not $settings.FmsSettings -or -not $settings.FmsSettings.DbPassword) {
    throw 'Refusing to update settings without the existing database password.'
}
if (-not $settings.Contains('CabinComms')) {
    $settings.CabinComms = @{}
}

$key = [string]$settings.CabinComms.DispatcherKey
$generated = $RotateKey -or $key.Length -lt 32
if ($generated) {
    $key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
$settings.CabinComms.Enabled = $true
$settings.CabinComms.DispatcherKey = $key

$directory = Split-Path -Parent $path
$temporary = Join-Path $directory ('.appsettings.Local.' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    $settings | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $temporary -Encoding utf8 -NoNewline
    Set-Acl -LiteralPath $temporary -AclObject (Get-Acl -LiteralPath $path)
    Move-Item -LiteralPath $temporary -Destination $path -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}

$check = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
if ($check.CabinComms.Enabled -ne $true -or
    ([string]$check.CabinComms.DispatcherKey).Length -lt 32 -or
    -not $check.FmsSettings.DbPassword) {
    throw 'Private settings verification failed.'
}

Write-Host "Cabin communication configured in $path. Key generated: $generated. Restart the backend process."
if ($ShowKey) {
    Write-Warning 'Keep this dispatcher key private. Never put it in a WebGL build, APK, or repository.'
    Write-Host "Dispatcher key: $key"
}
