# BugFix: Auction SignalR Connection & Event Mapping

## Problem Statement

### Issue 1: Connection Failure
```
TypeError: Cannot read properties of null (reading 'connectionId')
at AuctionService.connect
```

**Cause**: After `hubConnection.start()` failed, the code tried to access `connectionId` on a failed connection object.

### Issue 2: No Auction State Events
- Before refactoring: `AuctionState` event arrived with raw data
- After refactoring: No events received, connection established but silent

**Cause**: `user.email` was `undefined` when `useEffect` executed, so `this.userEmail` in service remained `null`, but connection proceeded anyway.

---

## Root Cause Analysis

### Timeline of Events (BEFORE Fix)

```
1. Component mounts
2. useEffect runs IMMEDIATELY
3. user from useAuth = undefined (still loading)
4. user?.email = undefined
5. if (user?.email) → SKIPPED ❌
6. setEventHandlers() → called
7. connect() → succeeds ✅
8. registerEventHandlers() → this.userEmail = null ❌
9. Backend sends AuctionState event
10. Handler tries to map with userEmail = null
11. isAuctioneer = false (always)
12. Component renders but with wrong state
```

### Key Problems

1. **Race Condition**: `useEffect` ran before `user` was loaded from `AuthProvider`
2. **Silent Failure**: No error when `userEmail` was `null`, just wrong mapping
3. **No Validation**: Service didn't validate that `userEmail` was set before connecting

---

## Solutions Implemented

### 1. Wait for User Before Connecting ✅

**File**: `screens/auction-live.tsx`

```typescript
// BEFORE ❌
useEffect(() => {
  const connectAndJoin = async () => {
    if (user?.email) {
      auctionService.setUserEmail(user.email)
    }
    // ... connect anyway
  }
}, [auctionId, user?.email])

// AFTER ✅
useEffect(() => {
  // Wait for user to be loaded
  if (!user?.email) {
    console.log('⏳ Waiting for user to be loaded...')
    return  // Don't connect yet!
  }

  const connectAndJoin = async () => {
    console.log('👤 User loaded:', user.email)
    auctionService.setUserEmail(user.email)
    // ... now connect
  }
}, [auctionId, user?.email])
```

**Impact**:
- ✅ `useEffect` runs again when `user` becomes available
- ✅ Connection only happens when `user.email` exists
- ✅ `this.userEmail` is always set before mapping

---

### 2. Robust Error Handling in Service ✅

**File**: `services/auctionService.ts`

```typescript
// BEFORE ❌
this.hubConnection = new HubConnectionBuilder()...
this.registerEventHandlers()
try {
  await this.hubConnection.start()
  console.log('...', this.hubConnection.connectionId) // ❌ NPE if failed
} catch (error) {
  throw error  // ❌ hubConnection not reset
}

// AFTER ✅
try {
  this.hubConnection = new HubConnectionBuilder()...
  this.registerEventHandlers()
  // ... lifecycle handlers
  
  await this.hubConnection.start()
  console.log('...', this.hubConnection?.connectionId) // ✅ Safe
} catch (error) {
  console.error('❌ Failed to connect:', error)
  console.error('Error details:', {
    message: error instanceof Error ? error.message : 'Unknown',
    stack: error instanceof Error ? error.stack : undefined,
    hubConnectionState: this.hubConnection?.state
  })
  this.hubConnection = null  // ✅ Reset on failure
  throw error
}
```

**Impact**:
- ✅ No null pointer exceptions
- ✅ Detailed error logging for debugging
- ✅ Clean state after connection failure

---

### 3. Validation and Logging ✅

**File**: `services/auctionService.ts`

```typescript
// setUserEmail with validation
setUserEmail(email: string | null): void {
  console.log('📧 Setting user email:', email)
  this.userEmail = email
  if (!email) {
    console.warn('⚠️ User email is null - isAuctioneer will always be false')
  }
}

// AuctionState event handler with detailed logging
this.hubConnection.on('AuctionState', (wrapperRaw: AuctionHubWrapperRaw) => {
  console.log('📦 Auction state received (raw):', wrapperRaw)
  console.log('📧 Current userEmail for mapping:', this.userEmail)
  
  try {
    const mapped = mapRawAuctionHubWrapperToAuctionState(wrapperRaw, this.userEmail)
    console.log('✅ Auction state mapped successfully:', mapped)
    
    if (this.eventHandlers.onAuctionState) {
      console.log('🔔 Calling onAuctionState handler')
      this.eventHandlers.onAuctionState(mapped)
    } else {
      console.warn('⚠️ No onAuctionState handler registered')
    }
  } catch (error) {
    console.error('❌ Error mapping auction state:', error)
  }
})
```

**Impact**:
- ✅ Clear visibility into what's happening
- ✅ Easy debugging with emoji-prefixed logs
- ✅ Catch mapping errors before they propagate

---

### 4. Better Error UI ✅

**File**: `screens/auction-live.tsx`

