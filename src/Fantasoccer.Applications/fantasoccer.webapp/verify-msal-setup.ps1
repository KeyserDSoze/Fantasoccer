# Script per verificare e installare le dipendenze MSAL

Write-Host "Verifica dipendenze MSAL per Fantasoccer..." -ForegroundColor Green

# Naviga alla directory root del progetto webapp
Set-Location "c:\Users\aless\source\repos\Fantasoccer\src\Fantasoccer.Applications\fantasoccer.webapp"

Write-Host "Directory corrente: $(Get-Location)" -ForegroundColor Yellow

# Verifica se package.json esiste
if (Test-Path "package.json") {
    Write-Host "✅ package.json trovato" -ForegroundColor Green
    
    # Leggi il contenuto di package.json
    $packageJson = Get-Content "package.json" | ConvertFrom-Json
    
    # Verifica dipendenze MSAL
    $msalDeps = @(
        "@azure/msal-browser",
        "@azure/msal-react",
        "@azure/msal-react-native"
    )
    
    Write-Host "`nVerifica dipendenze MSAL:" -ForegroundColor Yellow
    
    foreach ($dep in $msalDeps) {
        if ($packageJson.dependencies.$dep -or $packageJson.devDependencies.$dep) {
            $version = if ($packageJson.dependencies.$dep) { $packageJson.dependencies.$dep } else { $packageJson.devDependencies.$dep }
            Write-Host "✅ ${dep}: ${version}" -ForegroundColor Green
        } else {
            Write-Host "❌ ${dep}: Non trovato" -ForegroundColor Red
        }
    }
    
    Write-Host "`nComandi per installare le dipendenze mancanti:" -ForegroundColor Yellow
    Write-Host "npm install @azure/msal-browser @azure/msal-react @azure/msal-react-native" -ForegroundColor Cyan
    Write-Host "# oppure con yarn:" -ForegroundColor Gray
    Write-Host "yarn add @azure/msal-browser @azure/msal-react @azure/msal-react-native" -ForegroundColor Cyan
    
} else {
    Write-Host "❌ package.json non trovato nella directory corrente" -ForegroundColor Red
}

# Verifica la configurazione Expo
if (Test-Path "apps\expo\app.json") {
    Write-Host "`n✅ Configurazione Expo trovata in apps\expo\app.json" -ForegroundColor Green
} else {
    Write-Host "`n❌ Configurazione Expo non trovata" -ForegroundColor Red
}

# Verifica i file di autenticazione
$authFiles = @(
    "packages\app\auth\msalConfig.ts",
    "packages\app\auth\AuthProvider.tsx",
    "packages\app\auth\webAuth.ts",
    "packages\app\auth\nativeAuth.ts"
)

Write-Host "`nVerifica file di autenticazione:" -ForegroundColor Yellow

foreach ($file in $authFiles) {
    if (Test-Path $file) {
        Write-Host "✅ $file" -ForegroundColor Green
    } else {
        Write-Host "❌ $file" -ForegroundColor Red
    }
}

Write-Host "`nProssimi passi:" -ForegroundColor Yellow
Write-Host "1. Installa le dipendenze MSAL se mancanti" -ForegroundColor White
Write-Host "2. Configura i redirect URI nell'Azure App Registration:" -ForegroundColor White
Write-Host "   - Web: http://localhost:3000" -ForegroundColor Cyan
Write-Host "   - iOS: msauth.com.fantasoccer.app://auth" -ForegroundColor Cyan
Write-Host "   - Android: msauth://com.fantasoccer.app/auth" -ForegroundColor Cyan
Write-Host "3. Testa l'app con: npx expo start" -ForegroundColor White

Write-Host "`nConfigurazione MSAL completata! 🚀" -ForegroundColor Green