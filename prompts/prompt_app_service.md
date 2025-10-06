# 🔧 Fantasoccer App - Services Documentation

> Comprehensive reference for all service classes and API integration layers used in the Fantasoccer application.
> Each service is documented with its purpose, methods, parameters, and usage patterns.

---

## 📁 File: services/appIdentityService.ts

**Purpose:** Manage user identities, authentication state, and group memberships using Repository Framework.

### Class: AppIdentityService

#### Main CRUD Operations

##### `getAllIdentities(): Promise<AppIdentity[]>`
Get all user identities (admin/super-admin only).
- **Returns:** Array of all AppIdentity objects
- **Use Case:** Admin dashboard user listing

##### `getAppIdentity(email: string): Promise<AppIdentity | null>`
Get a specific user identity by email.
- **Parameters:** User's email address (repository key)
- **Returns:** AppIdentity or null if not found

##### `updateAppIdentity(email: string, identity: AppIdentity): Promise<boolean>`
Update existing user identity.
- **Returns:** True if successful

##### `insertAppIdentity(identity: AppIdentity): Promise<boolean>`
Create new user identity.
- **Returns:** True if successful

##### `deleteAppIdentity(email: string): Promise<boolean>`
Delete user identity.
- **Returns:** True if successful

#### State Management Operations

##### `updateUserState(email: string, stateUpdates: Partial<AppIdentityState>): Promise<boolean>`
Update user's context state (group, league, year).
- **Parameters:** Partial state updates
- **Returns:** True if successful

##### `updateUserGroup(email: string, groupId: string): Promise<boolean>`
Update user's selected group.

##### `updateUserLeague(email: string, leagueId: string): Promise<boolean>`
Update user's selected league.

##### `updateUserYear(email: string, year: number): Promise<boolean>`
Update user's selected year/season.

##### `updateUserWorkingEmail(email: string, workingEmail: string): Promise<boolean>`
Update user's working email (me or leader email).

#### Group Management Operations

##### `addUserGroup(email: string, newGroup: { id, name, role }): Promise<boolean>`
Add a group to user's groups list.

##### `removeUserGroup(email: string, groupId: string): Promise<boolean>`
Remove a group from user's groups list.

### Singleton Export
```typescript
export const appIdentityService = new AppIdentityService()
```

---

## 📁 File: services/auctionService.ts

**Purpose:** Manage live player auctions with SignalR real-time connection and HTTP auction creation.

### Class: AuctionService

#### HTTP Methods

##### `createAuction(auctionType: AuctionType): Promise<string>`
Create a new auction session (HTTP call).
- **Parameters:** Auction type (Normal, RandomByLetter, RandomList)
- **Returns:** Auction ID
- **Endpoint:** POST `/api/Auction/CreateAuctionAsync`

#### SignalR Connection Management

##### `connect(): Promise<void>`
Connect to auction SignalR hub.
- **Hub URL:** `/auctionHub`
- **Registers:** All event handlers

##### `disconnect(): Promise<void>`
Disconnect from auction hub and cleanup.

##### `isConnected(): boolean`
Check if currently connected to hub.

##### `setEventHandlers(handlers: AuctionEventHandlers): void`
Set event handlers before connecting.

#### SignalR Hub Methods (Invoke)

##### `joinAuction(auctionId: string): Promise<boolean>`
Join an auction room.

##### `leaveAuction(auctionId: string): Promise<void>`
Leave auction room.

##### `getAuctionState(auctionId: string): Promise<void>`
Request current auction state (triggers `onAuctionState` event).

##### `getCurrentPlayer(auctionId: string): Promise<void>`
Get current player being auctioned (triggers `onCurrentPlayer` event).

#### Auctioneer Methods (Admin/SuperAdmin Only)

##### `showNextPlayer(auctionId: string, role: Role): Promise<void>`
Show next player for bidding.

##### `assignPlayer(auctionId: string, email: string, price: number): Promise<void>`
Assign current player to a team.

##### `pauseAuction(auctionId: string): Promise<void>`
Pause the auction.

##### `resumeAuction(auctionId: string): Promise<void>`
Resume paused auction.

##### `closeAuction(auctionId: string): Promise<void>`
End the auction session.

### Event Handlers Interface
```typescript
interface AuctionEventHandlers {
  onConnected?: (message: string) => void
  onError?: (message: string) => void
  onUserJoined?: (email: string) => void
  onUserLeft?: (email: string) => void
  onPlayerShown?: (player: StatPlayer) => void
  onPlayerAssigned?: (data: PlayerAssignedData) => void
  onAuctionUpdated?: () => void
  onAuctionPaused?: () => void
  onAuctionResumed?: () => void
  onAuctionClosed?: () => void
  onAuctionState?: (state: AuctionState) => void
  onCurrentPlayer?: (player: PlayerWithStats | null) => void
}
```

### Singleton Export
```typescript
export const auctionService = new AuctionService()
```

---

## 📁 File: services/authService.ts

**Purpose:** Handle authentication, token exchange, and user information retrieval with automatic token refresh.

### Class: AuthService

#### Token Exchange Operations

##### `exchangeToken(authCode: string, options?: PKCEOptions): Promise<TokenExchangeResponse>`
Exchange authorization code or MSAL token for backend JWT.
- **Parameters:**
  - `authCode`: OAuth authorization code or MSAL token
  - `options.isPKCE`: Use PKCE flow
  - `options.codeVerifier`: PKCE code verifier
- **Returns:** Token response with accessToken, refreshToken, expiresIn
- **Endpoint:** GET `/api/Authentication/Social/Token?code={code}&provider=2`

##### `exchangeTokenWithPKCE(authCode: string, codeVerifier: string): Promise<TokenExchangeResponse>`
Convenience method for PKCE token exchange.

##### `refreshToken(refreshToken: string): Promise<TokenExchangeResponse>`
Refresh access token using refresh token.
- **Endpoint:** GET `/api/Authentication/Social/Token?code={refreshToken}&provider=0`