```tsx
{connectionError ? (
  <YStack gap="$3" alignItems="center" maxWidth={600}>
    <Card padding="$4" backgroundColor="$red2" borderColor="$red8">
      <YStack gap="$2">
        <Text fontSize="$5" fontWeight="700" color="$red11">
          Errore di Connessione
        </Text>
        <Text fontSize="$4" color="$red11">{connectionError}</Text>
        <Text fontSize="$3" marginTop="$2" color="$red10">
          Possibili cause:
        </Text>
        <Text fontSize="$3" color="$red10">• Il server non è raggiungibile</Text>
        <Text fontSize="$3" color="$red10">• Problemi di autenticazione</Text>
        <Text fontSize="$3" color="$red10">• Timeout di connessione</Text>
      </YStack>
    </Card>
    <Button onPress={() => window.location.reload()} theme="blue">
      Riprova Connessione
    </Button>
  </YStack>
) : (
  // Loading spinner
)}
```

**Impact**:
- ✅ User-friendly error messages
- ✅ Actionable suggestions
- ✅ Retry button

---

## Flow After Fix

### Successful Connection Flow ✅

```
1. Component mounts
2. useEffect runs with user = undefined
3. Early return: "⏳ Waiting for user to be loaded..."
4. AuthProvider loads user
5. useEffect re-runs with user.email = "user@example.com"
6. Console: "👤 User loaded: user@example.com"
7. Console: "📧 Setting user email: user@example.com"
8. setEventHandlers() → called
9. connect() → tries to connect
10. Console: "🔌 Connecting to SignalR hub: https://..."
11. Connection succeeds
12. Console: "✅ Connected to auction hub, connectionId: xyz"
13. joinAuction(auctionId) → succeeds
14. getAuctionState(auctionId) → invoked
15. Backend sends AuctionState event
16. Console: "📦 Auction state received (raw): {...}"
17. Console: "📧 Current userEmail for mapping: user@example.com"
18. Console: "✅ Auction state mapped successfully: {...}"
19. Console: "🔔 Calling onAuctionState handler"
20. Component: "Auction state received (mapped): {...}"
21. Component updates with correct isAuctioneer value
```

---

## Testing Checklist

### Manual Testing

- [x] User loads → connection waits → connects when ready
- [x] Connection failure → shows detailed error message
- [x] Retry button → reloads page and reconnects
- [x] Auctioneer user → sees "Controlli Banditore"
- [x] Participant user → does NOT see "Controlli Banditore"
- [x] Raw auction state → correctly mapped to clean interface
- [x] Teams and players → displayed correctly

### Edge Cases

- [x] User email is null → warning logged, no connection
- [x] Connection fails → hubConnection reset to null
- [x] Event arrives before handler registered → won't happen (handlers registered before start)
- [x] Multiple users in same auction → each sees correct isAuctioneer

### Logging Verification

```
Console output should show (in order):
1. "⏳ Waiting for user to be loaded..." (if user not ready)
2. "👤 User loaded: user@example.com"
3. "📧 Setting user email: user@example.com"
4. "🔌 Connecting to SignalR hub: ..."
5. "✅ Connected to auction hub, connectionId: ..."
6. "📨 Requesting auction state for: ..."
7. "✅ Auction state request sent"
8. "📦 Auction state received (raw): ..."
9. "📧 Current userEmail for mapping: user@example.com"
10. "✅ Auction state mapped successfully: ..."
11. "🔔 Calling onAuctionState handler"
12. "Auction state received (mapped): ..."
```

---

## Files Changed

1. ✅ `screens/auction-live.tsx`
   - Added early return if `!user?.email`
   - Improved error UI with suggestions
   - Added retry button

2. ✅ `services/auctionService.ts`
   - Wrapped connection in try-catch
   - Added optional chaining for `connectionId`
   - Reset `hubConnection` to `null` on error
   - Added detailed error logging
   - Added validation in `setUserEmail()`
   - Added try-catch in `AuctionState` handler

3. ✅ `types/auction.ts`
   - Kept clean (no excessive logging in mapper)

---

## Lessons Learned

### 1. Race Conditions with Auth
- Always check if user/auth is loaded before using it
- Use early returns in `useEffect` to wait for dependencies
- Add user to dependency array: `[auctionId, user?.email]`

### 2. Service State Management
- Always validate inputs (e.g., `userEmail` should not be null)
- Reset state to `null` on failure, not just on success
- Use optional chaining when accessing potentially null objects

### 3. Debugging Real-Time Apps
- Log at every critical step (connection, mapping, events)
- Use emoji prefixes for easy log filtering: 📦 🔔 ✅ ❌
- Include context in logs (userEmail, connectionId, etc.)

### 4. User Experience
- Show meaningful error messages, not technical jargon
- Provide actionable solutions (retry button)
- Display loading state while waiting for dependencies

---

## Future Improvements

1. **Connection Retry Logic**
   - Auto-retry with exponential backoff
   - Max retry limit before showing error

2. **State Management**
   - Consider using Zustand/Redux for auction state
   - Centralize SignalR connection management

3. **Type Safety**
   - Add runtime validation with Zod for SignalR events
   - Validate that `userEmail` is set before connecting

4. **Testing**
   - Add unit tests for mapper functions
   - Add integration tests for SignalR connection flow
   - Mock SignalR for component testing

---

## Summary

**Problem**: Connection established but events not received due to race condition with user auth loading.

**Solution**: Wait for `user.email` before connecting, add robust error handling, improve logging.

**Result**: 
- ✅ Connection only happens when user is ready
- ✅ Events received and mapped correctly
- ✅ `isAuctioneer` computed accurately
- ✅ Better error messages and debugging

**Impact**: Auction live page now works correctly with proper user context and error recovery.

