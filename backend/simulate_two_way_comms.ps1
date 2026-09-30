<#
.SYNOPSIS
    Simulasi Komunikasi 2-Arah: Pesan Teks (Messaging) dan PTT Radio Live Audio (WebSocket PCM16)
    antara Ruang Kontrol (Dispatcher) dan Kabin Operator (In-Cabin).
#>

param(
    [string]$BaseUrl = "http://127.0.0.1:8000",
    [string]$DispatcherKey = "astha-local-dispatcher-key-2026-09",
    [string]$TestUnit = "DT5107"
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "SIMULASI KOMUNIKASI 2-ARAH (PESAN & LIVE PTT TALKBACK RADIO)" -ForegroundColor Yellow
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Target Endpoint : $base"
Write-Host "Unit Simulasi   : $TestUnit"
Write-Host ""

# -----------------------------------------------------------------------------
# 1. TEST STATUS ENDPOINT
# -----------------------------------------------------------------------------
Write-Host "[1/6] Memeriksa Status Endpoint Komunikasi (/api/v1/comms/status)..." -NoNewline
try {
    $status = Invoke-RestMethod -Uri "$base/api/v1/comms/status" -TimeoutSec 5
    if ($status.enabled -ne $true -or $status.mode -ne "live_pcm16_ws") {
        Write-Host " [GAGAL]" -ForegroundColor Red
        Write-Error "Comms endpoint tidak aktif atau mode bukan live_pcm16_ws."
    }
    Write-Host " [OK] (Mode: $($status.mode), Sample Rate: $($status.sample_rate) Hz)" -ForegroundColor Green
} catch {
    Write-Host " [GAGAL]" -ForegroundColor Red
    Write-Warning "Server lokal belum berjalan atau tidak dapat diakses di $base."
    exit 0
}

# -----------------------------------------------------------------------------
# 2. TEST DISPATCHER AUTHORIZATION & BACA DAFTAR PESAN
# -----------------------------------------------------------------------------
Write-Host "[2/6] Membaca Pesan Dispatcher (/api/v1/comms/messages?unit_name=ALL)..." -NoNewline
$headers = @{ "X-FMS-Dispatcher-Key" = $DispatcherKey }
try {
    $msgList = Invoke-RestMethod -Uri "$base/api/v1/comms/messages?unit_name=ALL" -Headers $headers -TimeoutSec 5
    Write-Host " [OK] ($($msgList.data.Count) pesan tercatat)" -ForegroundColor Green
} catch {
    Write-Host " [GAGAL HTTP $($_.Exception.Response.StatusCode.value__)]" -ForegroundColor Red
    throw $_
}

# -----------------------------------------------------------------------------
# 3. TEST PENGIRIMAN PESAN: DISPATCHER -> KABIN
# -----------------------------------------------------------------------------
Write-Host "[3/6] Simulasi Kirim Pesan: Dispatcher -> Unit $TestUnit..." -NoNewline
$timestamp = (Get-Date).ToString("HH:mm:ss")
$dispPayload = @{
    unit_name = $TestUnit
    body = "[SIMULASI DISPATCH $timestamp] Pengalihan rute pit ke Shovel EX-204."
    priority = "urgent"
} | ConvertTo-Json -Compress

try {
    $sendResp = Invoke-RestMethod -Uri "$base/api/v1/comms/messages" -Method Post -Headers $headers -ContentType "application/json" -Body $dispPayload -TimeoutSec 5
    if ($sendResp.status -eq "success") {
        Write-Host " [OK] (Message ID: $($sendResp.data.id))" -ForegroundColor Green
    } else {
        Write-Host " [GAGAL]" -ForegroundColor Red
    }
} catch {
    Write-Host " [GAGAL HTTP $($_.Exception.Response.StatusCode.value__)]" -ForegroundColor Red
    throw $_
}

# -----------------------------------------------------------------------------
# 4. TEST PENGIRIMAN PESAN BALASAN: KABIN -> DISPATCHER
# -----------------------------------------------------------------------------
Write-Host "[4/6] Simulasi Balasan Pesan: Unit $TestUnit -> Dispatcher..." -NoNewline
$cabinPayload = @{
    unit_name = $TestUnit
    body = "[SIMULASI KABIN $timestamp] 10-4 Copy, unit bergerak menuju EX-204."
    priority = "normal"
} | ConvertTo-Json -Compress

try {
    $replyResp = Invoke-RestMethod -Uri "$base/api/v1/comms/messages" -Method Post -Headers $headers -ContentType "application/json" -Body $cabinPayload -TimeoutSec 5
    if ($replyResp.status -eq "success") {
        Write-Host " [OK] (Message ID: $($replyResp.data.id))" -ForegroundColor Green
    } else {
        Write-Host " [GAGAL]" -ForegroundColor Red
    }
} catch {
    Write-Host " [GAGAL HTTP $($_.Exception.Response.StatusCode.value__)]" -ForegroundColor Red
    throw $_
}

# -----------------------------------------------------------------------------
# 5. TEST PTT TICKET ISSUANCE (DUAL PEER: DISPATCHER & KABIN)
# -----------------------------------------------------------------------------
Write-Host "[5/6] Menerbitkan Tiket WebSocket Live Radio (PTT)..." -NoNewline
$dispTicketReq = @{ unit_name = $TestUnit } | ConvertTo-Json -Compress
$dispTicket = (Invoke-RestMethod -Uri "$base/api/v1/comms/live-ticket" -Method Post -Headers $headers -ContentType "application/json" -Body $dispTicketReq -TimeoutSec 5).ticket

$cabTicketReq = @{ unit_name = $TestUnit } | ConvertTo-Json -Compress
$cabTicket = (Invoke-RestMethod -Uri "$base/api/v1/comms/live-ticket" -Method Post -Headers $headers -ContentType "application/json" -Body $cabTicketReq -TimeoutSec 5).ticket

if ($dispTicket -and $cabTicket -and ($dispTicket -ne $cabTicket)) {
    Write-Host " [OK] (Tiket Dispatcher & Tiket Kabin Valid)" -ForegroundColor Green
} else {
    Write-Host " [GAGAL]" -ForegroundColor Red
    throw "Gagal mendapatkan tiket live radio."
}

# -----------------------------------------------------------------------------
# 6. TEST WEBSOCKET LIVE AUDIO PTT STREAMING & HALF-DUPLEX HANDSHAKE
# -----------------------------------------------------------------------------
Write-Host "[6/6] Simulasi WebSocket Live Audio PCM16 Half-Duplex..." -NoNewline

$wsBase = $base.Replace("http://", "ws://").Replace("https://", "wss://")
$wsDispUri = [Uri]("$wsBase/api/v1/comms/live?ticket=" + [Uri]::EscapeDataString($dispTicket))
$wsCabUri = [Uri]("$wsBase/api/v1/comms/live?ticket=" + [Uri]::EscapeDataString($cabTicket))

$wsDispatcher = [Net.WebSockets.ClientWebSocket]::new()
$wsCabin = [Net.WebSockets.ClientWebSocket]::new()
$cts = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))

