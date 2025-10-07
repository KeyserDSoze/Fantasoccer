# Update: Backend Teams Structure Changed from Dictionary to List<Entity>

## Change Summary

Backend changed the `Teams` property in `AuctionHubWrapper` from:
```csharp
// BEFORE
[JsonPropertyName("t")]
public Dictionary<string, Team> Teams { get; set; }
// Serialized as: { "t": { "key1": {...}, "key2": {...} } }

// AFTER
[JsonPropertyName("t")]
public List<Entity<Team, TeamKey>> Teams { get; set; } = [];
// Serialized as: { "t": [ { "k": "key1", "v": {...} }, { "k": "key2", "v": {...} } ] }
```

## C# Entity Definition

```csharp
[JsonConverter(typeof(EntityJsonConverterFactory))]
public class Entity<T, TKey> where TKey : notnull
{
    public TKey? Key { get; set; }
    public T? Value { get; set; }
    [JsonIgnore]
    public bool HasValue => Value != null;
    [JsonIgnore]
    public bool HasKey => Key != null;
}

// Custom converter serializes as:
// { "k": Key, "v": Value }
```

---

## Frontend Changes

### 1. **New Interface for Entity** ✅

**File**: `types/auction.ts`

```typescript
/**
 * Entity wrapper for Team with TeamKey
 * Matches C# Entity<Team, TeamKey> serialized as { k: TeamKey, v: Team }
 */
export interface EntityRaw<T, K> {
  k: K | null    // Key
  v: T | null    // Value
}
```

**Generic Type**: Can be reused for other `Entity<T, K>` types from backend.

---

### 2. **Updated AuctionHubWrapperRaw** ✅

**File**: `types/auction.ts`

```typescript
// BEFORE ❌
export interface AuctionHubWrapperRaw {
  // ...
  t: Record<string, AuctionTeamRaw>  // Dictionary
  // ...
}

// AFTER ✅
export interface AuctionHubWrapperRaw {
  // ...
  t: EntityRaw<AuctionTeamRaw, string>[]  // List<Entity<Team, TeamKey>>
  // ...
}
```

**Breaking Change**: 
- Was: `{ "t": { "teamKey1": {...}, "teamKey2": {...} } }`
- Now: `{ "t": [ { "k": "teamKey1", "v": {...} }, { "k": "teamKey2", "v": {...} } ] }`

---

### 3. **Updated Mapper Functions** ✅

**File**: `types/auction.ts`

#### Main Mapper

```typescript
// BEFORE ❌
export function mapRawAuctionHubWrapperToAuctionState(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null
): AuctionState {
  const teams: AuctionTeam[] = Object.values(raw.t || {}).map(mapRawTeam)
  // ...
}

// AFTER ✅
export function mapRawAuctionHubWrapperToAuctionState(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null
): AuctionState {
  // Map teams from List<Entity<Team, TeamKey>> to AuctionTeam[]
  // Filter out entities without value (HasValue = false)
  const teams: AuctionTeam[] = (raw.t || [])
    .filter(entity => entity.v !== null && entity.v !== undefined)
    .map(entity => mapRawTeam(entity.v!))
  // ...
}
```

**Key Changes**:
1. ✅ Changed from `Object.values(raw.t || {})` to `(raw.t || [])`
2. ✅ Added `.filter()` to exclude entities where `v` is `null` (respects `HasValue`)
3. ✅ Map `entity.v!` (the Team value) instead of the whole entity

#### Deprecated Mapper

```typescript
// Also updated for backward compatibility
export function mapRawAuctionHubWrapper(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null,
  isAuctioneer: boolean
): AuctionState {
  const teams: AuctionTeam[] = (raw.t || [])
    .filter(entity => entity.v !== null && entity.v !== undefined)
    .map(entity => mapRawTeam(entity.v!))
  // ...
}
```

---

## JSON Structure Comparison

### Before (Dictionary)

```json
{
  "w": { /* AuctionWrapper */ },
  "u": "2025-10-07T12:00:00Z",
  "ln": "Serie A League",
  "t": {
    "team-key-1": {
      "n": "Team Alpha",
      "o": "owner@example.com",
      "c": 500,
      "ap": [ /* players */ ]
    },
    "team-key-2": {
      "n": "Team Beta",
      "o": "owner2@example.com",
      "c": 450,
      "ap": [ /* players */ ]
    }
  },
  "r": [ /* real players */ ]
}
```

### After (List<Entity>)

```json
{
  "w": { /* AuctionWrapper */ },
  "u": "2025-10-07T12:00:00Z",
  "ln": "Serie A League",
  "t": [
    {
      "k": "team-key-1",
      "v": {
        "n": "Team Alpha",
        "o": "owner@example.com",
        "c": 500,
        "ap": [ /* players */ ]
      }
    },
    {
      "k": "team-key-2",
      "v": {
        "n": "Team Beta",
        "o": "owner2@example.com",
        "c": 450,
        "ap": [ /* players */ ]
      }
    }
  ],
  "r": [ /* real players */ ]
}
```

---

## Why This Change?

### Backend Perspective

1. **Type Safety**: `Entity<T, TKey>` is more type-safe than `Dictionary<string, T>`
2. **Null Safety**: `HasValue` and `HasKey` properties make null checks explicit
3. **Consistency**: Same pattern used across the codebase
4. **Serialization Control**: Custom `JsonConverter` ensures consistent format

### Potential Benefits

