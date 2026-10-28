[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName
)

# Read-only survey of the legacy real (Serie A) calendars: for every document the
# season key, the year stored inside it and in its days, days and games, the
# date range, the seasons the dated games belong to (boundary 10 August, as in
# the import) and what the import would do with it. No club labels or scores
# are printed. Nothing is written.
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dump = Join-Path (Join-Path $Root 'migration-data') $DumpName
if (-not (Test-Path (Join-Path $dump 'manifest.json') -PathType Leaf)) { throw 'Dump manifest not found.' }

$sha = [Security.Cryptography.SHA256]::Create()
$manifest = Get-Content -Raw (Join-Path $dump 'manifest.json') | ConvertFrom-Json
function Label([int]$season) { "$(2011 + $season)/$('{0:d2}' -f ((2012 + $season) % 100))" }
function SeasonOf([datetime]$date) { $date.Year - 2011 - $(if ($date.Month -lt 8 -or ($date.Month -eq 8 -and $date.Day -lt 10)) { 1 } else { 0 }) }

$entry = $manifest.containers | Where-Object name -eq 'realcalendar'
$rows = foreach ($blob in $(if ($entry) { @($entry.blobs) } else { @() })) {
    $file = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes("realcalendar`0$($blob.name)")) | ForEach-Object { $_.ToString('x2') })
    $doc = Get-Content -Raw (Join-Path (Join-Path $dump 'blobs') "$file.bin") | ConvertFrom-Json
    $key = if ($null -ne $doc.Key) { $doc.Key } else { $doc.k }
    $value = if ($null -ne $doc.Value) { $doc.Value } else { $doc.v }
    $days = @(foreach ($day in $value.d) { $day })
    $games = @(foreach ($day in $days) { foreach ($game in $day.g) { $game } })
    $dates = @(foreach ($game in $games) { if ($null -ne $game.d -and "$($game.d)" -ne '') { [datetime]$game.d } }) | Sort-Object
    $seasons = @(foreach ($date in $dates) { SeasonOf $date })
    $inside = @($seasons | Where-Object { $_ -eq [int]$key }).Count
    $numbers = @(foreach ($day in $days) { [int]$day.a })
    $badDays = @($numbers | Where-Object { $_ -lt 1 -or $_ -gt 38 }).Count + ($numbers.Count - @($numbers | Sort-Object -Unique).Count)
    # Same order of checks as the import. Year 0 (never stored) is accepted only
    # when every day carries the key's season (operator decision).
    $confirmed = [int]$value.y -eq 0 -and $days.Count -gt 0 -and @($days | Where-Object { [int]$_.y -ne [int]$key }).Count -eq 0
    $outcome = if ([int]$value.y -ne [int]$key -and -not $confirmed) { 'RIFIUTATO: calendar_season_mismatch (anno interno diverso dalla chiave)' }
        elseif ($seasons.Count -gt 0 -and $inside -eq 0) { 'escluso: tutte le partite datate sono di un''altra stagione' }
        elseif ($inside -lt $seasons.Count) { 'RIFIUTATO: real_calendar_mixed_seasons' }
        elseif ($badDays -gt 0) { 'RIFIUTATO: duplicate_real_matchday' }
        else { 'importato' }
    if ($confirmed -and [int]$key -ne 0) { $outcome += ' (anno interno assente, confermato dalle giornate)' }
    [pscustomobject]@{
        Chiave = "$key ($(Label $key))"; AnnoInterno = "$($value.y)$(if ($null -ne $value.y -and [int]$value.y -ge 1 -and [int]$value.y -le 40) { " ($(Label $value.y))" })"
        AnniGiornate = ((@($days | ForEach-Object { "$($_.y)" }) | Group-Object | Sort-Object Name | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ',')
        Giornate = $days.Count; Partite = $games.Count; Datate = $dates.Count
        Date = $(if ($dates.Count) { "$($dates[0].ToString('yyyy-MM-dd'))..$($dates[-1].ToString('yyyy-MM-dd'))" } else { '-' })
        StagioniDalleDate = ((@($seasons | Group-Object | Sort-Object { [int]$_.Name } | ForEach-Object { "$($_.Name) ($(Label $_.Name))x$($_.Count)" })) -join ', ')
        GiornateAnomale = $badDays; Esito = $outcome; Archivio = $file.Substring(0, 12)
    }
}
$columns = 'Chiave', 'AnnoInterno', 'AnniGiornate', 'Giornate', 'Partite', 'Datate', 'Date', 'StagioniDalleDate', 'GiornateAnomale', 'Esito', 'Archivio'
$lines = @($columns -join ' | ')
foreach ($row in ($rows | Sort-Object { [int](($_.Chiave -split ' ')[0]) })) { $lines += (@(foreach ($column in $columns) { [string]$row.$column }) -join ' | ') }
$text = "realcalendar : $(@($rows).Count) documenti`n" + ($lines -join "`n")
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
