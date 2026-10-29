[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$EnvFile = '.env',
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName,
    [switch]$Resume,
    [switch]$SourceQuiesced,
    [switch]$UseExistingDump,
    [switch]$Apply,
    [switch]$IncludeTables,
    [switch]$ResetFirst,
    [string]$ConfirmDatabase = ''
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [IO.Path]::IsPathRooted($EnvFile)) { $EnvFile = Join-Path $Root $EnvFile }
if (-not (Test-Path $EnvFile -PathType Leaf)) { throw 'Env file not found.' }
if ($UseExistingDump -and ($Resume -or $SourceQuiesced)) { throw 'An existing dump is not re-exported: do not combine -UseExistingDump with -Resume or -SourceQuiesced.' }
if ($ResetFirst -and -not $Apply) { throw '-ResetFirst requires -Apply.' }
if ($ResetFirst -and $ConfirmDatabase -cne 'fantasoccer_local') { throw 'Refused. -ResetFirst also needs -ConfirmDatabase fantasoccer_local: ALL local application data will be backed up and removed before the import.' }
$plan = if ($Apply) { 'Verify/analyze snapshot, stop local API/frontend, import SQL and restore local files (' + $(if ($ResetFirst) { 'after a backed-up local reset' } else { 'NO reset' }) + ', NO automatic restart)' } else { 'Export/verify/analyze only; no SQL import or file restore' }
if (-not $PSCmdlet.ShouldProcess("migration-data/$DumpName -> local fantasoccer_local", $plan)) { return }
if ($env:DOCKER_HOST -and $env:DOCKER_HOST -notmatch '^(unix|npipe)://') { throw 'Remote DOCKER_HOST is not supported by this local workflow.' }
$contextJson = & docker context inspect
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the Docker context.' }
$contexts = @(($contextJson -join "`n") | ConvertFrom-Json)
if ($contexts.Count -ne 1 -or $contexts[0].Endpoints.docker.Host -notmatch '^(unix|npipe)://') { throw 'This workflow requires a local Docker socket/named-pipe context.' }
$local = Join-Path $PSScriptRoot 'local.ps1'
if (-not $UseExistingDump) {
    Write-Host 'Source: FANTASOCCER_EXPORT_STORAGE from private env (never printed). Blob-only by default; Table download requires -IncludeTables. No changes to Azure.'
    & $local export -EnvFile $EnvFile -DumpName $DumpName -Resume:$Resume -SourceQuiesced:$SourceQuiesced -IncludeTables:$IncludeTables
}
& $local verify -EnvFile $EnvFile -DumpName $DumpName
& $local analyze-core -EnvFile $EnvFile -DumpName $DumpName
if (-not $Apply) {
    Write-Host 'Snapshot analyzed only. Review pendingContainers, pendingFields, preservedArtifacts and manifest consistency. Run again with -UseExistingDump -Apply to import explicitly.'
    return
}
Write-Warning 'Local target only. Stop host debug/dotnet processes too. A live-source export is exploratory, not a consistent cutover. No reset is automatic; imports after app writes are refused.'
if ($ResetFirst) {
    # Only after the dump verified and analyzed successfully; the reset backs up first.
    & (Join-Path $PSScriptRoot 'reset-postgres.ps1') -EnvFile $EnvFile -ConfirmDatabase $ConfirmDatabase -Confirm:$false
}
# Import SQL BEFORE restoring files, so a refused import cannot overwrite the
# local file store. Any later restore failure leaves applications stopped.
& $local import-core -EnvFile $EnvFile -DumpName $DumpName
& $local restore -EnvFile $EnvFile -DumpName $DumpName -IncludeTables:$IncludeTables
Write-Host 'Local SQL and Azurite updated from the verified snapshot. Review reports before restarting. No provider was called for writes, no notifications sent. This is NOT continuous/bidirectional sync or automatic production cutover.'
