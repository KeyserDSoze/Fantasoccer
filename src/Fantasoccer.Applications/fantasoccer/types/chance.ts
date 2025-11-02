import {
  RealPlayer,
  RealPlayerRaw,
  RealPlayerHelper,
  mapRawRealPlayerToRealPlayer,
} from './serieA'
import { Role } from './group'
import {
  StatPlayerGame,
  StatPlayerGameRaw,
  mapRawStatPlayerGameToGame,
} from './stat'

// ============================================
// ENUMS (matching C# enums)
// ============================================

export enum ChanceType {
  Normal = 0,
  Injury = 1,
  Warned = 2,
  Disqualified = 3,
  Maybe = 4,
}

export enum TrendType {
  Bad = 0,
  NotBad = 1,
  Normal = 2,
  VeryGood = 3,
  Excellent = 4,
}

export type ChanceSource = 'fantagazzetta' | 'gazzetta' | 'mediaset' | 'sky'

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

export interface ChanceRaw {
  f: boolean // Fantagazzetta (matches [JsonPropertyName("f")])
  g: boolean // Gazzetta (matches [JsonPropertyName("g")])
  m: boolean // Mediaset (matches [JsonPropertyName("m")])
  s: boolean // Sky (matches [JsonPropertyName("s")])
  t: ChanceType // Status (matches [JsonPropertyName("t")])
  d: string | null // Description (matches [JsonPropertyName("d")])
  l: StatPlayerGameRaw | null // LastGame (matches [JsonPropertyName("l")])
  r: TrendType // Trend (matches [JsonPropertyName("r")])
}

export interface ChancedRealPlayerRaw extends RealPlayerRaw {
  c: ChanceRaw // Chance information (matches [JsonPropertyName("c")])
}

export interface ChancedRealPlayerWrapperRaw {
  p: ChancedRealPlayerRaw[] // Players collection (matches [JsonPropertyName("p")])
}

// ============================================
// CLEAN INTERFACES (readable property names)
// ============================================

export interface Chance {
  fantagazzetta: boolean
  gazzetta: boolean
  mediaset: boolean
  sky: boolean
  status: ChanceType
  description: string | null
}

export interface EnhancedChance extends Chance {
  sources: ChanceSource[]
  isInjured: boolean
  isWarned: boolean
  isDisqualified: boolean
  isDoubtful: boolean
  isAvailable: boolean
  availabilityLabel: string
}

export interface ChancedRealPlayer extends RealPlayer {
  chance: EnhancedChance
}

export interface ChancedRealPlayerWrapper {
  players: ChancedRealPlayer[]
}

export type ChancedRealPlayerRoleBuckets = Partial<Record<Role, ChancedRealPlayer[]>>

export interface EnhancedChancedRealPlayerWrapper extends ChancedRealPlayerWrapper {
  playersByRole: ChancedRealPlayerRoleBuckets
  injuredPlayers: ChancedRealPlayer[]
  doubtfulPlayers: ChancedRealPlayer[]
  warnedPlayers: ChancedRealPlayer[]
  disqualifiedPlayers: ChancedRealPlayer[]
  availablePlayers: ChancedRealPlayer[]
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

export function mapRawChanceToChance(raw: ChanceRaw): Chance {
  return {
    fantagazzetta: raw?.f ?? false,
    gazzetta: raw?.g ?? false,
    mediaset: raw?.m ?? false,
    sky: raw?.s ?? false,
    status: raw?.t ?? ChanceType.Normal,
    description: raw?.d ?? null,
  }
}

export function enhanceChance(chance: Chance): EnhancedChance {
  const sources: ChanceSource[] = []
  if (chance.fantagazzetta) sources.push('fantagazzetta')
  if (chance.gazzetta) sources.push('gazzetta')
  if (chance.mediaset) sources.push('mediaset')
  if (chance.sky) sources.push('sky')

  const isInjured = chance.status === ChanceType.Injury
  const isWarned = chance.status === ChanceType.Warned
  const isDisqualified = chance.status === ChanceType.Disqualified
  const isDoubtful = chance.status === ChanceType.Maybe
  const isAvailable = !isInjured && !isDisqualified
  const availabilityLabel = ChanceHelper.getAvailabilityLabel(chance.status)

  return {
    ...chance,
    sources,
    isInjured,
    isWarned,
    isDisqualified,
    isDoubtful,
    isAvailable,
    availabilityLabel,
  }
}

export function mapRawChancedRealPlayerToPlayer(raw: ChancedRealPlayerRaw): ChancedRealPlayer {
  const basePlayer = mapRawRealPlayerToRealPlayer(raw)
  const chance = raw?.c ? mapRawChanceToChance(raw.c) : ChanceHelper.defaultChance()

  return {
    ...basePlayer,
    chance: enhanceChance(chance),
  }
}

export function mapRawChancedWrapperToWrapper(raw: ChancedRealPlayerWrapperRaw | null | undefined): ChancedRealPlayerWrapper {
  const players = raw?.p?.map(mapRawChancedRealPlayerToPlayer) ?? []

  return {
    players: ChancedRealPlayerHelper.sortByRoleAndTrend(players),
  }
}

// ============================================
// HELPERS
// ============================================

export class ChanceHelper {
  static defaultChance(): Chance {
    return {
      fantagazzetta: false,
      gazzetta: false,
      mediaset: false,
      sky: false,
      status: ChanceType.Normal,
      description: null,
    }
  }

