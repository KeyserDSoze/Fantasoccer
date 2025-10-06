# 📋 Fantasoccer App - Domain Types Documentation

> Comprehensive reference for all TypeScript types used in the Fantasoccer application.
> Each type includes its purpose, category (enum/interface/type), and whether it has a Raw counterpart for API mapping.

---

## 📁 File: types/auction.ts

**Purpose:** Real-time auction management with player bidding and team composition.

### Enums
- **AuctionType**: Defines how players are ordered during auction (Normal, RandomByLetter, RandomList)
- **AuctionStatus**: Current auction state (Stopped, Started, Paused, Ended)

### Interfaces
- **AuctionWin** (AuctionWinRaw): Represents a successful bid with winner email and amount
- **ActualAuction** (ActualAuctionRaw): Current player being auctioned with bid information
- **AuctionWrapper** (AuctionWrapperRaw): Complete auction state including teams, players, wins, and current auction
- **AuctionTeam** (AuctionTeamRaw): Team participating in auction with email, name, money, and players
- **AuctionPlayer** (PlayerRaw): Basic player info (email, name, role, price)
- **AuctionState**: Client-side auction state with user context and auctioneer status
- **PlayerWithStats**: Combines StatPlayer with additional auction-specific data
- **PlayerAssignedData**: Information about a player assignment (player, team, price, auctioneer)

### Constants
- **AuctionTypeLabels**: Human-readable labels for each AuctionType
- **AuctionTypeDescriptions**: Detailed descriptions for each AuctionType

### Mappers
- `mapRawAuctionWin()`: AuctionWinRaw → AuctionWin
- `mapRawActualAuction()`: ActualAuctionRaw → ActualAuction
- `mapRawAuctionWrapper()`: AuctionWrapperRaw → AuctionWrapper
- `mapRawPlayer()`: PlayerRaw → AuctionPlayer
- `mapRawTeam()`: AuctionTeamRaw → AuctionTeam
- `mapRawAuctionHubWrapper()`: AuctionHubWrapperRaw → AuctionState
- `statPlayerToPlayerWithStats()`: StatPlayer → PlayerWithStats

---

## 📁 File: types/calendar.ts

**Purpose:** Fantasy league calendar with games, results, and match scheduling.

### Enums
- **GameResultType**: Game outcome (HomeWon, AwayWon, Tie)

### Interfaces
- **Point** (PointRaw): Points scored in a game with bonuses (value, defensiveBonus, goodPeople, ownGoal)
- **GameResult** (GameResultRaw): Complete game result with home/away points, goals, and cancellation status
- **EnhancedGameResult**: GameResult + computed fields (hasValue, resultType)
- **CalendarGame** (CalendarGameRaw): Single fantasy league match with teams, owners, and result
- **CalendarDay** (DayRaw): Collection of games for a specific day (serieADay, number, games)
- **Calendar** (CalendarRaw): Full season calendar with year and rounds
- **EnhancedCalendar**: Calendar + computed fields (roundKeys, allDays, allGames, pendingGames)

### Types
- **CalendarRounds** (CalendarRoundsRaw): Dictionary mapping round keys to CalendarDay arrays

### Helpers
- **GameResultHelper**: Calculate goals, determine result type, check for valid results
- **CalendarHelper**: Extract rounds, days, games, find pending games, enhance calendar

### Mappers
- `mapRawPointToPoint()`: PointRaw → Point
- `mapRawGameResultToGameResult()`: GameResultRaw → GameResult
- `mapRawCalendarGameToCalendarGame()`: CalendarGameRaw → CalendarGame
- `mapRawDayToDay()`: DayRaw → CalendarDay
- `mapRawCalendarToCalendar()`: CalendarRaw → Calendar
- `enhanceCalendar()`: Calendar → EnhancedCalendar

---

## 📁 File: types/chance.ts

**Purpose:** Player availability and injury status from multiple sources.

### Enums
- **ChanceType**: Player status (Normal, Injury, Warned, Disqualified, Maybe)
- **TrendType**: Performance trend (Bad, NotBad, Normal, VeryGood, Excellent)

### Types
- **ChanceSource**: Source of availability info ('fantagazzetta' | 'gazzetta' | 'mediaset' | 'sky')

