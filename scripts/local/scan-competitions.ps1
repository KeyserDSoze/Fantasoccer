[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName
)

# Read-only survey of the structural checks the core import applies after the
# real calendars: market, randomized player orders, fantasy calendars,
# standings, opening standings, played games, auctions, hall of fame, push
# outbox, cards, news and AI advice. It prints the import error each problem
# would raise, counts, years and short archive references; never owners,
# names, players or texts. Owner/team resolution is covered by
# scan-team-identities.ps1, players by scan-player-names.ps1 and clubs by
# scan-club-labels.ps1. Nothing is written.
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
        $hasValue = $null -ne $doc.PSObject.Properties['Value'] -or $null -ne $doc.PSObject.Properties['v']
        [pscustomobject]@{ Key = $(if ($null -ne $doc.Key) { $doc.Key } else { $doc.k }); Value = $(if ($null -ne $doc.Value) { $doc.Value } else { $doc.v })
            HasValue = $hasValue; File = $file.Substring(0, 12) }
    }
}
function Has($object, $name) { $null -ne $object -and $object -is [Management.Automation.PSCustomObject] -and $null -ne $object.PSObject.Properties[$name] }
function Count($value) { if ($null -eq $value) { 0 } else { @($value).Count } }
function OwnerKey($value) {
    $text = [string]$value
    $email = $text.Trim().ToLowerInvariant(); $parsed = $null
    if ($email.Length -gt 0 -and $email.Length -le 320 -and [Net.Mail.MailAddress]::TryCreate($email, [ref]$parsed) -and $parsed.Address -ceq $email) { return "e:$email" }
    if ([string]::IsNullOrWhiteSpace($text) -or $text.Length -gt 200 -or $text.Contains('@')) { return $null }
    return "n:$text"
}

# Groups, leagues with their seasons, baskets and annual teams (import's maps).
$groups = Ordinal; $teams = Ordinal
foreach ($doc in Documents 'group') {
    $g = [string]$doc.Key
    $info = [pscustomobject]@{ Leagues = Ordinal; Baskets = Ordinal }
    foreach ($basket in $doc.Value.b) {
        $info.Baskets[[string]$basket.i] = $true
        foreach ($annual in $basket.y) { foreach ($team in $annual.t) { $owner = OwnerKey $team.o; if ($owner) { $teams["$g|$($basket.i)|$([int]$annual.y)|$owner"] = $true } } }
    }
    foreach ($league in $doc.Value.l) { $info.Leagues[[string]$league.i] = @(foreach ($annual in $league.y) { [int]$annual.y }) }
    $groups[$g] = $info
}

$found = [Collections.Generic.List[object]]::new()
$checked = [ordered]@{}
$notes = [Collections.Generic.List[string]]::new()
function Problem($container, $code, $year, $where, $detail = '') { $found.Add([pscustomobject]@{ Container = $container; Errore = $code; Anno = "$year"; Posizione = $where; Dettaglio = $detail }) }
function Scope($container, $key, $where, $referenceCode = 'missing_competition_reference', $seasonCode = 'missing_competition_season') {
    if ($null -eq $key) { Problem $container 'missing_competition_key' '' $where; return $false }
    $info = $groups[[string]$key.g]
    if (-not $info -or -not $info.Leagues.ContainsKey([string]$key.l)) { Problem $container $referenceCode $key.y $where 'gruppo o lega assente'; return $false }
    if ($info.Leagues[[string]$key.l] -notcontains [int]$key.y) { Problem $container $seasonCode $key.y $where "stagione $($key.y) assente nella lega"; return $false }
    return $true
}

