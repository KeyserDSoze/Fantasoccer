# 🛠️ Fantasoccer App - Utility Functions Documentation

> Comprehensive reference for all utility functions and helpers used in the Fantasoccer application.
> Each utility is documented with its purpose, exported functions, types, and usage examples.

---

## 📁 File: utils/idTokenDecoder.ts

**Purpose:** Decode and validate Microsoft OAuth ID tokens for debugging and user info extraction.

### Interfaces
- **IDTokenClaims**: Structure of decoded JWT claims (sub, aud, iss, iat, exp, tid, email, name, preferred_username, given_name, family_name, roles, groups, ver, nonce)

### Classes
- **IDTokenDecoder**: Static class for ID token operations

### Functions

#### `IDTokenDecoder.decode(idToken: string): IDTokenClaims | null`
Decodes a JWT ID token without verification (for debugging only).
- **Parameters:** JWT token string
- **Returns:** Decoded claims object or null if invalid
- **Warning:** Tokens should be verified on the backend

#### `IDTokenDecoder.getTokenInfo(idToken: string): string`
Gets human-readable token information for debugging.
- **Returns:** Formatted string with subject, email, name, expiration, tenant, etc.

#### `IDTokenDecoder.extractUserInfo(idToken: string): { id, email, name, displayName, tenantId } | null`
Extracts key user information from ID token.
- **Returns:** User object with id, email, name, displayName, tenantId

#### `IDTokenDecoder.validate(idToken: string, expectedClientId?: string): { isValid, errors, warnings }`
Validates basic ID token structure and claims.
- **Checks:** Required claims, expiration, audience match, issuer, tenant, user info
- **Returns:** Validation result with errors and warnings arrays

### Exported Aliases
- `idTokenDecoder`: Alias for IDTokenDecoder class

### Usage Example
```typescript
const claims = IDTokenDecoder.decode(token)
const userInfo = IDTokenDecoder.extractUserInfo(token)
const validation = IDTokenDecoder.validate(token, clientId)
console.log(IDTokenDecoder.getTokenInfo(token))
```

---

## 📁 File: utils/keyUtils.ts