### Interfaces
- **Chance** (ChanceRaw): Availability status from multiple sources with description and trend
- **EnhancedChance**: Chance + computed flags (sources[], isInjured, isWarned, isDisqualified, isDoubtful, isAvailable, availabilityLabel)
- **ChancedRealPlayer** (ChancedRealPlayerRaw): Real player with chance information
- **ChancedRealPlayerWrapper** (ChancedRealPlayerWrapperRaw): Collection of ChancedRealPlayer
- **EnhancedChancedRealPlayerWrapper**: Wrapper + playersByRole and sorted lists

### Types
- **ChancedRealPlayerRoleBuckets**: Partial record mapping Role to ChancedRealPlayer arrays

### Helpers
- **ChanceHelper**: Get availability label, check status flags, extract sources
- **ChancedRealPlayerHelper**: Sort by role, trend, availability; group by role

### Mappers
- `mapRawChanceToChance()`: ChanceRaw → Chance
- `enhanceChance()`: Chance → EnhancedChance
- `mapRawChancedRealPlayerToPlayer()`: ChancedRealPlayerRaw → ChancedRealPlayer
- `mapRawChancedWrapperToWrapper()`: ChancedRealPlayerWrapperRaw → ChancedRealPlayerWrapper
- `enhanceChancedRealPlayersWrapper()`: ChancedRealPlayerWrapper → EnhancedChancedRealPlayerWrapper

---

## 📁 File: types/game.ts

**Purpose:** Live game state with deployed teams, players, votes, and real-time updates.

### Interfaces
- **VoteValue** (VoteValueRaw): Final vote value with special bonus flag (value, special)
- **LivePlayer** (LivePlayerRaw): Player in live game with current state, vote, chance, realGame, finalValue, currentPosition
- **LiveTeam** (LiveTeamRaw): Team in live game with name, owner, additionalOwners, players, lastUpdate
- **GameWrapper** (GameWrapperRaw): Complete live game state with teams, game, serieADay, canEdit

### Mappers
- `mapRawVoteValueToVoteValue()`: VoteValueRaw → VoteValue
- `mapRawLivePlayerToLivePlayer()`: LivePlayerRaw → LivePlayer
- `mapRawLiveTeamToLiveTeam()`: LiveTeamRaw → LiveTeam
- `mapRawGameWrapperToGameWrapper()`: GameWrapperRaw → GameWrapper

---

## 📁 File: types/group.ts

**Purpose:** Group, league, basket, and user management with league settings.

### Enums
- **Role**: Player positions (Goalkeeper, Defender, Midfielder, Striker, Unknown)
- **LeagueType**: Tournament format (Normal, Cup, Championship, ChampionsCup, UefaCup)
- **MarketType**: Trade system type (All, OnlyBetweenMembers)
- **IdentityRole**: User roles (re-exported from identity.ts)

### Interfaces
- **VoteLeagueSetting** (VoteLeagueSettingRaw): Vote calculation settings (modifier, defenseBonus, goalBonus, assistBonus, etc.)
- **LeagueSetting** (LeagueSettingRaw): Complete league configuration (voteSettings, startingMoney, playersCount, benchCount, goalValue, etc.)
- **AnnualLeague** (AnnualLeagueRaw): League configuration for a specific year
- **League** (LeagueRaw): League with ID, name, type, basket ID, and annual settings
- **AnnualTeam** (AnnualTeamRaw): Team configuration for a specific year
- **YearlyBasket** (YearlyBasketRaw): Player basket for a specific year
- **Basket** (BasketRaw): Collection of yearly baskets
- **UserOfAGroup** (UserOfAGroupRaw): User membership in a group with role
- **Group** (GroupRaw): Complete group with ID, name, users, baskets, leagues

### Helpers
- **RoleExtensions**: Convert role to string
- **LeagueTypeExtensions**: Get all league types
- **GroupHelper**: Find leagues, baskets, users, check permissions

