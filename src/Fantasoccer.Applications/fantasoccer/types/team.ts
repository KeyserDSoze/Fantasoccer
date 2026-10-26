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
  Removed = 4
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
        return 'Venduto metà prezzo'
      case PlayerInTeamStatus.SoldWithNoReturnedPrice:
        return 'Venduto senza ritorno'
      case PlayerInTeamStatus.Sold:
        return 'Venduto'
      case PlayerInTeamStatus.Removed:
        return 'Rimosso'
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

export interface EnhancedTeam extends Team {
  totalPlayers: number
  activePlayers: Player[]
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

  static getRevenueMoney(team: Team): number {
    return team.players
      .filter(player => player.status === PlayerInTeamStatus.Sold)
      .reduce((sum, player) => sum + ((player.revenue ?? 0) - player.price), 0)
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

  static getCost(team: Team): number {
    return TeamHelper.getTotalCostForPlayers(team)
      - TeamHelper.getRevenueMoney(team)
      - team.moneyFromRank
      - (team.marketBalanceAdjustment ?? 0)
      - team.openingCompetitionPrize
      - TeamHelper.getMoneyFromSoldWithOneHalfReturnedPrice(team)
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

    return {
      ...team,
      totalPlayers: team.players.length,
      activePlayers: TeamHelper.getActivePlayers(team),
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