# marketwrapper
$books = Ordinal; $shapes = @{}
foreach ($doc in Documents 'marketwrapper') {
    $checked['marketwrapper'] = 1 + [int]$checked['marketwrapper']
    $shape = if (-not $doc.HasValue -or $null -eq $doc.Value) { 'Value assente o nullo' } elseif (-not (Has $doc.Value 'm')) { "m assente (campi: $((@($doc.Value.PSObject.Properties.Name) -join ',')))" } elseif ($null -eq $doc.Value.m) { 'm nullo' } elseif ((Count $doc.Value.m) -eq 0) { 'm vuoto' } else { 'con scambi' }
    $shapes[$shape] = 1 + [int]$shapes[$shape]
    if (-not (Scope 'marketwrapper' $doc.Key $doc.File)) { continue }
    $id = "$($doc.Key.g)|$($doc.Key.l)|$($doc.Key.y)"
    if ($books.ContainsKey($id)) { Problem 'marketwrapper' 'market_book_collision' $doc.Key.y $doc.File }
    $books[$id] = $true
    if ($null -eq $doc.Value -or $null -eq $doc.Value.m) { Problem 'marketwrapper' 'missing_market_trades' $doc.Key.y $doc.File $shape; continue }
    $ids = Ordinal; $i = 0
    foreach ($trade in $doc.Value.m) {
        if ([string]::IsNullOrWhiteSpace([string]$trade.i) -or $ids.ContainsKey([string]$trade.i) -or [int]$trade.st -lt 0 -or [int]$trade.st -gt 6) { Problem 'marketwrapper' 'invalid_market_trade' $doc.Key.y "$($doc.File) m[$i]" }
        $ids[[string]$trade.i] = $true
        $voters = @(foreach ($owner in @($trade.a) + @($trade.d)) { if ($null -ne $owner) { OwnerKey $owner } })
        if ($voters.Count -ne @($voters | Sort-Object -Unique -CaseSensitive).Count) { Problem 'marketwrapper' 'market_vote_collision' $doc.Key.y "$($doc.File) m[$i]" }
        $i++
    }
}
foreach ($shape in $shapes.Keys) { $notes.Add("marketwrapper: $($shapes[$shape]) documenti '$shape'") }

# randomizedplayers (only with IMPORT_LEGACY_AUCTIONS=true)
$orders = Ordinal
foreach ($doc in Documents 'randomizedplayers') {
    $checked['randomizedplayers (solo con aste)'] = 1 + [int]$checked['randomizedplayers (solo con aste)']
    if (-not (Scope 'randomizedplayers' $doc.Key $doc.File)) { continue }
    $id = "$($doc.Key.g)|$($doc.Key.l)|$($doc.Key.y)"
    if ($orders.ContainsKey($id)) { Problem 'randomizedplayers' 'player_order_collision' $doc.Key.y $doc.File }
    $orders[$id] = $true
    if ($null -eq $doc.Value -or $null -eq $doc.Value.p) { Problem 'randomizedplayers' 'missing_randomized_players' $doc.Key.y $doc.File; continue }
    $keys = @(foreach ($player in $doc.Value.p) { ([string]$player.n).ToLowerInvariant() -replace '[^a-z]', '' })
    if ($keys.Count -ne @($keys | Sort-Object -Unique -CaseSensitive).Count) { Problem 'randomizedplayers' 'duplicate_player_in_order' $doc.Key.y $doc.File 'stesso giocatore due volte (per nome)' }
}

# Fantasy calendars: also the match index used by played games.
$calendars = Ordinal; $fantasyMatches = Ordinal
foreach ($doc in Documents 'calendar') {
    $checked['calendar'] = 1 + [int]$checked['calendar']
    if (-not (Scope 'calendar' $doc.Key $doc.File)) { continue }
    if ($null -eq $doc.Value) { Problem 'calendar' 'missing_fantasy_calendar' $doc.Key.y $doc.File; continue }
    $id = "$($doc.Key.g)|$($doc.Key.l)|$($doc.Key.y)"
    if ([int]$doc.Value.y -ne [int]$doc.Key.y -or $calendars.ContainsKey($id)) { Problem 'calendar' 'fantasy_calendar_collision' $doc.Key.y $doc.File "anno interno $($doc.Value.y)" }
    $calendars[$id] = $true
    $ids = Ordinal
    foreach ($stage in @($doc.Value.r.PSObject.Properties)) {
        if ($null -eq $stage) { continue }
        if ($null -eq $stage.Value) { Problem 'calendar' 'missing_fantasy_days' $doc.Key.y "$($doc.File) r.$($stage.Name)"; continue }
        $d = 0
        foreach ($day in $stage.Value) {
            if ([int]$day.a -lt 1 -or [int]$day.a -gt 38) { Problem 'calendar' 'invalid_fantasy_matchday' $doc.Key.y "$($doc.File) r.$($stage.Name)[$d]" "giornata $($day.a)" }
            $n = 0
            foreach ($game in $day.g) {
                if ([string]::IsNullOrWhiteSpace([string]$game.i) -or $ids.ContainsKey([string]$game.i)) { Problem 'calendar' 'fantasy_game_collision' $doc.Key.y "$($doc.File) r.$($stage.Name)[$d].g[$n]" }
                $ids[[string]$game.i] = $true
                $fantasyMatches["$id|$($game.i)"] = [int]$day.a
                $n++
            }
            $d++
        }
    }
}

