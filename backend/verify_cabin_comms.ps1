param(
    [string]$BaseUrl = 'https://cobacoba.idccoal.id',
    [string]$SettingsPath = (Join-Path $PSScriptRoot 'appsettings.Local.json'),
    [string]$DiagnosticUnit = 'TEST_DIAG'
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
if (-not $base.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use the authorized HTTPS public endpoint for this test.'
}
if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) {
    throw 'Private settings file not found. Run this check from an authorized server/admin machine.'
}
$settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
$key = [string]$settings.CabinComms.DispatcherKey
if ($key.Length -lt 32) { throw 'Dispatcher key is missing from private settings.' }
$headers = @{ 'X-FMS-Dispatcher-Key' = $key }

$status = Invoke-RestMethod -Uri "$base/api/v1/comms/status" -TimeoutSec 15
if ($status.enabled -ne $true -or $status.mode -ne 'live_pcm16_ws') {
    throw 'Deployed communication endpoint is not enabled for live radio.'
}
Write-Host 'OK communication feature enabled'

$messages = Invoke-RestMethod -Uri "$base/api/v1/comms/messages?unit_name=ALL" -Headers $headers -TimeoutSec 15
if ($messages.status -ne 'success' -or $null -eq $messages.data) {
    throw 'Dispatcher authentication or message-table read failed.'
}
Write-Host 'OK dispatcher authentication and message read'

$ticketBody = @{ unit_name = $DiagnosticUnit } | ConvertTo-Json -Compress
$ticket = Invoke-RestMethod -Uri "$base/api/v1/comms/live-ticket" -Method Post -Headers $headers `
    -ContentType 'application/json' -Body $ticketBody -TimeoutSec 15
if (-not $ticket.ticket) { throw 'Live radio ticket was not issued.' }

$ws = [Net.WebSockets.ClientWebSocket]::new()
$timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(15))
try {
    $uri = [Uri]($base.Replace('https://', 'wss://') +
        '/api/v1/comms/live?ticket=' + [Uri]::EscapeDataString($ticket.ticket))
    [void]$ws.ConnectAsync($uri, $timeout.Token).GetAwaiter().GetResult()
    $buffer = [byte[]]::new(4096)
    $frame = $ws.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
    if ($frame.MessageType -ne [Net.WebSockets.WebSocketMessageType]::Text) {
        throw 'WebSocket did not return a ready event.'
    }
    $ready = [Text.Encoding]::UTF8.GetString($buffer, 0, $frame.Count) | ConvertFrom-Json
    if ($ready.type -ne 'ready' -or $ready.unit_name -ne $DiagnosticUnit) {
        throw 'WebSocket ready event does not match the diagnostic unit.'
    }
    Write-Host 'OK WebSocket upgrade and live radio ready event'
    [void]$ws.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
        'diagnostic', $timeout.Token).GetAwaiter().GetResult()
} finally {
    $ws.Dispose()
    $timeout.Dispose()
}

Write-Host 'Server communication path passed. Pairing, message delivery, and audio still require two physical clients.'