### Mappers
- `mapRawVoteLeagueSettingToVoteLeagueSetting()`: VoteLeagueSettingRaw → VoteLeagueSetting
- `mapRawLeagueSettingToLeagueSetting()`: LeagueSettingRaw → LeagueSetting
- `mapRawAnnualLeagueToAnnualLeague()`: AnnualLeagueRaw → AnnualLeague
- `mapRawLeagueToLeague()`: LeagueRaw → League
- `mapRawAnnualTeamToAnnualTeam()`: AnnualTeamRaw → AnnualTeam
- `mapRawYearlyBasketToYearlyBasket()`: YearlyBasketRaw → YearlyBasket
- `mapRawBasketToBasket()`: BasketRaw → Basket
- `mapRawUserOfAGroupToUserOfAGroup()`: UserOfAGroupRaw → UserOfAGroup
- `mapRawGroupToGroup()`: GroupRaw → Group
- Reverse mappers: `mapUserOfAGroupToRaw()`, `mapAnnualTeamToRaw()`, `mapYearlyBasketToRaw()`, `mapBasketToRaw()`, `mapVoteLeagueSettingToRaw()`, `mapLeagueSettingToRaw()`, `mapAnnualLeagueToRaw()`, `mapLeagueToRaw()`, `mapGroupToRaw()`

---

## 📁 File: types/hallOfFame.ts

**Purpose:** Historical records, winners, and all-time statistics.

### Interfaces
- **GeneralLeagueKey** (GeneralLeagueKeyRaw): Key for league (groupId, leagueId)
- **RankedTeam** (RankedTeamRaw): Team with complete statistics (name, owner, points, victories, draws, defeats, goals, sufferedGoals, valuePoints, sufferedValuePoints, plusMoney, money, valueAssets)
- **WinningTeam** (WinningTeamRaw): Team with championship wins (owner, teamName, wins)
- **WinningPlayer** (WinningPlayerRaw): Player with championship wins (player, wins)
- **PlayerWithMostPointsInYear** (PlayerWithMostPointsInYearRaw): Best player in a season (player, year, points)
- **RecordPlayer** (RecordPlayerRaw): Player with highest single-game points (player, points)
- **RecordGame** (RecordGameRaw): Highest scoring game (game, year)
- **HallOfFame** (HallOfFameRaw): Complete hall of fame data (recordGame, recordPlayer, playerWithMostPointsInYear, winningPlayers, winningTeams, allTimeRankings)

### Helpers
- **HallOfFameHelper**: Sort rankings, find top teams/players, filter by criteria

### Mappers
- `mapRawGeneralLeagueKey()`: GeneralLeagueKeyRaw → GeneralLeagueKey
- `mapRawRankedTeam()`: RankedTeamRaw → RankedTeam
- `mapRawWinningTeam()`: WinningTeamRaw → WinningTeam
- `mapRawWinningPlayer()`: WinningPlayerRaw → WinningPlayer
- `mapRawPlayerWithMostPointsInYear()`: PlayerWithMostPointsInYearRaw → PlayerWithMostPointsInYear
- `mapRawRecordPlayer()`: RecordPlayerRaw → RecordPlayer
- `mapRawRecordGame()`: RecordGameRaw → RecordGame
- `mapRawHallOfFame()`: HallOfFameRaw → HallOfFame
- `mapGeneralLeagueKeyToRaw()`: GeneralLeagueKey → GeneralLeagueKeyRaw
- `mapRankedTeamToRaw()`: RankedTeam → RankedTeamRaw

---

## 📁 File: types/identity.ts

**Purpose:** User authentication, authorization, and group membership.

### Enums
- **IdentityRole**: User role flags (None, Blocked, User, Admin, SuperAdmin)

### Interfaces
- **GroupForIdentity** (GroupForIdentityRaw): Group info for identity (id, name, role, year)
- **AppIdentityState** (AppIdentityStateRaw): User's current context (selectedGroup, selectedLeague, selectedYear)
- **AppIdentity** (AppIdentityRaw): Complete user identity (email, name, groups, state)

### Helpers
- `hasRole()`: Check if user has specific role
- `getUserRoleNames()`: Get array of role names from flags
- `updateAppIdentityState()`: Create new identity with updated state
- `addGroupToAppIdentity()`: Add group to user's identity
- `removeGroupFromAppIdentity()`: Remove group from user's identity

### Mappers
- `mapGroupForIdentity()`: GroupForIdentityRaw → GroupForIdentity
- `mapAppIdentityState()`: AppIdentityStateRaw → AppIdentityState
- `mapAppIdentity()`: AppIdentityRaw → AppIdentity
- `mapAppIdentityToRaw()`: AppIdentity → AppIdentityRaw

