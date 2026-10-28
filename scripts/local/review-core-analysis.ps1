[CmdletBinding()]
param(
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName = 'current',
    [string]$EnvFile = '.env',
    # Summarize a saved analysis/import output instead of running a new analysis.
    [string]$ReportFile
)

# Runs the read-only core analysis (no -Apply, no database writes), saves the
# full JSON report under migration-data/ (excluded from Git) and prints a
# summary: totals, warnings, uncovered containers and, for every list in the
# report, its size, numeric totals and distribution by season, container or
# reason. The report itself never contains emails, names or payloads.
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($ReportFile) { $lines = Get-Content $ReportFile }
else {
    $local = Join-Path $PSScriptRoot 'local.ps1'
    Write-Host 'Analisi in sola lettura (nessun -Apply, nessuna scrittura nel database)...'
    $failed = $null
    $lines = try { & $local analyze-core -EnvFile $EnvFile -DumpName $DumpName 2>&1 | ForEach-Object { "$_" } } catch { $failed = $_; @() }
    $refused = @($lines | Where-Object { $_ -like 'Core import refused*' })
    if ($failed -or $refused.Count) {
        if ($refused.Count) { $refused } else { "Analisi non riuscita: $failed" }
        exit 1
    }
}
$start = [Array]::IndexOf([string[]]$lines, '{'); $end = [Array]::LastIndexOf([string[]]$lines, '}')
if ($start -lt 0 -or $end -le $start) { throw 'Report JSON non trovato nell''output.' }
$json = ($lines[$start..$end] -join "`n")
if (-not $ReportFile) {
    $saved = Join-Path (Join-Path $Root 'migration-data') "$DumpName-analisi.json"
    Set-Content -Path $saved -Value $json -Encoding utf8
    Write-Host "Report completo salvato in $saved"
}
$report = $json | ConvertFrom-Json

$out = [Collections.Generic.List[string]]::new()
$out.Add('== Totali')
foreach ($property in $report.PSObject.Properties) {
    if ($null -eq $property.Value -or $property.Value -is [string] -or $property.Value -is [ValueType]) { $out.Add("$($property.Name): $($property.Value)") }
}
foreach ($name in 'pendingContainers', 'excludedContainers', 'pendingFields') {
    $items = @($report.$name | Where-Object { $null -ne $_ })
    $out.Add(''); $out.Add("== $name ($($items.Count))")
    foreach ($item in $items) { $out.Add("- $item") }
}
$artifacts = @($report.preservedArtifacts | Where-Object { $null -ne $_ })
$out.Add(''); $out.Add("== preservedArtifacts ($($artifacts.Count))")
foreach ($group in ($artifacts | Group-Object { ($_ -split ':')[0] } | Sort-Object Name)) {
    $out.Add("- $($group.Name): $($group.Count)$(if ($group.Count -eq 1) { " -> $($group.Group[0])" })")
}
$out.Add(''); $out.Add('== Elenchi')
$skip = 'pendingContainers', 'excludedContainers', 'pendingFields', 'preservedArtifacts'
foreach ($property in $report.PSObject.Properties) {
    if ($skip -contains $property.Name -or $property.Value -isnot [Array]) { continue }
    $items = @($property.Value)
    if ($items.Count -eq 0) { $out.Add("$($property.Name): 0"); continue }
    if ($items[0] -isnot [Management.Automation.PSCustomObject]) { $out.Add("$($property.Name): $($items -join ',')"); continue }
    $line = "$($property.Name): $($items.Count) voci"
    $numeric = @($items[0].PSObject.Properties | Where-Object { $_.Value -is [ValueType] -and $_.Value -isnot [bool] -and $_.Name -notmatch '^(seasonIndex|day|year|ordinal|position|status|role)$' } | ForEach-Object Name)
    $sums = foreach ($field in $numeric) { "$field=$((($items | Measure-Object -Property $field -Sum).Sum))" }
    if ($sums) { $line += " | totali: $($sums -join ', ')" }
    foreach ($field in 'reason', 'container', 'seasonIndex') {
        if ($items[0].PSObject.Properties[$field]) {
            $line += " | per $field`: " + ((@($items | Group-Object $field | Sort-Object { if ($field -eq 'seasonIndex') { [int]$_.Name } else { $_.Name } } | ForEach-Object { "$($_.Name)x$($_.Count)" })) -join ', ')
        }
    }
    $out.Add($line)
}
$text = $out -join "`n"
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
