$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Virexaone.FMS.Backend.csproj'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $workspace 'Builds'
$output = Join-Path $buildRoot ('Backend_Windows_x64_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))

New-Item -ItemType Directory -Path $output -Force | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true -p:UseAppHost=true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw "Backend publish failed with exit code $LASTEXITCODE" }

foreach ($name in @('verify_deployment.ps1', 'DEPLOYMENT.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $output $name)
}

if (-not (Test-Path -LiteralPath (Join-Path $output 'Virexaone.FMS.Backend.exe'))) {
    throw 'The Windows executable is missing from the server package.'
}
if (Test-Path -LiteralPath (Join-Path $output 'appsettings.Local.json')) {
    throw 'Private local settings must not be included in the server package.'
}

if (Test-Path -LiteralPath (Join-Path $output 'appsettings.Development.json')) {
    throw 'Development settings must not be included in the server package.'
}
if (Get-ChildItem -LiteralPath $output -Filter '*.pdb' -File) {
    throw 'Debug symbols must not be included in the server package.'
}
Write-Host "Server package ready: $output"