---

## 📁 File: types/liveGroup.ts

**Purpose:** Live group state combining calendar rounds and rankings.

### Interfaces
- **LiveLeagueRound**: Single round with key and CalendarDay
- **LiveLeague** (LiveLeagueRaw): League with live data (id, name, rounds, rank)
- **LiveGroup** (LiveGroupRaw): Group with live leagues (name, leagues)
- **EnhancedLiveLeague**: LiveLeague + computed fields (roundKeys, roundsList, latestRoundKey, pendingGames, enhancedRank)
- **EnhancedLiveGroup**: LiveGroup + computed fields (leaguesWithRounds, totalPendingGames)

### Types
- **LiveLeagueRoundsRaw**: Dictionary mapping round keys to DayRaw or DayRaw arrays

### Helpers
- **LiveLeagueHelper**: Get rounds, find latest round, extract pending games, enhance league
- **LiveGroupHelper**: Find league by ID, filter leagues with rounds, enhance group

### Mappers
- `mapRawLiveLeagueToLiveLeague()`: LiveLeagueRaw → LiveLeague
- `mapRawLiveGroupToLiveGroup()`: LiveGroupRaw → LiveGroup
- `enhanceLiveLeague()`: LiveLeague → EnhancedLiveLeague
- `enhanceLiveGroup()`: LiveGroup → EnhancedLiveGroup

---

## 📁 File: types/log.ts

**Purpose:** Application logging and error tracking.

### Interfaces
- **FantasoccerLog** (FantasoccerLogRaw): Log entry (primaryKey, secondaryKey, timestamp, message, error, raw)

### Helpers
- `logHasError()`: Check if log contains error
- `getLogSeverity()`: Get severity level (info/error)
- `formatLogTimestamp()`: Format timestamp for display
- `sortLogsByTimestamp()`: Sort logs newest first
- `filterLogsByError()`: Filter logs by error presence
- `searchLogs()`: Search logs by text query

### Mappers
- `mapFantasoccerLog()`: FantasoccerLogRaw → FantasoccerLog
- `mapFantasoccerLogToRaw()`: FantasoccerLog → FantasoccerLogRaw

---

## 📁 File: types/market.ts

**Purpose:** Player trading system with approval workflow.

### Enums
- **MarketStatus**: Trade status (Pending, Approved, Denied, Cancelled, NoMoney, NoPlayers, Expired)

### Interfaces
- **Market** (MarketRaw): Trade offer (id, buyer, seller, sellerPlayers, buyerPlayers, moneyFromBuyer, moneyFromSeller, approvers, deniers, status, creationTime)
- **MarketWrapper** (MarketWrapperRaw): Collection of markets
- **MarketKey** (MarketKeyRaw): Repository key for market (groupId, year, leagueId, marketId)

### Helpers
- **MarketStatusHelper**: Get label, check status flags (isPending, isCompleted, isSuccessful, canBeModified)
- **MarketHelper**: Validate trades, calculate balances, check approval status, filter trades

### Mappers
- `mapRawMarketToMarket()`: MarketRaw → Market
- `mapRawMarketWrapperToMarketWrapper()`: MarketWrapperRaw → MarketWrapper
- `mapRawMarketKeyToMarketKey()`: MarketKeyRaw → MarketKey
- `mapMarketToRawMarket()`: Market → MarketRaw
- `mapMarketWrapperToRawMarketWrapper()`: MarketWrapper → MarketWrapperRaw
- `mapMarketKeyToRawMarketKey()`: MarketKey → MarketKeyRaw

---

## 📁 File: types/pushNotification.ts

**Purpose:** Push notification settings and subscription management.

### Interfaces
- **PushSubscription** (PushSubscriptionRaw): Push endpoint subscription (endpoint, p256dh, auth)
- **PushNotificationSettings** (PushNotificationSettingsRaw): Notification preferences (matchEvents, opponentMatchEvents, deploymentReminder, endDay, marketEvents, subscriptions)
- **PushNotificationSummary**: Summary of notification settings (totalEnabled, hasSubscriptions, enabledCategories, subscriptionCount)

