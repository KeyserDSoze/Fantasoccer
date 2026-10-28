[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName
)

# Read-only survey of the real-club labels the core import still has to
# validate. It applies the same rules as KeyUtils.GetClubKey, reading names,
# aliases and provider code variants from FantacalcioClubCodes.cs, and prints
# club labels only (never players, owners or votes). Nothing is written.
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
$knownCodes = @{}; foreach ($code in @($codes.Values)) { $knownCodes[$code] = $true }
$version = [regex]::Match($source, 'Version = "([^"]+)"').Groups[1].Value
Write-Host "Catalogo $version : $($knownCodes.Count) club, $($aliases.Count) alias, $($variants.Count) varianti di sigla"

function Problem([string]$name, [string]$abbr) {
    if ([string]::IsNullOrEmpty($name)) { return 'club senza nome' }
    $verified = $codes[$name.Trim()]
    if ([string]::IsNullOrWhiteSpace($abbr)) {
        if ($verified -or $name -cmatch '^[A-Za-z]{3}$') { return $null }
        return 'nome non verificato senza sigla'
    }
    if ($verified -and $variants[$abbr.ToLowerInvariant()] -ceq $verified) { return $null }
    if ($abbr -cnotmatch '^[A-Za-z]{3}$') { return 'sigla non di 3 lettere' }
    if ($verified -and $abbr.ToLowerInvariant() -cne $verified) { return 'sigla diversa dal nome verificato' }
    # Not refused by the import: an unlisted name carrying a known club's code
    # is merged into that club, so check that it really is the same team.
    if (-not $verified -and $knownCodes[$abbr.ToLowerInvariant()]) { return 'da verificare: sigla di un club noto con altro nome' }
    return $null
}
$found = [Collections.Generic.List[object]]::new()
function Check($team, $container, $year, $file, $path) {
    $reason = Problem $team.n $team.a
    if ($reason) { $found.Add([pscustomobject]@{ Container = $container; Anno = "$year"; Nome = "[$($team.n)]"; Sigla = "[$($team.a)]"; Motivo = $reason; Posizione = "$file $path" }) }
}
function Walk($node, $path, $container, $year, $file) {
    if ($null -eq $node) { return }
    if ($node -is [Array]) { for ($i = 0; $i -lt $node.Count; $i++) { Walk $node[$i] "$path[$i]" $container $year $file }; return }
    if ($node -isnot [Management.Automation.PSCustomObject]) { return }
    # Real clubs sit under the "t" member of player-shaped objects (name + role).
    $player = $node.PSObject.Properties['n'] -and $node.PSObject.Properties['r']
    foreach ($property in $node.PSObject.Properties) {
        $value = $property.Value
        if ($player -and $property.Name -ieq 't' -and $value -is [Management.Automation.PSCustomObject]) { Check $value $container $year $file "$path.$($property.Name)" }
        else { Walk $value "$path.$($property.Name)" $container $year $file }
    }
}
function SeasonOf([datetime]$date) { $date.Year - 2011 - $(if ($date.Month -lt 8 -or ($date.Month -eq 8 -and $date.Day -lt 10)) { 1 } else { 0 }) }

$sha = [Security.Cryptography.SHA256]::Create()
$manifest = Get-Content -Raw (Join-Path $dump 'manifest.json') | ConvertFrom-Json
$players = 'live', 'notification', 'official', 'chancedrealplayerwrapper', 'statplayerswrapper', 'team', 'dailyteams', 'openingteams', 'openingdailyteams'
$generic = 'marketwrapper', 'calendar', 'openingstandings', 'playedgame', 'auction', 'halloffame', 'pushnotificationsent'
foreach ($container in @('realcalendar', 'realrank') + $players + $generic) {
    $entry = $manifest.containers | Where-Object name -eq $container
    $blobs = if ($entry) { @($entry.blobs) } else { @() }
    Write-Host "$container : $($blobs.Count) documenti"
    foreach ($blob in $blobs) {
        $file = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes("$container`0$($blob.name)")) | ForEach-Object { $_.ToString('x2') })
        $doc = Get-Content -Raw (Join-Path (Join-Path $dump 'blobs') "$file.bin") | ConvertFrom-Json
        $key = if ($null -ne $doc.Key) { $doc.Key } else { $doc.k }
        $value = if ($null -ne $doc.Value) { $doc.Value } else { $doc.v }
        $year = if ($null -ne $key -and $key.PSObject.Properties['y']) { $key.y } elseif ($key -is [ValueType]) { $key } else { '' }
        $short = $file.Substring(0, 12)
        if ($container -eq 'realcalendar') {
            # Same rule as the import: another season's calendar is not read.
            $games = @(foreach ($day in @($value.d)) { @($day.g) })
            $seasons = @(foreach ($game in $games) { if ($game.d) { SeasonOf ([datetime]$game.d) } })
            $inside = @($seasons | Where-Object { $_ -eq [int]$year }).Count
            if ($seasons.Count -gt 0 -and $inside -eq 0) { continue }
            if ($inside -lt $seasons.Count) { $found.Add([pscustomobject]@{ Container = $container; Anno = "$year"; Nome = ''; Sigla = ''; Motivo = 'calendario con piu stagioni'; Posizione = $short }); continue }
            $days = @($value.d)
            for ($d = 0; $d -lt $days.Count; $d++) { $list = @($days[$d].g); for ($g = 0; $g -lt $list.Count; $g++) { foreach ($side in 'h', 'a') { Check $list[$g].$side $container $year $short "d[$d].g[$g].$side" } } }
        }
        elseif ($container -eq 'realrank') {
            $teams = @($value.r)
            for ($t = 0; $t -lt $teams.Count; $t++) { Check ([pscustomobject]@{ n = $teams[$t].n; a = $null }) $container $year $short "r[$t]" }
        }
        elseif ($players -contains $container) {
            $list = @($value.p)
            for ($p = 0; $p -lt $list.Count; $p++) { if ($null -ne $list[$p] -and $null -ne $list[$p].t) { Check $list[$p].t $container $year $short "p[$p].t" } }
        }
        else { Walk $value '' $container $year $short }
    }
}
$text = if ($found.Count -eq 0) { 'Nessun problema sui club trovato.' } else {
    $found | Group-Object Container, Nome, Sigla, Motivo | ForEach-Object {
        [pscustomobject]@{ Container = $_.Group[0].Container; Nome = $_.Group[0].Nome; Sigla = $_.Group[0].Sigla; Motivo = $_.Group[0].Motivo; Volte = $_.Count
            Anni = (@($_.Group.Anno | Sort-Object -Unique) -join ','); Esempio = $_.Group[0].Posizione }
    } | Sort-Object Motivo, Container, Nome | Format-Table -AutoSize -Wrap | Out-String -Width 1000
}
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
