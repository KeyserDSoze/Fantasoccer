# 🪝 Fantasoccer App - React Hooks Documentation

> Comprehensive reference for all custom React hooks used in the Fantasoccer application.
> Each hook is documented with its purpose, options, return values, and usage patterns.

---

## 📖 Overview

Gli hook sono la **chiave di volta** dell'architettura frontend: ogni service ha tipicamente uno o più hook corrispondenti che ne semplificano l'utilizzo nei componenti React. Gli hook gestiscono:

- ⚡ **State management** (loading, error, data)
- 🔄 **Auto-refresh** e polling
- 🎯 **Memoization** e ottimizzazioni
- 🛠️ **Helper methods** per manipolazione dati
- 📊 **Computed properties** e filtering

---

## 🎯 Pattern Comune degli Hook

La maggior parte degli hook segue questo pattern standardizzato:

```typescript
export interface UseEntityOptions {
  refreshInterval?: number      // Auto-refresh interval (0 = disabled)
  autoRefresh?: boolean         // Enable/disable auto-refresh
  enabled?: boolean             // Enable/disable fetching
  skip?: boolean                // Skip fetching (alternative to enabled)
}

export interface UseEntityResult {
  entity: Entity | null         // Raw data
  enhancedEntity: EnhancedEntity | null  // Enhanced data with computed fields
  loading: boolean              // Initial loading state
  refreshing?: boolean          // Refresh loading state (keeps old data visible)
  error: string | null          // Error message
  lastUpdated: Date | null      // Last successful fetch timestamp
  refetch: () => Promise<void>  // Manual refetch function
  // ... entity-specific helpers
}
```

### Convenzioni di Naming

- `useEntity` - Hook base per singola entità
- `useEntityByComponents` - Versione che accetta componenti del key separati
- `useEntities` - Hook per collezioni (plurale)
- `useCurrent*` - Hook che usa il contesto utente corrente
- `useEnhanced*` - Hook che ritorna solo dati enhanced

---

## 📁 File: hooks/useAppIdentity.ts

**Purpose:** Manage user identity, state, and groups with CRUD operations.

### Hook: `useAppIdentity`

Gestisce l'identità di un singolo utente.

