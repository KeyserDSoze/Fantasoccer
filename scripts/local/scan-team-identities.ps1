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

function Players($value) { @(foreach ($p in $value.p) { "$(([string]$p.n).ToLowerInvariant() -replace '[^a-z]', '')|$($p.s)|$($p.p)|$($p.rv)|$($p.k)" }) }
function Same($left, $right) {
    if (($left | ConvertTo-Json -Compress -Depth 30) -ceq ($right | ConvertTo-Json -Compress -Depth 30)) { return 'identica' }
    $a = @(Players $left | Sort-Object); $b = @(Players $right | Sort-Object)
    if (($a -join ';') -ceq ($b -join ';')) { return 'stessi giocatori, altri campi diversi' }
    $common = @($a | Where-Object { $b -ccontains $_ }).Count
    return "diversa ($common giocatori in comune su $($a.Count)/$($b.Count))"
}
# Same exclusions as the import (operator decision): an exact copy of the roster
# kept in the basket of the owner's only team that year, or an empty roster of
# an owner without any team in the group that year.
function SkippedRoster($entry, $info) {
    $owned = @($info.Teams | Where-Object { $_.Year -eq $entry.Origin -and ($_.Owner -ceq $entry.Key -or $_.CoOwners -ccontains $entry.Key) })
    $value = $entry.Doc.Value
    if ($owned.Count -eq 0) {
        $players = if ($null -eq $value.p) { 0 } else { @($value.p).Count }
        $history = if ($null -eq $value.fh) { 0 } else { @($value.fh).Count }
        if ($players -eq 0 -and [int]$value.m -eq 0 -and [int]$value.op -eq 0 -and [int]$value.fc -eq 0 -and $history -eq 0) { return 'rose vuote di proprietari senza squadra' }
        return $null
    }
    $teams = @($owned | Where-Object { $_.Owner -ceq $entry.Key })
    if ($teams.Count -ne 1 -or $teams[0].Basket -ceq $entry.Basket) { return $null }
    $kept = $rosterIndex["$($entry.Container)|$($entry.Group)|$($teams[0].Basket)|$($entry.Year)|$($entry.Day)|$($entry.League)|$($entry.Key)"]
    if ($kept -and (Same $value $kept.Doc.Value) -ceq 'identica') { return 'copie identiche della rosa nel basket della squadra' }
    return $null
}

