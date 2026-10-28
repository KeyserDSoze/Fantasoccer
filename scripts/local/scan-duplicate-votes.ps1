[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName
)

# Read-only survey of duplicate player votes in live/notification/official
# documents. It repeats the import's legacy "6 d'ufficio" classification
# (paired actual vote, postponed calendar game, same game opponent) and shows
# in detail only the duplicates that would still refuse the import. Football
# player names, clubs and votes are printed locally; nothing is written.
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dump = Join-Path (Join-Path $Root 'migration-data') $DumpName
if (-not (Test-Path (Join-Path $dump 'manifest.json') -PathType Leaf)) { throw 'Dump manifest not found.' }

$source = Get-Content -Raw (Join-Path $Root 'src/Fantasoccer.Domain/SerieA/Calendar/FantacalcioClubCodes.cs')
function Section([string]$field) {
    $start = $source.IndexOf($field); $end = $source.IndexOf('}.ToFrozenDictionary', $start)
    if ($start -lt 0 -or $end -lt 0) { throw "Catalogue section $field not found." }
    $map = @{}
    foreach ($match in [regex]::Matches($source.Substring($start, $end - $start), '\["([^"]+)"\]\s*=\s*"([^"]+)"')) { $map[$match.Groups[1].Value] = $match.Groups[2].Value }
    $map
}
$codes = Section 's_codes = '
$aliases = Section 's_aliases = '
$variants = Section 's_codeVariants = '
foreach ($alias in @($aliases.Keys)) { $codes[$alias] = $codes[$aliases[$alias]] }
function ClubCode($team) {
    # Same result as KeyUtils.GetClubKey, or $null when it would be refused.
    if ($null -eq $team -or [string]::IsNullOrEmpty([string]$team.n)) { return $null }
    $name = [string]$team.n; $abbr = [string]$team.a; $verified = $codes[$name.Trim()]
    if ([string]::IsNullOrWhiteSpace($abbr)) { if ($verified) { return $verified }; if ($name -cmatch '^[A-Za-z]{3}$') { return $name.ToLowerInvariant() }; return $null }
    if ($verified -and $variants[$abbr.ToLowerInvariant()] -ceq $verified) { return $verified }
    if ($abbr -cnotmatch '^[A-Za-z]{3}$') { return $null }
    if ($verified -and $abbr.ToLowerInvariant() -cne $verified) { return $null }
    return $abbr.ToLowerInvariant()
}
function PlayerKey([string]$name) { $name.ToLowerInvariant() -replace '[^a-z]', '' }
function Flag($value) { $value -eq $true }
$playerFields = 'n', 't', 'r', 'a', 'vh', 'v'
$voteFields = 'r', 'v', 'i', 'g', 'p', 'a', 's', 'd', 'w', 'o', 't', 'c', 'h', 'u', 'n', 'j'
$eventFields = 'g', 'p', 'a', 's', 'd', 'w', 'o', 't'
function SubstituteVoteShape($sample) {
    $vote = $sample.v
    if ($null -eq $vote -or $vote -isnot [Management.Automation.PSCustomObject]) { return $false }
    if (@($sample.PSObject.Properties.Name | Where-Object { $playerFields -cnotcontains $_ }).Count -gt 0) { return $false }
    if (@($vote.PSObject.Properties.Name | Where-Object { $voteFields -cnotcontains $_ }).Count -gt 0) { return $false }
    if ($null -eq $vote.PSObject.Properties['n'] -or $vote.n -ne $false) { return $false }
    if ([int]$vote.r -lt -1 -or [int]$vote.r -gt 3) { return $false }
    if (-not (Flag $vote.h) -or -not (Flag $vote.i) -or [double]$vote.v -ne 6 -or (Flag $vote.u) -or (Flag $vote.c) -or (Flag $vote.j)) { return $false }
    foreach ($field in $eventFields) { if ([int]$vote.$field -ne 0) { return $false } }
    return $true
}
function PoliticalShape($sample) { (SubstituteVoteShape $sample) -and [int]$sample.r -ge -1 -and [int]$sample.r -le 3 -and [int]$sample.v.r -eq [int]$sample.r }
# Operator decision: a 6 d'ufficio given to an inactive entry is not a vote.
function InactiveSubstitute($sample) { (SubstituteVoteShape $sample) -and -not (Flag $sample.a) }
function SameIdentity($first, $other) {
    # Same rule as the import: name ignoring case, roles and the same club.
    if ([string]$first.n -ine [string]$other.n -or [int]$first.r -ne [int]$other.r -or [int]$first.v.r -ne [int]$other.v.r) { return $false }
    if ($null -eq $first.t -or $null -eq $other.t) { return ($null -eq $first.t -and $null -eq $other.t) }
    $code = ClubCode $first.t
    return [bool]$code -and $code -eq (ClubCode $other.t)
}
function Events($vote) {
    $list = @(foreach ($field in $eventFields) { if ([int]$vote.$field -ne 0) { "$field$($vote.$field)" } })
    if (Flag $vote.c) { $list += 'mvp' }; if (Flag $vote.u) { $list += 'uscito' }; if (Flag $vote.j) { $list += 'infortunio' }
    $list -join ' '
}
function SeasonOf([datetime]$date) { $date.Year - 2011 - $(if ($date.Month -lt 8 -or ($date.Month -eq 8 -and $date.Day -lt 10)) { 1 } else { 0 }) }

