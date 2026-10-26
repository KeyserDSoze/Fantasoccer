param(
    [Parameter(Mandatory = $true)][string]$DumpDirectory,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$Sha256
)
$ErrorActionPreference = 'Stop'
try {
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $DumpDirectory 'manifest.json') | ConvertFrom-Json
    $entries = @($manifest.containers | Where-Object { $_.name -eq 'appidentity' } | ForEach-Object { $_.blobs } | Where-Object { $_.sha256 -eq $Sha256 })
    if ($entries.Count -eq 0 -or $entries.Count -gt 10) { throw 'Matching identity records not found or ambiguous.' }

    function Test-EmailMatch([object]$Value, [string]$Mode, [string]$Expected) {
        if ($Value -isnot [string] -or $Value.Length -gt 4096) { return $false }
        try {
            switch ($Mode) {
                'uri' { $Value = [Uri]::UnescapeDataString($Value) }
                'json' { $Value = ConvertFrom-Json -InputObject $Value -ErrorAction Stop }
                'base64' {
                    $text = $Value.Replace('-', '+').Replace('_', '/')
                    $text = $text.PadRight([int](4 * [Math]::Ceiling($text.Length / 4.0)), [char]'=')
                    $Value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($text))
                }
                'jsonSuffix' {
                    if (-not $Value.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) { return $false }
                    $Value = $Value.Substring(0, $Value.Length - 5)
                }
            }
            return ($Value -is [string] -and $Value.Trim().ToLowerInvariant() -eq $Expected.Trim().ToLowerInvariant())
        } catch { return $false }
    }

    $reports = @(foreach ($entry in $entries) {
        $hasher = [Security.Cryptography.SHA256]::Create()
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes('appidentity' + [char]0 + $entry.name)
            $nameHash = [BitConverter]::ToString($hasher.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
        } finally { $hasher.Dispose() }

        $file = Join-Path (Join-Path $DumpDirectory 'blobs') ($nameHash + '.bin')
        if ((Get-Item -LiteralPath $file).Length -gt 64MB -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $Sha256) {
            throw 'Payload checksum/size mismatch.'
        }

        $doc = Get-Content -Raw -Encoding UTF8 -LiteralPath $file | ConvertFrom-Json
        $key = if ($doc.PSObject.Properties.Name -ccontains 'k') { $doc.k } else { $doc.Key }
        $profile = if ($doc.PSObject.Properties.Name -ccontains 'v') { $doc.v } else { $doc.Value }
        if ($null -eq $profile) { throw 'Missing identity profile.' }

        $email = if ($profile.PSObject.Properties.Name -ccontains 'e') { $profile.e } else { $profile.Email }
        if ($email -isnot [string] -or [string]::IsNullOrWhiteSpace($email)) { throw 'Missing profile email.' }

        $guid = [Guid]::Empty
        $result = [ordered]@{
            archiveReference = 'blobs/' + $nameHash + '.bin'
            matchingRecords = $entries.Count
            keyType = if ($null -eq $key) { 'null' } else { $key.GetType().Name }
            keyLength = if ($key -is [string]) { $key.Length } else { 0 }
            blobNameLength = ([string]$entry.name).Length
            keyIsGuid = ($key -is [string] -and [Guid]::TryParse($key, [ref]$guid))
            keyEqualsBlobName = ($key -is [string] -and $key.Trim().ToLowerInvariant() -eq ([string]$entry.name).Trim().ToLowerInvariant())
        }

        foreach ($mode in @('raw', 'uri', 'json', 'base64', 'jsonSuffix')) {
            $result['key_' + $mode + '_matchesProfile'] = Test-EmailMatch $key $mode $email
            $result['blob_' + $mode + '_matchesProfile'] = Test-EmailMatch ([string]$entry.name) $mode $email
        }
        [pscustomobject]$result
    })
    ConvertTo-Json -InputObject $reports -Depth 4
} catch {
    Write-Error 'Ispezione non riuscita: controlla cartella del dump, checksum e presenza del documento appidentity. Nessun valore del documento viene stampato.'
    exit 1
}
