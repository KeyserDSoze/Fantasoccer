# Guida Applicazione Modifiche a auction-live.tsx

## ✅ Modifiche Completate

1. **Import aggiornati** ✅
   - Aggiunti: `ActualAuctionRaw`, `mapRawActualAuction`, `PlayerAssignedMessageRaw`, `mapRawPlayerAssignedMessage`

2. **Dialog ricerca giocatore** ✅
   - Rimosso `forceNextPlayer()` 
   - Sostituito con `showNextPlayer(auctionId, selectedRole)`

## 🔧 Modifiche Rimanenti

### STEP FINALE: Sostituire useEffect Connessione

**File da modificare:** `auction-live.tsx`  
**Linee da sostituire:** ~474-785 (l'intero useEffect con `connectAndJoin`)

**Procedura:**

1. **Trova la sezione da sostituire** - Cerca questa riga:
   ```typescript
   useEffect(() => {
     if (!user?.email) {
       return
     }
     
     // Prevent multiple simultaneous connection attempts
     if (connectionAttemptedRef.current || isConnectingRef.current) {
   ```

2. **Sostituisci TUTTO fino al return:**
   ```typescript
   }, [auctionId, user?.email, connectionTrigger])
   ```

3. **Con il nuovo codice dal file:** `docs/auction-live-useEffect-replacement.tsx`

## 📋 Cosa Cambia nel Nuovo useEffect

### Prima (OLD):
```typescript
// 1. Setup handlers
auctionService.setEventHandlers({ ... })

// 2. Connect
await auctionService.connect(auctionId, user.email)

// 3. Join
await auctionService.joinAuction(auctionId)

// 4. Request state via SignalR
await auctionService.getAuctionState(auctionId)      // ❌ RIMOSSO
await auctionService.getCurrentPlayer(auctionId)     // ❌ RIMOSSO

// 5. Wait for events
// onAuctionState event → sets state                // ❌ RIMOSSO
// onCurrentPlayer event → sets player              // ❌ RIMOSSO
```

### Dopo (NEW):
```typescript
// 1. HTTP GET initial state
const initialState = await auctionService.getAuction(auctionId)  // ✅ NUOVO

// 2. Set state from HTTP
setAuctionState(initialState)
setTeams(initialState.teams)
if (initialState.wrapper.actual) {
  setCurrentPlayer(initialState.wrapper.actual.player)
  setCurrentBid(initialState.wrapper.actual.price)
  // ...
}

// 3. Setup NEW event handlers
auctionService.setEventHandlers({
  onPlayerShown: (actualAuctionRaw: ActualAuctionRaw) => { ... },    // ✅ NUOVO
  onPlayerAssigned: (dataRaw: PlayerAssignedMessageRaw) => { ... },  // ✅ NUOVO
  onTeamUpdated: (team) => { ... },                                  // ✅ NUOVO
  onTimerUpdate: (seconds) => { ... },                               // ✅ NUOVO
  // ...
})

// 4. Connect
await auctionService.connect(auctionId, user.email)

// 5. Join
await auctionService.joinAuction(auctionId)

// 6. DONE - State already loaded from HTTP!
```

## 🎯 Event Handlers - Cosa Cambia

### onPlayerShown
```typescript
// PRIMA
onPlayerShown: (player: StatPlayer) => {
  setCurrentPlayer(player)
  setCurrentBid(0)
  setCurrentBidder(null)
}

// DOPO
onPlayerShown: (actualAuctionRaw: ActualAuctionRaw) => {
  const actualAuction = mapRawActualAuction(actualAuctionRaw)
  setCurrentPlayer(actualAuction.player)
  setCurrentBid(actualAuction.price)        // ✅ Include prezzo esistente!
  setCurrentBidder(actualAuction.owner)      // ✅ Include offerente!
  setCurrentSubstitute(actualAuction.substituted)
}
```

### onPlayerAssigned
```typescript
// PRIMA
onPlayerAssigned: (data: PlayerAssignedData) => {
  // Solo notifica, nessun team aggiornato
  toast.show(`${data.playerName} → ${data.teamName}`)
  // Dovevi richiedere lo stato completo
}

// DOPO
onPlayerAssigned: (dataRaw: PlayerAssignedMessageRaw) => {
  const data = mapRawPlayerAssignedMessage(dataRaw)
  
  // Mostra notifica
  toast.show(`${data.playerAssignedInAuction.playerName} → ${data.playerAssignedInAuction.teamName}`)
  
  // Aggiorna team direttamente dall'evento!
  if (data.team) {
    setTeams(prev => {
      const index = prev.findIndex(t => t.key?.email === data.team?.key?.email)
      updated[index] = data.team  // ✅ Team già aggiornato!
    })
  }
}
```

### onTeamUpdated (NUOVO)
```typescript
// Ricevi aggiornamento singolo team (es. dopo cambio stato giocatore)
onTeamUpdated: (team) => {
  setTeams(prev => {
    const index = prev.findIndex(t => t.key?.email === team.key?.email)
    updated[index] = team
  })
}
```

### onTimerUpdate (NUOVO)
```typescript
// Ricevi nuovo valore timer quando banditore lo cambia
onTimerUpdate: (seconds) => {
  setOfferTimeoutSeconds(seconds)
  toast.show(`⏱️ Timer: ${seconds}s`)
}
```

### onBidPlaced
```typescript
// PRIMA
onBidPlaced: (data: BidPlacedData) => {
  setCurrentBid(data.amount)        // ❌ Lowercase
  setCurrentBidder(data.bidderEmail) // ❌ Lowercase
}

// DOPO
onBidPlaced: (data: BidPlacedData) => {
  setCurrentBid(data.Amount)         // ✅ PascalCase (C# JSON)
  setCurrentBidder(data.BidderEmail)  // ✅ PascalCase
  setCurrentSubstitute(data.Substituted)
}
```

## ⚠️ Eventi Rimossi

Questi eventi NON esistono più nel nuovo backend:

- ❌ `onAuctionState` - Sostituito da HTTP GET iniziale
- ❌ `onCurrentPlayer` - Incluso in HTTP response
- ❌ `onAuctionUpdated` - Sostituito da eventi specifici (TeamUpdated, etc.)

Se li trovi ancora nel codice, rimuovili!

## ✅ Checklist Finale

Prima di testare:

- [ ] Import contengono `ActualAuctionRaw`, `mapRawActualAuction`, `PlayerAssignedMessageRaw`, `mapRawPlayerAssignedMessage`
- [ ] useEffect sostituito con nuovo codice (HTTP GET iniziale)
- [ ] onPlayerShown riceve `ActualAuctionRaw` e usa `mapRawActualAuction`
- [ ] onPlayerAssigned riceve `PlayerAssignedMessageRaw` e usa `mapRawPlayerAssignedMessage`
- [ ] onTeamUpdated handler presente
- [ ] onTimerUpdate handler presente
- [ ] onBidPlaced usa `data.Amount`, `data.BidderEmail`, `data.Substituted` (PascalCase)
- [ ] Nessun riferimento a `getAuctionState()`, `getCurrentPlayer()`, `forceNextPlayer()`
- [ ] Nessun handler `onAuctionState`, `onCurrentPlayer`, `onAuctionUpdated`

## 🚀 Test Flow

1. **Caricamento iniziale:**
   - HTTP GET `/Auction/GetAuction` → Ricevi stato completo
   - SignalR connect → Connessione stabilita
   - SignalR join → Unito alla room

2. **Mostra giocatore:**
   - Banditore: click "Prossimo"
   - Evento `PlayerShown` → Ricevi `ActualAuction` completo
   - UI aggiorna player con prezzo/offerente esistenti

3. **Offerta:**
   - User: click "+1"
   - Evento `BidPlaced` → Ricevi offerta
   - Timer si resetta

4. **Assegnazione:**
   - Banditore: click "Aggiudica"
   - Evento `PlayerAssigned` → Ricevi `PlayerAssignedMessage` con team aggiornato
   - UI aggiorna lista team senza chiamate extra

5. **Cambio status:**
   - Banditore: cambia stato giocatore
   - Evento `TeamUpdated` → Ricevi team aggiornato
   - UI aggiorna quel team specifico

## 📁 File di Riferimento

- **Nuovo servizio:** `services/auctionService.refactored.ts` (rinominare → `auctionService.ts`)
- **Tipi aggiornati:** `types/auction.ts` (già modificato)
- **Template useEffect:** `docs/auction-live-useEffect-replacement.tsx`
- **Guida completa:** `docs/auction-live-refactoring-guide.md`

---

**Note:** Tutte le altre parti del file (grafica, UI, logica bid, dialogs, etc.) rimangono INALTERATE. Solo la sezione di connessione cambia!