#### User Information

##### `getUserInfo(accessToken: string, refreshToken?: string): Promise<AppIdentity>`
Get user identity and state.
- **Auto-Refresh:** If 401 and refreshToken provided, automatically refreshes and retries
- **Endpoint:** GET `/api/Authentication/Social/User`
- **Throws:** `ApiError('UNAUTHORIZED')` or `ApiError('REFRESH_FAILED')` to trigger re-login

#### User State Management

##### `updateUserState(updatedUser: AppIdentity): Promise<boolean>`
Update user state and groups in repository.

##### `updateUserYear(user: AppIdentity, newYear: number): Promise<boolean>`
Update user's current year.

##### `updateUserLeague(user: AppIdentity, newLeagueId: string): Promise<boolean>`
Update user's current league.

##### `updateUserGroup(user: AppIdentity, newGroupId: string): Promise<boolean>`
Update user's current group.

##### `updateUserWorkingEmail(user: AppIdentity, newWorkingEmail: string): Promise<boolean>`
Update user's working email.

##### `addUserGroup(user: AppIdentity, newGroup: { id, name, role }): Promise<boolean>`
Add a group to user's groups array.

##### `removeUserGroup(user: AppIdentity, groupIdToRemove: string): Promise<boolean>`
Remove a group from user's groups array.

##### `updateUserGroupInfo(user: AppIdentity, groupId: string, updatedGroup: { name?, role? }): Promise<boolean>`
Update specific group info in user's groups array.

### Response Types
```typescript
interface TokenExchangeResponse {
  tokenType: string      // "Bearer"
  accessToken: string    // JWT token
  expiresIn: number      // Seconds
  refreshToken: string   // Refresh token
}
```

### Singleton Export
```typescript
export const authService = new AuthService()
```

---

## 📁 File: services/calendarService.ts

**Purpose:** Manage fantasy league calendars with games, rounds, and results using Repository Framework.

### Class: CalendarService

#### Main Calendar Operations

##### `getCalendar(leagueKey: LeagueKey): Promise<Calendar | null>`
Get fantasy calendar for a league.
- **Repository:** `Calendar` with `LeagueKeyRaw`

##### `getCalendarByComponents(group: string, league: string, year: number): Promise<Calendar | null>`
Convenience method to get calendar by individual key components.

##### `getEnhancedCalendar(leagueKey: LeagueKey): Promise<EnhancedCalendar | null>`
Get enhanced calendar with computed properties (roundKeys, allDays, allGames, pendingGames).

##### `getEnhancedCalendarByComponents(group: string, league: string, year: number): Promise<EnhancedCalendar | null>`
Enhanced calendar by components.

#### Round & Day Utilities

##### `getRoundKeys(leagueKey: LeagueKey): Promise<string[]>`
Get all available round keys.

##### `getRound(leagueKey: LeagueKey, roundKey: string): Promise<CalendarDay[]>`
Get calendar days for a specific round.

##### `getDay(leagueKey: LeagueKey, dayNumber: number): Promise<CalendarDay | null>`
Get specific calendar day by number.

##### `getAllDays(leagueKey: LeagueKey): Promise<CalendarDay[]>`
Get all calendar days regardless of round structure.

#### Game Utilities

##### `getAllGames(leagueKey: LeagueKey): Promise<CalendarGame[]>`
Get all scheduled games.

##### `getGamesForTeam(leagueKey: LeagueKey, teamName: string): Promise<CalendarGame[]>`
Get all games for a specific team (home or away).

##### `getPendingGames(leagueKey: LeagueKey): Promise<CalendarGame[]>`
Get games without recorded results.

### Singleton Export
```typescript
export const calendarService = new CalendarService()
```

---

## 📁 File: services/chanceService.ts

**Purpose:** Retrieve player availability/injury status from multiple sources (Fantagazzetta, Gazzetta, Mediaset, Sky).

### Class: ChanceService

#### Main Repository Methods

##### `getWrapper(options: ChanceLookupOptions): Promise<ChancedRealPlayerWrapper | null>`
Get chance data wrapper.
- **Parameters:** `{ year, day }`
- **Repository:** `ChancedRealPlayerWrapper` with `YearDayKeyRaw`

##### `getEnhancedWrapper(options: ChanceLookupOptions): Promise<EnhancedChancedRealPlayerWrapper | null>`
Get enhanced wrapper with playersByRole and sorted lists.

##### `getPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]>`
Get all players with chance info.

##### `getBuckets(options: ChanceLookupOptions): Promise<ChancedRealPlayerRoleBuckets>`
Get players grouped by role.

#### Filtered Queries

##### `getInjuredPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]>`
Get only injured players.

##### `getDoubtfulPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]>`
Get players with doubtful status.

##### `getWarnedPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]>`
Get warned players (yellow card risk).

##### `getDisqualifiedPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]>`
Get disqualified players.

##### `getAvailablePlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]>`
Get only available players.

#### Convenience Helpers

##### `getCurrentYearWrapper(day: number): Promise<ChancedRealPlayerWrapper | null>`
Get chance data for current user's year.

##### `getCurrentYearEnhancedWrapper(day: number): Promise<EnhancedChancedRealPlayerWrapper | null>`
Enhanced wrapper for current year.

##### `getCurrentYearPlayers(day: number): Promise<ChancedRealPlayer[]>`
Players for current year.

##### `searchPlayers(term: string, options: ChanceLookupOptions, filters?: ChanceSearchFilters): Promise<ChancedRealPlayer[]>`
Search players with filters.

##### `searchCurrentYearPlayers(term: string, day: number, filters?: ChanceSearchFilters): Promise<ChancedRealPlayer[]>`
Search in current year.

### Interfaces
```typescript
interface ChanceLookupOptions {
  year: number
  day: number
}

interface ChanceSearchFilters {
  onlyAvailable?: boolean
  includeInjured?: boolean
  includeDisqualified?: boolean
  includeWarned?: boolean
  includeDoubtful?: boolean
}
```

