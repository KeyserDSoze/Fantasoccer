# Refactoring: Auction Mapping Architecture

## Objective
Centralizzare la logica di mapping dei dati dell'asta nel **service layer**, eliminando la duplicazione di codice nei componenti e seguendo il pattern architetturale corretto.

---

## Problem Statement

### Before
Il componente `auction-live.tsx` faceva il mapping manualmente:

```typescript
// ❌ BAD - mapping nel componente
onAuctionState: (wrapperRaw: AuctionHubWrapperRaw) => {
  if (isSubscribed && user?.email) {
    const teams: AuctionTeam[] = Object.values(wrapperRaw.t || {}).map(mapRawTeam)
    const realPlayers: StatPlayer[] = (wrapperRaw.r || []).map(mapRawStatPlayerToStatPlayer)
    
    const mapped: AuctionState = {
      wrapper: mapRawAuctionWrapper(wrapperRaw.w),
      lastUpdate: new Date(wrapperRaw.u),
      leagueName: wrapperRaw.ln,
      teams,
      realPlayers,
      isAuctioneer: wrapperRaw.w.c.toLowerCase() === user.email.toLowerCase(),
      userEmail: user.email
    }
    setAuctionState(mapped)
  }
}
```

**Issues**:
1. ❌ Mapping logic nel componente
2. ❌ Necessità di importare raw interfaces e mappers nel componente
3. ❌ Logica duplicata se altri componenti consumano eventi SignalR
4. ❌ User context gestito manualmente nel componente
5. ❌ Violazione del principio di Single Responsibility

---

## Solution

### 1. New Mapper Function in `types/auction.ts`

Creata nuova funzione mapper che calcola automaticamente `isAuctioneer`:

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
  
  // ✅ Compute isAuctioneer with case-insensitive comparison
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

**Benefits**:
- ✅ Logica centralizzata
- ✅ Case-insensitive comparison per email
- ✅ Computed property `isAuctioneer` inclusa
- ✅ Type-safe

---

### 2. Enhanced Service Layer (`services/auctionService.ts`)

#### Added User Context Management
```typescript
export class AuctionService {
  private hubConnection: HubConnection | null = null
  private eventHandlers: AuctionEventHandlers = {}
  private userEmail: string | null = null  // ✅ Store current user email

  /**
   * Set current user email for mapping auction state
   * Must be called before connecting to ensure correct isAuctioneer detection
   */
  setUserEmail(email: string | null): void {
    this.userEmail = email
  }
}
```

#### Updated Event Handler Type
```typescript
export interface AuctionEventHandlers {
  // ... other handlers
  onAuctionState?: (state: AuctionState) => void  // ✅ Clean interface, not raw
}
```

#### Mapping in SignalR Handler
```typescript
private registerEventHandlers(): void {
  // ... other handlers

  // ✅ State events - mapping nel service
  this.hubConnection.on('AuctionState', (wrapperRaw: AuctionHubWrapperRaw) => {
    console.log('Auction state received (raw):', wrapperRaw)
    // Map raw data to clean AuctionState with current user context
    const mapped = mapRawAuctionHubWrapperToAuctionState(wrapperRaw, this.userEmail)
    this.eventHandlers.onAuctionState?.(mapped)
  })
}
```

**Benefits**:
- ✅ Service gestisce il mapping
- ✅ User context memorizzato nel service
- ✅ Event handlers ricevono dati clean
- ✅ No raw data leak ai componenti

---

### 3. Simplified Component (`screens/auction-live.tsx`)

#### Cleaned Imports
```typescript
// Before
import {
  AuctionState,
  AuctionHubWrapperRaw,
  mapRawAuctionWrapper,
  mapRawTeam
} from 'types/auction'
import { mapRawStatPlayerToStatPlayer } from 'types/stat'

// After ✅
import {
  AuctionState,
  AuctionTeam,
  StatPlayer,
  PlayerWithStats,
  statPlayerToPlayerWithStats
} from 'types/auction'
```

#### Simplified useEffect
```typescript
useEffect(() => {
  let isSubscribed = true

  const connectAndJoin = async () => {
    try {
      // ✅ Set user email in service for mapping
      if (user?.email) {
        auctionService.setUserEmail(user.email)
      }

      // ✅ Event handlers now receive clean data
      auctionService.setEventHandlers({
        onAuctionState: (state: AuctionState) => {
          console.log('Auction state received (mapped):', state)
          if (isSubscribed) {
            setAuctionState(state)
            setTeams(state.teams)
            setIsAuctioneer(state.isAuctioneer)
          }
        }
      })

      // Connect and join...
    } catch (err) {
      // Error handling
    }
  }

  connectAndJoin()

  return () => {
    isSubscribed = false
    auctionService.leaveAuction(auctionId).catch(console.error)
    auctionService.disconnect().catch(console.error)
  }
}, [auctionId, user?.email])  // ✅ Include user.email in dependencies
```

**Benefits**:
- ✅ Componente più pulito (da ~110 righe a ~50 righe per useEffect)
- ✅ No mapping logic nel componente
- ✅ No import di raw interfaces
- ✅ Event handler semplice e leggibile
- ✅ Type-safe con autocomplete

---

## Files Changed

### 1. `types/auction.ts`
- ✅ Added `mapRawAuctionHubWrapperToAuctionState()` function
- ✅ Marked old `mapRawAuctionHubWrapper()` as `@deprecated`
- ✅ Compute `isAuctioneer` automatically in mapper

