// Types and mapping helpers for the Game API (GameWrapper / LiveTeam / LivePlayer)
import { CalendarGame, mapRawGameResultToGameResult } from 'types/calendar'
import { Player } from 'types/team'
import { Vote } from 'types/serieA'
import { Chance } from 'types/chance'

// RAW interfaces matching JSON names produced by the C# backend
export interface LivePlayerRaw {
  c: Player
  v?: Vote | null
  h?: Chance | null
  r?: CalendarGame | null
  f?: number | null
  p?: number | null
}

export interface LiveTeamRaw {
  n: string
  o: string
  a?: string[]
  p: LivePlayerRaw[]
  d?: string | null
}

export interface GameWrapperRaw {
  t?: LiveTeamRaw[]
  g?: CalendarGame | null
  s?: number
}

// CLEANED interfaces used in the client
export interface LivePlayer {
  current: Player
  vote?: Vote | null
  chance?: Chance | null
  realGame?: CalendarGame | null
  finalValue?: number | null
  currentPosition?: number | null
}

export interface LiveTeam {
  name: string
  owner: string
  additionalOwners: string[]
  players: LivePlayer[]
  lastUpdate?: string | null
}

export interface GameWrapper {
  teams: LiveTeam[]
  game?: CalendarGame | null
  serieADay?: number
}

// MAPPERS
export const mapRawLivePlayerToLivePlayer = (raw: LivePlayerRaw): LivePlayer => ({
  current: raw.c,
  vote: raw.v ?? undefined,
  chance: raw.h ?? undefined,
  realGame: raw.r ?? undefined,
  finalValue: raw.f ?? undefined,
  currentPosition: raw.p ?? undefined
})

export const mapRawLiveTeamToLiveTeam = (raw: LiveTeamRaw): LiveTeam => ({
  name: raw.n,
  owner: raw.o,
  additionalOwners: raw.a ?? [],
  players: (raw.p ?? []).map(mapRawLivePlayerToLivePlayer),
  lastUpdate: raw.d ?? undefined
})

export const mapRawGameWrapperToGameWrapper = (raw: GameWrapperRaw): GameWrapper => ({
  teams: (raw.t ?? []).map(mapRawLiveTeamToLiveTeam),
  game: raw.g ?? undefined,
  serieADay: raw.s ?? undefined
})

export default GameWrapper