### Singleton Export
```typescript
export const chanceService = new ChanceService()
```

---

## 📁 File: services/fantasoccerLogService.ts

**Purpose:** Retrieve and filter platform logs for debugging and monitoring (super admin only).

### Class: FantasoccerLogService

#### Main Log Operations

##### `getAllLogs(): Promise<FantasoccerLog[]>`
Get all platform logs (super admin only).
- **Repository:** `FantasoccerLog` with string key

##### `getLog(key: string): Promise<FantasoccerLog | null>`
Get specific log by key.

#### Query/Filter Operations

##### `getErrorLogs(): Promise<FantasoccerLog[]>`
Get all error logs.

##### `getInfoLogs(): Promise<FantasoccerLog[]>`
Get all info logs (without errors).

##### `searchLogs(query: string): Promise<FantasoccerLog[]>`
Search logs by query string (searches message, error, keys).

##### `getLogsByDateRange(startDate: Date, endDate: Date): Promise<FantasoccerLog[]>`
Get logs within date range.

##### `getRecentLogs(count: number = 100): Promise<FantasoccerLog[]>`
Get N most recent logs.

##### `getLogsByPrimaryKey(primaryKeyPattern: string): Promise<FantasoccerLog[]>`
Get logs matching primary key pattern.

##### `getLogsBySecondaryKey(secondaryKeyPattern: string): Promise<FantasoccerLog[]>`
Get logs matching secondary key pattern.

#### Statistics Operations

##### `getLogStatistics(): Promise<{ total, errors, info, today, lastHour }>`
Get log statistics.
- **Returns:** Object with counts for total, errors, info, today, lastHour

### Singleton Export
```typescript
export const fantasoccerLogService = new FantasoccerLogService()
```

---

## 📁 File: services/gameService.ts

**Purpose:** Retrieve live game data and save formations/teams.

### Functions

##### `gameService.getGame(params: { groupId, leagueId, year, gameId }): Promise<GameWrapper | null>`
Get live game data with teams and players.
- **Endpoint:** GET `/Game/Get`
- **Returns:** GameWrapper with teams, game, serieADay, canEdit

##### `gameService.saveFormation(payload: any): Promise<any>`
Save player formation for a game.
- **Endpoint:** POST `/Game/SaveFormation`
- **Auth Required:** Yes

##### `gameService.saveTeam(groupId: string, team: Team): Promise<{ i: boolean, m?: string }>`
Save team data.
- **Endpoint:** POST `/Game/SaveTeam`
- **Returns:** Response with `i` (success) and optional `m` (message)

### Exported Object
```typescript
export const gameService = { getGame, saveFormation, saveTeam }
```

---

## 📁 File: services/groupService.ts

**Purpose:** Manage groups, leagues, baskets, and users using Repository Framework.

### Class: GroupService

#### Main Group Operations

##### `getGroup(groupId: string): Promise<Group | null>`
Get group by ID.
- **Repository:** `Group` with string key

##### `getEnhancedGroup(groupId: string): Promise<EnhancedGroup | null>`
Get enhanced group with computed properties.

##### `updateGroup(groupId: string, group: Group): Promise<Group | null>`
Update group data.

##### `getUserGroups(): Promise<Group[]>`
Get all groups for current user.

#### League Operations

##### `getGroupLeagues(groupId: string): Promise<League[]>`
Get all leagues in a group.

##### `getLeague(groupId: string, leagueId: string): Promise<League | null>`
Get specific league.

##### `getAnnualLeague(groupId: string, leagueId: string, year: number): Promise<AnnualLeague | null>`
Get annual league for specific year.

##### `getAnnualLeagues(groupId: string, year: number): Promise<AnnualLeague[]>`
Get all annual leagues for a year.

#### Basket Operations

##### `getGroupBaskets(groupId: string): Promise<Basket[]>`
Get all baskets in a group.

##### `getBasket(groupId: string, basketId: string): Promise<Basket | null>`
Get specific basket.

##### `getBasketIdForOwner(groupId: string, owner: string, year?: number): Promise<string | null>`
Get basket ID for a specific owner.

#### User Operations

##### `getGroupUsers(groupId: string): Promise<UserOfAGroup[]>`
Get all users in a group.

##### `getActiveUsers(groupId: string): Promise<UserOfAGroup[]>`
Get active users only.

##### `getAdminUsers(groupId: string): Promise<UserOfAGroup[]>`
Get admin users only.

#### Utility Methods

##### `isTeamOwner(groupId: string, userEmail: string, year?: number): Promise<boolean>`
Check if user owns a team.

##### `getUserRole(groupId: string, userEmail: string): Promise<IdentityRole | null>`
Get user's role in group.

##### `canUserManageLeague(groupId: string, userEmail: string): Promise<boolean>`
Check if user can manage leagues.

### Singleton Export
```typescript
export const groupService = new GroupService()
```

---

## 📁 File: services/hallOfFameService.ts

**Purpose:** Retrieve historical records, winners, and all-time statistics.

### Class: HallOfFameService

#### Main Operations

##### `getHallOfFame(groupId: string, leagueId: string): Promise<HallOfFame | null>`
Get complete Hall of Fame data.
- **Repository:** `HallOfFame` with `GeneralLeagueKeyRaw`

#### Specific Data Extractors

##### `getAllTimeRankings(groupId: string, leagueId: string): Promise<RankedTeam[]>`
Get all-time rankings.

##### `getAllTimeRankingsSortedByPoints(groupId: string, leagueId: string): Promise<RankedTeam[]>`
Rankings sorted by points.

##### `getAllTimeRankingsSortedByValue(groupId: string, leagueId: string): Promise<RankedTeam[]>`
Rankings sorted by value assets.

##### `getWinningTeams(groupId: string, leagueId: string): Promise<WinningTeam[]>`
Get winning teams history.

##### `getWinningTeamsSorted(groupId: string, leagueId: string): Promise<WinningTeam[]>`
Winning teams sorted by total wins.