# Standings
$ranks = Ordinal
foreach ($doc in Documents 'rank') {
    $checked['rank'] = 1 + [int]$checked['rank']
    if ($null -eq $doc.Value) { Problem 'rank' 'missing_rank' $doc.Key.y $doc.File; continue }
    if (-not (Scope 'rank' $doc.Key $doc.File)) { continue }
    $id = "$($doc.Key.g)|$($doc.Key.l)|$($doc.Key.y)"
    if ([int]$doc.Value.d -lt 0 -or [int]$doc.Value.d -gt 38 -or $ranks.ContainsKey($id)) { Problem 'rank' 'rank_collision' $doc.Key.y $doc.File "giornata $($doc.Value.d)" }
    $ranks[$id] = $true
    foreach ($stage in @($doc.Value.r.PSObject.Properties)) {
        if ($null -eq $stage) { continue }
        if ($null -eq $stage.Value) { Problem 'rank' 'missing_rank_teams' $doc.Key.y "$($doc.File) r.$($stage.Name)"; continue }
        $owners = @(foreach ($team in $stage.Value) { OwnerKey $team.o })
        if ($owners.Count -ne @($owners | Sort-Object -Unique -CaseSensitive).Count) { Problem 'rank' 'invalid_rank_team' $doc.Key.y "$($doc.File) r.$($stage.Name)" 'stesso proprietario due volte' }
    }
}

# Opening standings
$openings = Ordinal
foreach ($doc in Documents 'openingstandings') {
    $checked['openingstandings'] = 1 + [int]$checked['openingstandings']
    if ($null -eq $doc.Value) { Problem 'openingstandings' 'missing_opening_standings' $doc.Key.y $doc.File; continue }
    if (-not (Scope 'openingstandings' $doc.Key $doc.File)) { continue }
    $id = "$($doc.Key.g)|$($doc.Key.l)|$($doc.Key.y)"
    if ([int]$doc.Value.y -ne [int]$doc.Key.y -or [string]$doc.Value.g -cne [string]$doc.Key.g -or [string]$doc.Value.l -cne [string]$doc.Key.l -or
        [int]$doc.Value.d -lt 1 -or [int]$doc.Value.d -gt 38 -or $openings.ContainsKey($id)) { Problem 'openingstandings' 'opening_standings_collision' $doc.Key.y $doc.File }
    $openings[$id] = $true
}

# Played games
$played = Ordinal
foreach ($doc in Documents 'playedgame') {
    $checked['playedgame'] = 1 + [int]$checked['playedgame']
    if (-not (Scope 'playedgame' $doc.Key $doc.File)) { continue }
    if ($null -eq $doc.Value) { Problem 'playedgame' 'missing_played_game' $doc.Key.y $doc.File; continue }
    $match = "$($doc.Key.g)|$($doc.Key.l)|$($doc.Key.y)|$($doc.Key.i)"
    if (-not $fantasyMatches.ContainsKey($match)) { Problem 'playedgame' 'missing_competition_reference' $doc.Key.y $doc.File 'partita assente dal calendario fantacalcio'; continue }
    if ([string]$doc.Value.i -cne [string]$doc.Key.i -or [int]$doc.Key.d -lt 1 -or [int]$doc.Key.d -gt 38 -or $played.ContainsKey("$match|$($doc.Key.d)")) { Problem 'playedgame' 'played_game_collision' $doc.Key.y $doc.File }
    $played["$match|$($doc.Key.d)"] = $true
    if ($fantasyMatches[$match] -ne [int]$doc.Key.d) { Problem 'playedgame' 'played_game_day_mismatch' $doc.Key.y $doc.File "chiave giornata $($doc.Key.d), calendario $($fantasyMatches[$match])" }
}