### Constants
- **EMPTY_PUSH_NOTIFICATION_SETTINGS**: Default empty settings

### Helpers
- `buildPushNotificationSummary()`: Create summary from settings
- `mergePushNotificationSettings()`: Merge base settings with updates
- `addSubscription()`: Add new subscription (avoiding duplicates)
- `removeSubscription()`: Remove subscription by endpoint

### Mappers
- `mapRawPushNotificationSettingsToSettings()`: PushNotificationSettingsRaw → PushNotificationSettings
- `mapSubscriptionToRaw()`: PushSubscription → PushSubscriptionRaw
- `mapPushNotificationSettingsToRaw()`: PushNotificationSettings → PushNotificationSettingsRaw

---

## 📁 File: types/rank.ts

**Purpose:** League standings and team rankings.

### Interfaces
- **RankedTeam** (RankedTeamRaw): Team with ranking stats (name, owner, points, victories, draws, defeats, goals, sufferedGoals, valuePoints, sufferedValuePoints, plusMoney, money, valueAssets)
- **Rank** (RankRaw): Daily rankings for multiple rounds (serieADay, rounds)
- **LeagueKey** (LeagueKeyRaw): Repository key (groupId, year, leagueId)
- **YearDayLeague** (YearDayLeagueRaw): Extended key with day (groupId, year, leagueId, day)
- **EnhancedRank**: Rank + computed fields (roundKeys, allTeams, topTeams)
- **EnhancedRankedTeam**: RankedTeam + computed fields (goalDifference, totalGamesPlayed, pointsPerGame, position)

### Constants
- **DefaultRankedTeam**: Empty ranked team template
- **DefaultRank**: Empty rank template

### Helpers
- **RankHelper**: Calculate statistics, sort teams, find positions, compare teams

### Mappers
- `mapRawRankedTeamToRankedTeam()`: RankedTeamRaw → RankedTeam
- `mapRankedTeamToRawRankedTeam()`: RankedTeam → RankedTeamRaw
- `mapRawRankToRank()`: RankRaw → Rank
- `mapRankToRawRank()`: Rank → RankRaw
- `mapRawLeagueKeyToLeagueKey()`: LeagueKeyRaw → LeagueKey
- `mapLeagueKeyToRawLeagueKey()`: LeagueKey → LeagueKeyRaw
- `mapRawYearDayLeagueToYearDayLeague()`: YearDayLeagueRaw → YearDayLeague
- `mapYearDayLeagueToRawYearDayLeague()`: YearDayLeague → YearDayLeagueRaw
- `enhanceRank()`: Rank → EnhancedRank
- `enhanceRankedTeam()`: RankedTeam → EnhancedRankedTeam
- `enhanceRankWithTeamPositions()`: Rank → Record<string, EnhancedRankedTeam[]>

---

## 📁 File: types/serieA.ts

**Purpose:** Real Serie A data (teams, players, matches, votes, calendar).

### Enums
- **Behaviour**: Player behavior (None, Injured, Disqualified, Warned)

### Interfaces
- **RealTeam** (RealTeamRaw): Serie A team (name, abbreviation)
- **RealGame** (RealGameRaw): Serie A match (home, away, homeGoal, awayGoal, serieADay, isPlayed)
- **RealDay** (RealDayRaw): Serie A matchday (serieADay, games)
- **RealCalendar** (RealCalendarRaw): Complete Serie A calendar (days)
- **RealRankedTeam** (RealRankedTeamRaw): Serie A team with standings (name, points, victories, draws, defeats, goals, sufferedGoals, diff)
- **RealRank** (RealRankRaw): Serie A standings (teams)
- **RealPlayer** (RealPlayerRaw): Serie A player (name, team, role, isActive, visible)
- **RealPlayersWrapper** (RealPlayersWrapperRaw): Collection of real players
- **EnhancedRealPlayersWrapper**: Wrapper + playersByRole and sorted lists
- **Vote** (VoteRaw): Player match vote (value, yellowCard, redCard, goal, ownGoal, penalty, wrongedPenalty, stoppedPenalty, assist, sufferedGoal, behaviour)
- **VotedRealPlayer** (VotedRealPlayerRaw): Real player with vote
- **VotedRealPlayerWrapper** (VotedRealPlayerWrapperRaw): Collection of voted players
- **EnhancedVotedRealPlayerWrapper**: Wrapper + playersByRole and sorted lists
- **YearDayKey** (YearDayKeyRaw): Repository key (year, day)

