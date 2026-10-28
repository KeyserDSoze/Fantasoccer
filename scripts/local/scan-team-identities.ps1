[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName
)

# Read-only survey of every place where the core import has to find the annual
# fantasy team of a legacy owner (unresolved_historical_team_identity): rosters,
# market trades and votes, fantasy calendars, standings, opening standings and
# auctions. It mirrors the import's rules and prints only containers, years,
# reasons, counts, short archive references and owner pseudonyms (never emails,
# owner labels or team names). Nothing is written.
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dump = Join-Path (Join-Path $Root 'migration-data') $DumpName
if (-not (Test-Path (Join-Path $dump 'manifest.json') -PathType Leaf)) { throw 'Dump manifest not found.' }

$sha = [Security.Cryptography.SHA256]::Create()
function Hex([string]$text) { -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) | ForEach-Object { $_.ToString('x2') }) }
function Ordinal { [hashtable]::new([StringComparer]::Ordinal) }
$manifest = Get-Content -Raw (Join-Path $dump 'manifest.json') | ConvertFrom-Json
function Documents($container) {
    $entry = $manifest.containers | Where-Object name -eq $container
    foreach ($blob in $(if ($entry) { @($entry.blobs) } else { @() })) {
        $file = Hex "$container`0$($blob.name)"
        $doc = Get-Content -Raw (Join-Path (Join-Path $dump 'blobs') "$file.bin") | ConvertFrom-Json
        [pscustomobject]@{ Key = $(if ($null -ne $doc.Key) { $doc.Key } else { $doc.k }); Value = $(if ($null -ne $doc.Value) { $doc.Value } else { $doc.v }); File = $file.Substring(0, 12) }
    }
}

# Same identity as the import: a valid email (trimmed, lowercase) or, with
# IMPORT_LEGACY_NAMED_OWNERS, the exact label within its group.
function OwnerKey($value) {
    $text = [string]$value
    $email = $text.Trim().ToLowerInvariant(); $parsed = $null
    if ($email.Length -gt 0 -and $email.Length -le 320 -and [Net.Mail.MailAddress]::TryCreate($email, [ref]$parsed) -and $parsed.Address -ceq $email) { return "e:$email" }
    if ([string]::IsNullOrWhiteSpace($text) -or $text.Length -gt 200 -or $text.Contains('@') -or @($text.ToCharArray() | Where-Object { [char]::IsControl($_) }).Count) { return $null }
    return "n:$text"
}

$groups = Ordinal
$primary = Ordinal   # "group|basket|year|owner" -> annual team exists
foreach ($doc in Documents 'group') {
    $g = [string]$doc.Key
    $info = [pscustomobject]@{ Ref = 'G' + (Hex "group`0$g").Substring(0, 6); Teams = [Collections.Generic.List[object]]::new(); Baskets = Ordinal; Leagues = Ordinal; Members = Ordinal }
    foreach ($user in $doc.Value.u) { $key = OwnerKey $user.e; if ($key) { $info.Members[$key] = $true } }
    foreach ($basket in $doc.Value.b) {
        $years = @{}
        foreach ($annual in $basket.y) {
            $years[[int]$annual.y] = $true
            foreach ($team in $annual.t) {
                $owner = OwnerKey $team.o
                $info.Teams.Add([pscustomobject]@{ Basket = [string]$basket.i; Year = [int]$annual.y; Owner = $owner; Raw = [string]$team.o
                    CoOwners = @(foreach ($co in $team.a) { OwnerKey $co }) })
                if ($owner) { $primary["$g|$($basket.i)|$([int]$annual.y)|$owner"] = $true }
            }
        }
        $info.Baskets[[string]$basket.i] = $years
    }
    foreach ($league in $doc.Value.l) {
        $info.Leagues[[string]$league.i] = [pscustomobject]@{ Baskets = @(foreach ($b in $league.b) { [string]$b }) | Select-Object -Unique; Years = @(foreach ($a in $league.y) { [int]$a.y }) }
    }
    $groups[$g] = $info
}