```typescript
function useAppIdentity(
  email: string | null,
  options?: { refreshInterval?: number }
): {
  identity: AppIdentity | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

**Use Case:** Visualizzare profilo utente, gestire permessi

---

### Hook: `useAllIdentities`

Ottiene tutte le identità utente (solo Super Admin).

```typescript
function useAllIdentities(options?: {
  refreshInterval?: number
}): {
  identities: AppIdentity[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

**Use Case:** Admin dashboard, gestione utenti

---

### Hook: `useUserState`

Gestisce lo stato dell'utente (gruppo, lega, anno selezionati).

```typescript
function useUserState(email: string | null): {
  state: AppIdentityState | null
  loading: boolean
  error: string | null
  updateGroup: (groupId: string) => Promise<void>
  updateLeague: (leagueId: string) => Promise<void>
  updateYear: (year: number) => Promise<void>
  updateWorkingEmail: (email: string) => Promise<void>
  refetch: () => Promise<void>
}
```

**Use Case:** Context switcher, user preferences

---

### Hook: `useUserGroups`

Gestisce i gruppi di un utente.

```typescript
function useUserGroups(email: string | null): {
  groups: GroupForIdentity[]
  loading: boolean
  error: string | null
  addGroup: (group: { id, name, role }) => Promise<void>
  removeGroup: (groupId: string) => Promise<void>
  updateGroup: (groupId: string, updates: { name?, role? }) => Promise<void>
  refetch: () => Promise<void>
}
```

**Use Case:** Group management UI

---

### Hook: `useAppIdentityOperations`

Operazioni CRUD su identità.

```typescript
function useAppIdentityOperations(): {
  createIdentity: (identity: AppIdentity) => Promise<void>
  updateIdentity: (email: string, identity: AppIdentity) => Promise<void>
  deleteIdentity: (email: string) => Promise<void>
}
```

**Use Case:** Admin user management

---

## 📁 File: hooks/useCalendar.ts

**Purpose:** Manage fantasy league calendars with games and rounds.

### Hook: `useCalendar`

Carica il calendario completo di una lega fantasy.

```typescript
function useCalendar(
  leagueKey: LeagueKey | null,
  options?: UseCalendarOptions
): {
  calendar: Calendar | null
  enhancedCalendar: EnhancedCalendar | null
  roundKeys: string[]
  days: CalendarDay[]
  games: CalendarGame[]
  pendingGames: CalendarGame[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getRound: (roundKey: string) => CalendarDay[]
  getDay: (dayNumber: number) => CalendarDay | null
  getGamesForTeam: (teamName: string) => CalendarGame[]
}
```

**Features:**
- ✅ Auto-refresh configurabile
- ✅ Helper per accesso a round/giorni/partite
- ✅ Filtro partite pendenti (senza risultato)
- ✅ Enhanced calendar con campi computed

**Use Case:** Calendario lega, lista partite

---

### Hook: `useCalendarDay`

Versione semplificata per un singolo giorno.

```typescript
function useCalendarDay(
  leagueKey: LeagueKey | null,
  options: { dayNumber: number, refreshInterval?, skip? }
): {
  day: CalendarDay | null
  games: CalendarGame[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

**Use Case:** Dettaglio giornata specifica

---

## 📁 File: hooks/useChance.ts

**Purpose:** Player availability and injury status from multiple sources.

### Hook: `useChancedRealPlayers`

Ottiene dati di disponibilità giocatori (infortunati, diffidati, etc.).

```typescript
function useChancedRealPlayers(options: {
  year?: number
  day: number | null
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
  filters?: ChanceSearchFilters
  search?: string
}): {
  players: ChancedRealPlayer[]
  playersByRole: ChancedRealPlayerRoleBuckets
  availablePlayers: ChancedRealPlayer[]
  injuredPlayers: ChancedRealPlayer[]
  doubtfulPlayers: ChancedRealPlayer[]
  warnedPlayers: ChancedRealPlayer[]
  disqualifiedPlayers: ChancedRealPlayer[]
  filteredPlayers: ChancedRealPlayer[]
  enhancedWrapper: EnhancedChancedRealPlayerWrapper | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  searchTerm: string
  setSearchTerm: (value: string) => void
  filters: ChanceSearchFilters
  setFilters: (filters: ChanceSearchFilters) => void
  refetch: () => Promise<void>
}
```

**Features:**
- ✅ Liste filtrate per status (infortunati, diffidati, etc.)
- ✅ Ricerca integrata con setSearchTerm
- ✅ Filtri configurabili (onlyAvailable, includeInjured, etc.)
- ✅ Raggruppamento per ruolo

**Use Case:** Lista infortunati, probabilities formazione

---

### Hook: `useCurrentYearChancedRealPlayers`

Versione semplificata che usa l'anno corrente dell'utente.

```typescript
function useCurrentYearChancedRealPlayers(
  options: Omit<UseChancedRealPlayersOptions, 'year'>
): UseChancedRealPlayersResult
```

---

## 📁 File: hooks/useFantasoccerLog.ts

**Purpose:** Platform logs for debugging and monitoring (Super Admin).

### Hook: `useAllLogs`

Ottiene tutti i log della piattaforma.

```typescript
function useAllLogs(options?: {
  refreshInterval?: number
  filterErrors?: boolean
  limit?: number
}): {
  logs: FantasoccerLog[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  statistics: LogStatistics | null
}
```

**Features:**
- ✅ Statistiche aggregate (total, errors, info, today, lastHour)
- ✅ Filtro solo errori
- ✅ Limit per performance

**Use Case:** Admin dashboard, monitoring

---

### Hook: `useLogSearch`

Ricerca nei log con debounce.

```typescript
function useLogSearch(options?: {
  debounceMs?: number
}): {
  logs: FantasoccerLog[]
  loading: boolean
  error: string | null
  search: (query: string) => void
  clearSearch: () => void
}
```

**Use Case:** Search box in log viewer

---

### Hook: `useLogStatistics`

Solo statistiche sui log.

```typescript
function useLogStatistics(): {
  statistics: LogStatistics | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useRecentLogs`

Ultimi N log.

```typescript
function useRecentLogs(count: number = 100): {
  logs: FantasoccerLog[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

## 📁 File: hooks/useGroup.ts

**Purpose:** Manage groups, leagues, baskets, and users.

### Hook: `useGroup`

Carica dati di un gruppo.

```typescript
function useGroup(
  groupId: string | null,
  options?: { refreshInterval?: number }
): {
  group: Group | null
  enhancedGroup: EnhancedGroup | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

**Use Case:** Group detail page

---

### Hook: `useUserGroups`

Ottiene tutti i gruppi dell'utente corrente.

```typescript
function useUserGroups(): {
  groups: Group[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

**Use Case:** Group selector, navigation

---

### Hook: `useGroupLeagues`

Leghe di un gruppo.

```typescript
function useGroupLeagues(
  groupId: string | null,
  options?: { year?: number, refreshInterval?: number }
): {
  leagues: League[]
  annualLeagues: AnnualLeague[]
  loading: boolean
  error: string | null
  getLeague: (leagueId: string) => League | null
  getAnnualLeague: (leagueId: string) => AnnualLeague | null
  refetch: () => Promise<void>
}
```

**Use Case:** League list, league selector

---

### Hook: `useGroupBaskets`

Basket di un gruppo.

```typescript
function useGroupBaskets(
  groupId: string | null,
  options?: { refreshInterval?: number }
): {
  baskets: Basket[]
  loading: boolean
  error: string | null
  getBasket: (basketId: string) => Basket | null
  getBasketForOwner: (owner: string, year?: number) => string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useGroupUsers`

Utenti di un gruppo.

```typescript
function useGroupUsers(
  groupId: string | null,
  options?: { activeOnly?: boolean, refreshInterval?: number }
): {
  users: UserOfAGroup[]
  activeUsers: UserOfAGroup[]
  adminUsers: UserOfAGroup[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

## 📁 File: hooks/useHallOfFame.ts

**Purpose:** Historical records, winners, and all-time statistics.

### Hook: `useHallOfFame`

Tutti i dati della Hall of Fame.

```typescript
function useHallOfFame(
  groupId: string | null,
  leagueId: string | null,
  autoRefresh?: number
): {
  hallOfFame: HallOfFame | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useAllTimeRankings`

Classifiche all-time.

```typescript
function useAllTimeRankings(
  groupId: string | null,
  leagueId: string | null,
  sortBy: 'points' | 'value' = 'points'
): {
  rankings: RankedTeam[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useWinningTeams`

Squadre vincitrici.

```typescript
function useWinningTeams(
  groupId: string | null,
  leagueId: string | null
): {
  winningTeams: WinningTeam[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useWinningPlayers`

Giocatori vincitori.

```typescript
function useWinningPlayers(
  groupId: string | null,
  leagueId: string | null
): {
  winningPlayers: WinningPlayer[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useRecords`

Record di partite e giocatori.

```typescript
function useRecords(
  groupId: string | null,
  leagueId: string | null
): {
  recordGame: RecordGame | null
  recordPlayer: RecordPlayer | null
  playerWithMostPointsInYear: PlayerWithMostPointsInYear | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useTopTeamsByPoints`

Top N squadre per punti.

```typescript
function useTopTeamsByPoints(
  groupId: string | null,
  leagueId: string | null,
  limit: number = 10
): {
  rankings: RankedTeam[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useTeamAllTimeStats`

Statistiche all-time di una squadra.

```typescript
function useTeamAllTimeStats(
  groupId: string | null,
  leagueId: string | null,
  teamOwner: string | null
): {
  teamStats: RankedTeam | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useChampionsByYear`

Vincitori per anno.

```typescript
function useChampionsByYear(
  groupId: string | null,
  leagueId: string | null
): {
  champions: Record<number, string>
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

## 📁 File: hooks/useLiveGroup.ts

**Purpose:** Live group data with calendar rounds and rankings (cached).

### Hook: `useLiveGroup`

Dati live di un gruppo (con cache).

```typescript
function useLiveGroup(
  groupId: string | null,
  options?: { refreshInterval?: number }
): {
  liveGroup: LiveGroup | null
  enhancedLiveGroup: EnhancedLiveGroup | null
  loading: boolean
  refreshing: boolean
  error: string | null
  refetch: () => Promise<void>
  getLeagueById: (leagueId: string) => LiveLeague | null
}
```

**Features:**
- ✅ Cached per performance
- ✅ Separate loading states (loading vs refreshing)
- ✅ Enhanced group con pendingGames e computed fields

**Use Case:** Dashboard principale, home page

---

### Hook: `useLiveLeague`

Singola lega live.

```typescript
function useLiveLeague(
  groupId: string | null,
  leagueId: string | null,
  options?: { refreshInterval?: number }
): {
  league: LiveLeague | null
  enhancedLeague: EnhancedLiveLeague | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  roundKeys: string[]
  pendingGames: CalendarGame[]
  latestRoundKey: string | null
}
```

---

### Hook: `useLiveRound`

Singolo round live.

```typescript
function useLiveRound(
  groupId: string | null,
  leagueId: string | null,
  roundKey: string | null,
  options?: { refreshInterval?: number }
): {
  round: LiveLeagueRound | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

## 📁 File: hooks/useMarket.ts

**Purpose:** Player trading system with approval workflow.

### Hook: `useMarkets`

Tutti i mercati di una lega.

```typescript
function useMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  options?: { refreshInterval?, enabled? }
): {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useUserMarkets`

Mercati di un utente (come buyer o seller).

```typescript
function useUserMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  userEmail: string | null,
  options?: UseMarketsOptions
): {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `usePendingMarkets`

Mercati in attesa di approvazione da un utente.

```typescript
function usePendingMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  userEmail: string | null,
  options?: UseMarketsOptions
): {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

### Hook: `useMarket`

Singolo mercato con operazioni CRUD.

```typescript
function useMarket(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  marketId: string | null,
  options?: UseMarketsOptions
): {
  market: Market | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  approve: () => Promise<void>
  deny: () => Promise<void>
  cancel: () => Promise<void>
  update: (market: Market) => Promise<void>
  delete: () => Promise<void>
}
```

**Features:**
- ✅ Operazioni integrate (approve, deny, cancel, update, delete)
- ✅ Gestione errori automatica

---

### Hook: `useActiveMarkets`

Solo mercati attivi (pending).

```typescript
function useActiveMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  options?: UseMarketsOptions
): {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}
```

---

## 📁 File: hooks/usePushNotifications.ts

**Purpose:** Push notification settings and subscription management.

### Hook: `usePushNotifications`

Gestisce impostazioni notifiche push di un utente.

```typescript
function usePushNotifications(options?: {
  email?: string | null
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
}): {
  settings: PushNotificationSettings
  summary: PushNotificationSummary
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  updateSettings: (updates: UpdatePushNotificationSettingsInput) => Promise<void>
  addDevice: (subscription: PushSubscription) => Promise<void>
  removeDevice: (endpoint: string) => Promise<void>
  disableAll: () => Promise<void>
}
```

**Features:**
- ✅ Summary con statistiche (totalEnabled, hasSubscriptions, etc.)
- ✅ Operazioni integrate (update, addDevice, removeDevice)
- ✅ disableAll helper per disabilitare tutte le notifiche

**Use Case:** Settings page, push notification management

---

### Hook: `useCurrentUserPushNotifications`

Versione semplificata per utente corrente.

```typescript
function useCurrentUserPushNotifications(
  options?: Omit<UsePushNotificationsOptions, 'email'>
): UsePushNotificationsResult
```

---

## 📁 File: hooks/useRank.ts

**Purpose:** League standings and team rankings.

### Hook: `useRank`

Classifica di una lega.

```typescript
function useRank(
  leagueKey: LeagueKey | null,
  options?: { refreshInterval?, skip? }
): {
  rank: Rank | null
  enhancedRank: EnhancedRank | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  availableRounds: string[]
  getRoundRanking: (roundId: string) => RankedTeam[]
  getEnhancedRoundRanking: (roundId: string) => EnhancedRankedTeam[]
  getTeamByOwner: (roundId: string, owner: string) => RankedTeam | null
  getTeamPosition: (roundId: string, owner: string) => number
}
```

**Features:**
- ✅ Helper per accesso a round specifici
- ✅ Enhanced teams con campi computed (goalDifference, pointsPerGame, etc.)
- ✅ Ricerca team per owner
- ✅ Calcolo posizione in classifica

---

### Hook: `useRankByComponents`

Versione con componenti del key separati.

```typescript
function useRankByComponents(
  group: string | null,
  league: string | null,
  year: number | null,
  options?: UseRankOptions
): UseRankResult
```

---

### Hook: `useDailyRank`

Classifica giornaliera.

```typescript
function useDailyRank(
  key: YearDayLeague | null,
  options?: UseRankOptions
): UseRankResult
```

---

### Hook: `useDailyRankByComponents`

Classifica giornaliera con componenti separati.

```typescript
function useDailyRankByComponents(
  group: string | null,
  league: string | null,
  year: number | null,
  day: number | null,
  options?: UseRankOptions
): UseRankResult
```

---

## 📁 File: hooks/useRealPlayers.ts

**Purpose:** Serie A players data and search.

### Hook: `useRealPlayers`

Tutti i giocatori della Serie A.

```typescript
function useRealPlayers(options?: {
  year?: number
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
}): {
  players: RealPlayer[]
  enhancedWrapper: EnhancedRealPlayersWrapper | null
  buckets: RealPlayerRoleBuckets
  activePlayers: RealPlayer[]
  inactivePlayers: RealPlayer[]
  hasVisiblePlayers: boolean
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string, options?) => Promise<RealPlayer[]>
  getPlayersByRole: (role: Role) => RealPlayer[]
}
```

**Features:**
- ✅ Liste filtrate (active/inactive)
- ✅ Ricerca integrata
- ✅ Raggruppamento per ruolo
- ✅ Enhanced wrapper con computed fields

**Use Case:** Player list, player search, team builder

---

### Hook: `useRandomizedRealPlayers`

Giocatori randomizzati per asta.

```typescript
function useRandomizedRealPlayers(
  leagueKey: LeagueKey | null,
  options?: {
    autoRefresh?: boolean
    refreshInterval?: number
    enabled?: boolean
  }
): {
  players: RealPlayer[]
  enhancedWrapper: EnhancedRealPlayersWrapper | null
  buckets: RealPlayerRoleBuckets
  hasVisiblePlayers: boolean
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string) => Promise<RealPlayer[]>
  getPlayersByRole: (role: Role) => RealPlayer[]
}
```

**Use Case:** Auction setup

---

## 📁 File: hooks/useRealVotes.ts

**Purpose:** Serie A votes (live and official).

### Hook: `useRealVotes`

Base hook per voti (live o ufficiali).

```typescript
function useRealVotes(
  type: 'live' | 'official',
  options: {
    year?: number
    day: number | null
    autoRefresh?: boolean
    refreshInterval?: number
    enabled?: boolean
  }
): {
  players: VotedRealPlayer[]
  enhancedWrapper: EnhancedVotedRealPlayerWrapper | null
  buckets: VotedRealPlayerRoleBuckets
  playersWithVote: VotedRealPlayer[]
  playersWithoutVote: VotedRealPlayer[]
  totalPlayers: number
  votedPlayersCount: number
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string) => Promise<VotedRealPlayer[]>
  getPlayersByRole: (role: Role) => VotedRealPlayer[]
  getYearDayKey: () => YearDayKey | null
}
```

**Features:**
- ✅ Supporto sia live che official
- ✅ Liste filtrate (with/without vote)
- ✅ Statistiche (totalPlayers, votedPlayersCount)
- ✅ Ricerca integrata

---

### Hook: `useLiveRealVotes`

Voti live (in tempo reale durante le partite).

```typescript
function useLiveRealVotes(
  options: UseRealVotesOptions
): UseRealVotesResult
```

---

### Hook: `useOfficialRealVotes`

Voti ufficiali (dopo le partite).

```typescript
function useOfficialRealVotes(
  options: UseRealVotesOptions
): UseRealVotesResult
```

---

## 📁 File: hooks/useSerieACalendar.ts

**Purpose:** Serie A calendar with live game detection.

### Hook: `useSerieACalendar`

Calendario Serie A con gestione partite live.

```typescript
function useSerieACalendar(options?: {
  refreshInterval?: number
  enablePolling?: boolean
  pollOnlyWhenLive?: boolean
}): {
  calendar: EnhancedRealCalendar | null
  liveDay: RealDay | null
  nextDay: RealDay | null
  liveGames: RealGame[]
  isLive: boolean
  liveSerieADay: number
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
}
```

**Features:**
- ✅ Rilevamento automatico partite live
- ✅ Polling intelligente (solo quando ci sono partite)
- ✅ liveDay e nextDay pre-calcolati
- ✅ Enhanced calendar con computed fields

**Use Case:** Dashboard Serie A, live scores

---

## 📁 File: hooks/useSerieALiveVotes.ts

**Purpose:** Live votes during Serie A matches with smart polling.

### Hook: `useSerieALiveVotes`

Voti live durante le partite con polling intelligente.

```typescript
function useSerieALiveVotes(options: {
  liveDay: RealDay | null
  isLive: boolean
  enablePolling?: boolean
}): {
  votes: EnhancedVotedRealPlayerWrapper | null
  isLoading: boolean
  error: string | null
  refetch: () => Promise<void>
  lastUpdated: Date | null
}
```

**Features:**
- ✅ Polling solo durante partite attive (entro 2h dall'inizio)
- ✅ Disattivazione automatica quando non ci sono partite
- ✅ Intervallo fisso: 10 secondi

**Use Case:** Live match tracking, real-time vote updates

---

## 📁 File: hooks/useTeam.ts

**Purpose:** Fantasy team data and player management.

### Hook: `useTeam`

Dati di una squadra fantasy.

```typescript
function useTeam(
  key: TeamKey | null,
  options?: { refreshInterval?, skip? }
): {
  team: Team | null
  enhancedTeam: EnhancedTeam | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getActivePlayers: () => Player[]
  getPlayersByRole: (role: Role) => Player[]
  getNetCost: () => number
}
```

**Features:**
- ✅ Enhanced team con computed fields
- ✅ Helper per filtro giocatori
- ✅ Calcolo netCost

---

### Hook: `useTeamByComponents`

Versione con componenti del key separati.

```typescript
function useTeamByComponents(options: {
  group: string | null
  year: number | null
  basketId: string | null
  email: string | null
  refreshInterval?: number
  skip?: boolean
}): UseTeamResult
```

---

### Hook: `useTeamDay`

Squadra per una giornata specifica.

```typescript
function useTeamDay(
  key: TeamDayKey | null,
  options?: UseTeamDayOptions
): {
  team: Team | null
  enhancedTeam: EnhancedTeam | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getActivePlayers: () => Player[]
  getPlayersByRole: (role: Role) => Player[]
  getNetCost: () => number
}
```

---

### Hook: `useTeamDayByComponents`

Versione con componenti separati.

```typescript
function useTeamDayByComponents(options: {
  group: string | null
  year: number | null
  basketId: string | null
  email: string | null
  day: number | null
  refreshInterval?: number
  skip?: boolean
}): UseTeamDayResult
```

---

## 📁 File: hooks/useAppData.ts

**Purpose:** Context-based hooks for current user's app data.

### Hook: `useCurrentGroup`

Gruppo corrente dal context.

```typescript
function useCurrentGroup(): {
  group: Group | null
  enhancedGroup: EnhancedGroup | null
  isLoaded: boolean
}
```

---

### Hook: `useAppUserGroups`

Gruppi dell'utente dal context.

```typescript
function useAppUserGroups(): {
  groups: Group[]
  loading: boolean
  error: string | null
  refresh: () => Promise<void>
}
```

---

### Hook: `useCurrentGroupLeagues`

Leghe del gruppo corrente.

```typescript
function useCurrentGroupLeagues(): League[]
```

---

### Hook: `useCurrentGroupBaskets`

Basket del gruppo corrente.

```typescript
function useCurrentGroupBaskets(): Basket[]
```

---

### Hook: `useCurrentGroupUsers`

Utenti del gruppo corrente.

```typescript
function useCurrentGroupUsers(): UserOfAGroup[]
```

---

### Hook: `useCurrentGroupAdmins`

Admin del gruppo corrente.

```typescript
function useCurrentGroupAdmins(): UserOfAGroup[]
```

---

### Hook: `useCurrentGroupLeague`

Singola lega del gruppo corrente.

```typescript
function useCurrentGroupLeague(leagueId: string): League | null
```

---

### Hook: `useCurrentGroupBasket`

Singolo basket del gruppo corrente.

```typescript
function useCurrentGroupBasket(basketId: string): Basket | null
```

---

## 📁 File: hooks/useResponsiveField.ts

**Purpose:** Responsive dimensions for soccer field visualization.

### Hook: `useResponsiveField`

Calcola dimensioni responsive per il campo da calcio.

```typescript
function useResponsiveField(): {
  width: number
  height: number
  isMobile: boolean
  isTablet: boolean
  isDesktop: boolean
  orientation: 'horizontal' | 'vertical'
  fieldWidthPercent: number
  cardScale: number
}
```

**Features:**
- ✅ Breakpoints configurati (mobile < 768px, tablet < 1024px)
- ✅ Aspect ratio ottimizzato per device
- ✅ cardScale per dimensioni card responsive
- ✅ Listener automatico su resize

**Use Case:** Football field visualization, responsive layouts

---

### Hook: `useBreakpoints`

Breakpoints semplificati.

```typescript
function useBreakpoints(): {
  isMobile: boolean
  isTablet: boolean
  isDesktop: boolean
  isSmallScreen: boolean
  isMediumScreen: boolean
  isLargeScreen: boolean
}
```

---

## 📁 File: hooks/useFormation.ts

**Purpose:** Manage optimal team formations with backup/restore capabilities.

### Hook: `useFormation`

Gestisce la formazione ottimale di un team con funzionalità di backup e ripristino.

```typescript
function useFormation(team?: LiveTeam | null): UseFormationResult
```

**Return Type:**
```typescript
interface UseFormationResult {
  applyOptimal: () => LiveTeam | null    // Calcola formazione ottimale (salva backup)
  restore: () => LiveTeam | null         // Ripristina formazione da backup
  hasBackup: boolean                     // True se c'è un backup disponibile
  clearBackup: () => void                // Pulisce il backup salvato
}
```

**Features:**
- ✅ Calcola formazione ottimale basata su algoritmo di scoring
- ✅ Salva automaticamente backup prima di modificare
- ✅ Permette ripristino della formazione precedente
- ✅ Gestione sicura dello stato con cleanup

**Algorithm:**
Il hook utilizza `formationService.applyOptimalFormation()` che valuta:
1. **Fantagazzetta** (+5 se true, -5 se false)
2. **Status** (Normal +1, Injury/Disqualified -99, Warned/Maybe -5)
3. **Trend** (0-4 dall'enum)
4. **Last Game Positiveness** (score partita precedente)
5. **Home/Away Factor**:
   - Casa vs normale: +2
   - Casa vs top (Inter/Milan/Juve/Napoli): -1
   - Trasferta vs normale: 0
   - Trasferta vs top: -2

**Use Case:** 
- Schieramento automatico formazione
- One-click optimal lineup
- Undo/Redo formazioni

**Example:**
```typescript
import { useFormation } from 'hooks/useFormation'

function FormationManager({ team }: { team: LiveTeam }) {
  const { applyOptimal, restore, hasBackup } = useFormation(team)
  
  const handleOptimize = () => {
    const optimizedTeam = applyOptimal()
    if (optimizedTeam) {
      console.log('Formazione ottimizzata!', optimizedTeam)
    }
  }
  
  const handleUndo = () => {
    if (hasBackup) {
      const previousTeam = restore()
      console.log('Formazione ripristinata!', previousTeam)
    }
  }
  
  return (
    <>
      <Button onPress={handleOptimize}>✨ Formazione Ottimale</Button>
      {hasBackup && (
        <Button onPress={handleUndo}>↩️ Ripristina</Button>
      )}
    </>
  )
}
```

**Integration with SoccerField:**
```typescript
// In SoccerField.tsx
const handleOptimalFormation = () => {
  const teamToOptimize = homeIsMyTeam ? home : away
  if (!teamToOptimize) return
  
  // Salva backup
  setFormationBackup({ home, away })
  
  // Applica formazione ottimale
  const optimizedTeam = formationService.applyOptimalFormation(teamToOptimize)
  setLocalTeams(prev => ({ ...prev, [homeIsMyTeam ? 'home' : 'away']: optimizedTeam }))
  setHasLocalChanges(true)
}
```

---

## 📁 File: hooks/useTimeAgo.ts

**Purpose:** Display elapsed time since last update.

### Hook: `useTimeAgo`

Mostra tempo trascorso da una data.

```typescript
function useTimeAgo(date: Date | null): string
```

**Output:**
- `< 60s` → "30s fa"
- `< 60m` → "15m fa"
- `>= 60m` → "14:30"
- `null` → "Mai"

**Features:**
- ✅ Aggiornamento automatico ogni secondo
- ✅ Formato italiano
- ✅ Cleanup automatico

**Use Case:** Last updated indicators, refresh timestamps

---

## 🎨 Best Practices

### 1. Scegliere l'Hook Giusto

```typescript
// ❌ NON fare: usare service direttamente nel componente
const [players, setPlayers] = useState([])
useEffect(() => {
  realPlayerService.getPlayers(year).then(setPlayers)
}, [year])

// ✅ Usa l'hook appropriato
const { players, loading, error } = useRealPlayers({ year })
```

### 2. Gestire Loading e Error States

```typescript
// ✅ Good: Gestisci tutti gli stati
const { team, loading, error, refetch } = useTeam(teamKey)

if (loading) return <LoadingSpinner />
if (error) return <ErrorMessage message={error} onRetry={refetch} />
if (!team) return <EmptyState />

return <TeamDisplay team={team} />
```

### 3. Auto-Refresh Intelligente

```typescript
// ✅ Good: Auto-refresh solo quando necessario
const { markets, loading } = useMarkets(groupId, year, leagueId, {
  refreshInterval: 30000,  // Solo su pending markets page
  enabled: isPendingMarketsPage
})

// ❌ Evita: Auto-refresh su tutto
const { data } = useData({ refreshInterval: 1000 })  // Troppo aggressivo!
```

### 4. Componenti "By Components"

```typescript
// ✅ Good: Usa variant "ByComponents" quando hai valori separati
const { rank } = useRankByComponents(groupId, leagueId, year)

// ❌ Evita: Costruire manualmente il key
const key = { groupId, leagueId, year }
const { rank } = useRank(key)  // Più verboso
```

### 5. Enhanced vs Raw Data

```typescript
// ✅ Preferisci enhanced data quando disponibile
const { enhancedTeam } = useTeam(teamKey)
const netCost = enhancedTeam?.netCost  // Campo pre-calcolato

// ❌ Evita: Ricalcolare manualmente
const { team } = useTeam(teamKey)
const netCost = team?.players.reduce((sum, p) => sum + p.cost, 0)
```

### 6. Refetch Manuale

```typescript
// ✅ Good: Refetch dopo mutazioni
const { markets, refetch } = useMarkets(groupId, year, leagueId)

const handleApprove = async (marketId: string) => {
  await marketService.approveMarket(marketId)
  await refetch()  // Ricarica dopo modifica
}
```

---

## 📊 Hook Categories Summary

| Category | Hooks | Purpose |
|----------|-------|---------|
| **Authentication** | useAppIdentity, useUserState, useUserGroups | User identity and state |
| **Groups & Leagues** | useGroup, useGroupLeagues, useGroupBaskets, useGroupUsers | Group management |
| **Live Data** | useLiveGroup, useLiveLeague, useLiveRound | Live calendar and rankings |
| **Calendar** | useCalendar, useCalendarDay | Fantasy league calendars |
| **Ranking** | useRank, useDailyRank | League standings |
| **Teams** | useTeam, useTeamDay | Fantasy team data |
| **Markets** | useMarkets, useUserMarkets, usePendingMarkets, useMarket | Player trading |
| **Serie A Data** | useRealPlayers, useRealVotes, useSerieACalendar, useSerieALiveVotes | Real Serie A data |
| **Player Info** | useChancedRealPlayers | Injuries and availability |
| **Hall of Fame** | useHallOfFame, useAllTimeRankings, useWinningTeams, etc. | Historical records |
| **Push Notifications** | usePushNotifications | Notification settings |
| **Logs** | useAllLogs, useLogSearch, useLogStatistics | Platform monitoring |
| **Context Helpers** | useCurrentGroup, useAppUserGroups, useCurrentGroupLeagues, etc. | Context-based shortcuts |
| **UI Utilities** | useResponsiveField, useBreakpoints, useTimeAgo | UI helpers |

---

## 🔗 Quick Reference

| Cosa ti serve | Hook da usare | Service sottostante |
|---------------|---------------|---------------------|
| Giocatori Serie A | `useRealPlayers` | realPlayerService |
| Voti live | `useLiveRealVotes` | realVoteService |
| Voti ufficiali | `useOfficialRealVotes` | realVoteService |
| Infortunati | `useChancedRealPlayers` | chanceService |
| Calendario Serie A | `useSerieACalendar` | serieAService |
| Calendario lega | `useCalendar` | calendarService |
| Classifica | `useRank` | rankService |
| Squadra | `useTeam` | teamService |
| Mercato | `useMarkets` | marketService |
| Gruppo | `useGroup` | groupService |
| Live group | `useLiveGroup` | liveGroupService |
| Hall of Fame | `useHallOfFame` | hallOfFameService |
| Notifiche | `usePushNotifications` | pushNotificationService |
| Log piattaforma | `useAllLogs` | fantasoccerLogService |
| Identità utente | `useAppIdentity` | appIdentityService |

---

**Last Updated:** October 6, 2025  
**Total Hooks:** 80+ hooks across 18 files  
**Code Coverage:** Complete service layer abstraction for React components

---

## 💡 Note Finali

Gli hook sono il **layer di astrazione** tra i service e i componenti React:

- ✅ **Non chiamare mai service direttamente nei componenti** - Usa sempre gli hook
- ✅ **Un hook per ogni service** - Pattern consistente
- ✅ **Enhanced data quando possibile** - Sfrutta i campi computed
- ✅ **Auto-refresh solo quando serve** - Evita polling inutili
- ✅ **Gestisci sempre loading/error** - UX migliore

Quando crei un **nuovo service**, crea sempre anche il **corrispondente hook** seguendo i pattern documentati qui! 🚀