### Types
- **RealPlayerRoleBuckets**: Partial record mapping Role to RealPlayer arrays
- **VotedRealPlayerRoleBuckets**: Partial record mapping Role to VotedRealPlayer arrays

### Helpers
- **RealPlayerHelper**: Sort players, group by role, filter by criteria

### Mappers
- `mapRawTeamToTeam()`: RealTeamRaw → RealTeam
- `mapRawGameToGame()`: RealGameRaw → RealGame
- `mapRawDayToDay()`: RealDayRaw → RealDay
- `mapRawCalendarToCalendar()`: RealCalendarRaw → RealCalendar
- `mapRawRankedTeamToRankedTeam()`: RealRankedTeamRaw → RealRankedTeam
- `mapRawRankToRank()`: RealRankRaw → RealRank
- `mapRawRealPlayerToRealPlayer()`: RealPlayerRaw → RealPlayer
- `mapRawPlayersWrapperToPlayersWrapper()`: RealPlayersWrapperRaw → RealPlayersWrapper
- `mapRawVoteToVote()`: VoteRaw → Vote
- `mapRawVotedRealPlayerToVotedRealPlayer()`: VotedRealPlayerRaw → VotedRealPlayer
- `mapRawVotedPlayersWrapperToWrapper()`: VotedRealPlayerWrapperRaw → VotedRealPlayerWrapper
- `mapRawYearDayKeyToYearDayKey()`: YearDayKeyRaw → YearDayKey
- `mapYearDayKeyToRawYearDayKey()`: YearDayKey → YearDayKeyRaw
- `enhanceRealPlayersWrapper()`: RealPlayersWrapper → EnhancedRealPlayersWrapper

---

## 📁 File: types/stat.ts

**Purpose:** Player statistics and performance metrics.

### Interfaces
- **StatPlayerGame** (StatPlayerGameRaw): Single game stats (serieADay, vote, positiveness)
- **StatPlayer** (StatPlayerRaw): Player with full statistics (summatory, fantaSummatory, withVote, withoutVote, withSpecial, goals, penalties, assists, stoppedPenalties, sufferedGoals, wrongedPenalties, ownGoals, yellowCards, redCards, enoughVotes, manOfTheMatch, injured, games, average, fantaAverage)
- **StatPlayersWrapper** (StatPlayersWrapperRaw): Collection of stat players
- **EnhancedStatPlayersWrapper**: Wrapper + playersByRole, topByAverage, topByFantaAverage

### Types
- **StatPlayerRoleBuckets**: Partial record mapping Role to StatPlayer arrays

### Constants
- **DEFAULT_TOP_LIMIT**: Default number of top players to return (5)

### Helpers
- **StatPlayerHelper**: Calculate averages, sort by performance, filter by criteria

### Mappers
- `mapRawStatPlayerGameToGame()`: StatPlayerGameRaw → StatPlayerGame
- `mapRawStatPlayerToStatPlayer()`: StatPlayerRaw → StatPlayer
- `mapRawStatPlayersWrapperToWrapper()`: StatPlayersWrapperRaw → StatPlayersWrapper
- `enhanceStatPlayersWrapper()`: StatPlayersWrapper → EnhancedStatPlayersWrapper

---

## 📁 File: types/team.ts

**Purpose:** Fantasy team management with players and financial tracking.

### Enums
- **PlayerInTeamStatus**: Player status in team (Active, Bench, Substituted, Excluded, Injured, Disqualified, Warned)
- **FantaSoccerRole**: Fantasy roles (Goalkeeper, RightDefender, CentralDefender, LeftDefender, RightMidfielder, CentralMidfielder, LeftMidfielder, RightStriker, CentralStriker, LeftStriker, Unknown)