- **Order Preservation**: List maintains insertion order (Dictionary order is undefined)
- **Nullable Values**: Can have entities with null values (filtered out in frontend)
- **Key Access**: TeamKey is explicitly available in `k` property
- **Extensibility**: Can add more properties to `Entity` without breaking contract

---

## Migration Impact

### What Changed
- ✅ **TypeScript interfaces**: `AuctionHubWrapperRaw.t` type updated
- ✅ **Mapper functions**: Updated to handle array instead of dictionary
- ✅ **Filtering**: Added null safety check for entity values

### What Stayed the Same
- ✅ **Clean interfaces**: `AuctionState` and `AuctionTeam` unchanged
- ✅ **Component code**: No changes needed in React components
- ✅ **Service layer**: SignalR handler works transparently
- ✅ **User experience**: No visible changes

### Backward Compatibility
- ❌ **Breaking**: Old data format (dictionary) won't parse correctly
- ✅ **Solution**: Backend and frontend must be updated together
- ✅ **Safe**: TypeScript compiler catches any mismatches

---

## Testing Checklist

### Data Structure
- [x] Verify teams array arrives correctly from backend
- [x] Check that `entity.k` contains TeamKey
- [x] Check that `entity.v` contains Team object
- [x] Test with empty teams array (`t: []`)
- [x] Test with entities where `v` is null (should be filtered)

### Mapping
- [x] All teams displayed correctly in UI
- [x] Team count is accurate
- [x] No duplicate teams
- [x] Team order preserved (if backend sends in specific order)

### Edge Cases
- [x] Empty auction (no teams)
- [x] Single team
- [x] Many teams (10+)
- [x] Entities with null values filtered out
- [x] Malformed data (missing `k` or `v`)

---

## Example Data Flow

### 1. Backend Sends Event

```csharp
var teams = new List<Entity<Team, TeamKey>>
{
    new Entity<Team, TeamKey>(new Team { Name = "Alpha", Owner = "user1@example.com" }, new TeamKey("key1")),
    new Entity<Team, TeamKey>(new Team { Name = "Beta", Owner = "user2@example.com" }, new TeamKey("key2"))
};

var wrapper = new AuctionHubWrapper
{
    Teams = teams,
    // ...
};

// Sends via SignalR
await Clients.Group(auctionId).SendAsync("AuctionState", wrapper);
```

### 2. SignalR Serializes

```json
{
  "t": [
    { "k": "key1", "v": { "n": "Alpha", "o": "user1@example.com", "c": 0, "ap": [] } },
    { "k": "key2", "v": { "n": "Beta", "o": "user2@example.com", "c": 0, "ap": [] } }
  ]
}
```

### 3. Frontend Receives

```typescript
// services/auctionService.ts
this.hubConnection.on('AuctionState', (wrapperRaw: AuctionHubWrapperRaw) => {
  // wrapperRaw.t is EntityRaw<AuctionTeamRaw, string>[]
  const mapped = mapRawAuctionHubWrapperToAuctionState(wrapperRaw, this.userEmail)
  // ...
})
```

### 4. Mapper Transforms

```typescript
// types/auction.ts
const teams: AuctionTeam[] = (raw.t || [])
  .filter(entity => entity.v !== null)  // Only teams with values
  .map(entity => mapRawTeam(entity.v!)) // Extract team from entity
```

### 5. Component Renders

```typescript
// screens/auction-live.tsx
{teams.map((team) => (
  <Card key={team.owner}>
    <Text>{team.name}</Text>
    <Text>{team.owner}</Text>
  </Card>
))}
```

---

## Future Considerations

### Potential Enhancements

1. **Use TeamKey in UI**
   - Currently we only use `entity.v` (Team)
   - Could use `entity.k` (TeamKey) for more robust identification
   - Example: `key={entity.k}` instead of `key={team.owner}`

2. **Generic Entity Mapper**
   ```typescript
   function mapEntityArray<TRaw, TClean, K>(
     entities: EntityRaw<TRaw, K>[],
     mapper: (raw: TRaw) => TClean
   ): TClean[] {
     return entities
       .filter(e => e.v !== null)
       .map(e => mapper(e.v!))
   }
   
   // Usage
   const teams = mapEntityArray(raw.t, mapRawTeam)
   ```

3. **Entity Type Guards**
   ```typescript
   function hasValue<T, K>(entity: EntityRaw<T, K>): entity is EntityRaw<NonNullable<T>, K> {
     return entity.v !== null && entity.v !== undefined
   }
   
   // Usage
   const teams = raw.t.filter(hasValue).map(e => mapRawTeam(e.v))
   ```

### Other Backend Entities

If backend uses `Entity<T, K>` elsewhere, we can reuse `EntityRaw<T, K>`:

```typescript
// Example: Players as entities
interface PlayerEntityRaw extends EntityRaw<PlayerRaw, number> {}

// Example: Generic auction items
interface AuctionItemEntityRaw extends EntityRaw<AuctionItemRaw, string> {}
```

---

## Summary

**Change**: Backend switched from `Dictionary<string, Team>` to `List<Entity<Team, TeamKey>>`

**Frontend Impact**: 
- ✅ Updated `AuctionHubWrapperRaw.t` type
- ✅ Updated mapper to handle array of entities
- ✅ Added null filtering for entity values
- ✅ No component changes needed

**Status**: ✅ Complete, tested, ready for production

**Compatibility**: ⚠️ Breaking change - backend and frontend must be deployed together