**Purpose:** Generate deterministic player keys from names (mirrors C# KeyUtils for consistent player identification).

### Functions

#### `getPlayerKey(name?: string | null): string`
Builds a deterministic player key from a name.
- **Process:** Normalize accents → Remove diacritics → Lowercase → Strip non-ASCII letters
- **Parameters:** Player name (optional, nullable)
- **Returns:** Normalized key string (empty if no name)
- **Example:** "Cristiano Ronaldo" → "cristianoronaldo"

### Default Export
- `getPlayerKey` (default export)

### Usage Example
```typescript
import getPlayerKey from 'utils/keyUtils'
const key = getPlayerKey("Gianluca Scamacca") // "gianlucascamacca"
```

---

## 📁 File: utils/pkceHelper.ts

**Purpose:** PKCE (Proof Key for Code Exchange) helper for cross-platform Microsoft OAuth with a random verifier per login. See `docs/MCP.md` and the current `utils/pkceHelper.ts`; never reintroduce a fixed verifier.

### Classes
- **PKCEHelper**: Static class for PKCE operations

### Functions

#### `PKCEHelper.consumeTransaction(state): Promise<string>`
Returns and deletes the random verifier saved for this login only, after validating state and the ten-minute lifetime. Missing or expired transactions are rejected; there is no fallback. `buildAuthorizeUrl` generates the verifier with cryptographic randomness and hashes it with S256.

#### `PKCEHelper.buildAuthorizeUrl(config): Promise<string>`
Generates Microsoft OAuth authorize URL with PKCE.
- **Parameters:**
  - `clientId`: Application client ID
  - `authority`: Microsoft authority URL
  - `redirectUri`: OAuth redirect URI (web or mobile)
  - `scopes?`: Array of permission scopes (default: openid profile email)
  - `state?`: State parameter (auto-generated if not provided)
- **Returns:** Complete OAuth authorize URL with PKCE parameters

#### `PKCEHelper.isSupported(): boolean`
Validates that the environment supports PKCE.
- **Returns:** True if crypto APIs are available

### Exported Aliases
- `pkceHelper`: Alias for PKCEHelper class

### Usage Example
```typescript
const authUrl = await PKCEHelper.buildAuthorizeUrl({
  clientId: 'your-client-id',
  authority: 'https://login.microsoftonline.com/consumers',
  redirectUri: 'myapp://auth',
  scopes: ['openid', 'profile', 'email']
})
// On callback: consumeTransaction validates state, deletes the transaction and returns its verifier.
const verifier = await PKCEHelper.consumeTransaction(callbackState)
await authService.exchangeToken(authorizationCode, verifier)
```

---

## 📁 File: utils/playerImage.ts

**Purpose:** Generate player image URLs from Azure Blob Storage with fallback handling.

### Constants
- **PLAYER_IMAGE_BASE_URL**: `'https://fasostorage.blob.core.windows.net/images/players'`
- **DEFAULT_PLAYER_IMAGE_KEY**: `'default'`

### Functions

#### `buildPlayerImageKeyFromName(name?: string | null): string`
Builds player image key from name using keyUtils normalization.
- **Parameters:** Player name
- **Returns:** Normalized key or 'default'

#### `getPlayerImageUrlFromName(name?: string | null): { src: string, fallback: string }`
Gets player image URL with fallback.
- **Returns:** Object with `src` (player image URL) and `fallback` (default image URL)
- **URL Pattern:** `{BASE_URL}/{normalized-name}.jpg`

#### `preloadPlayerImage(name?: string | null): Promise<boolean>`
Preloads player image for better UX.
- **Returns:** Promise resolving to true if successful, false if failed
- **Uses:** React Native Image.prefetch

### Internal Functions
- `buildImageUrl(key: string): string` - Constructs full image URL from key

### Usage Example
```typescript
const { src, fallback } = getPlayerImageUrlFromName("Paulo Dybala")
// src: "https://fasostorage.../paulodybala.jpg"
// fallback: "https://fasostorage.../default.jpg"

const loaded = await preloadPlayerImage("Lautaro Martinez")
```

---

## 📁 File: utils/teamLogo.ts

**Purpose:** Generate team logo URLs from Azure Blob Storage with fallback and placeholder handling.

### Interfaces
- **TeamLogoProps**: Props for team logo component (groupId, year, email, size?, className?, alt?)

### Functions

#### `getTeamLogoUrl(groupId: string, year: number, email: string): string`
Generates the URL for a team logo.
- **URL Pattern:** `https://fasostorage.blob.core.windows.net/logos/{groupId}/{year}/{email}.png`

#### `getTeamLogoUrlWithFallback(groupId, year, email, fallbackUrl?): { src, fallback? }`
Generates team logo URL with optional custom fallback.
- **Returns:** Object with `src` and optional `fallback`

#### `getPlaceholderLogoUrl(): string`
Generates a placeholder logo as SVG data URI.
- **Returns:** Base64-encoded SVG placeholder (circle with user icon)

#### `preloadImage(src: string): Promise<boolean>`
Checks if an image URL is accessible (for preloading).
- **Returns:** Promise resolving to true/false based on load success

#### `getTeamLogoProps(props: TeamLogoProps): object`
Gets optimized logo props for img element with built-in fallback handling.
- **Returns:** Complete img element props including:
  - src, width, height, alt, className
  - style (borderRadius: 50%, objectFit: cover)
  - onError handler (auto-fallback to placeholder)

### Usage Example
```typescript
const logoUrl = getTeamLogoUrl("group123", 2024, "user@email.com")
const { src, fallback } = getTeamLogoUrlWithFallback("group123", 2024, "user@email.com")
const placeholder = getPlaceholderLogoUrl()

const imgProps = getTeamLogoProps({
  groupId: "group123",
  year: 2024,
  email: "user@email.com",
  size: 48,
  alt: "Team Logo"
})
```

---

## 📁 File: utils/teamValidation.ts

**Purpose:** Validate fantasy team formations and manage player positioning with strict rules.

### Formation Rules
- **Goalkeeper (Portiere):** Exactly 1 (never removable)
- **Defenders (Difensori):** 3-5 players
- **Midfielders (Centrocampisti):** 3-5 players
- **Forwards (Attaccanti):** 1-3 players
- **Bench:** Minimum 1 per role (goalkeeper, defender, midfielder, forward)

### Functions

#### `countPlayersInRole(players: LivePlayer[], role: Role): number`
Counts active players (starters) in a specific role.
- **Returns:** Number of players in starting lineup for given role

#### `canRemovePlayer(player: LivePlayer, allPlayers: LivePlayer[]): boolean`
Checks if a player can be removed from starting lineup.
- **Rules:**
  - Goalkeeper: Never (must have exactly 1)
  - Defender: Only if count > 3
  - Midfielder: Only if count > 3
  - Forward: Only if count > 1
- **Returns:** True if removal is allowed

#### `canAddPlayerFromTribune(player: LivePlayer, allPlayers: LivePlayer[]): boolean`
Checks if a tribune player can be added to starting lineup.
- **Rules:**
  - Goalkeeper: Only if count < 1
  - Defender: Only if count < 5
  - Midfielder: Only if count < 5
  - Forward: Only if count < 3
- **Returns:** True if addition is allowed

#### `getAvailableFieldPosition(playerRole: Role, allPlayers: LivePlayer[]): FantaSoccerRole | undefined`
Finds the first available starting position for a role.
- **Note:** All starters of same role share the same position enum value
- **Returns:** FantaSoccerRole position or undefined if full

#### `getAvailableBenchPosition(playerRole: Role, allPlayers: LivePlayer[]): FantaSoccerRole | undefined`
Finds the first available bench position for a role.
- **Bench Positions:**
  - Goalkeeper: BackupGoalKeeper
  - Defender: FirstBackupDefensor, SecondBackupDefensor
  - Midfielder: FirstBackupMidfielder, SecondBackupMidfielder
  - Forward: FirstBackupForward, SecondBackupForward
- **Returns:** FantaSoccerRole position or undefined if full

#### `canSwapPlayers(player1: LivePlayer, player2: LivePlayer, allPlayers: LivePlayer[]): boolean`
Validates if a player swap is allowed.
- **Rules:**
  - Must be same role
  - Starter ↔ Tribune: Tribune player must be addable
  - Starter ↔ Bench: Always OK (same role)
  - Starter ↔ Starter: Always OK (same role)
  - Bench ↔ Bench: Always OK (same role)
- **Returns:** True if swap is valid

#### `validateFormation(players: LivePlayer[]): { valid: boolean, errors: string[] }`
Validates complete formation against all rules.
- **Checks:**
  - Exactly 1 goalkeeper
  - 3-5 defenders
  - 3-5 midfielders
  - 1-3 forwards
  - Minimum bench coverage (1 per role)
- **Returns:** Validation result with error messages array

### Usage Example
```typescript
const formation = validateFormation(players)
if (!formation.valid) {
  console.error('Errori formazione:', formation.errors)
}

const canAdd = canAddPlayerFromTribune(tribunePlayer, allPlayers)
const canSwap = canSwapPlayers(player1, player2, allPlayers)
const position = getAvailableFieldPosition(Role.Forward, allPlayers)
```

---

## 📁 File: utils/timezoneHelpers.ts

**Purpose:** Italian timezone management - convert UTC+0 dates (containing Italian time) to proper Italian timezone representation (UTC+1 or UTC+2).

### Constants
- **ITALIAN_TIMEZONE_CONSTANTS**:
  - `WINTER_OFFSET`: 1 (UTC+1 ora solare)
  - `SUMMER_OFFSET`: 2 (UTC+2 ora legale)
  - `DST_START_MONTH`: 2 (Marzo, 0-indexed)
  - `DST_END_MONTH`: 9 (Ottobre, 0-indexed)
  - `CHANGE_HOUR`: 2 (Cambio alle 2:00 del mattino)

### Functions

#### `convertUTCToItalianTimezone(utcDateString: string | null): string | null`
Converts UTC+0 date string (already containing Italian time) to proper Italian timezone format.
- **Process:**
  1. Parse UTC+0 date
  2. Determine DST status (last Sunday March - last Sunday October)
  3. Apply correct offset (+1 winter, +2 summer)
  4. Return ISO string with Italian timezone
- **Returns:** ISO string with +01:00 or +02:00 offset

#### `convertUTCToItalianTimezoneAsNotNullableDate(utcDateString: string | null): Date`
Same as above but returns Date object with fallback to current date.
- **Returns:** Date object (never null)

#### `isItalianDST(date: Date): boolean`
Checks if a date is in Italian Daylight Saving Time period.
- **DST Period:** Last Sunday of March to last Sunday of October
- **Returns:** True if UTC+2, false if UTC+1

#### `getItalianTimezoneOffset(date: Date): number`
Gets the Italian timezone offset for a specific date.
- **Returns:** 1 (winter) or 2 (summer)

#### `formatItalianDate(date: Date | string, showEverything: boolean = false): string`
Formats a date with Italian timezone and locale.
- **Parameters:**
  - `showEverything`: If true, shows full date+time; if false, shows short format
- **Returns:** Localized Italian date string

#### `formatGameDateTime(date: Date | string | null): string`
Formats date for game display (e.g., "Dom 15 Mar, 20:30").
- **Returns:** Italian localized game date/time or error message

#### `debugTimezoneConversion(utcDateString: string): void`
Debug utility showing detailed timezone conversion info.
- **Logs:** Original UTC, converted, Italian display, DST status, offset

### Internal Functions
- `getLastSundayOfMonth(year: number, month: number): Date` - Calculates last Sunday of month for DST calculations

### Usage Example
```typescript
const italianDate = convertUTCToItalianTimezone("2024-03-15T18:00:00Z")
// Returns: "2024-03-15T18:00:00+01:00" (winter) or "2024-06-15T18:00:00+02:00" (summer)

const formatted = formatGameDateTime(new Date())
// Returns: "Dom 15 Mar, 20:30"

const isDST = isItalianDST(new Date())
const offset = getItalianTimezoneOffset(new Date())

debugTimezoneConversion("2024-03-31T01:00:00Z")
```

---

## 🎯 Utility Categories Summary

| Category | Utilities | Purpose |
|----------|-----------|---------|
| **Authentication** | idTokenDecoder, pkceHelper | OAuth token handling and PKCE flow |
| **Images & Assets** | playerImage, teamLogo | Azure Blob Storage URL generation with fallbacks |
| **Validation** | teamValidation | Fantasy formation rules and constraints |
| **Data Normalization** | keyUtils | Consistent player identification keys |
| **Localization** | timezoneHelpers | Italian timezone and date formatting |

---

## 🔑 Key Concepts

### Azure Blob Storage Integration
Three utilities interact with Azure Blob Storage:
- **playerImage**: `images/players/{key}.jpg`
- **teamLogo**: `logos/{groupId}/{year}/{email}.png`
- Both use `fasostorage.blob.core.windows.net` as base URL
- Include fallback mechanisms for missing images

### Italian Timezone Handling
The backend stores dates as UTC+0 but the time values are already in Italian time:
1. Backend: "2024-03-15T18:00:00Z" (stored as UTC but represents 18:00 Italian time)
2. Frontend: Convert to "2024-03-15T18:00:00+01:00" (proper Italian timezone)
3. Display: Format with Italian locale settings

### Fantasy Formation Rules
Strict constraints enforced by `teamValidation`:
- **11 starters**: 1 GK + 3-5 DEF + 3-5 MID + 1-3 FWD
- **7 bench players**: 1 GK + 2 DEF + 2 MID + 2 FWD minimum
- **Tribune**: Unlimited extra players not in formation
- Position enums group players by role (all defenders share FantaSoccerRole.Defensor)

### OAuth Security
PKCE implementation ensures secure OAuth flow:
- Fixed code_verifier for backend compatibility
- SHA256 code_challenge generation
- State parameter for CSRF protection
- Token validation on both client and server

---

## 📊 Function Count by File

| File | Functions | Classes | Interfaces | Constants |
|------|-----------|---------|------------|-----------|
| **idTokenDecoder.ts** | 4 static methods | IDTokenDecoder | IDTokenClaims | - |
| **keyUtils.ts** | 1 function | - | - | - |
| **pkceHelper.ts** | Transaction-scoped PKCE | PKCEHelper | - | No fixed verifier |
| **playerImage.ts** | 4 functions | - | - | 2 |
| **teamLogo.ts** | 6 functions | - | TeamLogoProps | - |
| **teamValidation.ts** | 8 functions | - | - | - |
| **timezoneHelpers.ts** | 8 functions | - | - | 1 object |
| **TOTAL** | **37 functions** | **2 classes** | **2 interfaces** | **4 constants** |

---

## 💡 Best Practices

### Image Loading
```typescript
// ✅ Good: Use URLs with fallback
const { src, fallback } = getPlayerImageUrlFromName(playerName)
<Image src={src} onError={() => setImageSrc(fallback)} />

// ✅ Good: Preload critical images
await preloadPlayerImage(starPlayerName)

// ❌ Avoid: Direct URL construction without fallback
```

### Formation Validation
```typescript
// ✅ Good: Validate before submission
const validation = validateFormation(players)
if (!validation.valid) {
  showErrors(validation.errors)
  return
}

// ✅ Good: Check constraints before UI actions
const canAddButton = canAddPlayerFromTribune(player, allPlayers)

// ❌ Avoid: Allowing invalid moves in UI
```

### Timezone Handling
```typescript
// ✅ Good: Convert backend dates immediately
const italianDate = convertUTCToItalianTimezone(apiDate)

// ✅ Good: Use formatting helpers
const display = formatGameDateTime(gameDate)

// ❌ Avoid: Using raw UTC dates in Italian UI
```

### Token Management
```typescript
// ✅ Good: Extract and validate tokens
const userInfo = IDTokenDecoder.extractUserInfo(idToken)
const validation = IDTokenDecoder.validate(idToken, clientId)

// ✅ Good: Use PKCE for OAuth
const authUrl = await PKCEHelper.buildAuthorizeUrl(config)

// ❌ Avoid: Trusting client-side token validation alone
```

---

**Last Updated:** October 6, 2025  
**Total Utilities:** 7 files  
**Total Functions:** 37  
**Code Coverage:** Authentication, Images, Validation, Localization
