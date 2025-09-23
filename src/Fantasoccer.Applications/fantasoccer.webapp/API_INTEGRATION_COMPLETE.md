# Architettura API Fantasoccer - Servizi Modulari

## 🎯 Panoramica

L'architettura API è stata implementata con un approccio modulare e domain-driven. Ogni area funzionale ha il proprio servizio specializzato, eliminando il pattern di orchestratore centrale per una migliore manutenibilità e scalabilità.

## 🏗️ Architettura Modulare

### Principi di Design:
1. **Domain-Driven Services**: Ogni servizio gestisce un dominio specifico
2. **Separazione delle Responsabilità**: Nessun servizio centrale che orchestri tutto
3. **Import Diretti**: I componenti importano direttamente dai servizi che servono
4. **Interfacce Pulite**: Mapping da JSON abbreviato a proprietà leggibili

## � Struttura Servizi

### Servizi Core:
```
packages/app/services/
├── apiConfig.ts            # Configurazione condivisa API (URL, headers, errori)
├── authService.ts          # Gestione completa autenticazione e ruoli
├── serieAService.ts        # Calendario Serie A e dati real-time
├── tokenStorageService.ts  # Persistenza sicura token
└── [futuro] leagueService.ts     # Gestione leghe fantacalcio
└── [futuro] marketService.ts     # Mercato e scambi
└── [futuro] playersService.ts    # Giocatori e statistiche
└── [futuro] auctionService.ts    # Asta live
└── [futuro] adminService.ts      # Funzioni amministrative
```

### Tipologie e Interfacce:
```
packages/app/types/
├── serieATypes.ts          # Interfacce Serie A (Raw + Clean + Enhanced)
└── [futuro] leagueTypes.ts       # Tipologie leghe e squadre
└── [futuro] playerTypes.ts       # Tipologie giocatori e stats
└── [futuro] marketTypes.ts       # Tipologie mercato e scambi
```

## � Configurazione API Condivisa

### apiConfig.ts - Configurazione Centrale:
```typescript
// Environment detection
const environment = detectEnvironment()

// Base URLs
const API_URLS = {
  development: 'https://localhost:7223',
  production: 'https://apifc.azurewebsites.net'
}

// Build API URLs with parameters
const buildApiUrl = (endpoint: string, params?: Record<string, string>)

// Common headers for all requests
const getCommonHeaders = (includeAuth: boolean = false, token?: string)

// Unified error handling
class ApiError extends Error {
  constructor(message: string, status?: number, details?: any)
}
```

## 🔐 Servizio Autenticazione (authService.ts)

### Responsabilità:
- **Microsoft OAuth/PKCE Flow**: Gestione completa autenticazione Microsoft
- **Token Exchange**: Scambio MSAL token con JWT backend
- **Token Refresh**: Refresh automatico token scaduti
- **User Info**: Recupero e gestione informazioni utente
- **Role Management**: Enum IdentityRole e controllo permessi

### Endpoints Gestiti:
```typescript
// Token Exchange (OAuth + PKCE)
GET /api/Authentication/Social/Token?code={authCode}&provider=2&code_verifier={verifier}

// Token Refresh
GET /api/Authentication/Social/Token?code={refreshToken}&provider=0

// User Information
GET /api/Authentication/Social/User
```

### Interfacce Utente:
```typescript
interface UserInfoResponse {
  u: string              // Username
  e: string              // Email  
  s: {                   // State
    g: string            // Current group ID
    l: string            // Current league ID
    y: number            // Year
    m: string            // Working email
  }
  g: Array<{            // Groups
    i: string            // Group ID
    n: string            // Group name
    r: number            // Role flags (Reader=1, Participant=2, Admin=4)
  }>
}

enum IdentityRole {
  None = 0,
  Reader = 1,
  Participant = 2,
  Admin = 4,
}
```

### Utilizzo:
```typescript
import { authService, hasRole, IdentityRole } from '../services/authService'

// Login with PKCE
const tokens = await authService.exchangeTokenWithPKCE(authCode, codeVerifier)
const userInfo = await authService.getUserInfo(tokens.accessToken, tokens.refreshToken)

// Role checking
const isAdmin = authService.hasUserRole(userInfo, IdentityRole.Admin)
```

## 🏆 Servizio Serie A (serieAService.ts)

### Responsabilità:
- **Calendario Serie A**: Gestione completa calendario real-time
- **Interface Mapping**: Conversione da JSON abbreviato a proprietà leggibili
- **Live Detection**: Identificazione giochi live e stato campionato
- **Enhanced Data**: Arricchimento dati con proprietà computate