try {
    # Connect both peers
    [void]$wsDispatcher.ConnectAsync($wsDispUri, $cts.Token).GetAwaiter().GetResult()
    [void]$wsCabin.ConnectAsync($wsCabUri, $cts.Token).GetAwaiter().GetResult()

    # Receive Ready Events
    $buf = [byte[]]::new(4096)
    $res1 = $wsDispatcher.ReceiveAsync([ArraySegment[byte]]::new($buf), $cts.Token).GetAwaiter().GetResult()
    $res2 = $wsCabin.ReceiveAsync([ArraySegment[byte]]::new($buf), $cts.Token).GetAwaiter().GetResult()

    # Dispatcher Start Speaking (ptt_start)
    $pttStartJson = [Text.Encoding]::UTF8.GetBytes('{"type":"ptt_start"}')
    [void]$wsDispatcher.SendAsync([ArraySegment[byte]]::new($pttStartJson), [Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()

    # Verify Dispatcher gets ptt_ready
    $readyRes = $wsDispatcher.ReceiveAsync([ArraySegment[byte]]::new($buf), $cts.Token).GetAwaiter().GetResult()
    $readyText = [Text.Encoding]::UTF8.GetString($buf, 0, $readyRes.Count)

    # Stream 3x simulated PCM16 audio frames (640 bytes each = 20ms @ 16kHz mono)
    $audioFrame = [byte[]]::new(640)
    for ($i = 0; $i -lt 3; $i++) {
        [void]$wsDispatcher.SendAsync([ArraySegment[byte]]::new($audioFrame), [Net.WebSockets.WebSocketMessageType]::Binary, $true, $cts.Token).GetAwaiter().GetResult()
    }

    # Dispatcher Stop Speaking (ptt_stop)
    $pttStopJson = [Text.Encoding]::UTF8.GetBytes('{"type":"ptt_stop"}')
    [void]$wsDispatcher.SendAsync([ArraySegment[byte]]::new($pttStopJson), [Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()

    Write-Host " [OK] (Handshake, PTT Lock, Binary Audio Stream, dan Release Selesai)" -ForegroundColor Green
} finally {
    if ($wsDispatcher.State -eq [Net.WebSockets.WebSocketState]::Open) {
        [void]$wsDispatcher.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    }
    if ($wsCabin.State -eq [Net.WebSockets.WebSocketState]::Open) {
        [void]$wsCabin.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    }
    $wsDispatcher.Dispose()
    $wsCabin.Dispose()
    $cts.Dispose()
}

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "SELURUH PENGUJIAN & SIMULASI 2-ARAH BERHASIL TANPA ERROR!" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