# Auctions (only with IMPORT_LEGACY_AUCTIONS=true)
$auctions = Ordinal; $encrypted = 0
foreach ($doc in Documents 'auction') {
    $checked['auction (solo con aste)'] = 1 + [int]$checked['auction (solo con aste)']
    $value = $doc.Value
    if ($null -eq $value) { Problem 'auction' 'missing_auction' '' $doc.File; continue }
    if ([string]::IsNullOrWhiteSpace([string]$value.i) -or [string]$value.i -cne [string]$doc.Key -or $auctions.ContainsKey([string]$value.i)) { Problem 'auction' 'auction_collision' '' $doc.File }
    $auctions[[string]$value.i] = $true
    if ($null -eq $value.l) { Problem 'auction' 'missing_auction_scope' '' $doc.File; continue }
    if (-not (Scope 'auction' $value.l $doc.File 'missing_auction_reference' 'missing_auction_season')) { continue }
    if ([int]$value.tp -lt 0 -or [int]$value.tp -gt 2 -or [int]$value.knd -lt 0 -or [int]$value.knd -gt 1 -or [int]$value.s -lt 0 -or [int]$value.s -gt 3 -or [int]$value.cr -lt 0 -or [int]$value.cr -gt 3) { Problem 'auction' 'invalid_auction_state' $value.l.y $doc.File }
    if ($null -ne $value.a -and $null -eq $value.a.pl) { Problem 'auction' 'missing_actual_auction_player' $value.l.y $doc.File }
    foreach ($role in @($value.p.PSObject.Properties)) { if ($null -ne $role) { $encrypted += Count $role.Value } }
}
if ($encrypted -gt 0) { $notes.Add("auction: $encrypted giocatori in liste cifrate; con IMPORT_LEGACY_AUCTIONS=true servono LEGACY_ENCRYPTION_KEY/IV originali") }

# Hall of fame
$halls = Ordinal
foreach ($doc in Documents 'halloffame') {
    $checked['halloffame'] = 1 + [int]$checked['halloffame']
    if ($null -eq $doc.Key) { Problem 'halloffame' 'missing_hall_key' '' $doc.File; continue }
    if ($null -eq $doc.Value) { Problem 'halloffame' 'missing_hall_archive' '' $doc.File; continue }
    $info = $groups[[string]$doc.Key.g]
    if (-not $info -or -not $info.Leagues.ContainsKey([string]$doc.Key.l)) { Problem 'halloffame' 'missing_hall_reference' '' $doc.File; continue }
    $id = "$($doc.Key.g)|$($doc.Key.l)"
    if ($halls.ContainsKey($id)) { Problem 'halloffame' 'hall_archive_collision' '' $doc.File }
    $halls[$id] = $true
    $value = $doc.Value
    if ($null -ne $value.rg -and $null -eq $value.rg.g) { Problem 'halloffame' 'missing_hall_record_game' '' "$($doc.File) rg" }
    if ($null -ne $value.rp -and $null -eq $value.rp.p) { Problem 'halloffame' 'missing_hall_player' '' "$($doc.File) rp.p" }
    if ($null -ne $value.ppmy -and $null -eq $value.ppmy.p) { Problem 'halloffame' 'missing_hall_player' '' "$($doc.File) ppmy.p" }
    $w = 0; foreach ($winning in $value.wp) { if ($null -eq $winning.p) { Problem 'halloffame' 'missing_hall_player' '' "$($doc.File) wp[$w].p" }; $w++ }
}