##### `getWinningPlayers(groupId: string, leagueId: string): Promise<WinningPlayer[]>`
Get winning players history.

##### `getWinningPlayersSorted(groupId: string, leagueId: string): Promise<WinningPlayer[]>`
Winning players sorted by total wins.

##### `getRecordGame(groupId: string, leagueId: string): Promise<RecordGame | null>`
Get highest scoring game.

##### `getRecordPlayer(groupId: string, leagueId: string): Promise<RecordPlayer | null>`
Get player with most points overall.

##### `getPlayerWithMostPointsInYear(groupId: string, leagueId: string): Promise<PlayerWithMostPointsInYear | null>`
Get player with most points in a specific year.

#### Helper Queries

##### `getTeamHistory(groupId: string, leagueId: string, teamName: string): Promise<TeamHistory>`
Get specific team's all-time stats and achievements.

### Singleton Export
```typescript
export const hallOfFameService = new HallOfFameService()
```

---

## 📁 File: services/leagueManagerService.ts

**Purpose:** Handle league administration operations (create calendar, randomize players, recalculate).

### Class: LeagueManagerService

##### `createCalendarAndRank(groupId: string, leagueId: string, year: number): Promise<boolean>`
Create calendar and rank for a league (randomized).
- **Endpoint:** GET `/LeagueManager/CreateCalendarAndRankAsync`
- **Auth Required:** Yes

##### `randomizePlayersForAuctions(groupId: string, leagueId: string, year: number): Promise<boolean>`
Randomize players for auctions.
- **Endpoint:** GET `/LeagueManager/RandomizePlayerForAuctionsAsync`
- **Auth Required:** Yes

##### `recalculate(groupId: string, leagueId: string, year: number, dayToRecalculate: number, onlyLastDay: boolean = false, preciseDay: boolean = false): Promise<boolean>`
Recalculate league rankings.
- **Endpoint:** GET `/LeagueManager/RecalculateAsync`
- **Parameters:**
  - `dayToRecalculate`: Day to recalculate from
  - `onlyLastDay`: Recalculate only last day
  - `preciseDay`: Recalculate precise day only

### Singleton Export
```typescript
export const leagueManagerService = new LeagueManagerService()
```

---

## 📁 File: services/liveGroupService.ts

**Purpose:** Retrieve live group data with calendar rounds and rankings (cached for performance).

### Class: LiveGroupService

#### Cache Management
- **Internal Cache:** Map of groupId to liveGroup and enhancedGroup
- **Force Refresh:** Available via `{ forceRefresh: true }` option

#### Main Operations

##### `getLiveGroup(groupId: string, options?: { forceRefresh?: boolean }): Promise<LiveGroup | null>`
Get live group data.
- **Repository:** `LiveGroup` with string key
- **Cached:** Yes (unless forceRefresh)

##### `getEnhancedLiveGroup(groupId: string, options?: { forceRefresh?: boolean }): Promise<EnhancedLiveGroup | null>`
Get enhanced live group with computed properties.

##### `getLiveLeagues(groupId: string, options?: { forceRefresh?: boolean }): Promise<LiveLeague[]>`
Get all live leagues in a group.

##### `getEnhancedLiveLeagues(groupId: string, options?: { forceRefresh?: boolean }): Promise<EnhancedLiveLeague[]>`
Get enhanced live leagues with rounds and pending games.

##### `getLiveLeague(groupId: string, leagueId: string, options?: { forceRefresh?: boolean }): Promise<LiveLeague | null>`
Get specific live league.

##### `getEnhancedLiveLeague(groupId: string, leagueId: string, options?: { forceRefresh?: boolean }): Promise<EnhancedLiveLeague | null>`
Get enhanced live league.

##### `getLiveRound(groupId: string, leagueId: string, roundKey: string): Promise<CalendarDay | null>`
Get specific round data.

##### `getPendingGames(groupId: string): Promise<CalendarGame[]>`
Get all pending games across all leagues.

### Singleton Export
```typescript
export const liveGroupService = new LiveGroupService()
```

---

## 📁 File: services/marketService.ts

**Purpose:** Manage player trades with approval workflow using Repository Framework.

### Class: MarketService

#### Main Market Operations

##### `getMarketsByLeague(groupId: string, year: number, leagueId: string): Promise<Market[]>`
Get all markets for a league.
- **Repository:** `Market` with `MarketKeyRaw` (partial key query)

##### `getMarket(groupId: string, year: number, leagueId: string, marketId: string): Promise<Market | null>`
Get specific market by ID.

##### `updateMarket(groupId: string, year: number, leagueId: string, market: Market): Promise<boolean>`
Create or update a market.

##### `deleteMarket(groupId: string, year: number, leagueId: string, marketId: string): Promise<boolean>`
Delete a market.

#### Market Approval Operations

##### `approveMarket(groupId: string, year: number, leagueId: string, marketId: string, userEmail: string): Promise<boolean>`
Approve a market or change vote from deny to approve.

##### `denyMarket(groupId: string, year: number, leagueId: string, marketId: string, userEmail: string): Promise<boolean>`
Deny a market or change vote from approve to deny.

##### `cancelMarket(groupId: string, year: number, leagueId: string, marketId: string, userEmail: string): Promise<boolean>`
Cancel a market (only by buyer or seller).

#### Filtered Queries

##### `getMarketsForUser(groupId: string, year: number, leagueId: string, userEmail: string): Promise<Market[]>`
Get markets where user is buyer or seller.

##### `getPendingMarketsForApproval(groupId: string, year: number, leagueId: string, userEmail: string): Promise<Market[]>`
Get pending markets that need user's approval.

##### `getActiveMarkets(groupId: string, year: number, leagueId: string): Promise<Market[]>`
Get all pending markets.

##### `getCompletedMarkets(groupId: string, year: number, leagueId: string): Promise<Market[]>`
Get completed markets (approved/denied/cancelled/expired).

### Singleton Export
```typescript
export const marketService = new MarketService()
```

