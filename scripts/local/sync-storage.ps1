[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$EnvFile = '.env',
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName,
    [switch]$Resume,
    [switch]$SourceQuiesced,
    [switch]$UseExistingDump,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [IO.Path]::IsPathRooted($EnvFile)) { $EnvFile = Join-Path $Root $EnvFile }
if (-not (Test-Path $EnvFile -PathType Leaf)) { throw 'Env file not found.' }
if ($UseExistingDump -and ($Resume -or $SourceQuiesced)) { throw 'An existing dump is not re-exported: do not combine -UseExistingDump with -Resume or -SourceQuiesced.' }
$plan = if ($Apply) { 'Verify/analyze snapshot, stop local API/frontend, import SQL and restore local files (NO reset, NO automatic restart)' } else { 'Export/verify/analyze only; no SQL import or file restore' }
if (-not $PSCmdlet.ShouldProcess("migration-data/$DumpName -> local fantasoccer_local", $plan)) { return }
if ($env:DOCKER_HOST -and $env:DOCKER_HOST -notmatch '^(unix|npipe)://') { throw 'Remote DOCKER_HOST is not supported by this local workflow.' }
$contextJson = & docker context inspect
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the Docker context.' }
$contexts = @(($contextJson -join "`n") | ConvertFrom-Json)
if ($contexts.Count -ne 1 -or $contexts[0].Endpoints.docker.Host -notmatch '^(unix|npipe)://') { throw 'This workflow requires a local Docker socket/named-pipe context.' }
$local = Join-Path $PSScriptRoot 'local.ps1'
if (-not $UseExistingDump) {
    Write-Host 'Source: FANTASOCCER_EXPORT_STORAGE from private env (never printed). Only Blob/Table current-state reads; no changes to Azure.'
    & $local export -EnvFile $EnvFile -DumpName $DumpName -Resume:$Resume -SourceQuiesced:$SourceQuiesced
}
& $local verify -EnvFile $EnvFile -DumpName $DumpName
& $local analyze-core -EnvFile $EnvFile -DumpName $DumpName
if (-not $Apply) {
    Write-Host 'Snapshot analyzed only. Review pendingContainers, pendingFields, preservedArtifacts and manifest consistency. Run again with -UseExistingDump -Apply to import explicitly.'
    return
}
Write-Warning 'Local target only. Stop host debug/dotnet processes too. A live-source export is exploratory, not a consistent cutover. No reset is automatic; imports after app writes are refused.'
# Import SQL BEFORE restoring files, so a refused import cannot overwrite the
# local file store. Any later restore failure leaves applications stopped.
& $local import-core -EnvFile $EnvFile -DumpName $DumpName
& $local restore -EnvFile $EnvFile -DumpName $DumpName
Write-Host 'Local SQL and Azurite updated from the verified snapshot. Review reports before restarting. No provider was called for writes, no notifications sent. This is NOT continuous/bidirectional sync or automatic production cutover.'
