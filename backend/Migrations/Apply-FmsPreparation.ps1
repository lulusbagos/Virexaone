param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$backend = Split-Path -Parent $PSScriptRoot
$runtime = Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $runtime) { throw 'ASP.NET Core runtime not found.' }
Add-Type -Path (Join-Path $runtime.FullName 'Microsoft.Extensions.Logging.Abstractions.dll')
Add-Type -Path (Join-Path $backend 'bin\Release\net10.0\Npgsql.dll')

$settings = Get-Content -LiteralPath (Join-Path $backend 'appsettings.json') -Raw | ConvertFrom-Json
$localPath = Join-Path $backend 'appsettings.Local.json'
$local = if (Test-Path -LiteralPath $localPath) { Get-Content -LiteralPath $localPath -Raw | ConvertFrom-Json } else { $null }
$fms = $settings.FmsSettings
$password = if ($env:FmsSettings__DbPassword) { $env:FmsSettings__DbPassword } elseif ($local) { $local.FmsSettings.DbPassword } else { $null }
if (-not $password) { throw 'Missing FmsSettings:DbPassword in environment or local settings.' }

$connectionString = [Npgsql.NpgsqlConnectionStringBuilder]::new()
$connectionString.Host = if ($env:FmsSettings__DbHost) { $env:FmsSettings__DbHost } else { $fms.DbHost }
$connectionString.Port = if ($env:FmsSettings__DbPort) { [int]$env:FmsSettings__DbPort } else { [int]$fms.DbPort }
$connectionString.Database = if ($env:FmsSettings__DbName) { $env:FmsSettings__DbName } else { $fms.DbName }
$connectionString.Username = if ($env:FmsSettings__DbUser) { $env:FmsSettings__DbUser } else { $fms.DbUser }
$connectionString.Password = $password
$connectionString.Timeout = 10
$connectionString.CommandTimeout = 120

$migrations = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.sql' -File |
    Where-Object Name -Match '^\d{3}_.*\.sql$' | Sort-Object Name)
if ($migrations.Count -ne 4) { throw "Expected 4 FMS migrations, found $($migrations.Count)." }

$connection = [Npgsql.NpgsqlConnection]::new($connectionString.ConnectionString)
try {
    $connection.Open()
    $transaction = $connection.BeginTransaction()
    try {
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandText = "SELECT pg_advisory_xact_lock(hashtext('astha-fms-migrations'))"
        [void]$command.ExecuteScalar()

        $command.CommandText = "SELECT to_regclass('public.tbl_m_fms_schema_migration_astha') IS NOT NULL"
        $ledgerExists = [bool]$command.ExecuteScalar()
        $applied = @{}
        if ($ledgerExists) {
            $command.CommandText = 'SELECT version, checksum_sha256 FROM tbl_m_fms_schema_migration_astha'
            $reader = $command.ExecuteReader()
            while ($reader.Read()) { $applied[$reader.GetString(0)] = $reader.GetString(1) }
            $reader.Close()
        }

        $pending = 0
        foreach ($migration in $migrations) {
            $version = [IO.Path]::GetFileNameWithoutExtension($migration.Name)
            $checksum = (Get-FileHash -LiteralPath $migration.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($applied.ContainsKey($version)) {
                if ($applied[$version] -ne $checksum) { throw "Migration checksum mismatch: $version" }
                continue
            }
            $command.CommandText = Get-Content -LiteralPath $migration.FullName -Raw
            [void]$command.ExecuteNonQuery()
            $record = $connection.CreateCommand()
            $record.Transaction = $transaction
            $record.CommandText = 'INSERT INTO tbl_m_fms_schema_migration_astha(version, checksum_sha256) VALUES (@version, @checksum)'
            [void]$record.Parameters.AddWithValue('version', $version)
            [void]$record.Parameters.AddWithValue('checksum', $checksum)
            [void]$record.ExecuteNonQuery()
            $pending++
        }

        $command.CommandText = @'
SELECT (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name LIKE '%\_astha' ESCAPE '\'),
       (SELECT count(*) FROM tbl_m_fms_menu_astha),
       (SELECT count(*) FROM tbl_m_fms_menu_astha WHERE is_visible OR is_enabled),
       (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name LIKE '%\_hexagon' ESCAPE '\'),
       (SELECT count(*) FROM tbl_m_fms_schema_migration_astha),
       (SELECT count(*) FROM pg_constraint WHERE conname = 'fk_fms_audit_command_site_astha' AND contype = 'f')
'@
        $reader = $command.ExecuteReader()
        [void]$reader.Read()
        $tableCount = $reader.GetInt64(0)
        $menuCount = $reader.GetInt64(1)
        $activeMenus = $reader.GetInt64(2)
        $hexagonTables = $reader.GetInt64(3)
        $migrationCount = $reader.GetInt64(4)
        $siteScopedAudit = $reader.GetInt64(5)
        $reader.Close()
        if ($tableCount -ne 27 -or $menuCount -ne 34 -or $activeMenus -ne 0 -or
            $migrationCount -ne 4 -or $siteScopedAudit -ne 1) {
            throw "FMS validation failed: tables=$tableCount menus=$menuCount active=$activeMenus migrations=$migrationCount audit_fk=$siteScopedAudit"
        }

        if ($Apply -and $pending -gt 0) {
            $transaction.Commit()
            Write-Host "Applied $pending migration(s). Verified: $tableCount Astha tables, $menuCount hidden menus, $hexagonTables Hexagon tables."
        } else {
            $transaction.Rollback()
            $message = if ($pending -eq 0) { 'Already applied' } else { 'Check passed; changes rolled back' }
            Write-Host "$message. Verified: $tableCount Astha tables, $menuCount hidden menus, $hexagonTables Hexagon tables."
        }
    } catch {
        try { $transaction.Rollback() } catch {}
        throw
    } finally {
        $transaction.Dispose()
    }
} finally {
    $connection.Dispose()
}
