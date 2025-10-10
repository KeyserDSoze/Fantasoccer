# 🔄 Migrazione auction-live.tsx al Nuovo AuctionService

## ✅ Modifiche Completate

### 1. **Rimosso `setUserEmail()`**
```typescript
// ❌ VECCHIO
auctionService.setUserEmail(user.email)
await auctionService.connect()

// ✅ NUOVO
await auctionService.connect(auctionId, user.email)
```

**Motivo**: Il servizio ora riceve il contesto dell'utente direttamente nella connessione, permettendo una gestione più pulita del mapping dello stato.

---

### 2. **Aggiunto Handler `onReconnecting`**
```typescript
// ✅ NUOVO
onReconnecting: () => {
  console.log('🔄 SignalR reconnecting...')
  if (isSubscribed) {
    setConnectionState('Connecting')
    toast.show('🔄 Riconnessione in corso...', { variant: 'info', duration: 2000 })
  }
}
```

**Motivo**: Fornisce feedback immediato all'utente quando inizia la riconnessione automatica.

---

### 3. **Semplificato `onReconnected`**
```typescript
// ❌ VECCHIO - Logica complessa con wait e rejoin manuale
onReconnected: async (connectionId) => {
  toast.show('🔄 Riconnesso - caricamento stato asta...', { variant: 'info', duration: 3000 })
  try {
    const joined = await auctionService.joinAuction(auctionId)
    if (!joined) {
      throw new Error('Failed to rejoin auction')
    }
    await auctionService.getAuctionState(auctionId)
    await new Promise(resolve => setTimeout(resolve, 1000)) // Wait artificiale
    toast.show('✅ Stato asta aggiornato', { variant: 'success', duration: 3000 })
  } catch (error) {
    console.error('❌ Failed to reload state after reconnect:', error)
    toast.show('⚠️ Errore ricaricamento stato - riprova a ricaricare la pagina', { variant: 'error', duration: 5000 })
  }
}

// ✅ NUOVO - Il service gestisce automaticamente il rejoin
onReconnected: async (connectionId) => {
  console.log('✅ SignalR reconnected, connectionId:', connectionId)
  
  if (isSubscribed) {
    setConnectionState('Connected')
    toast.show('✅ Riconnesso - caricamento stato asta...', { variant: 'success', duration: 3000 })
    
    // Il service ha già chiamato joinAuction automaticamente
    // Richiediamo solo lo stato aggiornato
    try {
      await auctionService.getAuctionState(auctionId)
      await auctionService.getCurrentPlayer(auctionId)
      toast.show('✅ Stato asta aggiornato', { variant: 'success', duration: 2000 })
    } catch (error) {
      console.error('❌ Failed to reload state after reconnect:', error)
      toast.show('⚠️ Errore ricaricamento stato', { variant: 'error', duration: 3000 })
    }
  }
}
```

**Vantaggi**:
- ✅ Nessun wait artificiale (500ms, 1000ms)
- ✅ Il service gestisce automaticamente il rejoin nella room
- ✅ Codice più pulito e leggibile
- ✅ Meno punti di fallimento

---

### 4. **Rimosso Wait Stabilization**
```typescript
// ❌ VECCHIO
await auctionService.connect()
await new Promise(resolve => setTimeout(resolve, 500)) // Wait stabilization
if (!auctionService.isConnected()) {
  throw new Error('Connection not established')
}

// ✅ NUOVO
await auctionService.connect(auctionId, user.email)
if (!auctionService.isConnected()) {
  throw new Error('Connection not established')
}
```

**Motivo**: Il nuovo servizio gestisce internamente la stabilizzazione della connessione, quindi il wait non è più necessario.

---

### 5. **Rimosso Wait per Eventi**
```typescript
// ❌ VECCHIO
await auctionService.getAuctionState(auctionId)
await new Promise(resolve => setTimeout(resolve, 1000)) // Wait per eventi

// ✅ NUOVO
await auctionService.getAuctionState(auctionId)
// Gli eventi arriveranno via SignalR quando pronti
```

**Motivo**: Gli eventi SignalR sono asincroni per natura. Il wait era un workaround che mascherava altri problemi (handler duplicati, broadcast bloccanti nel backend). Con le fix al backend (fire-and-forget) e al service (cleanup handler), gli eventi arrivano immediatamente.

---

### 6. **Migliorato Logging**
```typescript
// ✅ NUOVO - Logging più chiaro e informativo
console.log('🔌 Connecting to auction hub...')
await auctionService.connect(auctionId, user.email)
console.log('✅ Connected successfully')

console.log('📨 Joining auction room...')
const joined = await auctionService.joinAuction(auctionId)
console.log('✅ Joined auction room successfully')

console.log('📨 Requesting initial auction state...')
await auctionService.getAuctionState(auctionId)

console.log('✅ Initial setup complete - waiting for state events...')
```

**Vantaggi**:
- ✅ Ogni fase ha un log chiaro
- ✅ Facile debug in caso di problemi
- ✅ Emojis per identificare rapidamente il tipo di log

---

## 🔄 Flusso di Connessione Aggiornato

### Connessione Iniziale
```
1. 🔌 connect(auctionId, userEmail)
   ├─ Refresh token
   ├─ Build connection with automatic reconnect
   ├─ Setup lifecycle handlers (onclose, onreconnecting, onreconnected)
   ├─ Register event handlers
   └─ Start connection

2. 📨 joinAuction(auctionId)
   ├─ Invoke JoinAuctionAsync
   └─ Server adds to group + sends AuctionState

3. 📨 getAuctionState(auctionId)
   └─ Request current state (arrive via event)

4. 📨 getCurrentPlayer(auctionId)
   └─ Request current player (arrive via event)

5. ✅ Setup complete
   └─ Wait for events to arrive asynchronously
```

