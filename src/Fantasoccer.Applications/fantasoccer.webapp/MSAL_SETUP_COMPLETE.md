# Configurazione Microsoft Login con MSAL - Fantasoccer

## 🎯 Configurazione Completata

L'implementazione del login Microsoft è ora completata per l'app Fantasoccer cross-platform (Web + Mobile).

## 📋 Checklist Configurazione

### ✅ Completato:

1. **Dipendenze installate:**
   - ✅ `@azure/msal-browser` (Web)
   - ✅ `@azure/msal-react` (React Web)
   - ✅ `react-native-msal` (React Native)

2. **File di configurazione:**
   - ✅ `packages/app/auth/msalConfig.ts` - Configurazione MSAL cross-platform
   - ✅ `packages/app/auth/AuthProvider.tsx` - Provider React Context
   - ✅ `packages/app/auth/webAuth.ts` - Servizio auth web
   - ✅ `packages/app/auth/nativeAuth.ts` - Servizio auth mobile
   - ✅ `packages/app/auth/types.ts` - Tipi TypeScript

3. **Componenti UI:**
   - ✅ `packages/app/features/auth/LoginButton.tsx` - Pulsante login/logout
   - ✅ `packages/app/features/auth/UserProfile.tsx` - Profilo utente
   - ✅ `packages/app/features/auth/LoginScreen.tsx` - Schermata login
   - ✅ `packages/app/features/auth/AuthGuard.tsx` - Protezione route
   - ✅ `packages/app/features/auth/AuthTest.tsx` - Componente test

4. **Configurazione Expo:**
   - ✅ `apps/expo/app.json` - Configurato con redirect URI per iOS/Android
   - ✅ Intent filters per Android
   - ✅ Info.plist entries per iOS

## 🔧 Client ID Configurato

```
Client ID: 95de3061-5eab-437d-a2cc-6d71cfaef4d1
Authority: https://login.microsoftonline.com/consumers
```

## 🌍 Redirect URI per Azure App Registration

### Web (Development/Production):
- `http://localhost:3000` (development)
- `https://tuodominio.com` (production)

### Mobile:
- **iOS**: `msauth.com.fantasoccer.app://auth`
- **Android**: `msauth://com.fantasoccer.app/auth`

## 🚀 Come Usare

### 1. Wrappa la tua app con AuthProvider:

```tsx
import { AuthProvider } from './packages/app/auth/AuthProvider'
import { YourApp } from './YourApp'

export default function App() {
  return (
    <AuthProvider>
      <YourApp />
    </AuthProvider>
  )
}
```

### 2. Usa i componenti di autenticazione:

```tsx
import { LoginButton, UserProfile, AuthGuard } from './packages/app/features/auth'

// Pulsante login
<LoginButton />

// Profilo utente (solo se autenticato)
<UserProfile />

// Proteggi una rotta
<AuthGuard>
  <ProtectedContent />
</AuthGuard>
```

### 3. Usa l'hook useAuth:

```tsx
import { useAuth } from './packages/app/auth/AuthProvider'

const { user, isAuthenticated, isLoading, login, logout } = useAuth()
```

## 🧪 Testing

Per testare l'implementazione, usa il componente `AuthTest`:

```tsx
import { AuthTest } from './packages/app/features/auth/AuthTest'

export default AuthTest
```

## 📱 Avvio delle App

### Web:
```bash
yarn web
# o
cd apps/next && yarn dev
```

### Mobile:
```bash
yarn native
# o
cd apps/expo && yarn start
```

## ⚠️ Note Importanti

1. **Azure App Registration**: Assicurati di configurare tutti i redirect URI nell'Azure App Registration.

2. **Permissions**: L'app richiede le seguenti permission:
   - `User.Read`
   - `email`
   - `profile`
   - `openid`

3. **Platform Detection**: Il sistema rileva automaticamente la piattaforma (web/native) e usa il servizio auth appropriato.

4. **Error Handling**: Tutti i servizi includono gestione errori completa.

## 🔐 Sicurezza

- Token vengono salvati in `sessionStorage` su web
- Su mobile, MSAL gestisce automaticamente la persistenza sicura
- Refresh automatico dei token con `acquireTokenSilent`
- Logout completo rimuove tutti i token

## 🐛 Troubleshooting

### Errore "MSAL not initialized":
- Assicurati che `AuthProvider` wrappi tutta l'app
- Verifica che le dipendenze siano installate correttamente

### Redirect URI non funziona su mobile:
- Verifica la configurazione in `app.json`
- Controlla che i redirect URI siano configurati in Azure

### React 19 compatibility warnings:
- Le librerie MSAL sono compatibili con React 19 usando `--legacy-peer-deps`

## 🎉 Prossimi Passi

1. Configura i redirect URI nell'Azure App Registration
2. Testa su dispositivi fisici per mobile
3. Integra con il backend `/api/auth/microsoft`
4. Implementa la persistenza dello stato utente
5. Aggiungi analytics per il tracking login

La configurazione è completa e pronta per essere utilizzata! 🚀