$seasonDocs = Ordinal     # "group|basket|year|owner" -> season roster document
$dailyBaskets = Ordinal   # "group|year|owner" -> basket -> matchday rosters
$rosterIndex = Ordinal    # "container|group|basket|year|day|league|owner" -> roster
$rosterEntries = [Collections.Generic.List[object]]::new()
$handledRosters = @{}
$unplaced = [Collections.Generic.List[object]]::new()
foreach ($container in 'team', 'dailyteams', 'openingteams', 'openingdailyteams') {
    foreach ($doc in Documents $container) {
        $g = [string]$doc.Key.g; $b = [string]$doc.Key.b; $y = [int]$doc.Key.y
        $entry = [pscustomobject]@{ Container = $container; Doc = $doc; Group = $g; Basket = $b; Year = $y; Day = $(if ($null -ne $doc.Key.d) { [int]$doc.Key.d } else { 0 })
            League = [string]$doc.Key.l; Key = (OwnerKey $doc.Key.e); Origin = $(if ($container -like 'opening*') { $y - 1 } else { $y }) }
        $rosterEntries.Add($entry)
        if (-not $entry.Key) { continue }
        $rosterIndex["$container|$g|$b|$y|$($entry.Day)|$($entry.League)|$($entry.Key)"] = $entry
        if ($container -eq 'team') { $seasonDocs["$g|$b|$y|$($entry.Key)"] = $doc }
        if ($container -eq 'dailyteams') {
            if (-not $dailyBaskets.ContainsKey("$g|$y|$($entry.Key)")) { $dailyBaskets["$g|$y|$($entry.Key)"] = Ordinal }
            $dailyBaskets["$g|$y|$($entry.Key)"][$b] = 1 + [int]$dailyBaskets["$g|$y|$($entry.Key)"][$b]
        }
    }
}
foreach ($entry in $rosterEntries) {
    $container = $entry.Container; $doc = $entry.Doc; $g = $entry.Group; $b = $entry.Basket; $y = $entry.Year; $origin = $entry.Origin; $key = $entry.Key
    Tally $container
    if ($key -and $primary.ContainsKey("$g|$b|$origin|$key")) { continue }
    $info = $groups[$g]
    $reason = if (-not $info) { 'gruppo assente' } elseif (-not $info.Baskets.ContainsKey($b)) { 'basket assente' }
        elseif (-not $info.Baskets[$b].ContainsKey($origin)) { "basket senza squadre nell'anno della rosa" }
        else { Explain $info $doc.Key.e @($b) $origin }
    if (-not $reason) { continue }
    $handled = if ($info -and $key -and $info.Baskets.ContainsKey($b)) { SkippedRoster $entry $info } else { $null }
    if ($handled) { $handledRosters["$container : $handled"] = 1 + [int]$handledRosters["$container : $handled"]; continue }
    $day = if ($null -ne $doc.Key.d) { " giornata $($doc.Key.d)" } else { '' }
    Report $container $g $y $(if ($origin -ne $y) { "$reason (squadre dell'anno $origin)" } else { $reason }) $doc.Key.e "$($doc.File)$day" $(if ($null -eq $doc.Value.p) { 0 } else { @($doc.Value.p).Count })
    if ($container -eq 'team' -and $info -and $key) { $unplaced.Add([pscustomobject]@{ Doc = $doc; Group = $g; Basket = $b; Year = $y; Key = $key; Reason = $reason }) }
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

# Season rosters saved under a basket other than the owner's team that year:
# where the owner's team is, whether that basket also has a roster (identical or
# not), where the matchday rosters are, and what the document carries.
function BasketRef($g, $b) { 'B' + (Hex "basket`0$g`0$b").Substring(0, 6) }
$details = foreach ($item in $unplaced) {
    $info = $groups[$item.Group]; $value = $item.Doc.Value
    $owned = @($info.Teams | Where-Object { $_.Year -eq $item.Year -and $_.Owner -ceq $item.Key })
    $copies = @(foreach ($team in $owned) {
        $other = $seasonDocs["$($item.Group)|$($team.Basket)|$($item.Year)|$($item.Key)"]
        if ($other) { Same $value $other.Value } else { 'nessuna' }
    })
    $keyYears = (@($info.Teams | Where-Object { $_.Basket -ceq $item.Basket -and ($_.Owner -ceq $item.Key -or $_.CoOwners -ccontains $item.Key) } | ForEach-Object { $_.Year }) | Sort-Object -Unique) -join ','
    $shared = @(foreach ($team in $owned) { @($info.Leagues.Values | Where-Object { $_.Years -contains $item.Year -and $_.Baskets -ccontains $item.Basket -and $_.Baskets -ccontains $team.Basket }).Count })
    $daily = $dailyBaskets["$($item.Group)|$($item.Year)|$($item.Key)"]
    $dailyText = if (-not $daily) { 'nessuna' } else { (@($daily.Keys | Sort-Object | ForEach-Object { "$(BasketRef $item.Group $_)=$($daily[$_])" }) -join ',') }
    [pscustomobject]@{ Anno = $item.Year; Proprietario = (Pseudonym $item.Group $item.Key); BasketChiave = (BasketRef $item.Group $item.Basket)
        BasketSquadra = ((@($owned | ForEach-Object { BasketRef $item.Group $_.Basket }) -join ',') -replace '^$', '-')
        ChiaveNegliAnni = $(if ($keyYears) { $keyYears } else { 'mai' })
        LegheComuni = ((@($shared) -join ',') -replace '^$', '-')
        CopiaNelBasketSquadra = ((@($copies) -join ',') -replace '^$', '-')
        Giornaliere = $dailyText; Giocatori = $(if ($null -eq $value.p) { 0 } else { @($value.p).Count })
        Soldi = "m=$([int]$value.m) op=$([int]$value.op) fc=$([int]$value.fc) fh=$(if ($null -eq $value.fh) { 0 } else { @($value.fh).Count })"
        Esempio = $item.Doc.File }
}

# Plain " | " rows: terminal tables can merge long columns.
function Rows($rows, [string[]]$columns) {
    $lines = @($columns -join ' | ')
    foreach ($row in $rows) { $lines += (@(foreach ($column in $columns) { [string]$row.$column }) -join ' | ') }
    $lines -join "`n"
}
function Years($values) { (@($values | ForEach-Object { [int]$_ }) | Sort-Object -Unique) -join ',' }
$summary = foreach ($container in 'team', 'dailyteams', 'openingteams', 'openingdailyteams', 'marketwrapper', 'calendar', 'rank', 'openingstandings', 'auction') {
    "$container : $([int]$checked[$container]) controlli, $(@($found | Where-Object Container -eq $container).Count) problemi"
}
$handledText = if ($handledRosters.Count) { "`nRose escluse dall'import per decisione del gestore (restano nel dump; vedi skippedRosterDocuments nel report):`n" + ((@($handledRosters.Keys | Sort-Object | ForEach-Object { "$_ : $($handledRosters[$_])" })) -join "`n") + "`n" } else { '' }
$text = ($summary -join "`n") + "`n" + $handledText + "`n" + $(if ($found.Count -eq 0) { 'Nessun problema di identita delle squadre trovato.' } else {
    "Riepilogo:`n" + (Rows ($found | Group-Object Container, Motivo | ForEach-Object {
        [pscustomobject]@{ Container = $_.Group[0].Container; Motivo = $_.Group[0].Motivo; Volte = $_.Count
            Proprietari = @($_.Group.Proprietario | Sort-Object -Unique).Count; Gruppi = (@($_.Group.Gruppo | Sort-Object -Unique) -join ',')
            Anni = (Years $_.Group.Anno); Esempio = $_.Group[0].Posizione }
    } | Sort-Object Container, Motivo) 'Container', 'Motivo', 'Volte', 'Proprietari', 'Gruppi', 'Anni', 'Esempio') +
    "`n`nDettaglio per proprietario (pseudonimo stabile per gruppo, non reversibile; c = solo co-proprietario):`n" + (Rows ($found | Group-Object Container, Gruppo, Proprietario, Motivo | ForEach-Object {
        $first = $_.Group[0]
        [pscustomobject]@{ Container = $first.Container; Gruppo = $first.Gruppo; Proprietario = $first.Proprietario; Tipo = $first.Tipo; Motivo = $first.Motivo
            Volte = $_.Count; Anni = (Years $_.Group.Anno); AnniSquadra = (@($first.AnniSquadra -split ',' | Where-Object { $_ } | Sort-Object { [int]($_ -replace 'c', '') }, { $_ }) -join ',')
            Giocatori = $(if ($first.Container -in 'team', 'dailyteams', 'openingteams', 'openingdailyteams') { (@($_.Group.Giocatori | Sort-Object { [int]$_ } -Unique) -join ',') } else { '' })
            Esempio = $first.Posizione }
    } | Sort-Object Container, Gruppo, Proprietario) 'Container', 'Gruppo', 'Proprietario', 'Tipo', 'Motivo', 'Volte', 'Anni', 'AnniSquadra', 'Giocatori', 'Esempio') +
    $(if ($details) { "`n`nRose stagionali non collegate, una riga per documento (BasketSquadra = basket della squadra del proprietario in quell'anno; ChiaveNegliAnni = anni in cui il proprietario ha una squadra nel basket della chiave; LegheComuni = leghe che contengono entrambi i basket; Giornaliere = rose giornaliere per basket):`n" +
        (Rows ($details | Sort-Object Proprietario, Anno) 'Anno', 'Proprietario', 'BasketChiave', 'BasketSquadra', 'ChiaveNegliAnni', 'LegheComuni', 'CopiaNelBasketSquadra', 'Giornaliere', 'Giocatori', 'Soldi', 'Esempio') } else { '' })
})
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