# Push outbox (property names are case-insensitive, as in the import).
$pushKeys = @{}
foreach ($doc in Documents 'pushnotificationsent') {
    $checked['pushnotificationsent'] = 1 + [int]$checked['pushnotificationsent']
    if ($null -eq $doc.Key) { Problem 'pushnotificationsent' 'missing_push_outbox_key' '' $doc.File; continue }
    $value = $doc.Value; $y = [int]$doc.Key.y; $d = [int]$doc.Key.d
    if ($null -eq $value) { Problem 'pushnotificationsent' 'missing_push_outbox' $y $doc.File; continue }
    if ($pushKeys.ContainsKey("$y|$d") -or $d -lt 0 -or $d -gt 38 -or $y -lt 0 -or ($y -eq 0 -and $d -ne 0)) { Problem 'pushnotificationsent' 'invalid_push_outbox_scope' $y $doc.File "giornata $d" }
    $pushKeys["$y|$d"] = $true
    if ($y -eq 0) {
        if ((Count $value.pendingNotifications) -gt 0 -or $null -ne $value.matchEventVotes -or [long]$value.matchEventSequence -ne 0 -or $value.deploymentReminder -eq $true -or
            $value.endDay -eq $true -or $value.liveChangesReminder -eq $true -or (Count $value.liveFormationChangeNotifications) -gt 0) { Problem 'pushnotificationsent' 'unexpected_push_index_state' $y $doc.File }
        foreach ($active in $value.activeOutboxDays) { if ([int]$active.d -lt 0 -or [int]$active.d -gt 38) { Problem 'pushnotificationsent' 'invalid_push_active_day' $active.y $doc.File } }
        continue
    }
    if ((Count $value.activeOutboxDays) -gt 0) { Problem 'pushnotificationsent' 'unexpected_push_day_index' $y $doc.File }
    $i = 0
    foreach ($sample in $value.matchEventVotes.p) {
        $named = (([string]$sample.n).ToLowerInvariant() -replace '[^a-z]', '') -ne ''
        if ($named -and $null -eq $sample.v) { Problem 'pushnotificationsent' 'missing_push_baseline_vote' $y "$($doc.File) matchEventVotes.p[$i]" }
        $i++
    }
    $ids = Ordinal; $i = 0
    foreach ($pending in $value.pendingNotifications) {
        $kind = $pending.kind
        $validKind = ($kind -is [long] -or $kind -is [int]) -and [long]$kind -ge 0 -and [long]$kind -le 6
        if ([string]::IsNullOrEmpty([string]$pending.id) -or $ids.ContainsKey([string]$pending.id) -or -not $validKind -or [int]$pending.attempts -lt 0) {
            Problem 'pushnotificationsent' 'invalid_pending_push' $y "$($doc.File) pendingNotifications[$i]" }
        elseif ($null -eq $pending.notification) { Problem 'pushnotificationsent' 'missing_pending_push_payload' $y "$($doc.File) pendingNotifications[$i]" }
        $ids[[string]$pending.id] = $true; $i++
    }
}

# Cards
$libraries = Ordinal
foreach ($doc in Documents 'cards') {
    $checked['cards'] = 1 + [int]$checked['cards']
    if ([string]::IsNullOrWhiteSpace([string]$doc.Key) -or $libraries.ContainsKey([string]$doc.Key)) { Problem 'cards' 'card_library_collision' '' $doc.File }
    $libraries[[string]$doc.Key] = $true
    if ($null -eq $doc.Value -or $null -eq $doc.Value.c) { Problem 'cards' 'missing_cards' '' $doc.File; continue }
    $ids = Ordinal; $i = 0
    foreach ($card in $doc.Value.c) {
        if ([string]::IsNullOrWhiteSpace([string]$card.id) -or $ids.ContainsKey([string]$card.id)) { Problem 'cards' 'invalid_card' '' "$($doc.File) c[$i]" }
        $ids[[string]$card.id] = $true; $i++
    }
}