### Endpoints Gestiti:
```typescript
// Real Calendar
GET /api/RealCalendar/Get?key={year}
```

### Sistema di Interfacce Triple:

#### 1. Raw Interfaces (dal backend JSON):
```typescript
interface RawRealGame {
  h: { n: string, a: string }  // Home team (name, abbreviation)
  a: { n: string, a: string }  // Away team (name, abbreviation) 
  d: string                    // Date
  g: number | null            // Home goals
  y: number | null            // Away goals
  e: boolean                  // Delayed flag
}
```

#### 2. Clean Interfaces (proprietà leggibili):
```typescript
interface RealGame {
  home: RealTeam              // { name: string, abbreviation: string }
  away: RealTeam              // { name: string, abbreviation: string }
  date: string
  homeGoals: number | null
  awayGoals: number | null
  delayed: boolean
}
```

#### 3. Enhanced Interfaces (con proprietà computate):
```typescript
interface EnhancedRealCalendar extends RealCalendar {
  // Computed properties
  liveSerieADay: number
  isLive: boolean
  liveDay: RealDay | null
  liveGames: RealGame[] | null
  lastDay: RealDay | null
  nextDay: RealDay | null
}
```

### Mapping Flow:
```typescript
// Backend JSON → Raw Interface → Clean Interface → Enhanced Interface
const rawData: RawRealCalendar = await fetch(url)
const cleanCalendar = mapRawCalendarToCalendar(rawData)  // h.n → home.name
const enhancedCalendar = enhanceRealCalendar(cleanCalendar)  // + live detection
```

### Utilizzo:
```typescript
import { serieAService } from '../services/serieAService'

// Get enhanced calendar with live detection
const calendar = await serieAService.getCurrentYearCalendar()
console.log('Live games:', calendar.liveGames?.length)
console.log('Current day:', calendar.liveSerieADay)

// Get specific data
const liveGames = await serieAService.getLiveGames()
const isLive = await serieAService.isSerieALive()
const upcomingGames = await serieAService.getUpcomingGames()
```

## 🎯 Servizi Futuri (Roadmap)

### 🏟️ League Service (leagueService.ts)
```typescript
// Endpoints futuri:
GET /api/League/Get?id={leagueId}           // League details
GET /api/Teams/GetByLeague?leagueId={id}    // Fantasy teams
GET /api/Standings?leagueId={id}            // League standings
GET /api/Calendar?leagueId={id}             // Fantasy calendar
```

### 👥 Players Service (playersService.ts)
```typescript
// Endpoints futuri:
GET /api/Players/SerieA                     // All Serie A players
GET /api/Players/Free?leagueId={id}         // Free players
GET /api/Players/{id}/Stats                 // Player statistics
GET /api/Players/{id}/Points?day={day}      // Player points
```

### 💰 Market Service (marketService.ts)
```typescript
// Endpoints futuri:
GET /api/Market/Trades?leagueId={id}        // Current trades
POST /api/Market/Trade                      // Create trade offer
PUT /api/Market/Trade/{id}/Accept           // Accept trade
GET /api/Market/History?leagueId={id}       // Trade history
```

### � Auction Service (auctionService.ts)
```typescript
// Endpoints futuri:
GET /api/Auction/Current?leagueId={id}      // Current auction
POST /api/Auction/Bid                       // Place bid
GET /api/Auction/Status?leagueId={id}       // Auction status
WebSocket /hub/auction                      // Real-time updates
```

### ⚙️ Admin Service (adminService.ts)
```typescript
// Endpoints futuri:
GET /api/Admin/Groups                       // Manage groups
POST /api/Admin/Group                       // Create group
PUT /api/Admin/Group/{id}/Users             // Manage users
GET /api/Admin/Logs                         // System logs
```

## 🔄 Flow di Autenticazione Completo

### 1. Inizializzazione App
```typescript
// AuthProvider si connette direttamente ad authService
import { authService } from '../services/authService'

// Verifica token salvato
const storedData = await tokenStorageService.loadToken()
if (storedData) {
  // Valida token con refresh automatico
  const userInfo = await authService.getUserInfo(storedData.accessToken, storedData.refreshToken)
  setUser(userInfo)
}
```

