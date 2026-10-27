import { getPlayerKey } from '../utils/keyUtils'
import { Role } from './group'

export enum Behaviour { Nothing = 0, YellowCard = 1, RedCard = 2 }
export interface RealTeam { clubId?: string; name: string; abbreviation: string }
export interface RealGame { ordinal?: number; home: RealTeam; away: RealTeam; date: string | Date | null; homeGoals: number | null; awayGoals: number | null; delayed: boolean }
export interface RealDay { year: number; serieADay: number; games: RealGame[] }
export interface RealCalendar { year: number; days: RealDay[] }
export interface RealRankedTeam { clubId?: string; name: string; points: number; victories: number; draws: number; defeats: number; goalsFor: number; goalsAgainst: number; goalDifference: number; played: number }
export interface RealRank { teams: RealRankedTeam[] }
export interface RealPlayer { playerId?: string; seasonIndex?: number | null; playerKey?: string | null; name: string; key: string; realTeam: RealTeam; role: Role; roleLabel: string; isActive: boolean; isVisible: boolean }
export interface RealPlayersWrapper { players: RealPlayer[] }
export type RealPlayerRoleBuckets = Partial<Record<Role, RealPlayer[]>>
export interface EnhancedRealPlayersWrapper extends RealPlayersWrapper { activePlayers: RealPlayer[]; inactivePlayers: RealPlayer[]; playersByRole: RealPlayerRoleBuckets; hasVisiblePlayers: boolean }
export interface Vote {
  role: Role; value: number; isFinal: boolean; goals: number; penalties: number; assists: number; stoppedPenalties: number; sufferedGoals: number
  wrongedPenalties: number; ownGoals: number; status: Behaviour; manOfTheMatch: boolean; hasVote: boolean; isOut: boolean; isIn: boolean
  injured: boolean; hasPositiveBonus: boolean; hasNegativeBonus: boolean; hasDoneSomething: boolean
}
export interface VotedRealPlayer extends Omit<RealPlayer, 'realTeam'> { realTeam: RealTeam | null; vote: Vote }
export interface VotedRealPlayerWrapper { players: VotedRealPlayer[] }
export type VotedRealPlayerRoleBuckets = Partial<Record<Role, VotedRealPlayer[]>>
export interface EnhancedVotedRealPlayerWrapper extends VotedRealPlayerWrapper {
  playersByRole: VotedRealPlayerRoleBuckets; playersWithVote: VotedRealPlayer[]; playersWithoutVote: VotedRealPlayer[]; totalPlayers: number; votedPlayersCount: number
}
export interface YearDayKey { year: number; day: number }
const ROLE_ORDER = [Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward, Role.Undefined]
export class RealPlayerHelper {
  static buildKey(name: string) { return getPlayerKey(name) }
  static sortByRoleAndName<T extends Pick<RealPlayer, 'name' | 'role'>>(players: T[]): T[] { return [...players].sort((a, b) => ROLE_ORDER.indexOf(a.role) - ROLE_ORDER.indexOf(b.role) || a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' })) }
  static groupByRole<T extends Pick<RealPlayer, 'role'>>(players: T[]): Partial<Record<Role, T[]>> { return players.reduce<Partial<Record<Role, T[]>>>((groups, player) => { (groups[player.role] ??= []).push(player); return groups }, {}) }
  static filterByRole<T extends RealPlayer>(players: T[], role: Role) { return players.filter(player => player.role === role) }
  static filterActive<T extends RealPlayer>(players: T[]) { return players.filter(player => player.isActive) }
  static filterVisible<T extends RealPlayer>(players: T[]) { return players.filter(player => player.isVisible) }
  static search<T extends RealPlayer>(players: T[], term: string) { const key = this.buildKey(term); return term ? players.filter(player => player.name.toLowerCase().includes(term.toLowerCase()) || player.key.includes(key)) : players }
}
export const enhanceRealPlayersWrapper = (wrapper: RealPlayersWrapper): EnhancedRealPlayersWrapper => ({ ...wrapper,
  activePlayers: RealPlayerHelper.sortByRoleAndName(wrapper.players.filter(player => player.isActive)),
  inactivePlayers: RealPlayerHelper.sortByRoleAndName(wrapper.players.filter(player => !player.isActive)),
  playersByRole: RealPlayerHelper.groupByRole(RealPlayerHelper.sortByRoleAndName(wrapper.players)),
  hasVisiblePlayers: wrapper.players.some(player => player.isActive && player.isVisible) })
export class VotedRealPlayerHelper {
  static sortByRoleAndName(players: VotedRealPlayer[]) { return RealPlayerHelper.sortByRoleAndName(players) }
  static groupByRole(players: VotedRealPlayer[]) { return RealPlayerHelper.groupByRole(players) }
  static filterWithVote(players: VotedRealPlayer[]) { return players.filter(player => player.vote.hasVote) }
  static filterWithoutVote(players: VotedRealPlayer[]) { return players.filter(player => !player.vote.hasVote) }
}
export const enhanceVotedRealPlayersWrapper = (wrapper: VotedRealPlayerWrapper): EnhancedVotedRealPlayerWrapper => ({ ...wrapper,
  playersByRole: VotedRealPlayerHelper.groupByRole(VotedRealPlayerHelper.sortByRoleAndName(wrapper.players)),
  playersWithVote: VotedRealPlayerHelper.filterWithVote(wrapper.players), playersWithoutVote: VotedRealPlayerHelper.filterWithoutVote(wrapper.players),
  totalPlayers: wrapper.players.length, votedPlayersCount: wrapper.players.filter(player => player.vote.hasVote).length })

export class RealCalendarHelper {
  private static readonly GAME_DURATION = 135 * 60 * 1000
  private static readonly LIVE_DURATION = (8 * 60 + 135) * 60 * 1000
  static getCurrentItalyTime() { return new Date() }
  static isGamePlayed(game: RealGame) { return !!game.date && !game.delayed && Date.now() > new Date(game.date).getTime() }
  private static times(day: RealDay) { return day.games.filter(game => !game.delayed && !!game.date).map(game => new Date(game.date!).getTime()) }
  static getLiveDay(calendar: RealCalendar): RealDay | null {
    const now = Date.now()
    return [...calendar.days].sort((a, b) => b.serieADay - a.serieADay).find(day => {
      const times = this.times(day); return times.length > 0 && now >= Math.min(...times) && now <= Math.max(...times) + this.LIVE_DURATION
    }) ?? null
  }
  static getLastDay(calendar: RealCalendar): RealDay | null {
    return [...calendar.days].sort((a, b) => b.serieADay - a.serieADay).find(day => {
      const times = this.times(day); return times.length > 0 && Date.now() >= Math.max(...times) + this.GAME_DURATION
    }) ?? null
  }
  static getNextDay(calendar: RealCalendar): RealDay | null {
    const day = (this.getLiveDay(calendar)?.serieADay ?? this.getLastDay(calendar)?.serieADay ?? 0) + 1
    return calendar.days.find(value => value.serieADay === day) ?? null
  }
  static getLiveGames(calendar: RealCalendar): RealGame[] {
    return this.getLiveDay(calendar)?.games.filter(game => !!game.date && !game.delayed && Date.now() >= new Date(game.date).getTime() && Date.now() <= new Date(game.date).getTime() + this.GAME_DURATION) ?? []
  }
  static isLive(calendar: RealCalendar) { return this.getLiveGames(calendar).length > 0 }
  static getLiveSerieADay(calendar: RealCalendar) { return this.getLiveDay(calendar)?.serieADay ?? 0 }
  static isDuringSerieADay(calendar: RealCalendar) { return this.getLiveSerieADay(calendar) > 0 }
}
export interface EnhancedRealCalendar extends RealCalendar { liveDay?: RealDay | null; lastDay?: RealDay | null; nextDay?: RealDay | null; liveGames?: RealGame[]; isLive?: boolean; liveSerieADay?: number; isDuringSerieADay?: boolean }
export const enhanceRealCalendar = (calendar: RealCalendar): EnhancedRealCalendar => ({ ...calendar, liveDay: RealCalendarHelper.getLiveDay(calendar),
  lastDay: RealCalendarHelper.getLastDay(calendar), nextDay: RealCalendarHelper.getNextDay(calendar), liveGames: RealCalendarHelper.getLiveGames(calendar),
  isLive: RealCalendarHelper.isLive(calendar), liveSerieADay: RealCalendarHelper.getLiveSerieADay(calendar), isDuringSerieADay: RealCalendarHelper.isDuringSerieADay(calendar) })