---

## 📁 File: services/pushNotificationService.ts

**Purpose:** Manage push notification settings and device subscriptions.

### Class: PushNotificationService

#### Settings Retrieval

##### `getSettings(email: string): Promise<PushNotificationSettings>`
Get push notification settings for a user.
- **Repository:** `PushNotificationSettings` with string key (email)

##### `getCurrentUserSettings(): Promise<PushNotificationSettings | null>`
Get settings for current authenticated user.

##### `getSettingsSummary(email: string): Promise<PushNotificationSummary>`
Get summary of notification settings.

##### `getCurrentUserSettingsSummary(): Promise<PushNotificationSummary | null>`
Get summary for current user.

#### Mutations

##### `updateSettings(email: string, updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings>`
Update notification settings.

##### `updateCurrentUserSettings(updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings | null>`
Update current user's settings.

##### `addDevice(email: string, subscription: PushSubscription): Promise<PushNotificationSettings>`
Add a device subscription.

##### `addDeviceForCurrentUser(subscription: PushSubscription): Promise<PushNotificationSettings | null>`
Add device for current user.

##### `removeDevice(email: string, endpoint: string): Promise<PushNotificationSettings>`
Remove a device subscription by endpoint.

##### `removeDeviceForCurrentUser(endpoint: string): Promise<PushNotificationSettings | null>`
Remove device for current user.

##### `disableAll(email: string): Promise<PushNotificationSettings>`
Disable all notification types.

##### `disableAllForCurrentUser(): Promise<PushNotificationSettings | null>`
Disable all for current user.

### Interfaces
```typescript
interface UpdatePushNotificationSettingsInput {
  matchEvents?: boolean
  opponentMatchEvents?: boolean
  deploymentReminder?: boolean
  endDay?: boolean
  marketEvents?: boolean
  subscriptions?: PushSubscription[]
}
```

### Singleton Export
```typescript
export const pushNotificationService = new PushNotificationService()
```

---

## 📁 File: services/rankService.ts

**Purpose:** Manage league standings and team rankings with season and daily views.

### Class: RankService

#### Main Rank Operations (Season)

##### `getRank(leagueKey: LeagueKey): Promise<Rank | null>`
Get rank data for a league.
- **Repository:** `Rank` with `LeagueKeyRaw`

##### `getRankByComponents(group: string, league: string, year: number): Promise<Rank | null>`
Get rank by individual components.

##### `getEnhancedRank(leagueKey: LeagueKey): Promise<EnhancedRank | null>`
Get enhanced rank with computed properties.

##### `getEnhancedRankByComponents(group: string, league: string, year: number): Promise<EnhancedRank | null>`
Enhanced rank by components.

##### `saveRank(leagueKey: LeagueKey, rank: Rank): Promise<boolean>`
Save/create rank data.

##### `updateRank(leagueKey: LeagueKey, rank: Rank): Promise<boolean>`
Update existing rank data.

##### `deleteRank(leagueKey: LeagueKey): Promise<boolean>`
Delete rank data.

#### Daily Rank Operations

##### `getDailyRank(key: YearDayLeague): Promise<Rank | null>`
Get daily rank data.
- **Repository:** `DailyRank` with `YearDayLeagueRaw`

##### `getDailyRankByComponents(group: string, league: string, year: number, day: number): Promise<Rank | null>`
Get daily rank by components.

##### `getEnhancedDailyRank(key: YearDayLeague): Promise<EnhancedRank | null>`
Enhanced daily rank.

##### `getEnhancedDailyRankByComponents(group: string, league: string, year: number, day: number): Promise<EnhancedRank | null>`
Enhanced daily rank by components.

#### Team-Specific Operations

##### `getTeamRank(leagueKey: LeagueKey, teamName: string): Promise<RankedTeam | null>`
Get specific team's ranking.

##### `getEnhancedTeamRank(leagueKey: LeagueKey, teamName: string): Promise<EnhancedRankedTeam | null>`
Enhanced team rank with computed fields.

##### `getTeamPosition(leagueKey: LeagueKey, teamName: string, roundKey?: string): Promise<number | null>`
Get team's position in standings.

##### `getTopTeams(leagueKey: LeagueKey, count: number, roundKey?: string): Promise<RankedTeam[]>`
Get top N teams.

##### `compareTeams(leagueKey: LeagueKey, team1: string, team2: string): Promise<ComparisonResult>`
Compare two teams' statistics.

### Singleton Export
```typescript
export const rankService = new RankService()
```

---

## 📁 File: services/realPlayerService.ts

**Purpose:** Retrieve Serie A players with year-based and randomized (league-scoped) repositories.

### Class: RealPlayerService

#### Year-Based Repository Methods

##### `getPlayersWrapperByYear(year: number): Promise<RealPlayersWrapper | null>`
Get players wrapper for a year.
- **Repository:** `RealPlayersWrapper` with year key

##### `getEnhancedPlayersWrapperByYear(year: number): Promise<EnhancedRealPlayersWrapper | null>`
Enhanced wrapper with playersByRole.

##### `getPlayersByYear(year: number): Promise<RealPlayer[]>`
Get all players for a year.

##### `getCurrentUserPlayersWrapper(): Promise<RealPlayersWrapper | null>`
Get wrapper for current user's year.

##### `getCurrentUserPlayers(): Promise<RealPlayer[]>`
Get players for current user's year.

##### `getCurrentUserEnhancedWrapper(): Promise<EnhancedRealPlayersWrapper | null>`
Enhanced wrapper for current user.

##### `getPlayersBucketsByYear(year: number): Promise<RealPlayerRoleBuckets>`
Get players grouped by role.

#### Randomized (League Scoped) Methods

##### `getRandomizedPlayersWrapper(leagueKey: LeagueKey): Promise<RealPlayersWrapper | null>`
Get randomized players for a league.
- **Repository:** `RandomizedRealPlayers` with `LeagueKeyRaw`