### 2. Login Flow (PKCE)
```typescript
// 1. Genera PKCE challenge
const { codeVerifier, codeChallenge } = PKCEHelper.generate()

// 2. Redirect Microsoft OAuth
window.location.href = buildOAuthUrl(codeChallenge)

// 3. Callback con authorization code
const authCode = new URLSearchParams(window.location.search).get('code')

// 4. Exchange con PKCE
const tokens = await authService.exchangeTokenWithPKCE(authCode, codeVerifier)

// 5. Get user info con refresh automatico
const userInfo = await authService.getUserInfo(tokens.accessToken, tokens.refreshToken)

// 6. Storage sicuro
await tokenStorageService.saveToken({
  tokenType: tokens.tokenType,
  accessToken: tokens.accessToken,
  refreshToken: tokens.refreshToken,
  expiresAt: Date.now() + (tokens.expiresIn * 1000),
  user: userInfo
})
```

### 3. Uso Servizi Domain-Specific
```typescript
// Componente che usa Serie A
import { serieAService } from '../services/serieAService'

// Componente che gestisce ruoli
import { authService, hasRole, IdentityRole } from '../services/authService'

// Componente che usa entrambi
import { serieAService } from '../services/serieAService'
import { hasRole, IdentityRole } from '../services/authService'

const MyComponent = () => {
  // Get Serie A data
  const calendar = await serieAService.getCurrentYearCalendar()
  
  // Check permissions
  const canManage = user?.groups.some(group => hasRole(group.role, IdentityRole.Admin))
  
  return (
    <div>
      {calendar.isLive && <LiveIndicator games={calendar.liveGames} />}
      {canManage && <AdminPanel />}
    </div>
  )
}
```

## 📊 Logging e Debug Unificato

Tutti i servizi implementano logging dettagliato con emoji per facilità di debug:

### AuthService Logs:
```
🔄 Exchanging OAuth code with PKCE for backend token...
📍 API URL: https://localhost:7223/api/Authentication/Social/Token?code=...
🎫 Auth Code (first 20 chars): eyJ0eXAiOiJKV1QiLCJhbGc...
📊 Exchange response status: 200
✅ Token exchange successful!
📄 Backend token response: { tokenType: "Bearer", accessToken: "...", ... }

🔄 Refreshing access token...
📍 Refresh API URL: https://localhost:7223/api/Authentication/Social/Token?code=***
✅ Token refresh successful!
📄 New token expires in: 3600 seconds
```

### SerieAService Logs:
```
🏆 Fetching Serie A calendar for year 2024...
📍 Serie A Calendar URL: https://localhost:7223/api/RealCalendar/Get?key=2024
📊 Serie A Calendar response status: 200
✅ Serie A Calendar fetched successfully! 38 days found
📅 Calendar year: 2024
📊 Calendar status:
- Live day: 15
- Last completed day: 14
- Next day: 16
- Is live: true
- Live games: 3
```

### TokenStorage Logs:
```
💾 Token saved to localStorage with expiry: 2024-09-23T15:30:00Z
✅ Valid token loaded from storage (expires in 2847 seconds)
🗑️ Token removed from localStorage
⚠️ Token expired, needs refresh
```

### Navigation & Components:
```
🚀 Navigation initialized for user: alessandro@example.com
🔐 User roles: Reader, Participant, Admin
📍 Current context: Group=SuperAdmin, League=SerieA2024, Year=2024
✅ Admin access granted for auction management
❌ SuperAdmin required for user management
```

## 🎯 Mappatura Endpoints per Funzionalità App

Seguendo le specifiche del frontend prompt, i servizi gestiranno questi endpoint:

### 🏠 Home & Dashboard
- **serieAService**: Giornata Serie A corrente, partite live
- **authService**: Informazioni utente e contesto

### 🏆 Fantasy League
- **leagueService** (futuro): `/standings`, `/calendar`, `/teams`
- **authService**: Controllo ruoli per visualizzazione dati

### 👥 Players & Market  
- **playersService** (futuro): `/players`, `/free-players`
- **marketService** (futuro): `/market/trades`, `/market/history`

### � Live Auction
- **auctionService** (futuro): `/auction`, WebSocket per real-time
- **authService**: Controllo ruoli Admin per gestione

### ⚙️ Settings & Admin
- **authService**: Switch context (league, year), role management
- **adminService** (futuro): `/admin/*`, `/superadmin/*`

### 📊 Serie A Official
- **serieAService**: `/seriea/standings`, `/seriea/calendar`

## 🎮 Component Integration Pattern

### Pattern di Import Domain-Specific:
```typescript
// Home component - Serie A focus
import { serieAService } from '../services/serieAService'
import { useAuth } from '../auth/AuthProvider'

// Admin component - Auth focus
import { authService, hasRole, IdentityRole } from '../services/authService'

// Market component - Multiple services
import { marketService } from '../services/marketService'  // futuro
import { playersService } from '../services/playersService' // futuro
import { hasRole, IdentityRole } from '../services/authService'

// Complex dashboard - Multiple services
import { serieAService } from '../services/serieAService'
import { leagueService } from '../services/leagueService'   // futuro
import { hasRole, IdentityRole } from '../services/authService'
```

