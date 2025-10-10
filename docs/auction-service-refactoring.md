# 🔄 AuctionService - Refactoring Completo

## 📋 Panoramica

Questo documento descrive il refactoring completo di `auctionService.ts` per risolvere i problemi di gestione della connessione SignalR e migliorare la robustezza dell'applicazione.

## ❌ Problemi del Vecchio Servizio

### 1. **Token Refresh Eccessivo**
```typescript
// ❌ VECCHIO: Token refreshed 2+ volte per ogni connessione
async connect(): Promise<void> {
  const token = await tokenStorageService.forceRefreshToken() // Refresh #1
  // ...
  accessTokenFactory: async () => {
    return await tokenStorageService.forceRefreshToken() // Refresh #2
  }
}
```

### 2. **Event Handlers Duplicati**
```typescript
// ❌ VECCHIO: registerEventHandlers() chiamato più volte senza cleanup
this.hubConnection.on('AuctionState', ...) // Prima registrazione
// Dopo reconnect:
this.hubConnection.on('AuctionState', ...) // Seconda registrazione → DUPLICATI!
```

### 3. **Gestione Stato Connessione Mancante**
```typescript
// ❌ VECCHIO: Nessun tracking dello stato, logica sparsa
if (this.hubConnection?.state === 'Connected') { ... }
// Ma nessun modo unificato per controllare lo stato
```

### 4. **Disconnessione Manuale Problematica**
```typescript
// ❌ VECCHIO: Component chiama disconnect() manualmente
useEffect(() => {
  return () => {
    auctionService.disconnect() // ❌ Forza chiusura, previene auto-reconnect
  }
}, [])
```

### 5. **Singleton con Stato Sporco**
```typescript
// ❌ VECCHIO: Una singola istanza può accumulare handler
export const auctionService = new AuctionService()
// Usato da più componenti → possibili memory leak
```

### 6. **Gestione Errori Generica**
```typescript
// ❌ VECCHIO: Troppi try/catch generici
catch (error) {
  throw error // Non discrimina tra tipi di errore
}
```

## ✅ Soluzioni del Nuovo Servizio

### 1. **Token Refresh Ottimizzato**
```typescript
// ✅ NUOVO: Token refreshed solo quando necessario
async connect(auctionId: string, userEmail: string): Promise<void> {
  // Refresh iniziale
  const token = await tokenStorageService.forceRefreshToken()
  
  // accessTokenFactory: refresh solo su reconnect automatici
  accessTokenFactory: async () => {
    return await tokenStorageService.forceRefreshToken()
  }
}
```

**Vantaggi:**
- Refresh esplicito all'inizio
- Refresh automatico su ogni riconnessione SignalR
- Nessun refresh ridondante

### 2. **Event Handlers con Cleanup**
```typescript
// ✅ NUOVO: Cleanup prima di re-registrare
private registerEventHandlers(): void {
  // Rimuovi tutti i vecchi handler
  this.hubConnection.off('AuctionState')
  this.hubConnection.off('BidPlaced')
  // ... tutti gli altri
  
  // Registra nuovi handler
  this.hubConnection.on('AuctionState', ...)
}
```

**Vantaggi:**
- Nessun handler duplicato
- Safe per chiamate multiple
- Prevenzione memory leak

### 3. **Connection State Management**
```typescript
// ✅ NUOVO: Tipo specifico per stato connessione
export type ConnectionStatus = 
  | 'Disconnected' 
  | 'Connecting' 
  | 'Connected' 
  | 'Reconnecting' 
  | 'Failed'

getConnectionState(): ConnectionStatus {
  if (!this.hubConnection) return 'Disconnected'
  // Mapping chiaro dallo stato SignalR
  switch (this.hubConnection.state) {
    case 'Connected': return 'Connected'
    // ...
  }
}
```

**Vantaggi:**
- Stato chiaro e tipizzato
- Facile da usare nella UI
- Consistente in tutta l'app

### 4. **Disconnessione Solo Quando Necessario**
```typescript
// ✅ NUOVO: SignalR gestisce la disconnessione automaticamente
useEffect(() => {
  return () => {
    // Solo notifica server che lasciamo la room
    auctionService.leaveAuction(auctionId)
    // SignalR chiuderà la connessione quando il browser/tab si chiude
  }
}, [])
```

**Vantaggi:**
- Auto-reconnect funziona correttamente
- Navigazione tra pagine non causa problemi
- Gestione timeout automatica

