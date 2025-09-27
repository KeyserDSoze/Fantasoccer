// Types matching Fantasoccer team/player domain structures
import { Role } from './group'
import {
  RealTeam,
  RealTeamRaw,
  mapRawTeamToTeam
} from './serieA'

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

export interface RealPlayerRaw {
  n: string
  t: RealTeamRaw
  r: Role
  a: boolean
  vh: boolean
}

export interface PlayerRaw extends RealPlayerRaw {
  p: number
  s: PlayerInTeamStatus
  k: FantaSoccerRole
}

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
  price: number
  status: PlayerInTeamStatus
  position: FantaSoccerRole
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

export const mapRawRealPlayerToRealPlayer = (raw: RealPlayerRaw): RealPlayer => ({
  name: raw.n,
  team: mapRawTeamToTeam(raw.t),
  role: raw.r,
  isActive: raw.a,
  visible: raw.vh
})

export const mapRawPlayerToPlayer = (raw: PlayerRaw): Player => ({
  ...mapRawRealPlayerToRealPlayer(raw),
  price: raw.p,
  status: raw.s,
  position: raw.k
})

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
