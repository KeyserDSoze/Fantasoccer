# Modifiche Puntuali Rimanenti - auction-live.tsx

## Errori da Correggere

Ci sono 10 errori da sistemare nel file `auction-live.tsx`. Tutti sono nel vecchio useEffect di connessione che deve essere sostituito.

### SOLUZIONE RAPIDA

Sostituisci l'intero `useEffect` (linee ~474-785) con il contenuto di:
`docs/auction-live-useEffect-replacement.tsx`

### OPPURE - Modifiche Puntuali

Se preferisci sistemare manualmente gli errori uno per uno:

---

## 1. Linea ~527-528 - onReconnected

**ERRORE:**
```typescript
await auctionService.getAuctionState(auctionId)
await auctionService.getCurrentPlayer(auctionId)
```

**SOLUZIONE - RIMUOVI COMPLETAMENTE:**
```typescript
onReconnected: (connectionId) => {
  console.log('✅ SignalR reconnected')
  if (isSubscribed) {
    setConnectionState('Connected')
    toast.show('✅ Riconnesso', { variant: 'success', duration: 2000 })
    // Service ha già fatto rejoin automaticamente
    // Stato client-side è già aggiornato, non serve ricaricarlo
  }
},
```

---

## 2. Linea ~557 - onPlayerShown

**ERRORE:**
```typescript
onPlayerShown: (player: StatPlayer) => {
  if (isSubscribed) {
    const roleChanged = currentPlayer && currentPlayer.role !== player.role
    setCurrentPlayer(player)
    setCurrentBid(0)
    setCurrentBidder(null)
    setCurrentSubstitute(null)
    // ...
  }
},
```

**SOLUZIONE:**
```typescript
onPlayerShown: (actualAuctionRaw: ActualAuctionRaw) => {
  if (!isSubscribed) return
  
  try {
    const actualAuction = mapRawActualAuction(actualAuctionRaw)
    const roleChanged = currentPlayer && currentPlayer.role !== actualAuction.player.role
    
    setCurrentPlayer(actualAuction.player)
    setCurrentBid(actualAuction.price)          // ✅ Da ActualAuction
    setCurrentBidder(actualAuction.owner)        // ✅ Da ActualAuction
    setCurrentSubstitute(actualAuction.substituted) // ✅ Da ActualAuction
    setFirstOfferHappened(actualAuction.price > 0)
    
    if (roleChanged) {
      setMySelectedSubstitute(null)
      setAuctioneerSelectedSubstitute(null)
    }
    
    setPlayerSearchQuery('')
    setSelectedPlayerForForce('')
    setIsLocked(false)
    setTempBlockButtons(false)
    
    playBidSound()
    vibrateOnBid()
    toast.show(`🎯 ${actualAuction.player.name} - ${getRoleLabel(actualAuction.player.role)}`, {
      variant: 'success',
      duration: 3000
    })
    
    resetTimer()
  } catch (error) {
    console.error('Error processing PlayerShown:', error)
  }
},
```

---

## 3. Linea ~580 - onPlayerAssigned

**ERRORE:**
```typescript
onPlayerAssigned: (data: PlayerAssignedData) => {
  if (isSubscribed) {
    playBidSound()
    vibrateOnBid()
    toast.show(`✅ Giocatore assegnato: ${data.playerName} a ${data.teamName} per €${data.price}...`)
    
    if (user?.email && data.owner.toLowerCase() === user.email.toLowerCase()) {
      setMySelectedSubstitute(null)
    }
    if (selectedTeamForBid && data.owner.toLowerCase() === selectedTeamForBid.toLowerCase()) {
      setAuctioneerSelectedSubstitute(null)
    }
    
    setCurrentPlayer(null)
    setCurrentBid(0)
    setCurrentBidder(null)
    setCurrentSubstitute(null)
    setAssignPrice('')
    resetTimer()
  }
},
```

**SOLUZIONE:**
```typescript
onPlayerAssigned: (dataRaw: PlayerAssignedMessageRaw) => {
  if (!isSubscribed) return
  
  try {
    const data = mapRawPlayerAssignedMessage(dataRaw)
    
    if (data.playerAssignedInAuction) {
      const a = data.playerAssignedInAuction
      
      playBidSound()
      vibrateOnBid()
      
      toast.show(
        `🎯 ${a.playerName} assegnato a ${a.teamName} per ${a.price} crediti`,
        { variant: 'success', duration: 4000 }
      )
      
      // Add to sales history
      setSalesHistory(prev => [...prev, {
        playerKey: getPlayerKey(a.playerName),
        owner: a.owner,
        price: a.price
      }])
      
      // Reset substitutes for winner
      if (user?.email && a.owner.toLowerCase() === user.email.toLowerCase()) {
        setMySelectedSubstitute(null)
      }
      if (selectedTeamForBid && a.owner.toLowerCase() === selectedTeamForBid.toLowerCase()) {
        setAuctioneerSelectedSubstitute(null)
      }
    }
    
    // ✅ NUOVO: Update team from event (no extra HTTP call!)
    if (data.team) {
      setTeams(prev => {
        const index = prev.findIndex(t => t.key?.email === data.team?.key?.email)
        if (index >= 0) {
          const updated = [...prev]
          updated[index] = data.team
          return updated
        }
        return prev
      })
      
      // Update myTeam if it's mine
      if (data.team.key?.email?.toLowerCase() === user.email.toLowerCase()) {
        setMyTeam(data.team)
        const cost = TeamHelper.getCost(data.team.value)
        setMyTeamBudget(startingMoney - cost)
      }
    }
    
    setCurrentPlayer(null)
    setCurrentBid(0)
    setCurrentBidder(null)
    setCurrentSubstitute(null)
    setFirstOfferHappened(false)
    setAssignPrice('')
    setTimerRunning(false)
    setTimerExpired(false)
    resetTimer()
  } catch (error) {
    console.error('Error processing PlayerAssigned:', error)
  }
},
```

