// Types matching Fantasoccer team/player domain structures
import { Role } from './group'
import type { RealTeam } from './serieA'

// ============================================
// ENUMS (matching C# enums)
// ============================================

export enum PlayerInTeamStatus {
  Active = 0,
  SoldForOneHalf = 1,
  SoldWithNoReturnedPrice = 2,
  Sold = 3,
  Removed = 4,
  // Left Serie A (or long out): revenue holds the refund fixed by the league rules at that moment.
  LeftSerieA = 5
}

export enum FantaSoccerRole {
  All = -2,
  Invasion = -1,
  GoalKeeper = 0,
  Defensor = 1,
  Midfielder = 2,
  Forward = 3,
  BackupGoalKeeper = 4,
  FirstBackupDefensor = 5,
  SecondBackupDefensor = 6,
  FirstBackupMidfielder = 7,
  SecondBackupMidfielder = 8,
  FirstBackupForward = 9,
  SecondBackupForward = 10,
  Tribune = 11
}

export const PlayerStatusHelper = {
  isActive: (status: PlayerInTeamStatus): boolean => status === PlayerInTeamStatus.Active,
  asLabel: (status: PlayerInTeamStatus): string => {
    switch (status) {
      case PlayerInTeamStatus.Active:
        return 'Attivo'
      case PlayerInTeamStatus.SoldForOneHalf:
        return 'Infortunato/Estero (metà prezzo)'
      case PlayerInTeamStatus.SoldWithNoReturnedPrice:
        return 'Scartato (nessun rimborso)'
      case PlayerInTeamStatus.Sold:
        return 'Venduto'
      case PlayerInTeamStatus.Removed:
        return 'Rimosso'
      case PlayerInTeamStatus.LeftSerieA:
        return 'Uscito dalla Serie A'
      default:
        return 'Sconosciuto'
    }
  }
}

export const FantaSoccerRoleHelper = {
  toMainRole: (role: FantaSoccerRole): Role => {
    switch (role) {
      case FantaSoccerRole.GoalKeeper:
      case FantaSoccerRole.BackupGoalKeeper:
        return Role.GoalKeeper
      case FantaSoccerRole.Defensor:
      case FantaSoccerRole.FirstBackupDefensor:
      case FantaSoccerRole.SecondBackupDefensor:
        return Role.Defensor
      case FantaSoccerRole.Midfielder:
      case FantaSoccerRole.FirstBackupMidfielder:
      case FantaSoccerRole.SecondBackupMidfielder:
        return Role.Midfielder
      case FantaSoccerRole.Forward:
      case FantaSoccerRole.FirstBackupForward:
      case FantaSoccerRole.SecondBackupForward:
        return Role.Forward
      default:
        return Role.Undefined
    }
  }
}

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

export interface RealPlayer {
  name: string
  team: RealTeam
  role: Role
  isActive: boolean
  visible: boolean
}

export interface Player extends RealPlayer {
  playerId?: string
  seasonIndex?: number | null
  playerKey?: string | null
  ordinal?: number
  price: number
  revenue: number
  status: PlayerInTeamStatus
  position: FantaSoccerRole
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

// ============================================
// HELPER FUNCTIONS
// ============================================

export class PlayerHelper {
  static filterActive(players: Player[]): Player[] {
    return players.filter(player => PlayerStatusHelper.isActive(player.status))
  }

  static filterByMainRole(players: Player[], role: Role): Player[] {
    return players.filter(player => FantaSoccerRoleHelper.toMainRole(player.position) === role)
  }

  static sortByPriceDesc(players: Player[]): Player[] {
    return [...players].sort((a, b) => b.price - a.price)
  }
}

// ============================================
// TEAM KEYS (matching C# key classes)
// ============================================

export interface TeamKey {
  group: string
  year: number
  basketId: string
  email: string
}

export interface TeamDayKey extends TeamKey {
  day: number
}

export class TeamKeyHelper {
  static create(group: string, year: number, basketId: string, email: string): TeamKey {
    return { group, year, basketId, email }
  }

  static createDayKey(group: string, year: number, basketId: string, day: number, email: string): TeamDayKey {
    return { group, year, basketId, day, email }
  }

  static toString(key: TeamKey): string {
    return JSON.stringify([key.group, key.year, key.basketId, key.email])
  }

  static toDayString(key: TeamDayKey): string {
    return JSON.stringify([key.group, key.year, key.basketId, key.day, key.email])
  }
}

// ============================================
// TEAM RAW/CLEAN TYPES
// ============================================

export interface Team {
  teamId?: string
  groupId?: string
  basketId?: string
  seasonIndex?: number
  kind?: number
  day?: number
  leagueId?: string | null
  version?: number
  sourceVersion?: number | null
  logoUrl?: string | null
  name: string
  owner: string
  additionalOwners: string[]
  players: Player[]
  moneyFromRank: number
  marketBalanceAdjustment?: number
  rankCreditGranted?: number | null
  openingCompetitionPrize: number
  lastUpdate: string | Date | null
}

// A team's money, the same way the server computes it for auctions, market and standings.
export interface TeamEconomy {
  startingMoney: number
  activeCost: number // price of the players in the roster now
  releasedCost: number // what the players no longer in the roster still cost (price minus refund)
  refunds: number // credits given back: sale prices, refunds of players who left Serie A, old half refunds
  rankCredits: number // goal credits already received at an auction
  market: number // credits exchanged in market trades
  openingPrize: number
  cost: number
  balance: number // credits available now
}

export interface EnhancedTeam extends Team {
  totalPlayers: number
  activePlayers: Player[]
  activeCost: number
  releasedCost: number
  refunds: number
  totalCost: number
  revenueMoney: number
  moneyFromSoldWithNoReturnedPrice: number
  moneyFromSoldWithOneHalfReturnedPrice: number
  cost: number
  netCost: number // Alias for cost
}

// ============================================
// TEAM HELPERS
// ============================================

export class TeamHelper {
  static getActivePlayers(team: Team): Player[] {
    return PlayerHelper.filterActive(team.players)
  }

