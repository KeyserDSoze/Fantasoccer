// Types for Fantasoccer played games repository
import {
  GameResult,
  GameResultType,
  GameResultHelper,
  mapRawGameResultToGameResult,
  GameResultRaw
} from './calendar'
import {
  Player,
  PlayerRaw,
  mapRawPlayerToPlayer,
  FantaSoccerRole,
  PlayerHelper
} from './team'
import { Role } from './group'

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

export interface GameKeyRaw {
  g: string
  l: string
  y: number
  d: number
  i: string
}

export interface PlayedGameRaw {
  i: string
  h: string
  a: string
  r: GameResultRaw | null
  '1': PlayerRaw[]
  '2': PlayerRaw[]
}

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

export interface GameKey {
  group: string
  league: string
  year: number
  day: number
  gameId: string
}

export interface PlayedGame {
  id: string
  home: string
  away: string
  result: GameResult | null
  homePlayers: Player[]
  awayPlayers: Player[]
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

export const mapRawGameKeyToGameKey = (raw: GameKeyRaw): GameKey => ({
  group: raw.g,
  league: raw.l,
  year: raw.y,
  day: raw.d,
  gameId: raw.i
})

export const mapGameKeyToRawGameKey = (key: GameKey): GameKeyRaw => ({
  g: key.group,
  l: key.league,
  y: key.year,
  d: key.day,
  i: key.gameId
})

export const mapRawPlayedGameToPlayedGame = (raw: PlayedGameRaw): PlayedGame => ({
  id: raw.i,
  home: raw.h,
  away: raw.a,
  result: raw.r ? mapRawGameResultToGameResult(raw.r) : null,
  homePlayers: raw['1']?.map(mapRawPlayerToPlayer) ?? [],
  awayPlayers: raw['2']?.map(mapRawPlayerToPlayer) ?? []
})

// ============================================
// HELPER FUNCTIONS
// ============================================

export class GameKeyHelper {
  static create(group: string, league: string, year: number, day: number, gameId: string): GameKey {
    return { group, league, year, day, gameId }
  }

  static toString(key: GameKey): string {
    return `${key.group}-${key.league}-${key.year}-${key.day}-${key.gameId}`
  }
}

export interface PlayedGameScore {
  homeGoals: number
  awayGoals: number
  resultType: GameResultType
}

export type PlayedGameWinner = 'home' | 'away' | 'tie'

export interface EnhancedPlayedGame extends PlayedGame {
  hasResult: boolean
  score: PlayedGameScore
  winner: PlayedGameWinner
}

export class PlayedGameHelper {
  static hasResult(game: PlayedGame): boolean {
    return GameResultHelper.hasValue(game.result)
  }

  static getScore(game: PlayedGame): PlayedGameScore {
    if (game.result) {
      return {
        homeGoals: game.result.homeGoals,
        awayGoals: game.result.awayGoals,
        resultType: GameResultHelper.getResultType(game.result)
      }
    }

    return {
      homeGoals: 0,
      awayGoals: 0,
      resultType: GameResultType.Tie
    }
  }

  static getPlayersByRole(game: PlayedGame, role: Role, team: 'home' | 'away'): Player[] {
    const players = team === 'home' ? game.homePlayers : game.awayPlayers
    return PlayerHelper.filterByMainRole(players, role)
  }

  static getMVP(game: PlayedGame): Player | null {
    const allPlayers = [...game.homePlayers, ...game.awayPlayers]
    const sortedByPrice = PlayerHelper.sortByPriceDesc(allPlayers)
    return sortedByPrice[0] ?? null
  }

  static getPlayersByPosition(game: PlayedGame, position: FantaSoccerRole): Player[] {
    return [...game.homePlayers, ...game.awayPlayers].filter(player => player.position === position)
  }

  static getWinner(game: PlayedGame): PlayedGameWinner {
    if (!game.result) {
      return 'tie'
    }

    const resultType = GameResultHelper.getResultType(game.result)
    if (resultType === GameResultType.HomeWon) return 'home'
    if (resultType === GameResultType.AwayWon) return 'away'
    return 'tie'
  }
}

export const enhancePlayedGame = (game: PlayedGame): EnhancedPlayedGame => {
  const score = PlayedGameHelper.getScore(game)
  return {
    ...game,
    hasResult: PlayedGameHelper.hasResult(game),
    score,
    winner: PlayedGameHelper.getWinner(game)
  }
}