### 2. `services/auctionService.ts`
- ✅ Added `private userEmail: string | null` field
- ✅ Added `setUserEmail(email: string | null)` method
- ✅ Updated `AuctionEventHandlers.onAuctionState` to accept `AuctionState` instead of raw
- ✅ Updated SignalR handler to map raw → clean before calling event handler
- ✅ Imported new mapper function

### 3. `screens/auction-live.tsx`
- ✅ Removed raw interface imports (`AuctionHubWrapperRaw`, `mapRawTeam`, etc.)
- ✅ Removed manual mapping logic from `onAuctionState`
- ✅ Added `auctionService.setUserEmail(user.email)` call
- ✅ Simplified event handler to just set state
- ✅ Added `user?.email` to useEffect dependencies

### 4. `prompts/architecture_patterns.md` (NEW)
- ✅ Created comprehensive documentation of mapping pattern
- ✅ Explained layer architecture (Types → Services → Components)
- ✅ Provided examples and anti-patterns
- ✅ Defined best practices

---

## Testing Checklist

### Unit Tests
- [ ] Test `mapRawAuctionHubWrapperToAuctionState()` with various inputs
  - [ ] Valid user email → `isAuctioneer = true`
  - [ ] Different user email → `isAuctioneer = false`
  - [ ] Null userEmail → `isAuctioneer = false`
  - [ ] Case-insensitive email matching

### Integration Tests
- [ ] Connect to auction hub with valid user
- [ ] Verify `onAuctionState` receives clean `AuctionState`
- [ ] Verify `isAuctioneer` is correct for auctioneer user
- [ ] Verify `isAuctioneer` is correct for participant user
- [ ] Verify teams array is populated correctly
- [ ] Verify real players array is populated correctly

### Manual Testing
- [ ] Log in as auctioneer → should see "Controlli Banditore"
- [ ] Log in as participant → should NOT see "Controlli Banditore"
- [ ] Verify team highlighting works (LA TUA SQUADRA)
- [ ] Verify auction state updates in real-time
- [ ] Test with multiple users simultaneously

---

## Migration Guide for Future APIs

When integrating new APIs that follow this pattern:

### Step 1: Define Types
```typescript
// types/newEntity.ts
export interface NewEntityRaw {
  i: string    // JsonPropertyName = "i"
  n: string    // JsonPropertyName = "n"
  // ...
}

export interface NewEntity {
  id: string
  name: string
  // ...
}
```

### Step 2: Create Mapper
```typescript
export function mapRawNewEntity(raw: NewEntityRaw): NewEntity {
  return {
    id: raw.i,
    name: raw.n,
    // ...
  }
}
```

### Step 3: Update Service
```typescript
// services/newService.ts
this.hubConnection.on('NewEvent', (raw: NewEntityRaw) => {
  const mapped = mapRawNewEntity(raw)  // ✅ Map in service
  this.eventHandlers.onNewEvent?.(mapped)
})
```

### Step 4: Define Event Handler Type
```typescript
export interface NewServiceEventHandlers {
  onNewEvent?: (entity: NewEntity) => void  // ✅ Clean interface
}
```

### Step 5: Use in Component
```typescript
newService.setEventHandlers({
  onNewEvent: (entity: NewEntity) => {  // ✅ Receives clean data
    setState(entity)
  }
})
```

---

## Performance Considerations

### Before
- Mapping logic eseguito nel componente
- Re-render potenzialmente causato da re-creazione dell'handler

### After
- ✅ Mapping eseguito una volta nel service
- ✅ Event handler stabile (non causa re-render)
- ✅ Memoization possibile se necessario

---

## Backward Compatibility

### Deprecated Function
La vecchia funzione `mapRawAuctionHubWrapper()` è stata marcata come `@deprecated` ma **non rimossa** per garantire backward compatibility:

```typescript
/**
 * @deprecated Use mapRawAuctionHubWrapperToAuctionState instead
 * Kept for backward compatibility
 */
export function mapRawAuctionHubWrapper(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null,
  isAuctioneer: boolean
): AuctionState
```

**Migration Plan**:
1. ✅ Introduce new mapper with improved API
2. ✅ Update service to use new mapper
3. ✅ Update components to remove manual mapping
4. ⏳ Mark old mapper as deprecated
5. ⏳ (Future) Remove deprecated mapper in next major version

---

## Lessons Learned

### What Worked Well
1. ✅ **Centralized mapping** - singola fonte di verità
2. ✅ **Type safety** - compile-time errors prevented bugs
3. ✅ **Service-level context** - user email stored in service
4. ✅ **Clean component code** - more readable and maintainable
5. ✅ **Computed properties** - `isAuctioneer` calcolato automaticamente

### What to Improve
1. ⚠️ Consider caching mapped data if performance becomes an issue
2. ⚠️ Add unit tests for all mapper functions
3. ⚠️ Consider using a state management library (Redux/Zustand) for complex state

### Best Practices Established
1. **Never pass raw data to components**
2. **Always map in service layer**
3. **Use clean interfaces in event handlers**
4. **Store user context in service, not component**
5. **Document mapping functions with JSDoc**

---

## Conclusion

Questo refactoring ha:
- ✅ Eliminato duplicazione di codice
- ✅ Migliorato type safety
- ✅ Semplificato i componenti
- ✅ Centralizzato la logica di mapping
- ✅ Creato un pattern riusabile per future integrazioni

**Golden Rule**: 
> **Raw data enters at the API boundary, mapping happens in services, components only see clean interfaces.**

