import { RealPlayer, RealPlayerRaw, mapRawRealPlayerToRealPlayer, RealPlayerHelper } from './serieA'
import { Role } from './group'

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

export interface StatPlayerGameRaw {
  d: number   // Serie A day (matches [JsonPropertyName("d")])
  v: number | null // Vote (matches [JsonPropertyName("v")])
  p: number   // Positiveness (matches [JsonPropertyName("p")])
}

export interface StatPlayerRaw extends RealPlayerRaw {
  ds?: PlayerDescriptionRaw | null
  z: number   // Summatory (matches [JsonPropertyName("z")])
  f: number   // FantaSummatory (matches [JsonPropertyName("f")])
  v: number   // WithVote (matches [JsonPropertyName("v")])
  q: number   // WithoutVote (matches [JsonPropertyName("q")])
  np: number  // NoPlayed (matches [JsonPropertyName("np")])
  s: number   // WithSpecial (matches [JsonPropertyName("s")])
  g: number   // Goal (matches [JsonPropertyName("g")])
  p: number   // Penalty (matches [JsonPropertyName("p")])
  u: number   // Assist (matches [JsonPropertyName("u")])
  j: number   // StoppedPenalty (matches [JsonPropertyName("j")])
  m: number   // SufferedGoal (matches [JsonPropertyName("m")])
  w: number   // WrongedPenalty (matches [JsonPropertyName("w")])
  o: number   // OwnGoal (matches [JsonPropertyName("o")])
  y: number   // YellowCard (matches [JsonPropertyName("y")])
  d: number   // RedCard (matches [JsonPropertyName("d")])
  e: number   // IsEnough (matches [JsonPropertyName("e")])
  c: number   // ManOfTheMatch (matches [JsonPropertyName("c")])
  ij: number  // Injured (matches [JsonPropertyName("ij")])
  lg?: StatPlayerGameRaw[] | null // Games (matches [JsonPropertyName("lg")])
}

export interface StatPlayersWrapperRaw {
  p: StatPlayerRaw[] // Players collection (matches [JsonPropertyName("p")])
}

// ============================================
// CLEAN INTERFACES (readable property names)
// ============================================

export interface StatPlayerGame {
  serieADay: number
  vote: number | null
  positiveness: number
}

export interface PlayerDescriptionRaw {
  top?: string | null
  down?: string | null
}

export interface PlayerDescription {
  top?: string
  down?: string
}

export interface StatPlayer extends RealPlayer {
  description?: PlayerDescription
  summatory: number
  fantaSummatory: number
  withVote: number
  withoutVote: number
  noPlayed: number
  withSpecial: number
  goals: number
  penalties: number
  assists: number
  stoppedPenalties: number
  sufferedGoals: number
  wrongedPenalties: number
  ownGoals: number
  yellowCards: number
  redCards: number
  enoughVotes: number
  manOfTheMatch: number
  injured: number
  games: StatPlayerGame[]
  average: number
  fantaAverage: number
}

export interface StatPlayersWrapper {
  players: StatPlayer[]
}

export type StatPlayerRoleBuckets = Partial<Record<Role, StatPlayer[]>>

export interface EnhancedStatPlayersWrapper extends StatPlayersWrapper {
  playersByRole: StatPlayerRoleBuckets
  topByAverage: StatPlayer[]
  topByFantaAverage: StatPlayer[]
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

export function mapRawStatPlayerGameToGame(raw: StatPlayerGameRaw): StatPlayerGame {
  return {
    serieADay: raw.d,
    vote: raw.v ?? null,
    positiveness: raw.p ?? 0
  }
}

export function mapRawStatPlayerToStatPlayer(raw: StatPlayerRaw): StatPlayer {
  const base = mapRawRealPlayerToRealPlayer(raw)

  const games = raw.lg?.map(mapRawStatPlayerGameToGame) ?? []
  const withVote = raw.v ?? 0
  const summatory = raw.z ?? 0
  const fantaSummatory = raw.f ?? 0

  const average = withVote > 0 ? summatory / withVote : 0
  const fantaAverage = withVote > 0 ? fantaSummatory / withVote : 0

  return {
    ...base,
    description: raw.ds ? { top: raw.ds.top ?? undefined, down: raw.ds.down ?? undefined } : undefined,
    summatory,
    fantaSummatory,
    withVote,
    withoutVote: raw.q ?? 0,
    noPlayed: raw.np ?? 0,
    withSpecial: raw.s ?? 0,
    goals: raw.g ?? 0,
    penalties: raw.p ?? 0,
    assists: raw.u ?? 0,
    stoppedPenalties: raw.j ?? 0,
    sufferedGoals: raw.m ?? 0,
    wrongedPenalties: raw.w ?? 0,
    ownGoals: raw.o ?? 0,
    yellowCards: raw.y ?? 0,
    redCards: raw.d ?? 0,
    enoughVotes: raw.e ?? 0,
    manOfTheMatch: raw.c ?? 0,
    injured: raw.ij ?? 0,
    games,
    average,
    fantaAverage
  }
}

export function mapRawStatPlayersWrapperToWrapper(raw: StatPlayersWrapperRaw | null | undefined): StatPlayersWrapper {
  const players = raw?.p?.map(mapRawStatPlayerToStatPlayer) ?? []

  return {
    players: StatPlayerHelper.sortByAverage(players)
  }
}

// ============================================
// HELPERS & ENHANCERS
// ============================================

const DEFAULT_TOP_LIMIT = 5

export class StatPlayerHelper {
  static sortByAverage(players: StatPlayer[]): StatPlayer[] {
    return [...players].sort((a, b) => b.average - a.average || a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' }))
  }

  static sortByFantaAverage(players: StatPlayer[]): StatPlayer[] {
    return [...players].sort((a, b) => b.fantaAverage - a.fantaAverage || a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' }))
  }

  static groupByRole(players: StatPlayer[]): StatPlayerRoleBuckets {
    return players.reduce<StatPlayerRoleBuckets>((acc, player) => {
      const role = player.role ?? Role.Undefined
      acc[role] = acc[role] ? [...acc[role]!, player] : [player]
      return acc
    }, {})
  }

  static search(players: StatPlayer[], term: string): StatPlayer[] {
    if (!term) {
      return players
    }

    const normalizedTerm = RealPlayerHelper.buildKey(term)

    return players.filter(player =>
      player.name.toLowerCase().includes(term.toLowerCase()) ||
      player.key.includes(normalizedTerm)
    )
  }

  static getTopByAverage(players: StatPlayer[], limit: number = DEFAULT_TOP_LIMIT): StatPlayer[] {
    return StatPlayerHelper.sortByAverage(players).slice(0, limit)
  }

  static getTopByFantaAverage(players: StatPlayer[], limit: number = DEFAULT_TOP_LIMIT): StatPlayer[] {
    return StatPlayerHelper.sortByFantaAverage(players).slice(0, limit)
  }
}

export const enhanceStatPlayersWrapper = (wrapper: StatPlayersWrapper, topLimit: number = DEFAULT_TOP_LIMIT): EnhancedStatPlayersWrapper => {
  const playersByRole = StatPlayerHelper.groupByRole(wrapper.players)

  return {
    ...wrapper,
    playersByRole,
    topByAverage: StatPlayerHelper.getTopByAverage(wrapper.players, topLimit),
    topByFantaAverage: StatPlayerHelper.getTopByFantaAverage(wrapper.players, topLimit)
  }
}