$found = [Collections.Generic.List[object]]::new()
$checked = @{}
function Pseudonym($g, $key) { if ($key) { 'P' + (Hex "$g`0$key").Substring(0, 8) } else { '(non valido)' } }
# Years with a team of the owner in the group; "c" marks co-ownership only.
function TeamYears($info, $key) {
    (@($info.Teams | ForEach-Object { if ($_.Owner -ceq $key) { "$($_.Year)" } elseif ($_.CoOwners -ccontains $key) { "$($_.Year)c" } }) | Sort-Object -Unique) -join ','
}
# Why an owner has no single primary annual team among $baskets in $year.
function Explain($info, $raw, [string[]]$baskets, [int]$year) {
    $key = OwnerKey $raw
    if (-not $key) { return 'proprietario non valido (ne email ne etichetta)' }
    $scope = @($info.Teams | Where-Object { $_.Year -eq $year -and $baskets -ccontains $_.Basket })
    $count = @($scope | Where-Object { $_.Owner -ceq $key }).Count
    if ($count -eq 1) { return $null }
    if ($count -gt 1) { return 'piu squadre dello stesso proprietario nei basket della lega' }
    if (@($scope | Where-Object { $_.CoOwners -ccontains $key }).Count) { return 'e solo co-proprietario della squadra' }
    if ($key.StartsWith('n:') -and @($scope | Where-Object { $_.Raw.Trim() -ieq ([string]$raw).Trim() }).Count) { return 'etichetta uguale salvo maiuscole/spazi' }
    $elsewhere = @($info.Teams | Where-Object { $_.Owner -ceq $key -or $_.CoOwners -ccontains $key })
    if (@($elsewhere | Where-Object { $_.Year -eq $year }).Count) { return 'squadra nello stesso anno ma in un altro basket' }
    if ($elsewhere.Count) { return 'squadra solo in altri anni' }
    if ($info.Members.ContainsKey($key)) { return 'membro del gruppo senza squadre' }
    return 'sconosciuto nel gruppo'
}
function Report($container, $g, $year, $reason, $raw, $where, $players) {
    $info = $groups[$g]; $key = OwnerKey $raw
    $found.Add([pscustomobject]@{ Container = $container; Gruppo = $(if ($info) { $info.Ref } else { '?' }); Anno = [int]$year; Motivo = $reason
        Proprietario = (Pseudonym $g $key); Tipo = $(if (-not $key) { '-' } elseif ($key.StartsWith('e:')) { 'email' } else { 'etichetta' })
        AnniSquadra = $(if ($info -and $key) { TeamYears $info $key } else { '' }); Posizione = $where; Giocatori = $players })
}
function Tally($container) { $checked[$container] = 1 + [int]$checked[$container] }
# Competitions: exactly one primary team among the league's baskets with teams that year.
function Resolve($container, $scopeKey, $raw, $where) {
    Tally $container
    $g = [string]$scopeKey.g; $l = [string]$scopeKey.l; $y = [int]$scopeKey.y
    $info = $groups[$g]
    $reason = if (-not $info) { 'gruppo assente' } elseif (-not $info.Leagues.ContainsKey($l)) { 'lega assente' }
        elseif ($info.Leagues[$l].Years -notcontains $y) { 'anno assente nella lega' }
        else { Explain $info $raw @($info.Leagues[$l].Baskets | Where-Object { $info.Baskets.ContainsKey($_) -and $info.Baskets[$_].ContainsKey($y) }) $y }
    if ($reason) { Report $container $g $y $reason $raw $where '' }
}

