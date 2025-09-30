// Types and mapping helpers for the Game API (GameWrapper / LiveTeam / LivePlayer)
// Use the existing raw interfaces and mappers from other modules so we produce consistent cleaned types.
import { CalendarGame, CalendarGameRaw, mapRawCalendarGameToCalendarGame } from 'types/calendar'
import { FantaSoccerRole, Player, PlayerRaw, mapRawPlayerToPlayer } from 'types/team'
import { RealGame, RealGameRaw, Vote, VoteRaw, mapRawGameToGame, mapRawVoteToVote } from 'types/serieA'
import { Chance, ChanceRaw, mapRawChanceToChance } from 'types/chance'

// RAW interfaces matching JSON names produced by the C# backend
export interface LivePlayerRaw {
  // raw shapes coming from the server
  c: PlayerRaw
  v?: VoteRaw | null
  h?: ChanceRaw | null
  r?: RealGameRaw | null
  f?: number | null
  p: FantaSoccerRole
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
  g?: CalendarGameRaw | null
  s?: number
}

// CLEANED interfaces used in the client
export interface LivePlayer {
  current: Player
  vote?: Vote | null
  chance?: Chance | null
  realGame?: RealGame | null
  finalValue?: number | null
  currentPosition: FantaSoccerRole
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
  current: mapRawPlayerToPlayer(raw.c),
  vote: raw.v ? mapRawVoteToVote(raw.v) : undefined,
  chance: raw.h ? mapRawChanceToChance(raw.h) : undefined,
  realGame: raw.r ? mapRawGameToGame(raw.r) : undefined,
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
  game: raw.g ? mapRawCalendarGameToCalendarGame(raw.g) : undefined,
  serieADay: raw.s ?? undefined
})

export default GameWrapper