$sha = [Security.Cryptography.SHA256]::Create()
$manifest = Get-Content -Raw (Join-Path $dump 'manifest.json') | ConvertFrom-Json
function Documents($container) {
    $entry = $manifest.containers | Where-Object name -eq $container
    foreach ($blob in $(if ($entry) { @($entry.blobs) } else { @() })) {
        $file = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes("$container`0$($blob.name)")) | ForEach-Object { $_.ToString('x2') })
        $doc = Get-Content -Raw (Join-Path (Join-Path $dump 'blobs') "$file.bin") | ConvertFrom-Json
        [pscustomobject]@{ Key = $(if ($null -ne $doc.Key) { $doc.Key } else { $doc.k }); Value = $(if ($null -ne $doc.Value) { $doc.Value } else { $doc.v }); File = $file.Substring(0, 12) }
    }
}

# Postponed games and opponents from the calendars the import keeps.
$postponed = @{}; $opponents = @{}
foreach ($doc in Documents 'realcalendar') {
    $year = [int]$doc.Key
    $games = @(foreach ($day in @($doc.Value.d)) { @($day.g) })
    $seasons = @(foreach ($game in $games) { if ($game.d) { SeasonOf ([datetime]$game.d) } })
    if ($seasons.Count -gt 0 -and @($seasons | Where-Object { $_ -eq $year }).Count -ne $seasons.Count) { continue }
    foreach ($day in @($doc.Value.d)) {
        foreach ($game in @($day.g)) {
            $homeClub = ClubCode $game.h; $awayClub = ClubCode $game.a
            if (-not $homeClub -or -not $awayClub) { continue }
            $opponents["$year/$($day.a)/$homeClub"] = $awayClub; $opponents["$year/$($day.a)/$awayClub"] = $homeClub
            if ((Flag $game.e) -or (Flag $game.wp)) { $postponed["$year/$($day.a)/$homeClub"] = $true; $postponed["$year/$($day.a)/$awayClub"] = $true }
        }
    }
}

