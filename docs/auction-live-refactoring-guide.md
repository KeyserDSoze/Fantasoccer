# Auction Live Refactoring Guide

## Changes Required for New Backend API

### 1. Initial State Loading - NEW FLOW

**OLD FLOW** (removed):
```typescript
// After connection
await auctionService.getAuctionState(auctionId)  // SignalR method
await auctionService.getCurrentPlayer(auction Id)  // SignalR method
// Wait for AuctionState and CurrentPlayer events
```

**NEW FLOW**:
```typescript
// BEFORE connection - HTTP GET
const initialState = await auctionService.getAuction(auctionId)  // HTTP API

// Set all state from HTTP response
setAuctionState(initialState)
setTeams(initialState.teams)
setIsAuctioneer(initialState.isAuctioneer)
// ... etc

// THEN connect to SignalR
await auctionService.connect(auctionId, user.email)
await auctionService.joinAuction(auctionId)

// Events will come from SignalR for updates only
```

### 2. Event Handlers - UPDATED

#### PlayerShown - Changed Signature
```typescript
// OLD
onPlayerShown: (player: StatPlayer) => {
  setCurrentPlayer(player)
}

// NEW - Receives full ActualAuction object
onPlayerShown: (actualAuctionRaw: ActualAuctionRaw) => {
  const actualAuction = mapRawActualAuction(actualAuctionRaw)
  
  setCurrentPlayer(actualAuction.player)
  setCurrentBid(actualAuction.price)
  setCurrentBidder(actualAuction.owner)
  setCurrentSubstitute(actualAuction.substituted)
  setFirstOfferHappened(actualAuction.price > 0)
  
  // Timer logic
  if (actualAuction.startingTime) {
    // Timer is running
    setTimerRunning(true)
  } else {
    setTimerRunning(false)
  }
}
```

#### PlayerAssigned - Changed Signature
```typescript
// OLD
onPlayerAssigned: (data: PlayerAssignedData) => {
  // data was just { owner, teamName, price, playerName, teamCost, teamPlayersCount }
  // Had to manually request updated state
}

// NEW - Receives PlayerAssignedMessage with full team data
onPlayerAssigned: (dataRaw: PlayerAssignedMessageRaw) => {
  const data = mapRawPlayerAssignedMessage(dataRaw)
  
  toast.show(`🎯 ${data.playerAssignedInAuction?.playerName} assegnato a ${data.playerAssignedInAuction?.teamName} per ${data.playerAssignedInAuction?.price} crediti`, ...)
  
  // Update team directly from event (no need to request full state)
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
  }
  
  // Clear current auction
  setCurrentPlayer(null)
  setCurrentBid(0)
  setCurrentBidder(null)
  setCurrentSubstitute(null)
  setFirstOfferHappened(false)
  setTimerRunning(false)
}
```

#### TeamUpdated - NEW Event
```typescript
// NEW - Single team update (e.g., after changing player status)
onTeamUpdated: (team: Entity<Team, TeamKey>) => {
  console.log('📨 Team updated:', team)
  
  setTeams(prev => {
    const index = prev.findIndex(t => t.key?.email === team.key?.email)
    if (index >= 0) {
      const updated = [...prev]
      updated[index] = team
      return updated
    }
    return [...prev, team]
  })
}
```

#### TimerUpdate - NEW Event
```typescript
// NEW - Timer duration changed by auctioneer
onTimerUpdate: (seconds: number) => {
  console.log('📨 Timer duration updated:', seconds)
  setOfferTimeoutSeconds(seconds)
  
  toast.show(`⏱️ Timer impostato a ${seconds} secondi`, {
    variant: 'info',
    duration: 2000
  })
}
```

#### UserJoined - Simplified
```typescript
// OLD - Triggered state reload
onUserJoined: async (email) => {
  await auctionService.getAuctionState(auctionId)  // Get updated participants
}

// NEW - Just notification, no state reload needed
onUserJoined: (email) => {
  console.log('📨 User joined:', email)
  toast.show(`✅ ${email} si è unito all'asta`, { variant: 'success', duration: 2000 })
}
```

### 3. Removed Methods/Events

#### Removed SignalR Methods:
- ❌ `getAuctionState()` - Use HTTP `getAuction()` instead
- ❌ `getCurrentPlayer()` - Use HTTP `getAuction()` instead
- ❌ `forceNextPlayer()` - Removed from backend

#### Removed Events:
- ❌ `AuctionState` - No longer sent by backend
- ❌ `CurrentPlayer` - No longer sent by backend
- ❌ `AuctionUpdated` - Use specific events instead

### 4. Reconnection Logic - Simplified

```typescript
// OLD
onReconnected: async () => {
  // Service auto-rejoins
  await auctionService.getAuctionState(auctionId)
  await auctionService.getCurrentPlayer(auctionId)
}

