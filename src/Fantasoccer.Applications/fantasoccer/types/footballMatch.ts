import type { GameWrapper } from './game'
import type { RealGame } from './serieA'

export type LineupSource = 'official' | 'live' | 'probable' | 'unavailable'

// A real Serie A match: lineup is shaped like a fantasy game (the two "teams" are the clubs) so the same field draws it.
export interface FootballMatch {
  year: number
  serieADay: number
  game: RealGame
  isPlaying: boolean
  homeLineupSource: LineupSource
  awayLineupSource: LineupSource
  lineup: GameWrapper
  generatedAtUtc: string
  fantasyLeagueName?: string | null
}