##### `getRandomizedPlayers(leagueKey: LeagueKey): Promise<RealPlayer[]>`
Get randomized player list.

##### `getRandomizedEnhancedWrapper(leagueKey: LeagueKey): Promise<EnhancedRealPlayersWrapper | null>`
Enhanced randomized wrapper.

##### `getRandomizedPlayersBuckets(leagueKey: LeagueKey): Promise<RealPlayerRoleBuckets>`
Randomized players grouped by role.

#### Search & Filter Utilities

##### `searchPlayers(term: string, options?: SearchPlayersOptions): Promise<RealPlayer[]>`
Search players by name.
- **Options:** `{ year?, onlyActive?, onlyVisible? }`

##### `getPlayersByRole(role: Role, year?: number): Promise<RealPlayer[]>`
Get players filtered by role.

##### `getActivePlayers(year?: number): Promise<RealPlayer[]>`
Get only active players.

##### `getVisiblePlayers(year?: number): Promise<RealPlayer[]>`
Get only visible players.

### Interfaces
```typescript
interface SearchPlayersOptions {
  year?: number
  onlyActive?: boolean
  onlyVisible?: boolean
}
```

### Singleton Export
```typescript
export const realPlayerService = new RealPlayerService()
```

---

## 📁 File: services/realVoteService.ts

**Purpose:** Retrieve Serie A player votes (live and official) for specific days.

### Class: RealVoteService

#### Live Votes

##### `getLiveWrapper(options: VoteLookupOptions): Promise<VotedRealPlayerWrapper | null>`
Get live votes wrapper.
- **Repository:** `LiveVotedRealPlayers` with `YearDayKeyRaw`
- **Parameters:** `{ year, day }`

##### `getEnhancedLiveWrapper(options: VoteLookupOptions): Promise<EnhancedVotedRealPlayerWrapper | null>`
Enhanced live votes with playersByRole.

##### `getLivePlayers(options: VoteLookupOptions): Promise<VotedRealPlayer[]>`
Get live voted players list.

##### `getLiveBuckets(options: VoteLookupOptions): Promise<VotedRealPlayerRoleBuckets>`
Live players grouped by role.

#### Official Votes

##### `getOfficialWrapper(options: VoteLookupOptions): Promise<VotedRealPlayerWrapper | null>`
Get official votes wrapper.
- **Repository:** `OfficialVotedRealPlayers` with `YearDayKeyRaw`

##### `getEnhancedOfficialWrapper(options: VoteLookupOptions): Promise<EnhancedVotedRealPlayerWrapper | null>`
Enhanced official votes.

##### `getOfficialPlayers(options: VoteLookupOptions): Promise<VotedRealPlayer[]>`
Get official voted players list.

##### `getOfficialBuckets(options: VoteLookupOptions): Promise<VotedRealPlayerRoleBuckets>`
Official players grouped by role.

#### Convenience Helpers

##### `getCurrentYearLiveWrapper(day: number): Promise<VotedRealPlayerWrapper | null>`
Live votes for current user's year.

##### `getCurrentYearEnhancedLiveWrapper(day: number): Promise<EnhancedVotedRealPlayerWrapper | null>`
Enhanced live for current year.

##### `getCurrentYearOfficialWrapper(day: number): Promise<VotedRealPlayerWrapper | null>`
Official votes for current year.

##### `getCurrentYearEnhancedOfficialWrapper(day: number): Promise<EnhancedVotedRealPlayerWrapper | null>`
Enhanced official for current year.

##### `searchLivePlayers(term: string, options: VoteLookupOptions): Promise<VotedRealPlayer[]>`
Search live voted players.

##### `searchOfficialPlayers(term: string, options: VoteLookupOptions): Promise<VotedRealPlayer[]>`
Search official voted players.

### Interfaces
```typescript
interface VoteLookupOptions {
  year: number
  day: number
}
```

### Singleton Export
```typescript
export const realVoteService = new RealVoteService()
```

---

## 📁 File: services/recalculationService.ts

**Purpose:** Recalculate Serie A day votes and statistics (admin operations).

### Functions

##### `recalculationService.recalculateDay(year: number, serieADay: number): Promise<boolean>`
Recalculate all votes for a Serie A matchday.
- **Endpoint:** GET `/Recalculation/RecalculateDayAsync`
- **Auth Required:** Yes
- **Returns:** True if successful

##### `recalculationService.recalculateStats(year: number): Promise<boolean>`
Recalculate all statistics for a year.
- **Endpoint:** GET `/Recalculation/RecalculateStatsAsync`
- **Auth Required:** Yes
- **Returns:** True if successful

### Exported Object
```typescript
export const recalculationService = { recalculateDay, recalculateStats }
```

---

## 📁 File: services/serieAService.ts

**Purpose:** Retrieve Serie A calendar and rankings.

### Class: SerieAService

#### Calendar Operations

##### `getCalendarByYear(year: number): Promise<RealCalendar | null>`
Get Serie A calendar for a year.
- **Repository:** `RealCalendar` with year key

##### `getCurrentUserCalendar(): Promise<RealCalendar | null>`
Get calendar for current user's year.

##### `getEnhancedCurrentUserCalendar(): Promise<EnhancedRealCalendar | null>`
Enhanced calendar for current user.

##### `getEnhancedCalendarByYear(year: number): Promise<EnhancedRealCalendar | null>`
Enhanced calendar for specific year.

##### `isCalendarAvailable(year: number): Promise<boolean>`
Check if calendar exists for a year.

#### Rank Operations

##### `getRankByYear(year: number): Promise<RealRank | null>`
Get Serie A rank for a year.
- **Repository:** `RealRank` with year key

##### `getCurrentUserRank(): Promise<RealRank | null>`
Get rank for current user's year.

#### Utility Methods

##### `getAvailableYears(): Promise<number[]>`
Get available years (current and previous years).

##### `updateGameDelayed(year: number, serieADay: number, gameIndex: number, delayed: boolean): Promise<RealCalendar | null>`
Update a game's delayed status.
- **Updates:** Calendar in repository