// NEW
onReconnected: () => {
  // Service auto-rejoins
  // State is maintained client-side
  // Only new events will update state
  toast.show('✅ Riconnesso all\'asta', { variant: 'success' })
}
```

### 5. Complete Connection Flow

```typescript
useEffect(() => {
  if (!user?.email) return
  
  let isSubscribed = true
  setLoading(true)
  
  const initialize = async () => {
    try {
      // STEP 1: Get initial state via HTTP
      console.log('📥 Fetching initial auction state...')
      const initialState = await auctionService.getAuction(auctionId)
      
      if (!isSubscribed) return
      
      // STEP 2: Set all state from HTTP response
      console.log('✅ Initial state loaded:', initialState)
      setAuctionState(initialState)
      setTeams(initialState.teams)
      setIsAuctioneer(initialState.isAuctioneer)
      setSalesHistory(initialState.wrapper.winnings)
      setOfferTimeoutSeconds(initialState.wrapper.secondsPerAuction)
      
      // Set current player from wrapper.actual
      if (initialState.wrapper.actual) {
        setCurrentPlayer(initialState.wrapper.actual.player)
        setCurrentBid(initialState.wrapper.actual.price)
        setCurrentBidder(initialState.wrapper.actual.owner)
        setCurrentSubstitute(initialState.wrapper.actual.substituted)
        setFirstOfferHappened(initialState.wrapper.actual.price > 0)
      }
      
      // Find my team
      const myTeamEntity = initialState.teams.find(t => 
        t.key?.email?.toLowerCase() === user.email.toLowerCase()
      )
      if (myTeamEntity) {
        setMyTeam(myTeamEntity)
        setMyTeamBudget(calculateBudgetRemaining(myTeamEntity))
      }
      
      // Get starting money from league settings
      if (initialState.league?.settings?.startingMoney) {
        setStartingMoney(initialState.league.settings.startingMoney)
      }
      
      // STEP 3: Setup event handlers
      auctionService.setEventHandlers({
        onPlayerShown: (actualAuctionRaw) => { /* see above */ },
        onPlayerAssigned: (dataRaw) => { /* see above */ },
        onTeamUpdated: (team) => { /* see above */ },
        onTimerUpdate: (seconds) => { /* see above */ },
        onBidPlaced: (data) => { /* existing logic */ },
        onAuctionPaused: () => { /* existing logic */ },
        onAuctionResumed: () => { /* existing logic */ },
        onAuctionClosed: () => { /* existing logic */ },
        onCurrentAuctionReset: () => { /* existing logic */ },
        onTimerReset: () => { /* existing logic */ },
        onConnected: () => setConnectionState('Connected'),
        onReconnecting: () => setConnectionState('Connecting'),
        onReconnected: () => {
          setConnectionState('Connected')
          toast.show('✅ Riconnesso', { variant: 'success' })
        },
        onDisconnected: (error) => {
          setConnectionState('Disconnected')
          if (error) {
            console.error('Disconnected:', error)
          }
        }
      })
      
      // STEP 4: Connect to SignalR
      console.log('🔌 Connecting to SignalR...')
      await auctionService.connect(auctionId, user.email)
      
      // STEP 5: Join auction room
      console.log('📨 Joining auction room...')
      const joined = await auctionService.joinAuction(auctionId)
      
      if (joined && isSubscribed) {
        console.log('✅ Successfully joined auction')
        setLoading(false)
      }
      
    } catch (error) {
      console.error('❌ Initialization failed:', error)
      if (isSubscribed) {
        setConnectionError(error instanceof Error ? error.message : 'Errore di connessione')
        setLoading(false)
      }
    }
  }
  
  initialize()
  
  return () => {
    isSubscribed = false
    auctionService.leaveAuction(auctionId)
  }
}, [auctionId, user?.email])
```

### 6. Import Updates

```typescript
// Add these imports
import { 
  ActualAuctionRaw,
  mapRawActualAuction,
  PlayerAssignedMessageRaw,
  mapRawPlayerAssignedMessage
} from 'types/auction'
```

### 7. Summary of Changes

| Old Behavior | New Behavior |
|-------------|--------------|
| SignalR `getAuctionState()` | HTTP `GET /Auction/GetAuction` |
| SignalR `getCurrentPlayer()` | Included in HTTP response (`wrapper.actual`) |
| Event `AuctionState` | No event - use HTTP initially |
| Event `CurrentPlayer` | No event - use HTTP initially |
| Event `PlayerShown(player)` | Event `PlayerShown(ActualAuction)` |
| Event `PlayerAssigned(data)` | Event `PlayerAssigned(PlayerAssignedMessage)` with team |
| No `TeamUpdated` event | NEW Event `TeamUpdated(team)` |
| No `TimerUpdate` event | NEW Event `TimerUpdate(seconds)` |
| Event `AuctionUpdated` | Removed - use specific events |
| Method `forceNextPlayer()` | Removed |

