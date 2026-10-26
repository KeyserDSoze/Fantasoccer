import { RealPlayerHelper, type RealPlayer } from './serieA'
import type { Role } from './group'

export interface StatPlayerGame { serieADay: number; vote: number | null; positiveness: number }
export interface PlayerDescription { top?: string | null; down?: string | null }
export interface StatPlayer extends RealPlayer {
  description?: PlayerDescription | null
  summatory: number; fantaSummatory: number; withVote: number; withoutVote: number; noPlayed: number; withSpecial: number
  goals: number; penalties: number; assists: number; stoppedPenalties: number; sufferedGoals: number; wrongedPenalties: number
  ownGoals: number; yellowCards: number; redCards: number; enoughVotes: number; manOfTheMatch: number; injured: number
  games: StatPlayerGame[]; average: number; fantaAverage: number
}
export interface StatPlayersWrapper { players: StatPlayer[] }
export type StatPlayerRoleBuckets = Partial<Record<Role, StatPlayer[]>>
export interface EnhancedStatPlayersWrapper extends StatPlayersWrapper { playersByRole: StatPlayerRoleBuckets; topByAverage: StatPlayer[]; topByFantaAverage: StatPlayer[] }
export class StatPlayerHelper {
  static sortByAverage(players: StatPlayer[]) { return [...players].sort((a, b) => b.average - a.average || a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' })) }
  static sortByFantaAverage(players: StatPlayer[]) { return [...players].sort((a, b) => b.fantaAverage - a.fantaAverage || a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' })) }
  static groupByRole(players: StatPlayer[]) { return players.reduce<StatPlayerRoleBuckets>((groups, player) => { (groups[player.role] ??= []).push(player); return groups }, {}) }
  static search(players: StatPlayer[], term: string) {
    if (!term) return players
    const normalized = RealPlayerHelper.buildKey(term)
    return players.filter(player => player.name.toLowerCase().includes(term.toLowerCase()) || player.key.includes(normalized))
  }
  static getTopByAverage(players: StatPlayer[], limit = 5) { return this.sortByAverage(players).slice(0, limit) }
  static getTopByFantaAverage(players: StatPlayer[], limit = 5) { return this.sortByFantaAverage(players).slice(0, limit) }
}
export const enhanceStatPlayersWrapper = (wrapper: StatPlayersWrapper, limit = 5): EnhancedStatPlayersWrapper => ({ ...wrapper,
  playersByRole: StatPlayerHelper.groupByRole(wrapper.players), topByAverage: StatPlayerHelper.getTopByAverage(wrapper.players, limit),
  topByFantaAverage: StatPlayerHelper.getTopByFantaAverage(wrapper.players, limit) })
