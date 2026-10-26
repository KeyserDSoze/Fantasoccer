[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('init', 'up', 'up-https', 'certificate', 'down', 'test', 'export', 'verify', 'restore', 'migrate', 'analyze-core', 'import-core', 'smoke', 'logs')]
    [string]$Action,
    [string]$EnvFile = '.env',
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')]
    [string]$DumpName = 'current',
    [switch]$Resume,
    [switch]$SourceQuiesced
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [IO.Path]::IsPathRooted($EnvFile)) { $EnvFile = Join-Path $Root $EnvFile }

if ($Action -eq 'init') {
    if (Test-Path $EnvFile) { throw 'Environment file already exists; it has NOT been overwritten.' }
    function New-LocalKey([int]$Length) {
        $bytes = New-Object byte[] $Length
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        return [Convert]::ToBase64String($bytes)
    }
    $text = [IO.File]::ReadAllText((Join-Path $Root '.env.test'))
    foreach ($entry in @{
        POSTGRES_PASSWORD = (New-LocalKey 32)
        DASHBOARD_TOKEN = (New-LocalKey 32)
        ENCRYPTION_KEY = (New-LocalKey 32)
        ENCRYPTION_IV = (New-LocalKey 16)
        LOCAL_HTTPS_CERT_PASSWORD = (New-LocalKey 32)
    }.GetEnumerator()) {
        $text = [regex]::Replace($text, "(?m)^$($entry.Key)=.*$", "$($entry.Key)=$($entry.Value)")
    }
    [IO.File]::WriteAllText($EnvFile, $text, (New-Object Text.UTF8Encoding $false))
    if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
        [IO.File]::SetUnixFileMode($EnvFile, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite)
    }
    Write-Host 'Created private env file with generated local keys. Add real credentials only there, never in .env.test.'
    Write-Host 'Protect the file with your Windows ACLs; it is excluded from Git and Docker build contexts.'
    exit 0
}

if (-not (Test-Path $EnvFile)) { throw 'Env file not found. Run local.ps1 init or pass -EnvFile .env.test explicitly.' }
$ComposeArgs = @('compose', '--env-file', $EnvFile, '-f', (Join-Path $Root 'compose.yml'))
$HasCertificate = Test-Path (Join-Path $Root 'local-certificates/localhost.pfx') -PathType Leaf
if ($HasCertificate -and $Action -ne 'certificate') { $ComposeArgs += @('-f', (Join-Path $Root 'compose.https.yml')) }
function Invoke-Compose([string[]]$CommandArgs) {
    & docker @ComposeArgs @CommandArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed (exit $LASTEXITCODE). Credentials are not printed by this wrapper." }
}

switch ($Action) {
    'up' { Invoke-Compose -CommandArgs @('up', '-d', '--build', '--wait', 'frontend', 'api', 'postgres', 'azurite', 'dashboard') }
    'up-https' {
        if (-not $HasCertificate) { throw 'Run certificate first, then explicitly trust localhost.cer on your Windows account.' }
        Invoke-Compose -CommandArgs @('up', '-d', '--build', '--wait', 'frontend', 'api', 'postgres', 'azurite', 'dashboard')
    }
    'certificate' {
        $Directory = Join-Path $Root 'local-certificates'
        if (-not (Test-Path $Directory)) {
            New-Item -ItemType Directory $Directory > $null
            if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) { [IO.File]::SetUnixFileMode($Directory, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute) }
        }
        Invoke-Compose -CommandArgs @('--profile', 'tools', 'run', '--rm', '--build', '--no-deps', 'certificates')
    }
    'down' { Invoke-Compose -CommandArgs @('--profile', 'test', '--profile', 'tools', 'down') }
    'test' {
        Invoke-Compose -CommandArgs @('--profile', 'test', 'run', '--rm', '--build', 'tests')
        Invoke-Compose -CommandArgs @('--profile', 'test', 'run', '--rm', '--build', 'frontend-tests')
    }
    'export' {
        $command = @('--profile', 'tools', 'run', '--rm', '--build', 'storage-export', 'export', '--directory', "/dumps/$DumpName", '--allow-remote-source')
        if ($Resume) { $command += '--resume' }
        if ($SourceQuiesced) { $command += '--source-quiesced' }
        Invoke-Compose -CommandArgs $command
    }
    'verify' { Invoke-Compose -CommandArgs @('--profile', 'tools', 'run', '--rm', '--build', 'storage-verify', 'verify', '--directory', "/dumps/$DumpName") }
    'restore' {
        # Stop all application writers before replacing documents. No automatic restart.
        Invoke-Compose -CommandArgs @('stop', 'frontend', 'api')
        Invoke-Compose -CommandArgs @('--profile', 'tools', 'run', '--rm', '--build', 'storage-restore', 'restore-local', '--directory', "/dumps/$DumpName", '--public-local-files')
        Write-Host 'Verified snapshot restored to local Azurite. SQL data import is separate (analyze-core/import-core).'
    }
    'migrate' { Invoke-Compose -CommandArgs @('--profile', 'tools', 'run', '--rm', '--build', 'migrate') }
    'analyze-core' { Invoke-Compose -CommandArgs @('--profile', 'tools', 'run', '--rm', '--build', '--no-deps', 'import-core', 'analyze-core', '--directory', "/dumps/$DumpName") }
    'import-core' {
        Invoke-Compose -CommandArgs @('stop', 'frontend', 'api')
        Invoke-Compose -CommandArgs @('--profile', 'tools', 'run', '--rm', '--build', 'import-core', 'import-core', '--directory', "/dumps/$DumpName")
        Write-Host 'SQL snapshot imported. Review pendingContainers, pendingFields and preservedArtifacts; production cutover is not automatic. API/frontend remain stopped.'
    }
    'logs' { Invoke-Compose -CommandArgs @('logs', '--tail', '100', 'api') }
    'smoke' {
        if ($HasCertificate) { Invoke-Compose -CommandArgs @('exec', '-T', 'api', 'curl', '--fail', '--silent', '--cacert', '/https/localhost.pem', 'https://localhost:8443/health/ready') }
        else { Invoke-Compose -CommandArgs @('exec', '-T', 'api', 'curl', '--fail', '--silent', 'http://localhost:8080/health/ready') }
        Invoke-Compose -CommandArgs @('exec', '-T', 'frontend', 'wget', '-q', '-O', '/dev/null', 'http://127.0.0.1/health')
        if ($HasCertificate) { Invoke-Compose -CommandArgs @('exec', '-T', 'api', 'curl', '--fail', '--silent', '--cacert', '/https/localhost.pem', '--output', '/dev/null', 'https://localhost:8443/swagger/index.html') }
        else { Invoke-Compose -CommandArgs @('exec', '-T', 'api', 'curl', '--fail', '--silent', '--output', '/dev/null', 'http://localhost:8080/swagger/index.html') }
        Invoke-Compose -CommandArgs @('ps')
    }
}