### 5. **Lifecycle Chiaro**
```typescript
// ✅ NUOVO: Lifecycle esplicito con cleanup interno
private async cleanup(): Promise<void> {
  // Cleanup gestito internamente, non esposto
}

async disconnect(): Promise<void> {
  // Solo per chiusura app completa
  await this.cleanup()
}
```

**Vantaggi:**
- Cleanup controllato internamente
- Nessuna esposizione di metodi pericolosi
- State management consistente

### 6. **Gestione Errori Specifica**
```typescript
// ✅ NUOVO: Error handling discriminato per tipo
private handleInvokeError(error: unknown, operation: string): Error {
  const errorMsg = error instanceof Error ? error.message : String(error)
  
  // Errori di autenticazione
  if (errorMsg.includes('401') || errorMsg.includes('Unauthorized')) {
    return new Error('Sessione scaduta - ricarica la pagina')
  }
  
  // Errori di connessione
  if (errorMsg.includes('Connection')) {
    return new Error('Connessione persa')
  }
  
  // Errore generico con contesto
  return new Error(`Errore durante ${operation}: ${errorMsg}`)
}
```

**Vantaggi:**
- Messaggi utente-friendly
- Errori discriminati per tipo
- Facile debug con contesto

## 🔄 Come Usare il Nuovo Servizio

### Setup Iniziale (Component Mount)

```typescript
useEffect(() => {
  let isSubscribed = true

  const setupAuction = async () => {
    try {
      // 1. Setup event handlers PRIMA di connettere
      auctionService.setEventHandlers({
        onConnected: (connectionId) => {
          console.log('Connected:', connectionId)
          setConnectionState('Connected')
        },
        onReconnecting: () => {
          console.log('Reconnecting...')
          setConnectionState('Reconnecting')
        },
        onReconnected: async (connectionId) => {
          console.log('Reconnected:', connectionId)
          // Il service ha già chiamato joinAuction automaticamente
          // Richiedi solo lo stato aggiornato
          await auctionService.getAuctionState(auctionId)
        },
        onDisconnected: (error) => {
          console.log('Disconnected:', error)
          setConnectionState('Disconnected')
        },
        onAuctionState: (state) => {
          if (isSubscribed) {
            setAuctionState(state)
          }
        },
        // ... altri handler
      })

      // 2. Connetti (include automaticamente: connect + join + getState)
      await auctionService.connect(auctionId, user.email)

      // 3. Join alla room
      const joined = await auctionService.joinAuction(auctionId)
      if (!joined) {
        throw new Error('Failed to join auction')
      }

      // 4. Richiedi stato iniziale
      await auctionService.getAuctionState(auctionId)
      await auctionService.getCurrentPlayer(auctionId)

    } catch (error) {
      console.error('Setup failed:', error)
      if (isSubscribed) {
        setConnectionError(error.message)
      }
    }
  }

  setupAuction()

  // Cleanup
  return () => {
    isSubscribed = false
    // Solo lascia la room, NON disconnettere
    auctionService.leaveAuction(auctionId)
  }
}, [auctionId, user.email])
```

### Place Bid (User Action)

```typescript
const handlePlaceBid = async () => {
  try {
    await auctionService.placeBid(auctionId, bidAmount, selectedTeam)
    // Il server invierà BidPlaced event → handler verrà chiamato
  } catch (error) {
    toast.show(error.message, { variant: 'error' })
  }
}
```

### Auctioneer Actions

```typescript
const handleShowNextPlayer = async (role: Role) => {
  try {
    await auctionService.showNextPlayer(auctionId, role)
    // Il server invierà PlayerShown event → handler verrà chiamato
  } catch (error) {
    toast.show(error.message, { variant: 'error' })
  }
}
```

## 🎯 Sequence Diagram