### Auto-Reconnect (Network Loss)
```
1. 🔌 Connection lost
   └─ onDisconnected(error) → Update UI

2. 🔄 SignalR auto-reconnect starts
   ├─ onReconnecting() → Show "Reconnecting..." toast
   └─ Exponential backoff (0s, 2s, 4s, 8s, 16s, 30s)

3. 🔑 accessTokenFactory called
   └─ Force refresh token

4. ✅ Reconnected
   ├─ onreconnected → Re-register handlers
   ├─ Auto-rejoin auction room (internal)
   └─ onReconnected callback → Request fresh state

5. 📨 Component requests state
   ├─ getAuctionState()
   └─ getCurrentPlayer()

6. ✅ Back to normal operation
```

---

## 📊 Confronto Prima/Dopo

| Aspetto | Vecchio | Nuovo |
|---------|---------|-------|
| **Chiamata connect** | `connect()` poi `setUserEmail()` | `connect(auctionId, email)` |
| **Wait artificiali** | 500ms + 1000ms | Nessuno |
| **Rejoin su reconnect** | Manuale nel component | Automatico nel service |
| **Handler duplicati** | Possibili | Preventi con cleanup |
| **Logging** | Sparso e generico | Strutturato con emoji |
| **Gestione errori** | Try/catch generici | Error handler specifici |
| **onReconnecting** | Non gestito | Feedback UI immediato |
| **Code lines** | ~80 righe setup | ~50 righe setup |

---

## ✅ Checklist Verifica

- [x] ❌ Rimosso `setUserEmail()`
- [x] ✅ Aggiunto parametri a `connect(auctionId, userEmail)`
- [x] ✅ Aggiunto handler `onReconnecting`
- [x] ✅ Semplificato `onReconnected` (no rejoin manuale)
- [x] ✅ Rimossi wait artificiali (500ms, 1000ms)
- [x] ✅ Migliorato logging con emoji
- [x] ✅ Cleanup già gestito (solo `leaveAuction`)
- [x] ✅ Nessun errore TypeScript

---

## 🧪 Test da Eseguire

### Scenario 1: Connessione Iniziale
1. Apri pagina auction-live
2. ✅ Verifica connessione riuscita
3. ✅ Verifica stato asta caricato
4. ✅ Verifica player corrente visualizzato

### Scenario 2: Place Bid
1. Fai un'offerta
2. ✅ Verifica offerta accettata
3. ✅ Verifica BidPlaced event ricevuto
4. ✅ Verifica UI aggiornata

### Scenario 3: Network Loss
1. Disabilita rete (DevTools → Offline)
2. ✅ Verifica toast "Connessione persa"
3. ✅ Verifica stato UI "Disconnected"
4. Riabilita rete
5. ✅ Verifica toast "Riconnessione in corso"
6. ✅ Verifica toast "Riconnesso"
7. ✅ Verifica stato ricaricato
8. ✅ Verifica player corrente aggiornato

### Scenario 4: Token Expiration
1. Aspetta scadenza token (o simula con token invalido)
2. ✅ Verifica errore 401 catturato
3. ✅ Verifica auto-refresh token
4. ✅ Verifica riconnessione automatica

### Scenario 5: Navigazione
1. Apri auction-live
2. Naviga ad altra pagina
3. ✅ Verifica `leaveAuction` chiamato
4. ✅ Verifica nessun `disconnect` chiamato
5. Torna ad auction-live
6. ✅ Verifica riconnessione funzionante

### Scenario 6: Auctioneer Actions
1. Come banditore, mostra next player
2. ✅ Verifica PlayerShown event
3. ✅ Verifica UI aggiornata per tutti i partecipanti
4. Assegna player
5. ✅ Verifica PlayerAssigned event
6. ✅ Verifica stato squadre aggiornato

---

## 🐛 Possibili Problemi e Soluzioni

### Problema: "Schermo nero dopo riconnessione"
**Causa**: Handler non re-registrati o eventi non arrivano

**Fix già applicato**:
- ✅ `registerEventHandlers()` con cleanup (`.off()` prima di `.on()`)
- ✅ Backend usa fire-and-forget per broadcast
- ✅ Rejoin automatico dopo reconnect

---

### Problema: "Token expired durante session"
**Causa**: Token non refreshato su reconnect

**Fix già applicato**:
- ✅ `accessTokenFactory` chiama `forceRefreshToken()`
- ✅ Chiamato automaticamente su ogni reconnect attempt

---

### Problema: "Offerte duplicate dopo reconnect"
**Causa**: Handler duplicati che processano eventi multipli

**Fix già applicato**:
- ✅ Cleanup handler con `.off()` prima di re-registrare
- ✅ Chiamato in `registerEventHandlers()` e dopo reconnect

---

## 📚 Riferimenti

- [auctionService.new.ts](../src/Fantasoccer.Applications/fantasoccer/services/auctionService.new.ts) - Nuovo servizio
- [auction-service-refactoring.md](./auction-service-refactoring.md) - Documentazione completa refactoring
- [bugfix-auction-signalr-connection.md](./bugfix-auction-signalr-connection.md) - Fix originale 401 errors

---

## 🎯 Risultati Attesi

Dopo questa migrazione, dovresti avere:

✅ **Connessione stabile** - Nessun blocco su JoinAuction
✅ **Auto-reconnect funzionante** - Gestito da SignalR senza intervento manuale
✅ **Token sempre valido** - Refresh automatico su ogni reconnect
✅ **Nessun handler duplicato** - Cleanup automatico
✅ **Navigazione fluida** - Connessione persiste tra pagine
✅ **Errori chiari** - Messaggi user-friendly
✅ **Code più pulito** - Meno logica boilerplate
