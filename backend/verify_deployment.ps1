param(
    [string]$BaseUrl = 'https://cobacoba.idccoal.id',
    [string]$BearerToken = '',
    [string]$CabinUnit = '',
    [switch]$RequireComms
)

$base = $BaseUrl.TrimEnd('/')
$headers = @{}
if ($BearerToken) { $headers.Authorization = "Bearer $BearerToken" }
$failed = $false

function Test-Endpoint([string]$path, [bool]$requiresAuth) {
    $uri = "$base$path"
    try {
        $response = Invoke-WebRequest -Uri $uri -Headers $headers -TimeoutSec 20 -UseBasicParsing
        $body = $response.Content | ConvertFrom-Json
        if ($path -eq '/' -and ($body.PSObject.Properties.Name -contains 'db_host' -or $body.PSObject.Properties.Name -contains 'engine')) {
            Write-Host "FAIL $path`: public endpoint exposes backend internals"
            $script:failed = $true
        } elseif ($path -eq '/health' -and $body.status -ne 'healthy') {
            Write-Host "FAIL $path`: $($body.status) (GPS feed or database needs attention)"
            $script:failed = $true
        } elseif ($path -eq '/api/v1/fms/map-drafts' -and
            ($body.status -ne 'success' -or -not ($body.PSObject.Properties.Name -contains 'data'))) {
            Write-Host "FAIL $path`: map draft response has the wrong contract"
            $script:failed = $true
        } elseif ($path -like '/api/v1/fms/roads/audit*' -and
            ($body.status -ne 'success' -or -not $body.summary -or
             -not ($body.PSObject.Properties.Name -contains 'data'))) {
            Write-Host "FAIL $path`: road audit response has the wrong contract"
            $script:failed = $true
        } elseif ($path -eq '/api/v1/locations/actual' -and
            ($body.status -ne 'success' -or -not ($body.PSObject.Properties.Name -contains 'data'))) {
            Write-Host "FAIL $path`: actual locations unavailable"
            $script:failed = $true
        } elseif ($path -eq '/api/v1/fleet/live' -and $body.data.Count -gt 0 -and
            -not ($body.data[0].PSObject.Properties.Name -contains 'heading_available')) {
            Write-Host "FAIL $path`: mobile heading availability missing; deploy the new backend"
            $script:failed = $true
        } elseif ($path -eq '/api/v1/comms/status' -and $RequireComms -and $body.enabled -ne $true) {
            Write-Host "FAIL $path`: communications disabled on deployed server"
            $script:failed = $true
        } elseif ($path -like '/api/v1/cabin/hexagon/*' -and
            ($body.status -ne 'success' -or $body.source -ne 'hexagon_read_only' -or
             -not $body.data.unit_name)) {
            Write-Host "FAIL $path`: cabin source projection has the wrong contract"
            $script:failed = $true
        } else {
            Write-Host "OK   $path ($($response.StatusCode))"
        }
    } catch {
        $errorResponse = $_.Exception.Response
        $status = if ($errorResponse) { [int]$errorResponse.StatusCode } else { 0 }
        $blockedByBackend = $errorResponse -and $errorResponse.Headers.Contains('X-Virexa-Denied-By') -and
            $errorResponse.Headers.GetValues('X-Virexa-Denied-By') -contains 'backend-ip-policy'
        if ($requiresAuth -and -not $BearerToken -and $status -eq 401) {
            Write-Host "OK   $path (401, authentication required)"
        } elseif ($status -eq 403 -and $blockedByBackend) {
            Write-Host "FAIL $path (403): backend rejected the proxy/client IP. Check the server warning log and Backend:TrustedClientIps."
            $script:failed = $true
        } elseif ($status -eq 403) {
            Write-Host "FAIL $path (403): denied before backend or by another proxy rule. Check Cloudflare Access/WAF and reverse proxy logs."
            $script:failed = $true
        } elseif ($status -eq 502) {
            Write-Host "FAIL $path (502): Cloudflare/reverse proxy cannot reach the backend origin. Check tunnel/proxy upstream and firewall."
            $script:failed = $true
        } else {
            Write-Host "FAIL $path ($status): $($_.Exception.Message)"
            $script:failed = $true
        }
    }
}

Test-Endpoint '/' $false
Test-Endpoint '/health' $false
Test-Endpoint '/api/v1/site/profile' $true
Test-Endpoint '/api/v1/fleet/live' $true
Test-Endpoint '/api/v1/mtc/live' $true
Test-Endpoint '/api/v1/locations/actual' $true
Test-Endpoint '/api/v1/fms/catalog' $true
Test-Endpoint '/api/v1/fms/map-drafts' $true
Test-Endpoint '/api/v1/fms/roads/audit?scope=priority&limit=10' $true
Test-Endpoint '/api/v1/comms/status' $true
if ($CabinUnit) {
    Test-Endpoint "/api/v1/cabin/hexagon/$([uri]::EscapeDataString($CabinUnit))" $true
}
if ($failed) { exit 1 }