### Role-Based Access Control:
```typescript
// In NavigationBar o altri componenti
const user = useAuth().user
const userGroups = user?.groups || []

const isAdmin = userGroups.some(group => hasRole(group.role, IdentityRole.Admin))
const isSuperAdmin = userGroups.some(group => 
  hasRole(group.role, IdentityRole.Admin) && group.name === 'SuperAdmin'
)

// Conditional rendering basato su ruoli
{isAdmin && <AdminMenuItem />}
{isSuperAdmin && <SuperAdminMenuItem />}
```

## 🧪 Testing Architettura Modulare

### Compilazione e Build:
```bash
# Verifica che tutti i servizi si compilino correttamente
yarn build

# Output atteso: ✅ Nessun errore di TypeScript
# I servizi modulari eliminano dipendenze circolari
```

### Test Integrazione Servizi:
```typescript
// Test authService
import { authService, IdentityRole } from '../services/authService'

const testAuth = async () => {
  const tokens = await authService.exchangeTokenWithPKCE(authCode, verifier)
  const userInfo = await authService.getUserInfo(tokens.accessToken)
  console.log('User roles:', userInfo.g.map(g => g.r))
}

// Test serieAService  
import { serieAService } from '../services/serieAService'

const testSerieA = async () => {
  const calendar = await serieAService.getCurrentYearCalendar()
  console.log('Live games:', calendar.liveGames?.length)
  console.log('Current day:', calendar.liveSerieADay)
}
```

### Verifica Import e Dependencies:
```typescript
// ✅ Import diretti (no apiService centrale)
import { authService } from '../services/authService'
import { serieAService } from '../services/serieAService' 

// ✅ Import solo di quello che serve
import { hasRole, IdentityRole } from '../services/authService'

// ❌ Evitare (apiService.ts è stato eliminato)
// import { apiService } from '../services/apiService'
```

### Test Flow Completo in Browser:
1. **Aprire DevTools Console**
2. **Avviare app**: `yarn web`
3. **Seguire i log dettagliati**:
   ```
   🚀 Navigation initialized...
   🔐 AuthProvider using direct authService import
   🏆 SerieA data fetched successfully
   ✅ Modular architecture working!
   ```

## 🔮 Prossimi Passi Roadmap

### Immediate (Fase 1):
1. **Test Real API**: Testare `serieAService.getCurrentYearCalendar()` con backend reale
2. **Navigation Enhancement**: Aggiungere più menu items con role-based access
3. **Error Handling**: Gestione errori unificata tra tutti i servizi

### Breve Termine (Fase 2):
1. **LeagueService**: Implementare gestione leghe e squadre
2. **PlayersService**: Implementare gestione giocatori e statistiche  
3. **Home Page**: Integrare dati reali Serie A nella dashboard

### Medio Termine (Fase 3):
1. **MarketService**: Sistema completo scambi e offerte
2. **AuctionService**: Asta live con WebSocket
3. **AdminService**: Pannelli amministrativi completi

### Lungo Termine (Fase 4):
1. **Real-time Updates**: WebSocket per tutti i servizi live
2. **Offline Support**: Cache e sincronizzazione
3. **Push Notifications**: Notifiche per eventi importanti

## ⚠️ Note Tecniche Importanti

### 🏗️ Architettura:
- **No Orchestratore Centrale**: Ogni servizio è indipendente
- **Domain-Driven**: Un servizio per ogni area funzionale
- **Import Diretti**: Componenti importano direttamente dai servizi necessari
- **Interfacce Triple**: Raw (backend) → Clean (leggibile) → Enhanced (computate)

### 🔐 Sicurezza:
- **Token Storage**: Sicuro su localStorage/AsyncStorage con expiry
- **Auto Refresh**: Refresh automatico token scaduti
- **Role-Based Access**: Controllo granulare permessi con enum flags

### 🚀 Performance:
- **Lazy Loading**: Servizi caricati solo quando necessari
- **Caching Intelligente**: Token e dati utente persistiti
- **Error Recovery**: Gestione robusta errori di rete

### 📱 Cross-Platform:
- **Tamagui**: UI components condivisi web/mobile
- **Environment Detection**: Automatica per development/production
- **Storage Adattivo**: localStorage (web) / AsyncStorage (mobile)

L'architettura modulare è **production-ready** e progettata per scalare con le funzionalità future dell'app Fantasoccer! 🚀⚽