foreach ($container in 'team', 'dailyteams', 'openingteams', 'openingdailyteams') {
    foreach ($doc in Documents $container) {
        Tally $container
        $g = [string]$doc.Key.g; $b = [string]$doc.Key.b; $y = [int]$doc.Key.y
        $origin = if ($container -like 'opening*') { $y - 1 } else { $y }
        $key = OwnerKey $doc.Key.e
        if ($key -and $primary.ContainsKey("$g|$b|$origin|$key")) { continue }
        $info = $groups[$g]
        $reason = if (-not $info) { 'gruppo assente' } elseif (-not $info.Baskets.ContainsKey($b)) { 'basket assente' }
            elseif (-not $info.Baskets[$b].ContainsKey($origin)) { "basket senza squadre nell'anno della rosa" }
            else { Explain $info $doc.Key.e @($b) $origin }
        if ($reason) {
            $day = if ($null -ne $doc.Key.d) { " giornata $($doc.Key.d)" } else { '' }
            Report $container $g $y $(if ($origin -ne $y) { "$reason (squadre dell'anno $origin)" } else { $reason }) $doc.Key.e "$($doc.File)$day" $(if ($null -eq $doc.Value.p) { 0 } else { @($doc.Value.p).Count })
        }
    }
}
foreach ($doc in Documents 'marketwrapper') {
    $i = 0
    foreach ($trade in $doc.Value.m) {
        Resolve 'marketwrapper' $doc.Key $trade.b "$($doc.File) m[$i].b"
        Resolve 'marketwrapper' $doc.Key $trade.s "$($doc.File) m[$i].s"
        $j = 0; foreach ($owner in $trade.a) { Resolve 'marketwrapper' $doc.Key $owner "$($doc.File) m[$i].a[$j]"; $j++ }
        $j = 0; foreach ($owner in $trade.d) { Resolve 'marketwrapper' $doc.Key $owner "$($doc.File) m[$i].d[$j]"; $j++ }
        $i++
    }
}
foreach ($doc in Documents 'calendar') {
    foreach ($stage in @($doc.Value.r.PSObject.Properties)) {
        $d = 0
        foreach ($day in $stage.Value) {
            $n = 0
            foreach ($game in $day.g) {
                foreach ($side in 'o', 'u') {
                    if (-not [string]::IsNullOrWhiteSpace([string]$game.$side)) { Resolve 'calendar' $doc.Key $game.$side "$($doc.File) r.$($stage.Name)[$d].g[$n].$side" }
                }
                $n++
            }
            $d++
        }
    }
}
foreach ($doc in Documents 'rank') {
    foreach ($stage in @($doc.Value.r.PSObject.Properties)) {
        $t = 0; foreach ($team in $stage.Value) { Resolve 'rank' $doc.Key $team.o "$($doc.File) r.$($stage.Name)[$t].o"; $t++ }
    }
}
foreach ($doc in Documents 'openingstandings') {
    # Opening standings use the previous season's teams of all the league's baskets.
    $g = [string]$doc.Key.g; $l = [string]$doc.Key.l; $y = [int]$doc.Key.y; $info = $groups[$g]
    $t = 0
    foreach ($team in $doc.Value.t) {
        Tally 'openingstandings'
        $reason = if (-not $info) { 'gruppo assente' } elseif (-not $info.Leagues.ContainsKey($l)) { 'lega assente' }
            else { Explain $info $team.o @($info.Leagues[$l].Baskets) ($y - 1) }
        if ($reason) { Report 'openingstandings' $g $y "$reason (squadre dell'anno $($y - 1))" $team.o "$($doc.File) t[$t].o" '' }
        $t++
    }
}
foreach ($doc in Documents 'auction') {
    $scope = $doc.Value.l
    if (-not [string]::IsNullOrEmpty([string]$doc.Value.a.o)) { Resolve 'auction' $scope $doc.Value.a.o "$($doc.File) a.o" }
    $w = 0
    foreach ($win in $doc.Value.w) { if (-not [string]::IsNullOrEmpty([string]$win.o)) { Resolve 'auction' $scope $win.o "$($doc.File) w[$w].o" }; $w++ }
}

$summary = foreach ($container in 'team', 'dailyteams', 'openingteams', 'openingdailyteams', 'marketwrapper', 'calendar', 'rank', 'openingstandings', 'auction') {
    "$container : $([int]$checked[$container]) controlli, $(@($found | Where-Object Container -eq $container).Count) problemi"
}
$text = ($summary -join "`n") + "`n`n" + $(if ($found.Count -eq 0) { 'Nessun problema di identita delle squadre trovato.' } else {
    'Riepilogo:' + ($found | Group-Object Container, Motivo | ForEach-Object {
        [pscustomobject]@{ Container = $_.Group[0].Container; Motivo = $_.Group[0].Motivo; Volte = $_.Count
            Proprietari = @($_.Group.Proprietario | Sort-Object -Unique).Count; Gruppi = (@($_.Group.Gruppo | Sort-Object -Unique) -join ',')
            Anni = (@($_.Group.Anno | Sort-Object -Unique) -join ','); Esempio = $_.Group[0].Posizione }
    } | Sort-Object Container, Motivo | Format-Table -AutoSize -Wrap | Out-String -Width 1000) +
    'Dettaglio per proprietario (pseudonimo stabile per gruppo, non reversibile):' + ($found | Group-Object Container, Gruppo, Proprietario, Motivo | ForEach-Object {
        $first = $_.Group[0]
        [pscustomobject]@{ Container = $first.Container; Gruppo = $first.Gruppo; Proprietario = $first.Proprietario; Tipo = $first.Tipo; Motivo = $first.Motivo
            Volte = $_.Count; Anni = (@($_.Group.Anno | Sort-Object -Unique) -join ','); AnniSquadra = $first.AnniSquadra
            Giocatori = $(if ($first.Container -in 'team', 'dailyteams', 'openingteams', 'openingdailyteams') { (@($_.Group.Giocatori | Sort-Object -Unique) -join ',') } else { '' })
            Esempio = $first.Posizione }
    } | Sort-Object Container, Gruppo, Proprietario | Format-Table -AutoSize -Wrap | Out-String -Width 1000)
})
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
