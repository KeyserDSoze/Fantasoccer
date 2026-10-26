[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$EnvFile = '.env',
    [string]$ConfirmDatabase = ''
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [IO.Path]::IsPathRooted($EnvFile)) { $EnvFile = Join-Path $Root $EnvFile }
if (-not (Test-Path $EnvFile -PathType Leaf)) { throw 'Env file not found.' }
if ($ConfirmDatabase -cne 'fantasoccer_local') {
    throw 'Refused. Pass -ConfirmDatabase fantasoccer_local explicitly. ALL app accounts, groups, codes, OAuth and migration checkpoints in that local database will be removed.'
}
if (-not $PSCmdlet.ShouldProcess('Compose postgres / fantasoccer_local', 'Stop local API/frontend, back up PostgreSQL, then remove ALL application rows (keep schema and EF history)')) { return }

function Invoke-Docker([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker operation failed (exit $LASTEXITCODE). Reset stopped; inspect the local backup before retrying." }
}
# Never aim a maintenance command at a remote Docker daemon, even if it has a
# similarly named Compose project. Only local socket/named-pipe transports.
if ($env:DOCKER_HOST -and $env:DOCKER_HOST -notmatch '^(unix|npipe)://') { throw 'Remote DOCKER_HOST is not supported by this local reset.' }
$contextJson = & docker context inspect
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the Docker context.' }
$contexts = @(($contextJson -join "`n") | ConvertFrom-Json)
if ($contexts.Count -ne 1 -or $contexts[0].Endpoints.docker.Host -notmatch '^(unix|npipe)://') { throw 'Reset requires a local Docker socket/named-pipe context; remote contexts are refused.' }
$Compose = @('compose', '--env-file', $EnvFile, '-f', (Join-Path $Root 'compose.yml'))
if (Test-Path (Join-Path $Root 'local-certificates/localhost.pfx') -PathType Leaf) { $Compose += @('-f', (Join-Path $Root 'compose.https.yml')) }

Write-Host 'Target: local Compose postgres, database/role fantasoccer_local. Stop any host dotnet/debug process before proceeding.'
Invoke-Docker -Arguments ($Compose + @('stop', 'frontend', 'api'))
Invoke-Docker -Arguments ($Compose + @('up', '-d', '--wait', 'postgres'))
$running = & docker @Compose ps --status running --services
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect running services.' }
if (@($running | Where-Object { $_ -in @('migrate', 'import-core', 'storage-restore') }).Count -gt 0) { throw 'Finish the active migration/restore tool before resetting.' }

$BackupDirectory = Join-Path $Root 'migration-data/postgres-backups'
if (-not (Test-Path $BackupDirectory)) { New-Item -ItemType Directory $BackupDirectory > $null }
if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    [IO.File]::SetUnixFileMode($BackupDirectory, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
}
$file = 'before-reset-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N') + '.dump'
$backup = Join-Path $BackupDirectory $file
$temporary = "/tmp/$file"
# Use pg_dump -f + docker cp: PowerShell 5.1 output redirection would corrupt a
# binary custom-format dump. No passwords or connection strings are printed.
Invoke-Docker -Arguments ($Compose + @('exec', '-T', 'postgres', 'pg_dump', '-U', 'fantasoccer_local', '-d', 'fantasoccer_local', '-Fc', '-f', $temporary))
Invoke-Docker -Arguments ($Compose + @('cp', "postgres:$temporary", $backup))
if (-not (Test-Path $backup -PathType Leaf) -or (Get-Item $backup).Length -eq 0) { throw 'Backup is absent/empty. No data reset was performed.' }
if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) { [IO.File]::SetUnixFileMode($backup, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite) }
Invoke-Docker -Arguments ($Compose + @('exec', '-T', 'postgres', 'pg_restore', '--list', $temporary)) | Out-Null
Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'reset-postgres.sql') | & docker @Compose exec -T postgres psql -X --set ON_ERROR_STOP=1 -U fantasoccer_local -d fantasoccer_local
if ($LASTEXITCODE -ne 0) { throw 'SQL reset failed/rolled back. API/frontend remain stopped; the backup was retained.' }
Invoke-Docker -Arguments ($Compose + @('exec', '-T', 'postgres', 'rm', '--', $temporary))
Write-Host "Local PostgreSQL application data cleared. Backup: $backup"
Write-Host 'Schema/EF history, Azurite, certificates, key rings, telemetry and source dumps are preserved. API/frontend remain stopped. Complete the desired import BEFORE logging in or enabling MCP/jobs.'
