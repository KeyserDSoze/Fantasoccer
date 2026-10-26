import type { Player } from './team'
import type { CalendarGame } from './calendar'
import { formatSeasonFromYear } from '../utils/season'

export interface GeneralLeagueKey { groupId: string; leagueId: string }
export interface RankedTeam {
  ownerUserId: string; name: string; owner: string; points: number; victories: number; draws: number; defeats: number
  goals: number; sufferedGoals: number; valuePoints: number; sufferedValuePoints: number; plusMoney: number; money: number; valueAssets: number
}
// Wins are indexed by the original stage/round label, not another league's ID.
export interface WinningTeam { ownerUserId: string; owner: string; teamName: string | null; wins: Record<string, number[]> }
export interface WinningPlayer { player: Player; wins: Record<string, number[]> }
export interface PlayerWithMostPointsInYear { player: Player; year: number; points: number }
export interface RecordPlayer { player: Player; points: number }
export interface RecordGame { game: CalendarGame; year: number; isLinkedToCalendar: boolean }
export interface HallOfFame {
  recordGame: RecordGame | null; recordPlayer: RecordPlayer | null; playerWithMostPointsInYear: PlayerWithMostPointsInYear | null
  winningPlayers: WinningPlayer[]; winningTeams: WinningTeam[]; allTimeRankings: RankedTeam[]; version: number; baselineConfirmed: boolean
}
export class HallOfFameHelper {
  static getTotalWins(team: WinningTeam) { return Object.values(team.wins).reduce((sum, years) => sum + years.length, 0) }
  static getTotalPlayerWins(player: WinningPlayer) { return Object.values(player.wins).reduce((sum, years) => sum + years.length, 0) }
  static getWinsInStage(team: WinningTeam, stage: string) { return team.wins[stage] ?? [] }
  static getPlayerWinsInStage(player: WinningPlayer, stage: string) { return player.wins[stage] ?? [] }
  static sortByPoints(teams: RankedTeam[]) { return [...teams].sort((a, b) => b.points - a.points) }
  static sortByValueAssets(teams: RankedTeam[]) { return [...teams].sort((a, b) => b.valueAssets - a.valueAssets) }
  static getGoalDifference(team: RankedTeam) { return team.goals - team.sufferedGoals }
  static getValuePointsDifference(team: RankedTeam) { return team.valuePoints - team.sufferedValuePoints }
  static getTotalGamesPlayed(team: RankedTeam) { return team.victories + team.draws + team.defeats }
  static getWinPercentage(team: RankedTeam) { const games = this.getTotalGamesPlayed(team); return games > 0 ? team.victories / games * 100 : 0 }
  static sortWinningTeamsByWins(teams: WinningTeam[]) { return [...teams].sort((a, b) => this.getTotalWins(b) - this.getTotalWins(a)) }
  static sortWinningPlayersByWins(players: WinningPlayer[]) { return [...players].sort((a, b) => this.getTotalPlayerWins(b) - this.getTotalPlayerWins(a)) }
  static getYearsRange(wins: Record<string, number[]>) { const years = Object.values(wins).flat(); return years.length ? { min: Math.min(...years), max: Math.max(...years) } : null }
  static formatSeason(year: number) { return formatSeasonFromYear(year) }
  static getAllYears(wins: Record<string, number[]>) { return [...new Set(Object.values(wins).flat())].sort((a, b) => a - b) }
}