### Singleton Export
```typescript
export const serieAService = new SerieAService()
```

---

## 📁 File: services/statPlayerService.ts

**Purpose:** Retrieve player statistics and performance metrics.

### Class: StatPlayerService

#### Main Operations

##### `getWrapperByYear(year: number): Promise<StatPlayersWrapper | null>`
Get statistics wrapper for a year.
- **Repository:** `StatPlayersWrapper` with year key

##### `getEnhancedWrapperByYear(year: number, topLimit?: number): Promise<EnhancedStatPlayersWrapper | null>`
Enhanced wrapper with playersByRole, topByAverage, topByFantaAverage.

##### `getPlayersByYear(year: number): Promise<StatPlayer[]>`
Get all player statistics for a year.

##### `getCurrentUserWrapper(options?: StatPlayersLookupOptions): Promise<StatPlayersWrapper | null>`
Get wrapper for current user's year.

##### `getCurrentUserEnhancedWrapper(options?: StatPlayersLookupOptions): Promise<EnhancedStatPlayersWrapper | null>`
Enhanced wrapper for current user.

##### `getCurrentUserPlayers(options?: StatPlayersLookupOptions): Promise<StatPlayer[]>`
Get players for current user.

#### Query Operations

##### `searchPlayers(term: string, options?: StatPlayersLookupOptions): Promise<StatPlayer[]>`
Search players by name.

##### `getPlayersByRole(role: Role, options?: StatPlayersLookupOptions): Promise<StatPlayer[]>`
Get players filtered by role.

##### `getTopPlayersByAverage(options?: StatPlayersLookupOptions): Promise<StatPlayer[]>`
Get top players by average vote.

##### `getTopPlayersByFantaAverage(options?: StatPlayersLookupOptions): Promise<StatPlayer[]>`
Get top players by fantasy average.

### Interfaces
```typescript
interface StatPlayersLookupOptions {
  year?: number
  topLimit?: number // defaults to 5
}
```

### Singleton Export
```typescript
export const statPlayerService = new StatPlayerService()
```

---

## 📁 File: services/teamCalculatorService.ts