---

## 4. Linea ~607-609 - onBidPlaced

**ERRORE:**
```typescript
setCurrentBid(data.amount)        // ❌ lowercase
setCurrentBidder(data.bidderEmail) // ❌ lowercase
setCurrentSubstitute(data.substituted) // ❌ lowercase
```

**SOLUZIONE:**
```typescript
setCurrentBid(data.Amount)         // ✅ PascalCase
setCurrentBidder(data.BidderEmail)  // ✅ PascalCase
setCurrentSubstitute(data.Substituted) // ✅ PascalCase
```

Contesto completo:
```typescript
onBidPlaced: (data: BidPlacedData) => {
  if (!isSubscribed) return
  
  setCurrentBid(data.Amount)
  setCurrentBidder(data.BidderEmail)
  setCurrentSubstitute(data.Substituted)
  setFirstOfferHappened(true)
  
  resetTimer()
  startTimer()
  setTempBlockButtons(false)
  
  playBidSound()
  vibrateOnBid()
  
  const bidderName = data.BidderName || data.BidderEmail
  toast.show(`💰 ${bidderName}: ${data.Amount} crediti`, {
    variant: 'info',
    duration: 2000
  })
},
```

---

## 5. Linea ~654 - onAuctionUpdated

**ERRORE:**
```typescript
onAuctionUpdated: () => {
  auctionService.getAuctionState(auctionId).catch(console.error)
},
```

**SOLUZIONE - RIMUOVI COMPLETAMENTE:**
L'evento `onAuctionUpdated` non esiste più. Ora usi eventi specifici:
- `onTeamUpdated` per aggiornamenti singoli team
- `onPlayerAssigned` include già il team aggiornato
- `onTimerUpdate` per cambio timer

```typescript
// ❌ Rimuovi questo handler completamente
// onAuctionUpdated non esiste più nel nuovo backend
```

---

## 6. Linea ~714-717 - Caricamento Iniziale

**ERRORE:**
```typescript
console.log('📨 Requesting initial auction state...')
await auctionService.getAuctionState(auctionId)

console.log('📨 Requesting current player...')
await auctionService.getCurrentPlayer(auctionId)
```

**SOLUZIONE - Sostituisci con HTTP GET:**

Questa sezione fa parte del vecchio flusso. Nel nuovo flusso:

1. PRIMA chiami HTTP GET
2. POI connetti SignalR
3. POI fai join

```typescript
// ✅ NUOVO FLUSSO (tutto il useEffect va riscritto)

// STEP 1: HTTP GET
const initialState = await auctionService.getAuction(auctionId)

// STEP 2: Set state
setAuctionState(initialState)
setTeams(initialState.teams)
if (initialState.wrapper.actual) {
  setCurrentPlayer(initialState.wrapper.actual.player)
  setCurrentBid(initialState.wrapper.actual.price)
  // ...
}

// STEP 3: Setup handlers
auctionService.setEventHandlers({ ... })

// STEP 4: Connect
await auctionService.connect(auctionId, user.email)

// STEP 5: Join
await auctionService.joinAuction(auctionId)

// DONE! No more getAuctionState/getCurrentPlayer calls
```

---

## 7. AGGIUNGI NUOVI HANDLERS

Dopo aver sistemato gli errori, aggiungi questi 2 nuovi handler:

### onTeamUpdated
```typescript
onTeamUpdated: (team) => {
  if (!isSubscribed) return
  
  setTeams(prev => {
    const index = prev.findIndex(t => t.key?.email === team.key?.email)
    if (index >= 0) {
      const updated = [...prev]
      updated[index] = team
      return updated
    }
    return [...prev, team]
  })
  
  // Update myTeam if it's mine
  if (team.key?.email?.toLowerCase() === user.email.toLowerCase()) {
    setMyTeam(team)
    const cost = TeamHelper.getCost(team.value)
    setMyTeamBudget(startingMoney - cost)
  }
  
  // Update selectedTeamDetail if dialog is open
  if (selectedTeamDetail && team.key?.email === selectedTeamDetail.key?.email) {
    setSelectedTeamDetail(team)
  }
},
```

### onTimerUpdate
```typescript
onTimerUpdate: (seconds) => {
  if (!isSubscribed) return
  
  setOfferTimeoutSeconds(seconds)
  toast.show(`⏱️ Timer impostato a ${seconds} secondi`, {
    variant: 'info',
    duration: 2000
  })
},
```

---

## Riepilogo Modifiche

| Linea | Tipo | Azione |
|-------|------|--------|
| ~527-528 | Rimuovi | `getAuctionState()` e `getCurrentPlayer()` in onReconnected |
| ~557 | Sostituisci | onPlayerShown con nuovo signature ActualAuctionRaw |
| ~580 | Sostituisci | onPlayerAssigned con nuovo signature PlayerAssignedMessageRaw |
| ~607-609 | Fix | Proprietà BidPlacedData: lowercase → PascalCase |
| ~654 | Rimuovi | Tutto l'handler onAuctionUpdated |
| ~714-717 | Sostituisci | HTTP GET invece di SignalR getState |
| N/A | Aggiungi | Handler onTeamUpdated |
| N/A | Aggiungi | Handler onTimerUpdate |

---

**RACCOMANDAZIONE FINALE:**

Invece di fare queste 8+ modifiche manuali, **sostituisci l'intero useEffect** con il contenuto di:
`docs/auction-live-useEffect-replacement.tsx`

È più sicuro e veloce!