```
Component                   AuctionService              SignalR Hub              Server
    |                            |                           |                      |
    |-- setEventHandlers() ---->|                           |                      |
    |                            |                           |                      |
    |-- connect(id, email) ----->|                           |                      |
    |                            |-- forceRefreshToken() --->|                      |
    |                            |<-- token ----------------|                      |
    |                            |                           |                      |
    |                            |-- new HubConnectionBuilder                      |
    |                            |-- withAutomaticReconnect                        |
    |                            |-- start() --------------->|                      |
    |                            |                           |-- negotiate -------->|
    |                            |                           |<-- connectionId -----|
    |                            |<-- connected -------------|                      |
    |<-- onConnected(id) --------|                           |                      |
    |                            |                           |                      |
    |-- joinAuction(id) -------->|                           |                      |
    |                            |-- invoke(JoinAuctionAsync)                      |
    |                            |                           |-- JoinAuction ------>|
    |                            |                           |<-- true -------------|
    |                            |<-- true ------------------|                      |
    |<-- true -------------------|                           |                      |
    |                            |                           |                      |
    |-- getAuctionState(id) ---->|                           |                      |
    |                            |-- invoke(SendAuctionStateAsync)                 |
    |                            |                           |-- GetState --------->|
    |                            |<-- void ------------------|                      |
    |                            |                           |<-- AuctionState -----|
    |                            |<-- on('AuctionState') ----|                      |
    |<-- onAuctionState(state) --|                           |                      |
    |                            |                           |                      |
    
    [CONNECTION LOST - Network Issue]
    |                            |                           |                      |
    |                            |<-- onclose(error) --------|                      |
    |<-- onDisconnected(error) --|                           |                      |
    |                            |                           |                      |
    |                            |<-- onreconnecting() ------|                      |
    |<-- onReconnecting() -------|                           |                      |
    |                            |                           |                      |
    |                            |-- accessTokenFactory() -->|                      |
    |                            |-- forceRefreshToken() --->|                      |
    |                            |<-- fresh token -----------|                      |
    |                            |                           |-- reconnect -------->|
    |                            |                           |<-- connectionId -----|
    |                            |<-- onreconnected(id) -----|                      |
    |                            |                           |                      |
    |                            |-- registerEventHandlers() |                      |
    |                            |-- joinAuction(id) ------->|                      |
    |                            |                           |-- JoinAuction ------>|
    |                            |                           |<-- true -------------|
    |<-- onReconnected(id) ------|                           |                      |
    |                            |                           |                      |
    |-- getAuctionState(id) ---->|                           |                      |
    |                            |-- invoke(SendAuctionStateAsync)                 |
    |                            |                           |-- GetState --------->|
    |                            |<-- on('AuctionState') ----|<-- AuctionState -----|
    |<-- onAuctionState(state) --|                           |                      |
    |                            |                           |                      |

    [USER NAVIGATES AWAY]
    |                            |                           |                      |
    |-- leaveAuction(id) ------->|                           |                      |
    |                            |-- invoke(LeaveAuctionAsync)                     |
    |                            |                           |-- LeaveAuction ----->|
    |                            |<-- void ------------------|                      |
    |<-- void -------------------|                           |                      |
    |                            |                           |                      |
    |                            | [NO disconnect() call]    |                      |
    |                            | [SignalR keeps connection alive for reuse]      |
```

## 📝 Checklist Migrazione

- [ ] **Backup del vecchio servizio**
  ```bash
  cp auctionService.ts auctionService.old.ts
  ```

- [ ] **Sostituisci il file**
  ```bash
  cp auctionService.new.ts auctionService.ts
  ```

- [ ] **Aggiorna component `auction-live.tsx`**
  - [ ] Rimuovi `disconnect()` dal cleanup
  - [ ] Usa `connect(auctionId, userEmail)` invece di `connect()` + `setUserEmail()`
  - [ ] Aggiungi handler `onReconnecting`
  - [ ] Rimuovi logica manuale di riconnessione (gestita da SignalR)

- [ ] **Test scenari critici**
  - [ ] Connessione iniziale
  - [ ] Place bid
  - [ ] Show next player (auctioneer)
  - [ ] Reconnect dopo 401
  - [ ] Reconnect dopo network loss
  - [ ] Navigazione tra pagine
  - [ ] Chiusura tab/browser

## 🚀 Benefici Attesi

1. **✅ Nessun blocco su JoinAuction** - Fire-and-forget per broadcast
2. **✅ Auto-reconnect funzionante** - Nessuna disconnessione manuale
3. **✅ Token sempre valido** - Refresh automatico su reconnect
4. **✅ Nessun handler duplicato** - Cleanup prima di registrare
5. **✅ Navigazione fluida** - Connessione persiste tra pagine
6. **✅ Errori chiari** - Messaggi utente-friendly
7. **✅ Code più pulito** - Lifecycle chiaro e gestito

## 📚 Riferimenti

- [SignalR Automatic Reconnect](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-8.0#reconnect-clients)
- [SignalR Connection Lifecycle](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-8.0#handle-lost-connection)
- [Best Practices for SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-8.0)