# News and AI advice
$news = Ordinal
foreach ($doc in Documents 'news') {
    $checked['news'] = 1 + [int]$checked['news']
    if ($null -eq $doc.Key) { Problem 'news' 'missing_news_key' '' $doc.File; continue }
    if ($null -eq $doc.Value) { Problem 'news' 'missing_news' $doc.Key.y $doc.File; continue }
    $value = $doc.Value
    if ([int]$doc.Key.d -lt 1 -or [int]$doc.Key.d -gt 38 -or [int]$value.year -ne [int]$doc.Key.y -or [int]$value.day -ne [int]$doc.Key.d -or
        (-not [string]::IsNullOrEmpty([string]$value.group) -and [string]$value.group -cne [string]$doc.Key.g)) {
        Problem 'news' 'news_scope_mismatch' $doc.Key.y $doc.File "chiave $($doc.Key.y)/$($doc.Key.d), contenuto $($value.year)/$($value.day)$(if (-not [string]::IsNullOrEmpty([string]$value.group) -and [string]$value.group -cne [string]$doc.Key.g) { ', gruppo diverso' })" }
    if (-not $groups.ContainsKey([string]$doc.Key.g)) { Problem 'news' 'missing_editorial_reference' $doc.Key.y $doc.File 'gruppo assente'; continue }
    $id = "$($doc.Key.g)|$($doc.Key.y)|$($doc.Key.d)"
    if ($news.ContainsKey($id)) { Problem 'news' 'news_collision' $doc.Key.y $doc.File }
    $news[$id] = $true
}
$advice = Ordinal
foreach ($doc in Documents 'teamwithai') {
    $checked['teamwithai'] = 1 + [int]$checked['teamwithai']
    if ($null -eq $doc.Key) { Problem 'teamwithai' 'missing_advice_key' '' $doc.File; continue }
    $key = $doc.Key; $owner = OwnerKey $key.e
    if ([int]$key.d -lt 1 -or [int]$key.d -gt 38) { Problem 'teamwithai' 'invalid_advice_day' $key.y $doc.File; continue }
    $info = $groups[[string]$key.g]
    if (-not $info -or -not $info.Baskets.ContainsKey([string]$key.b)) { Problem 'teamwithai' 'missing_editorial_reference' $key.y $doc.File 'gruppo o basket assente'; continue }
    if (-not $owner -or -not $teams.ContainsKey("$($key.g)|$($key.b)|$([int]$key.y)|$owner")) { Problem 'teamwithai' 'missing_editorial_reference' $key.y $doc.File 'nessuna squadra del proprietario in quel basket e anno'; continue }
    $id = "$($key.g)|$($key.b)|$($key.y)|$($key.d)|$owner"
    if ($advice.ContainsKey($id)) { Problem 'teamwithai' 'advice_collision' $key.y $doc.File }
    $advice[$id] = $true
    if ($null -eq $doc.Value -or $null -eq $doc.Value.message) { Problem 'teamwithai' 'missing_advice' $key.y $doc.File }
}

$summary = foreach ($name in $checked.Keys) { "$name : $($checked[$name]) documenti, $(@($found | Where-Object { $name -like "$($_.Container)*" }).Count) problemi" }
$text = ($summary -join "`n") + "`n" + $(if ($notes.Count) { "`n" + ($notes -join "`n") + "`n" } else { '' }) + "`n" + $(if ($found.Count -eq 0) { 'Nessun problema strutturale trovato.' } else {
    $lines = @('Container | Errore | Volte | Anni | Esempio | Dettaglio')
    foreach ($group in ($found | Group-Object Container, Errore, Dettaglio | Sort-Object { $_.Group[0].Container }, { $_.Group[0].Errore })) {
        $first = $group.Group[0]
        $years = (@($group.Group.Anno | Where-Object { $_ -ne '' } | ForEach-Object { [int]$_ }) | Sort-Object -Unique) -join ','
        $lines += "$($first.Container) | $($first.Errore) | $($group.Count) | $years | $($first.Posizione) | $($first.Dettaglio)"
    }
    $lines -join "`n"
})
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
