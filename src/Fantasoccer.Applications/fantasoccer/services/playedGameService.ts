// Played game service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import {
  PlayedGame,
  PlayedGameRaw,
  GameKey,
  GameKeyRaw,
  mapGameKeyToRawGameKey,
  mapRawPlayedGameToPlayedGame,
  EnhancedPlayedGame,
  enhancePlayedGame,
  PlayedGameScore,
  PlayedGameHelper,
  GameKeyHelper
} from 'types/game'
import { Role } from 'types/group'
import { Player } from 'types/team'

export class PlayedGameService {
  private getRepository() {
    return RepositoryServices.Repository<PlayedGameRaw, GameKeyRaw>('PlayedGame')
  }

  private async fetchRawGame(key: GameKey): Promise<PlayedGameRaw | null> {
    try {
      const repository = this.getRepository()
      const rawKey = mapGameKeyToRawGameKey(key)
      return await repository.get(rawKey)
    } catch (error) {
      console.error('Error fetching played game:', error)
      return null
    }
  }

  async getPlayedGame(key: GameKey): Promise<PlayedGame | null> {
    const rawGame = await this.fetchRawGame(key)
    return rawGame ? mapRawPlayedGameToPlayedGame(rawGame) : null
  }

  async getPlayedGameByComponents(
    group: string,
    league: string,
    year: number,
    day: number,
    gameId: string
  ): Promise<PlayedGame | null> {
    const key = GameKeyHelper.create(group, league, year, day, gameId)
    return this.getPlayedGame(key)
  }

  async getEnhancedPlayedGame(key: GameKey): Promise<EnhancedPlayedGame | null> {
    const game = await this.getPlayedGame(key)
    return game ? enhancePlayedGame(game) : null
  }

  async getEnhancedPlayedGameByComponents(
    group: string,
    league: string,
    year: number,
    day: number,
    gameId: string
  ): Promise<EnhancedPlayedGame | null> {
    const key = GameKeyHelper.create(group, league, year, day, gameId)
    return this.getEnhancedPlayedGame(key)
  }

  async getHomePlayers(key: GameKey): Promise<Player[]> {
    const game = await this.getPlayedGame(key)
    return game ? game.homePlayers : []
  }

  async getAwayPlayers(key: GameKey): Promise<Player[]> {
    const game = await this.getPlayedGame(key)
    return game ? game.awayPlayers : []
  }

  async getPlayersByRole(key: GameKey, role: Role, team: 'home' | 'away' | 'both' = 'both'): Promise<Player[]> {
    const game = await this.getPlayedGame(key)
    if (!game) return []

    if (team === 'home') {
      return PlayedGameHelper.getPlayersByRole(game, role, 'home')
    }
    if (team === 'away') {
      return PlayedGameHelper.getPlayersByRole(game, role, 'away')
    }

    const home = PlayedGameHelper.getPlayersByRole(game, role, 'home')
    const away = PlayedGameHelper.getPlayersByRole(game, role, 'away')
    return [...home, ...away]
  }

  async getGameScore(key: GameKey): Promise<PlayedGameScore | null> {
    const game = await this.getPlayedGame(key)
    return game ? PlayedGameHelper.getScore(game) : null
  }

  async getMVP(key: GameKey): Promise<Player | null> {
    const game = await this.getPlayedGame(key)
    return game ? PlayedGameHelper.getMVP(game) : null
  }
}

export const playedGameService = new PlayedGameService()
