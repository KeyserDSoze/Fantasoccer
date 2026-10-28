[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName
)

# Read-only survey of player entries whose name forms no player key (missing,
# blank, or without letters such as "----"). It mirrors the import's rules
# per container, counts the entries the import already excludes or archives,
# and details only those that would still refuse the import. Nothing is written.
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dump = Join-Path (Join-Path $Root 'migration-data') $DumpName
if (-not (Test-Path (Join-Path $dump 'manifest.json') -PathType Leaf)) { throw 'Dump manifest not found.' }

function PlayerKey($name) { ([string]$name).ToLowerInvariant() -replace '[^a-z]', '' }
function Blank($name) { [string]::IsNullOrWhiteSpace([string]$name) }
function StoredKey($entry) { @($entry.PSObject.Properties | Where-Object { ($_.Name -ieq 'key' -or $_.Name -ieq 'playerKey') -and $_.Value -is [string] -and $_.Value -ne '' }).Count -gt 0 }
function Status($entry) { if ($null -eq $entry.s) { 0 } else { [int]$entry.s } }

$sha = [Security.Cryptography.SHA256]::Create()
$manifest = Get-Content -Raw (Join-Path $dump 'manifest.json') | ConvertFrom-Json
function Documents($container) {
    $entry = $manifest.containers | Where-Object name -eq $container
    foreach ($blob in $(if ($entry) { @($entry.blobs) } else { @() })) {
        $file = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes("$container`0$($blob.name)")) | ForEach-Object { $_.ToString('x2') })
        $doc = Get-Content -Raw (Join-Path (Join-Path $dump 'blobs') "$file.bin") | ConvertFrom-Json
        $key = if ($null -ne $doc.Key) { $doc.Key } else { $doc.k }
        $year = if ($null -ne $key -and $key.PSObject.Properties['y']) { $key.y } elseif ($null -ne $key -and $key.PSObject.Properties['Year']) { $key.Year } elseif ($key -is [ValueType]) { $key } else { '' }
        [pscustomobject]@{ Value = $(if ($null -ne $doc.Value) { $doc.Value } else { $doc.v }); File = $file.Substring(0, 12); Year = "$year" }
    }
}
$handled = @{}
$blocking = [Collections.Generic.List[object]]::new()
function Handled($label) { $handled[$label] = 1 + [int]$handled[$label] }
function Blocking($container, $doc, $path, $entry, $reason) {
    $blocking.Add([pscustomobject]@{ Container = $container; Anno = $doc.Year; Posizione = "$($doc.File) $path"; Motivo = $reason
        Voce = ($entry | ConvertTo-Json -Compress -Depth 4) })
}
function Classify($container, $doc, $path, $entry) {
    if ($null -eq $entry -or (PlayerKey $entry.n) -ne '') { return }
    switch -Regex ($container) {
        '^(live|notification|official|pushnotificationsent)$' { if (Blank $entry.n) { Handled 'voti/baseline senza nome: esclusi' } else { Blocking $container $doc $path $entry 'nome senza lettere in un voto' }; break }
        '^realplayerswrapper$' { if (Blank $entry.n) { Handled 'catalogo senza nome: archiviati' } else { Blocking $container $doc $path $entry 'nome senza lettere nel catalogo' }; break }
        '^(chancedrealplayerwrapper|statplayerswrapper)$' { Handled 'probabili formazioni/statistiche senza nome: escluse'; break }
        '^(team|dailyteams|openingteams|openingdailyteams)$' {
            if (StoredKey $entry) { Blocking $container $doc $path $entry 'da verificare: nome recuperabile solo se la chiave memorizzata e nota' }
            elseif ((Status $entry) -ge 1 -and (Status $entry) -le 4) { Handled 'rose: venduti/rimossi senza nome esclusi (soldi nel report)' }
            else { Blocking $container $doc $path $entry 'giocatore attivo senza nome in una rosa' }
            break
        }
        default { if (StoredKey $entry) { Blocking $container $doc $path $entry 'da verificare: chiave memorizzata' } else { Blocking $container $doc $path $entry 'giocatore senza nome' } }
    }
}
function Walk($container, $doc, $node, $path) {
    if ($null -eq $node) { return }
    if ($node -is [Array]) { for ($i = 0; $i -lt $node.Count; $i++) { Walk $container $doc $node[$i] "$path[$i]" }; return }
    if ($node -isnot [Management.Automation.PSCustomObject]) { return }
    # Player-shaped objects: name, role and club members (RealPlayer JSON).
    if ($node.PSObject.Properties['n'] -and $node.PSObject.Properties['r'] -and $node.PSObject.Properties['t']) { Classify $container $doc $path $node; return }
    foreach ($property in $node.PSObject.Properties) { Walk $container $doc $property.Value "$path.$($property.Name)" }
}

$lists = 'live', 'notification', 'official', 'realplayerswrapper', 'chancedrealplayerwrapper', 'statplayerswrapper', 'team', 'dailyteams', 'openingteams', 'openingdailyteams'
$generic = 'pushnotificationsent', 'marketwrapper', 'calendar', 'openingstandings', 'playedgame', 'auction', 'halloffame'
foreach ($container in $lists + $generic) {
    $count = 0
    foreach ($doc in Documents $container) {
        $count++
        if ($lists -contains $container) { $list = @($doc.Value.p); for ($i = 0; $i -lt $list.Count; $i++) { Classify $container $doc "p[$i]" $list[$i] } }
        else { Walk $container $doc $doc.Value '' }
    }
    Write-Host "$container : $count documenti"
}
$lines = @(foreach ($label in $handled.Keys | Sort-Object) { "Gia gestite - ${label}: $($handled[$label])" })
$lines += "Bloccanti o da verificare: $($blocking.Count)"
$lines += @($blocking | Group-Object Container, Motivo | ForEach-Object {
    "== $($_.Group[0].Container): $($_.Group[0].Motivo) ($($_.Count), anni $((@($_.Group.Anno | Sort-Object -Unique)) -join ','))"
    foreach ($row in @($_.Group | Select-Object -First 10)) { "   $($row.Posizione) voce=$($row.Voce)" }
    if ($_.Count -gt 10) { "   ... altre $($_.Count - 10)" }
})
$text = $lines -join "`n"
$text
try { $text | Set-Clipboard } catch { Write-Host 'Appunti non disponibili: copia il testo sopra.' }