### Interfaces
- **RealPlayer** (RealPlayerRaw): Basic player info (name, team, role, isActive, visible)
- **Player** (PlayerRaw): Fantasy player extending RealPlayer (price, status, position)
- **TeamKey** (TeamKeyRaw): Repository key for team (group, year, basketId, email)
- **TeamDayKey** (TeamDayKeyRaw): Extended key with day (group, year, basketId, email, day)
- **Team** (TeamRaw): Fantasy team (name, owner, additionalOwners, players, revenueMoney, moneyFromRank, lastUpdate)
- **EnhancedTeam**: Team + computed fields (totalValue, availableMoney, activePlayers, benchPlayers, etc.)

### Helpers
- **PlayerStatusHelper**: Get label, check if active
- **FantaSoccerRoleHelper**: Convert to main role
- **PlayerHelper**: Filter players, calculate values, validate lineup
- **TeamKeyHelper**: Serialize/deserialize keys, compare keys

### Mappers
- `mapRawRealPlayerToRealPlayer()`: RealPlayerRaw → RealPlayer
- `mapRawPlayerToPlayer()`: PlayerRaw → Player
- `mapPlayerToRawPlayer()`: Player → PlayerRaw
- `mapRawTeamKeyToTeamKey()`: TeamKeyRaw → TeamKey
- `mapTeamKeyToRawTeamKey()`: TeamKey → TeamKeyRaw
- `mapRawTeamDayKeyToTeamDayKey()`: TeamDayKeyRaw → TeamDayKey
- `mapTeamDayKeyToRawTeamDayKey()`: TeamDayKey → TeamDayKeyRaw
- `mapRawTeamToTeam()`: TeamRaw → Team
- `mapTeamToRawTeam()`: Team → TeamRaw
- `enhanceTeam()`: Team → EnhancedTeam

---

## 🔑 Key Concepts

### Raw vs Clean Interfaces
- **Raw interfaces**: Match C# backend JSON property names (usually single letters like `n`, `p`, `v`)
- **Clean interfaces**: Human-readable property names for frontend use
- **Mappers**: Convert between Raw and Clean formats

### Enhanced Interfaces
- Add computed/derived fields that don't exist in backend
- Provide convenience properties for UI rendering
- Often include sorted/filtered/grouped data

### Helper Classes
- Provide utility methods for type-specific operations
- Handle sorting, filtering, calculations, validation
- Centralize business logic related to the type

### Repository Keys
- Types ending in `Key` are used for data repository operations
- Usually combine multiple identifiers (groupId, year, leagueId, etc.)
- Have both Raw and Clean versions with bidirectional mappers

---

## 📊 Type Categories Summary

| Category | Count | Examples |
|----------|-------|----------|
| **Enums** | 10 | AuctionType, GameResultType, ChanceType, Role, LeagueType |
| **Raw Interfaces** | 60+ | PlayerRaw, TeamRaw, MarketRaw, CalendarGameRaw |
| **Clean Interfaces** | 60+ | Player, Team, Market, CalendarGame |
| **Enhanced Interfaces** | 15+ | EnhancedTeam, EnhancedRank, EnhancedCalendar |
| **Helper Classes** | 12 | TeamHelper, RankHelper, CalendarHelper |
| **Type Aliases** | 8 | CalendarRounds, RealPlayerRoleBuckets |
| **Constants** | 5 | DefaultRankedTeam, EMPTY_PUSH_NOTIFICATION_SETTINGS |

---

## 🎯 Usage Patterns

### API Integration
```typescript
// Backend returns raw data
const raw: TeamRaw = await api.getTeam(teamKey)

// Map to clean interface for frontend
const team: Team = mapRawTeamToTeam(raw)

// Enhance with computed fields
const enhanced: EnhancedTeam = enhanceTeam(team)
```

### Data Submission
```typescript
// User edits clean data
const updatedTeam: Team = { ...team, name: 'New Name' }

// Convert to raw for API
const raw: TeamRaw = mapTeamToRawTeam(updatedTeam)

// Send to backend
await api.updateTeam(teamKey, raw)
```

### Type Guards & Helpers
```typescript
// Use helper methods
const activePlayers = TeamHelper.getActivePlayers(team)
const canTrade = MarketStatusHelper.canBeModified(market.status)
const hasError = logHasError(log)

// Use enhanced types for UI
const { totalValue, availableMoney } = enhanceTeam(team)
```

---

**Last Updated:** {{ current_date }}
**Total Types:** 150+
**Files Covered:** 15