**Purpose:** Calculate team points, enrich players with votes/chances, and manage league-specific calculations (minimal TypeScript implementation mirroring C# TeamCalculator).

### Class: TeamCalculatorService

#### State Methods

##### `isLive(): boolean`
Check if using live votes.

##### `isEditable(): boolean`
Check if day is editable.

##### `serieADay(): number | null`
Get current Serie A day.

##### `getLiveWrapper(): VotedRealPlayerWrapper | null`
Get live votes wrapper.

##### `getOfficialWrapper(): VotedRealPlayerWrapper | null`
Get official votes wrapper.

##### `getChanceWrapper(): ChancedRealPlayerWrapper | null`
Get chance wrapper.

##### `getRealDay(): any | null`
Get Serie A day data.

##### `getLeagueSetting(): LeagueSetting | null`
Get league settings.

##### `getLeagueType(): LeagueType | null`
Get league type.

#### Configuration Methods

##### `buildRequest(year: number, day: number, leagueType: LeagueType, leagueSettings: LeagueSetting, withRealGame: boolean, withVote: boolean, withChance: boolean): Promise<void>`
Build request and fetch required data (votes, chances, real games).

##### `setLeagueSettings(leagueType: LeagueType, leagueSettings: LeagueSetting): void`
Set league settings for calculations.

#### Calculation Methods

##### `calculatePointFromPlayers(players: Player[] | EnrichedPlayerTs[]): Point`
Calculate total points from player list.

##### `enrich(player: Player): EnrichedPlayerTs`
Enrich player with vote, chance, realGame, finalValue.

##### `forceEnrich(player: Player, vote: any | null, chance: any | null, realGame: any | null): EnrichedPlayerTs`
Force enrich with specific data.

##### `enrichMany(players: Player[]): EnrichedPlayerTs[]`
Enrich multiple players.

##### `finalValueFromPlayer(player: Player | EnrichedPlayerTs): VoteValue`
Calculate final vote value for a player.

##### `recalculateDay(serieADay: number): Promise<boolean>`
Recalculate a specific day.

### Singleton Export
```typescript
export const teamCalculatorService = new TeamCalculatorService()
```

---

## 📁 File: services/tokenStorageService.ts

**Purpose:** Manage backend JWT tokens with automatic expiration handling using Zustand store.

### Zustand Store: useTokenStore

#### State
```typescript
interface TokenStoreState {
  tokenData: StoredTokenData | null
  isLoading: boolean
  // ... methods
}
```

#### Methods

##### `saveToken(tokenData: StoredTokenData): Promise<void>`
Save token data to storage.

##### `saveTokenWithExpiration(tokenData: Omit<StoredTokenData, 'expiresAt'>, options: TokenSaveOptions): Promise<void>`
Save token with calculated expiration.
- **Options:** `{ expiresInSeconds }`

##### `loadToken(): Promise<StoredTokenData | null>`
Load token from storage (sets loading state).

##### `loadTokenSilent(): Promise<StoredTokenData | null>`
Load token without setting loading state.

##### `removeToken(): Promise<void>`
Remove token from storage.

##### `hasValidToken(): Promise<boolean>`
Check if token exists and is not expired.

##### `getTokenSilent(): Promise<StoredTokenData | null>`
Get token silently (with validation).

##### `getTokenString(): Promise<string | null>`
Get just the access token string.

##### `isTokenNearExpiry(minutesBeforeExpiry: number = 5): Promise<boolean>`
Check if token expires soon.

##### `getTokenExpirationInfo(): Promise<{ isExpired, minutesUntilExpiry, expiresAt } | null>`
Get detailed expiration info.

##### `ensureValidToken(): Promise<string | null>`
Ensure token is valid, return access token.
- **Auto-loads** if not in state
- **Throws** if expired

##### `saveUserInfo(user: AppIdentity): Promise<void>`
Save user info to storage.

##### `loadUserInfo(): Promise<AppIdentity | null>`
Load user info from storage.

### Backward-Compatible Export
```typescript
export const tokenStorageService = {
  saveToken: (t) => useTokenStore.getState().saveToken(t),
  loadToken: () => useTokenStore.getState().loadToken(),
  // ... all other methods delegated to store
}
```

### Interfaces
```typescript
interface StoredTokenData {
  tokenType: string
  accessToken: string
  refreshToken: string
  expiresAt: number // timestamp
}

interface TokenSaveOptions {
  expiresInSeconds: number
}
```

---

## 📁 File: services/webPushClient.ts

**Purpose:** Manage web push notifications with Service Worker registration and subscription (web only).

### Functions

##### `getPushSupportState(): Promise<PushSupportState>`
Check if push notifications are supported.
- **Returns:** `{ supported: true, permission }` or `{ supported: false, reason }`
- **Platform:** Web only

##### `requestNotificationPermission(): Promise<NotificationPermission>`
Request notification permission from user.
- **Returns:** 'granted', 'denied', or 'default'

##### `getExistingSubscription(): Promise<PushSubscription | null>`
Get existing push subscription.

##### `subscribeForPush(): Promise<PushSubscription | null>`
Subscribe to push notifications.
- **Creates:** Service Worker subscription with VAPID key
- **Returns:** PushSubscription object

##### `unsubscribeFromPush(): Promise<PushSubscription | null>`
Unsubscribe from push notifications.
- **Returns:** Previously active subscription

##### `resetServiceWorkerRegistration(): Promise<void>`
Unregister service worker and cleanup.

### Exported Object
```typescript
export const webPushSupport = {
  isSupported: () => boolean,
  getSupportState,
  requestPermission,
  subscribe,
  unsubscribe,
  getExistingSubscription,
  resetRegistration
}
```

### Types
```typescript
type PushSupportState =
  | { supported: true; permission: NotificationPermission }
  | { supported: false; reason: string }
```

### Constants
- **SERVICE_WORKER_PATH:** `/FCServiceWorker.js`
- **APPLICATION_SERVER_PUBLIC_KEY:** VAPID public key for push

---

## 🎯 Service Categories Summary

| Category | Services | Purpose |
|----------|----------|---------|
| **Authentication** | authService, tokenStorageService, appIdentityService | OAuth, JWT, user identity |
| **Serie A Data** | serieAService, realPlayerService, realVoteService, statPlayerService, chanceService | Real-world data |
| **Fantasy League** | groupService, calendarService, rankService, marketService, liveGroupService, hallOfFameService | League management |
| **Game Management** | gameService, teamCalculatorService | Live games and calculations |
| **Administration** | leagueManagerService, recalculationService, fantasoccerLogService | Admin operations |
| **Real-time** | auctionService, webPushClient, pushNotificationService | SignalR and push notifications |

---

## 🔑 Key Concepts

### Repository Framework Pattern
Most services use the Repository Framework client:
```typescript
RepositoryServices.Repository<RawType, KeyType>('RepositoryName')
  .get(key)
  .query()
  .insert(data)
  .update(key, data)
  .delete(key)
```

### Authentication Flow
1. **Token Exchange:** authService.exchangeToken(code)
2. **Token Storage:** tokenStorageService.saveToken(tokenData)
3. **Auto-Refresh:** tokenStorageService.ensureValidToken()
4. **User Info:** authService.getUserInfo(token)

### Service Worker & Push Notifications
1. **Register:** webPushSupport.getSupportState()
2. **Permission:** webPushSupport.requestPermission()
3. **Subscribe:** webPushSupport.subscribe()
4. **Store:** pushNotificationService.addDeviceForCurrentUser(subscription)

### SignalR Real-Time (Auction)
1. **Connect:** auctionService.connect()
2. **Set Handlers:** auctionService.setEventHandlers({ onPlayerShown: ... })
3. **Join Room:** auctionService.joinAuction(auctionId)
4. **Invoke Methods:** auctionService.showNextPlayer(auctionId, role)

### Singleton Pattern
All services export singleton instances:
```typescript
export const someService = new SomeService()
```

---

## 📊 Statistics

| Metric | Count |
|--------|-------|
| **Total Services** | 23 |
| **Repository-Based** | 15 |
| **HTTP-Only** | 4 |
| **SignalR-Enabled** | 1 |
| **Zustand Store** | 1 |
| **Total Methods** | 200+ |

---

## 💡 Usage Patterns

### Fetching Data with Current User Context
```typescript
// Services auto-resolve current user's year
const players = await statPlayerService.getCurrentUserPlayers()
const calendar = await serieAService.getCurrentUserCalendar()
const liveVotes = await realVoteService.getCurrentYearLiveWrapper(day)
```

### Enhanced vs. Normal Data
```typescript
// Normal: raw data mapped from backend
const rank = await rankService.getRank(leagueKey)

// Enhanced: includes computed properties
const enhancedRank = await rankService.getEnhancedRank(leagueKey)
// enhancedRank includes: roundKeys, allTeams, topTeams, etc.
```

### Error Handling
```typescript
try {
  const token = await authService.exchangeToken(code)
  await tokenStorageService.saveToken(token)
} catch (error) {
  if (error instanceof ApiError) {
    if (error.message === 'UNAUTHORIZED') {
      // Trigger re-login
    } else if (error.message === 'REFRESH_FAILED') {
      // Clear tokens and re-login
    }
  }
}
```

### Real-Time Updates (SignalR)
```typescript
auctionService.setEventHandlers({
  onPlayerShown: (player) => {
    console.log('New player:', player)
  },
  onPlayerAssigned: (data) => {
    console.log('Player assigned:', data)
  }
})

await auctionService.connect()
await auctionService.joinAuction(auctionId)
```

---

**Last Updated:** October 6, 2025  
**Total Services:** 23  
**Framework:** Repository Framework, SignalR, Zustand  
**Platforms:** Web, React Native (Expo)
