param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$canonical = (Resolve-Path (Join-Path $PSScriptRoot '..\Backend')).Path
$canonicalTests = (Resolve-Path (Join-Path $PSScriptRoot '..\Backend.Tests')).Path
$mirror = Join-Path $PSScriptRoot 'Backend'
$mirrorTests = Join-Path $PSScriptRoot 'Backend.Tests'

$backendFiles = @(
    'Program.cs', 'appsettings.json', 'DEPLOYMENT.md',
    'publish_backend_server.ps1', 'verify_deployment.ps1',
    'Virexaone.FMS.Backend.csproj', 'Virexaone.FMS.Backend.http'
)
foreach ($directory in @('Hubs', 'Models', 'Services', 'Utils', 'Properties', 'Migrations', 'wwwroot')) {
    $root = Join-Path $canonical $directory
    $backendFiles += Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        [System.IO.Path]::GetRelativePath($canonical, $_.FullName)
    }
}
$testFiles = @('SiteConfigurationTests.cs', 'Virexaone.FMS.Backend.Tests.csproj')
$outdated = @()

function Compare-Source([string]$sourceRoot, [string]$mirrorRoot, [string[]]$files) {
    foreach ($relative in $files) {
        $source = Join-Path $sourceRoot $relative
        $target = Join-Path $mirrorRoot $relative
        $matches = (Test-Path -LiteralPath $target) -and
            ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -eq
             (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash)
        if ($matches) { continue }
        if ($Apply) {
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
            Write-Host "Synced $relative"
        } else {
            $script:outdated += $target
        }
    }
}

Compare-Source $canonical $mirror $backendFiles
Compare-Source $canonicalTests $mirrorTests $testFiles
if (Test-Path -LiteralPath (Join-Path $mirror 'appsettings.Local.json')) {
    throw 'Private appsettings.Local.json must never be placed in the Git mirror.'
}
if ($outdated.Count -gt 0) {
    $outdated | ForEach-Object { Write-Host "Outdated: $_" }
    throw "Backend mirror is out of sync ($($outdated.Count) files). Run ./sync_backend_source.ps1 -Apply."
}
Write-Host 'Backend source mirror is current. Private settings and build output are excluded.'