  static getAvailabilityLabel(status: ChanceType): string {
    switch (status) {
      case ChanceType.Injury:
        return 'Infortunato'
      case ChanceType.Warned:
        return 'Diffidato'
      case ChanceType.Disqualified:
        return 'Squalificato'
      case ChanceType.Maybe:
        return 'In dubbio'
      default:
        return 'Disponibile'
    }
  }
}

const TREND_ORDER: Record<TrendType, number> = {
  [TrendType.Bad]: 0,
  [TrendType.NotBad]: 1,
  [TrendType.Normal]: 2,
  [TrendType.VeryGood]: 3,
  [TrendType.Excellent]: 4,
}

export class ChancedRealPlayerHelper {
  static sortByRoleAndTrend(players: ChancedRealPlayer[]): ChancedRealPlayer[] {
    return [...players].sort((a, b) => {
      const roleComparison = ChancedRealPlayerHelper.getRoleOrder(a.role) - ChancedRealPlayerHelper.getRoleOrder(b.role)
      if (roleComparison !== 0) {
        return roleComparison
      }

      // Trend rimosso - ora ordinamento solo per ruolo e nome
      return a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' })
    })
  }

  private static getRoleOrder(role: Role): number {
    switch (role) {
      case Role.GoalKeeper:
        return 0
      case Role.Defensor:
        return 1
      case Role.Midfielder:
        return 2
      case Role.Forward:
        return 3
      default:
        return 4
    }
  }

  static groupByRole(players: ChancedRealPlayer[]): ChancedRealPlayerRoleBuckets {
    return players.reduce<ChancedRealPlayerRoleBuckets>((acc, player) => {
      const role = player.role ?? Role.Undefined
      acc[role] = acc[role] ? [...acc[role]!, player] : [player]
      return acc
    }, {})
  }

  static filterInjured(players: ChancedRealPlayer[]): ChancedRealPlayer[] {
    return players.filter(player => player.chance.isInjured)
  }

  static filterDoubtful(players: ChancedRealPlayer[]): ChancedRealPlayer[] {
    return players.filter(player => player.chance.isDoubtful)
  }

  static filterWarned(players: ChancedRealPlayer[]): ChancedRealPlayer[] {
    return players.filter(player => player.chance.isWarned)
  }

  static filterDisqualified(players: ChancedRealPlayer[]): ChancedRealPlayer[] {
    return players.filter(player => player.chance.isDisqualified)
  }

  static filterAvailable(players: ChancedRealPlayer[]): ChancedRealPlayer[] {
    return players.filter(player => player.chance.isAvailable)
  }
}

export const enhanceChancedRealPlayersWrapper = (wrapper: ChancedRealPlayerWrapper): EnhancedChancedRealPlayerWrapper => {
  const playersByRole = ChancedRealPlayerHelper.groupByRole(wrapper.players)
  const injuredPlayers = ChancedRealPlayerHelper.filterInjured(wrapper.players)
  const doubtfulPlayers = ChancedRealPlayerHelper.filterDoubtful(wrapper.players)
  const warnedPlayers = ChancedRealPlayerHelper.filterWarned(wrapper.players)
  const disqualifiedPlayers = ChancedRealPlayerHelper.filterDisqualified(wrapper.players)
  const availablePlayers = ChancedRealPlayerHelper.filterAvailable(wrapper.players)

  return {
    ...wrapper,
    playersByRole,
    injuredPlayers,
    doubtfulPlayers,
    warnedPlayers,
    disqualifiedPlayers,
    availablePlayers,
  }
}