$rows = [Collections.Generic.List[object]]::new(); $groups = 0; $separated = 0; $blocking = 0; $inactiveRemoved = 0
foreach ($container in 'live', 'notification', 'official') {
    $count = 0
    foreach ($doc in Documents $container) {
        $count++
        $year = [int]$doc.Key.y; $dayNumber = [int]$doc.Key.d
        $samples = @($doc.Value.p)
        $byKey = @{}
        for ($i = 0; $i -lt $samples.Count; $i++) {
            $sample = $samples[$i]
            if ($null -eq $sample -or [string]::IsNullOrWhiteSpace([string]$sample.n)) { continue }
            if ($container -eq 'official' -and (InactiveSubstitute $sample)) { $inactiveRemoved++; continue }
            $key = PlayerKey ([string]$sample.n)
            if (-not $key) { continue }
            if (-not $byKey.ContainsKey($key)) { $byKey[$key] = [Collections.Generic.List[int]]::new() }
            $byKey[$key].Add($i)
        }
        $duplicates = @($byKey.Keys | Where-Object { $byKey[$_].Count -gt 1 })
        if ($duplicates.Count -eq 0) { continue }
        # Same classification as the import (official documents only).
        $legacy = @{}
        if ($container -eq 'official') {
            $attested = @{}
            foreach ($key in $duplicates) {
                $indexes = $byKey[$key]
                foreach ($i in $indexes) {
                    if (-not (PoliticalShape $samples[$i])) { continue }
                    $code = ClubCode $samples[$i].t
                    $paired = @($indexes | Where-Object { $_ -ne $i -and -not (PoliticalShape $samples[$_]) -and (Flag $samples[$_].v.h) -and (Flag $samples[$_].v.i) -and (Flag $samples[$_].v.n) -and (SameIdentity $samples[$i] $samples[$_]) }).Count -gt 0
                    if ($paired) { $legacy[$i] = $true }
                    if ($code -and ($paired -or $postponed["$year/$dayNumber/$code"])) { $attested[$code] = $true }
                }
            }
            foreach ($code in @($attested.Keys)) { if ($opponents["$year/$dayNumber/$code"]) { $attested[$opponents["$year/$dayNumber/$code"]] = $true } }
            foreach ($key in $duplicates) { foreach ($i in $byKey[$key]) { $code = ClubCode $samples[$i].t; if ($code -and (PoliticalShape $samples[$i]) -and $attested[$code]) { $legacy[$i] = $true } } }
        }
        foreach ($key in $duplicates) {
            $groups++
            $indexes = @($byKey[$key])
            $actual = @($indexes | Where-Object { -not $legacy[$_] })
            # The import reads actual entries first; every other entry must
            # share the first one's identity (name, roles and club).
            $ordered = @($actual + @($indexes | Where-Object { $legacy[$_] }))
            $incompatible = @($ordered | Select-Object -Skip 1 | Where-Object { -not (SameIdentity $samples[$ordered[0]] $samples[$_]) }).Count -gt 0
            if ($actual.Count -le 1 -and -not $incompatible) { $separated++; continue }
            $blocking++
            $reason = if ($actual.Count -gt 1) { "$($actual.Count) voti reali" } else { 'stessa chiave con altra identita' }
            foreach ($i in $indexes) {
                $sample = $samples[$i]; $vote = $sample.v; $code = ClubCode $sample.t
                $rows.Add([pscustomobject]@{
                    Gruppo = "$container $year/$dayNumber $key"; Motivo = $reason; Posizione = "$($doc.File) p[$i]"; Nome = "[$($sample.n)]"
                    Club = $(if ($null -eq $sample.t) { '(nessuno)' } elseif ($code) { $code } else { "?[$($sample.t.n)/$($sample.t.a)]" })
                    Ruolo = $sample.r; Voto = $vote.v; HaVoto = Flag $vote.h; Finale = Flag $vote.i; Entrato = Flag $vote.n; Eventi = Events $vote
                    Forma6 = PoliticalShape $sample; Separata = [bool]$legacy[$i]
                    Rinvio = $(if ($code) { [bool]$postponed["$year/$dayNumber/$code"] } else { '' })
                })
            }
        }
    }
    Write-Host "$container : $count documenti"
}
$summary = "6 d'ufficio di giocatori inattivi tolti: $inactiveRemoved; gruppi duplicati: $groups; risolti come 6 d'ufficio: $separated; bloccanti: $blocking"
$lines = foreach ($group in $rows | Group-Object Gruppo) {
    "== $($group.Name): $($group.Group[0].Motivo)"
    foreach ($row in $group.Group) {
        "   $($row.Posizione) $($row.Nome) club=$($row.Club) ruolo=$($row.Ruolo) voto=$($row.Voto) haVoto=$($row.HaVoto) finale=$($row.Finale) entrato=$($row.Entrato) eventi=[$($row.Eventi)] forma6=$($row.Forma6) separata=$($row.Separata) rinvio=$($row.Rinvio)"
    }
}
$text = (@($summary) + $(if ($rows.Count -eq 0) { @('Nessun duplicato bloccante.') } else { @($lines) })) -join "`n"
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
