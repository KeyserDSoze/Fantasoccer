# Architecture Patterns - Fantasoccer Frontend

## Data Flow & Mapping Pattern

### Overview
L'architettura segue un pattern di **separazione delle responsabilità** con mapping centralizzato per gestire la comunicazione tra backend (C# con JsonPropertyName) e frontend (TypeScript clean interfaces).

---

## Layer Architecture

### 1. **Types Layer** (`types/`)
**Responsabilità**: Definire interfacce e funzioni di mapping

#### Raw Interfaces
Interfacce che corrispondono esattamente al formato JSON dal backend:
```typescript
// Esempio da types/auction.ts
export interface AuctionHubWrapperRaw {
  w: AuctionWrapperRaw      // Wrapper (JsonPropertyName = "w")
  u: string                  // LastUpdate (DateTime ISO string)
  t: Record<string, AuctionTeamRaw>  // Teams dictionary
  r: StatPlayerRaw[] | null  // RealPlayers
  ln: string | null          // LeagueName
}
```

**Naming Convention**: `{EntityName}Raw`
- Proprietà con nomi abbreviati (matching C# JsonPropertyName)
- Date come stringhe ISO
- Enum come numeri

#### Clean Interfaces
Interfacce user-friendly per l'uso nel frontend:
```typescript
// Clean interface - usata nei componenti
export interface AuctionState {
  wrapper: AuctionWrapper
  lastUpdate: Date           // Convertito a Date object
  leagueName: string | null
  teams: AuctionTeam[]       // Array invece di dictionary
  realPlayers: StatPlayer[]
  isAuctioneer: boolean      // Computed property
  userEmail: string | null   // User context
}
```

**Naming Convention**: `{EntityName}` (senza suffisso)
- Proprietà con nomi descrittivi
- Date come oggetti Date
- Enum con nomi typed
- Proprietà computate quando necessario

#### Mapping Functions
Funzioni pure che trasformano raw → clean:
```typescript
/**
 * Map raw AuctionHubWrapper to clean AuctionState
 * This is the main mapper to be used in the service layer
 */
export function mapRawAuctionHubWrapperToAuctionState(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null
): AuctionState {
  const teams: AuctionTeam[] = Object.values(raw.t || {}).map(mapRawTeam)
  const realPlayers: StatPlayer[] = (raw.r || []).map(mapRawStatPlayerToStatPlayer)
  
  const isAuctioneer = userEmail 
    ? raw.w.c.toLowerCase() === userEmail.toLowerCase() 
    : false

  return {
    wrapper: mapRawAuctionWrapper(raw.w),
    lastUpdate: new Date(raw.u),
    leagueName: raw.ln,
    teams,
    realPlayers,
    isAuctioneer,
    userEmail
  }
}
```

**Naming Convention**: `mapRaw{EntityName}To{CleanName}` o `mapRaw{EntityName}`
- Funzioni composte: chiamano altri mapper per nested objects
- Include logica di business quando necessario (es. isAuctioneer)
- Type-safe: input Raw, output Clean

---

### 2. **Service Layer** (`services/`)
**Responsabilità**: Gestire comunicazione API/SignalR e applicare mapping

#### HTTP Services
```typescript
// Esempio: chiamata HTTP con mapping automatico
async getTeamStats(teamId: string): Promise<TeamStats> {
  const response = await fetch(url)
  const raw: TeamStatsRaw = await response.json()
  return mapRawTeamStatsToTeamStats(raw)  // ✅ Mapping nel service
}
```

#### SignalR Services
```typescript
// Esempio: eventi SignalR con mapping automatico
private registerEventHandlers(): void {
  this.hubConnection.on('AuctionState', (wrapperRaw: AuctionHubWrapperRaw) => {
    console.log('Auction state received (raw):', wrapperRaw)
    // ✅ Mapping nel service, non nel componente
    const mapped = mapRawAuctionHubWrapperToAuctionState(wrapperRaw, this.userEmail)
    this.eventHandlers.onAuctionState?.(mapped)
  })
}
```

**Best Practices**:
- ✅ **DO**: Mappare i dati nel service prima di passarli ai componenti
- ✅ **DO**: Usare mapper centralizzati da `types/`
- ✅ **DO**: Includere context necessario (userEmail, etc.) per computed properties
- ❌ **DON'T**: Passare oggetti raw ai componenti
- ❌ **DON'T**: Duplicare logica di mapping in più servizi

#### Service State Management
```typescript
export class AuctionService {
  private hubConnection: HubConnection | null = null
  private eventHandlers: AuctionEventHandlers = {}
  private userEmail: string | null = null  // ✅ Store user context

  /**
   * Set current user email for mapping auction state
   */
  setUserEmail(email: string | null): void {
    this.userEmail = email
  }
}
```

---

### 3. **Component Layer** (`screens/`, `components/`)
**Responsabilità**: UI logic e rendering, **NO mapping**

#### Component Structure
```typescript
export default function AuctionLiveScreen() {
  const { user } = useAuth()
  const [auctionState, setAuctionState] = useState<AuctionState | null>(null)

  useEffect(() => {
    // ✅ Set user context in service
    if (user?.email) {
      auctionService.setUserEmail(user.email)
    }

    // ✅ Event handlers ricevono oggetti clean
    auctionService.setEventHandlers({
      onAuctionState: (state: AuctionState) => {  // Clean interface!
        console.log('Auction state received (mapped):', state)
        setAuctionState(state)
        setTeams(state.teams)
        setIsAuctioneer(state.isAuctioneer)
      }
    })

    // ... connect and join
  }, [auctionId, user?.email])

  // ✅ Component usa proprietà clean
  return (
    <Text>{auctionState?.wrapper.creator}</Text>
    <Text>{auctionState?.isAuctioneer ? 'Banditore' : 'Partecipante'}</Text>
  )
}
```

**Best Practices**:
- ✅ **DO**: Usare solo clean interfaces
- ✅ **DO**: Passare user context al service quando necessario
- ✅ **DO**: Consumare dati già mappati
- ❌ **DON'T**: Importare raw interfaces o mapper
- ❌ **DON'T**: Fare mapping inline nel componente

---

## Event Handler Type Definitions

### Service Layer
```typescript
// services/auctionService.ts
export interface AuctionEventHandlers {
  onConnected?: (message: string) => void
  onError?: (message: string) => void
  onPlayerShown?: (player: StatPlayer) => void        // ✅ Clean interface
  onPlayerAssigned?: (data: PlayerAssignedData) => void
  onAuctionState?: (state: AuctionState) => void      // ✅ Clean interface
  onCurrentPlayer?: (player: PlayerWithStats | null) => void
}
```

### Component Usage
```typescript
// screens/auction-live.tsx
auctionService.setEventHandlers({
  onPlayerShown: (player: StatPlayer) => {  // ✅ Type-safe, clean
    setCurrentPlayer(statPlayerToPlayerWithStats(player))
  },
  onAuctionState: (state: AuctionState) => {  // ✅ Already mapped
    setAuctionState(state)
  }
})
```

---

## Benefits of This Pattern

### 1. **Separation of Concerns**
- Types: Schema + Mapping logic
- Services: Communication + Data transformation
- Components: UI logic only

### 2. **Single Source of Truth**
- Mapping logic centralizzato in `types/`
- No duplicazione nei componenti

### 3. **Type Safety**
- TypeScript compiler verifica correttezza
- No casting unsafe
- Autocomplete in IDE

### 4. **Maintainability**
- Se backend cambia, update solo in `types/`
- Mapper unit-testable
- Refactoring sicuro

### 5. **Developer Experience**
- Componenti più leggibili
- Less boilerplate
- Clear data flow

---

## Common Patterns

### Pattern 1: Simple HTTP GET
```typescript
// Service
async getPlayer(playerId: string): Promise<Player> {
  const response = await fetch(url)
  const raw: PlayerRaw = await response.json()
  return mapRawPlayer(raw)
}

// Component
const player = await playerService.getPlayer(id)
console.log(player.name)  // Clean interface
```

### Pattern 2: SignalR with User Context
```typescript
// Service
private userEmail: string | null = null

setUserEmail(email: string | null): void {
  this.userEmail = email
}

this.hubConnection.on('Event', (raw: DataRaw) => {
  const mapped = mapRawData(raw, this.userEmail)
  this.eventHandlers.onEvent?.(mapped)
})

// Component
useEffect(() => {
  service.setUserEmail(user?.email)
  service.setEventHandlers({
    onEvent: (data: Data) => setState(data)
  })
}, [user?.email])
```

### Pattern 3: Nested Mapping
```typescript
// Mapper composes other mappers
export function mapRawAuctionWrapper(raw: AuctionWrapperRaw): AuctionWrapper {
  return {
    id: raw.i,
    leagueKey: mapRawLeagueKeyToLeagueKey(raw.l),  // ✅ Nested mapper
    creator: raw.c,
    actual: raw.a ? mapRawActualAuction(raw.a) : null,  // ✅ Conditional mapping
    winnings: raw.w.map(mapRawAuctionWin),  // ✅ Array mapping
    participants: raw.pr.map(mapRawAnnualTeamToAnnualTeam)
  }
}
```

---

## Migration Checklist

When creating new API integration:

- [ ] Define `{Entity}Raw` interface in `types/`
- [ ] Define clean `{Entity}` interface in `types/`
- [ ] Create `mapRaw{Entity}` function in `types/`
- [ ] Export mapper function
- [ ] Use mapper in service layer
- [ ] Define event handler types with clean interfaces
- [ ] Update service to pass clean data to handlers
- [ ] Use clean interfaces in components
- [ ] **Never** import raw interfaces or mappers in components

---

## Anti-Patterns to Avoid

### ❌ Mapping in Components
```typescript
// BAD - component fa mapping
onAuctionState: (wrapperRaw: AuctionHubWrapperRaw) => {
  const mapped = mapRawAuctionHubWrapper(wrapperRaw, user.email, isAuctioneer)
  setState(mapped)
}
```

### ❌ Passing Raw Data to Components
```typescript
// BAD - service passa raw
this.eventHandlers.onAuctionState?.(wrapperRaw)
```

### ❌ Duplicating Mapping Logic
```typescript
// BAD - duplicazione in più posti
const teams = Object.values(raw.t).map(t => ({
  name: t.n,
  owner: t.o,
  // ...
}))
```

### ✅ Correct Pattern
```typescript
// GOOD - mapping nel service
this.hubConnection.on('AuctionState', (raw: AuctionHubWrapperRaw) => {
  const mapped = mapRawAuctionHubWrapperToAuctionState(raw, this.userEmail)
  this.eventHandlers.onAuctionState?.(mapped)
})

// GOOD - component usa clean
onAuctionState: (state: AuctionState) => {
  setState(state)
}
```

---

## File Organization Example

```
src/
├── types/
│   ├── auction.ts          # Raw interfaces, Clean interfaces, Mappers
│   ├── stat.ts             # StatPlayerRaw, StatPlayer, mappers
│   ├── team.ts             # TeamRaw, Team, mappers
│   └── rank.ts             # RankRaw, Rank, mappers
│
├── services/
│   ├── auctionService.ts   # HTTP + SignalR, usa mappers, passa clean data
│   ├── teamService.ts      # HTTP, usa mappers
│   └── gameService.ts      # HTTP, usa mappers
│
├── screens/
│   ├── auction-live.tsx    # ✅ Solo clean interfaces
│   ├── team-detail.tsx     # ✅ Solo clean interfaces
│   └── game.tsx            # ✅ Solo clean interfaces
│
└── components/
    ├── lineup/
    │   └── SoccerField.tsx  # ✅ Solo clean interfaces
    └── auction/
        └── PlayerCard.tsx   # ✅ Solo clean interfaces
```

---

## Summary

**Golden Rule**: 
> **Raw data enters at the API boundary, mapping happens in services, components only see clean interfaces.**

**Data Flow**:
```
Backend (C# + JsonPropertyName) 
  ↓
[Network Layer]
  ↓
Raw Interface (exact JSON structure)
  ↓
[Service Layer] → Mapper Function (types/)
  ↓
Clean Interface (developer-friendly)
  ↓
[Component Layer] → UI Rendering
```

Questo pattern garantisce:
- **Manutenibilità**: cambio backend → modifica solo types
- **Type Safety**: compile-time errors
- **Readability**: componenti più puliti
- **Testability**: mapper unit-testable
- **Consistency**: single source of truth

