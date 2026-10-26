import type { LeagueSetting } from './group'

export enum GameResultType { HomeWon = 0, AwayWon = 1, Tie = 2 }
export interface Point { value: number; defensiveBonus: boolean; goodPeople: boolean; ownGoal: boolean }
export interface GameResult { home: Point; away: Point; isCancelled: boolean; homeGoals: number; awayGoals: number }
export interface EnhancedGameResult extends GameResult { hasValue: boolean; resultType: GameResultType }
export interface CalendarGame { id: string; number: number; home: string; homeOwner: string; away: string; awayOwner: string; result: GameResult | null; homeTeamId?: string | null; awayTeamId?: string | null }
export interface CalendarDay { serieADay: number; number: number; games: CalendarGame[] }
export type CalendarRounds = Record<string, CalendarDay[]>
export interface Calendar { year: number; rounds: CalendarRounds; version?: number }
export interface EnhancedCalendar extends Calendar { roundKeys: string[]; allDays: CalendarDay[]; allGames: CalendarGame[]; pendingGames: CalendarGame[] }

export class GameResultHelper {
  static hasValue(result: GameResult | null | undefined) { return !!result && ((result.home?.value ?? 0) > 0 || (result.away?.value ?? 0) > 0) }
  static getResultType(result: GameResult | null | undefined) {
    return !result || result.homeGoals === result.awayGoals ? GameResultType.Tie : result.homeGoals > result.awayGoals ? GameResultType.HomeWon : GameResultType.AwayWon
  }
  static calculateGoals(result: GameResult, settings: LeagueSetting) {
    const goals = (value: number, opponent: number) => {
      let count = value >= settings.pointForFirstGoal ? 1 + Math.floor((value - settings.pointForFirstGoal) / settings.pointForNextGoal) : 0
      const difference = settings.differencePointForOwnGoal
      if (difference > 0 && value < 66 && opponent <= 65.5 - difference && value - opponent >= difference) count++
      return count
    }
    return { home: goals(result.home.value, result.away.value), away: goals(result.away.value, result.home.value) }
  }
  static enhance(result: GameResult | null | undefined): EnhancedGameResult | null { return result ? { ...result, hasValue: this.hasValue(result), resultType: this.getResultType(result) } : null }
}
export class CalendarHelper {
  static getRound(calendar: Calendar, stage: string) { return calendar.rounds[stage] ?? [] }
  static getAllRoundKeys(calendar: Calendar) { return Object.keys(calendar.rounds).sort() }
  static getAllDays(calendar: Calendar) { return this.getAllRoundKeys(calendar).flatMap(stage => this.getRound(calendar, stage)) }
  static getDayByNumber(calendar: Calendar, number: number) { return this.getAllDays(calendar).find(day => day.number === number) ?? null }
  static getAllGames(calendar: Calendar) { return this.getAllDays(calendar).flatMap(day => day.games ?? []) }
  static getGamesForTeam(calendar: Calendar, name: string) { return this.getAllGames(calendar).filter(game => game.home.toLowerCase() === name.toLowerCase() || game.away.toLowerCase() === name.toLowerCase()) }
  static getPendingGames(calendar: Calendar) { return this.getAllGames(calendar).filter(game => !GameResultHelper.hasValue(game.result)) }
  static enhanceCalendar(calendar: Calendar): EnhancedCalendar { return { ...calendar, roundKeys: this.getAllRoundKeys(calendar), allDays: this.getAllDays(calendar), allGames: this.getAllGames(calendar), pendingGames: this.getPendingGames(calendar) } }
}
export const enhanceCalendar = (calendar: Calendar) => CalendarHelper.enhanceCalendar(calendar)