  static getPlayersByRole(team: Team, role: Role): Player[] {
    return PlayerHelper.filterByMainRole(team.players, role)
  }

  static getTotalCostForPlayers(team: Team): number {
    return team.players.reduce((sum, player) => sum + (player.price ?? 0), 0)
  }

  // Alias for consistency with C# naming
  static getTotalCost(team: Team): number {
    return TeamHelper.getTotalCostForPlayers(team)
  }

  // Credits a roster row gives back: half price (old injured/abroad status), the sale price of a sale,
  // the refund of a player who left Serie A. Active and discarded players give nothing back.
  static getRefund(player: Player): number {
    switch (player.status) {
      case PlayerInTeamStatus.SoldForOneHalf: return Math.floor((player.price ?? 0) / 2)
      case PlayerInTeamStatus.Sold:
      case PlayerInTeamStatus.LeftSerieA: return player.revenue ?? 0
      default: return 0
    }
  }

  // Sale prices and refunds of players who left Serie A.
  static getRevenueMoney(team: Team): number {
    return team.players
      .filter(player => player.status === PlayerInTeamStatus.Sold || player.status === PlayerInTeamStatus.LeftSerieA)
      .reduce((sum, player) => sum + TeamHelper.getRefund(player), 0)
  }

  static getEconomy(team: Team, startingMoney: number): TeamEconomy {
    const active = team.players.filter(player => player.status === PlayerInTeamStatus.Active)
    const released = team.players.filter(player => player.status !== PlayerInTeamStatus.Active)
    const refunds = team.players.reduce((sum, player) => sum + TeamHelper.getRefund(player), 0)
    const cost = TeamHelper.getCost(team)
    return {
      startingMoney,
      activeCost: active.reduce((sum, player) => sum + (player.price ?? 0), 0),
      releasedCost: released.reduce((sum, player) => sum + (player.price ?? 0) - TeamHelper.getRefund(player), 0),
      refunds,
      rankCredits: team.moneyFromRank,
      market: team.marketBalanceAdjustment ?? 0,
      openingPrize: team.openingCompetitionPrize,
      cost,
      balance: startingMoney - cost,
    }
  }

  static getMoneyFromSoldWithNoReturnedPrice(team: Team): number {
    return team.players
      .filter(player => player.status === PlayerInTeamStatus.SoldWithNoReturnedPrice)
      .reduce((sum, player) => sum + player.price, 0)
  }

  static getMoneyFromSoldWithOneHalfReturnedPrice(team: Team): number {
    return team.players
      .filter(player => player.status === PlayerInTeamStatus.SoldForOneHalf)
      .reduce((sum, player) => sum + Math.floor(player.price / 2), 0)
  }

  // Every player ever bought keeps his price; whoever left the roster gives back only his refund.
  static getCost(team: Team): number {
    return team.players.reduce((sum, player) => sum + (player.price ?? 0) - TeamHelper.getRefund(player), 0)
      - team.moneyFromRank
      - (team.marketBalanceAdjustment ?? 0)
      - team.openingCompetitionPrize
  }

  // Alias for backward compatibility
  static getNetCost(team: Team): number {
    return TeamHelper.getCost(team)
  }

  static enhance(team: Team): EnhancedTeam {
    const totalCost = TeamHelper.getTotalCostForPlayers(team)
    const revenueMoney = TeamHelper.getRevenueMoney(team)
    const moneyNoReturn = TeamHelper.getMoneyFromSoldWithNoReturnedPrice(team)
    const moneyHalfReturn = TeamHelper.getMoneyFromSoldWithOneHalfReturnedPrice(team)
    const cost = TeamHelper.getCost(team)

    const economy = TeamHelper.getEconomy(team, 0)
    return {
      ...team,
      totalPlayers: team.players.length,
      activePlayers: TeamHelper.getActivePlayers(team),
      activeCost: economy.activeCost,
      releasedCost: economy.releasedCost,
      refunds: economy.refunds,
      totalCost,
      revenueMoney,
      moneyFromSoldWithNoReturnedPrice: moneyNoReturn,
      moneyFromSoldWithOneHalfReturnedPrice: moneyHalfReturn,
      cost,
      netCost: cost // Alias
    }
  }
}

export const enhanceTeam = (team: Team): EnhancedTeam => TeamHelper.enhance(team)
