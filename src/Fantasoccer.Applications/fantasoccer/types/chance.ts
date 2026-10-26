import type { RealPlayer } from './serieA'
import { Role } from './group'
import type { StatPlayerGame } from './stat'

export enum ChanceType { Normal = 0, Injury = 1, Warned = 2, Disqualified = 3, Maybe = 4 }
export enum TrendType { Bad = 0, NotBad = 1, Normal = 2, VeryGood = 3, Excellent = 4 }
export type ChanceSource = 'fantagazzetta' | 'gazzetta' | 'mediaset' | 'sky'
export interface Chance { fantagazzetta: boolean; gazzetta: boolean; mediaset: boolean; sky: boolean; status: ChanceType; description: string | null; lastGame?: StatPlayerGame | null; trend?: TrendType | null }
export interface EnhancedChance extends Chance { sources: ChanceSource[]; isInjured: boolean; isWarned: boolean; isDisqualified: boolean; isDoubtful: boolean; isAvailable: boolean; availabilityLabel: string }
export interface ChancedRealPlayer extends RealPlayer { chance: EnhancedChance }
export interface ChancedRealPlayerWrapper { players: ChancedRealPlayer[] }
export type ChancedRealPlayerRoleBuckets = Partial<Record<Role, ChancedRealPlayer[]>>
export interface EnhancedChancedRealPlayerWrapper extends ChancedRealPlayerWrapper {
  playersByRole: ChancedRealPlayerRoleBuckets; injuredPlayers: ChancedRealPlayer[]; doubtfulPlayers: ChancedRealPlayer[]
  warnedPlayers: ChancedRealPlayer[]; disqualifiedPlayers: ChancedRealPlayer[]; availablePlayers: ChancedRealPlayer[]
}
export class ChanceHelper {
  static defaultChance(): Chance { return { fantagazzetta: false, gazzetta: false, mediaset: false, sky: false, status: ChanceType.Normal, description: null } }
  static getAvailabilityLabel(status: ChanceType) { return ({ 0: 'Disponibile', 1: 'Infortunato', 2: 'Diffidato', 3: 'Squalificato', 4: 'In dubbio' })[status] ?? 'Disponibile' }
}
export const enhanceChance = (chance: Chance): EnhancedChance => ({ ...chance,
  sources: (['fantagazzetta', 'gazzetta', 'mediaset', 'sky'] as const).filter(source => chance[source]),
  isInjured: chance.status === ChanceType.Injury, isWarned: chance.status === ChanceType.Warned,
  isDisqualified: chance.status === ChanceType.Disqualified, isDoubtful: chance.status === ChanceType.Maybe,
  isAvailable: chance.status !== ChanceType.Injury && chance.status !== ChanceType.Disqualified,
  availabilityLabel: ChanceHelper.getAvailabilityLabel(chance.status) })
export class ChancedRealPlayerHelper {
  static sortByRoleAndTrend(players: ChancedRealPlayer[]) { const roles = [Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward, Role.Undefined]; return [...players].sort((a, b) => roles.indexOf(a.role) - roles.indexOf(b.role) || a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' })) }
  static groupByRole(players: ChancedRealPlayer[]) { return players.reduce<ChancedRealPlayerRoleBuckets>((groups, player) => { (groups[player.role] ??= []).push(player); return groups }, {}) }
  static filterInjured(players: ChancedRealPlayer[]) { return players.filter(player => player.chance.isInjured) }
  static filterDoubtful(players: ChancedRealPlayer[]) { return players.filter(player => player.chance.isDoubtful) }
  static filterWarned(players: ChancedRealPlayer[]) { return players.filter(player => player.chance.isWarned) }
  static filterDisqualified(players: ChancedRealPlayer[]) { return players.filter(player => player.chance.isDisqualified) }
  static filterAvailable(players: ChancedRealPlayer[]) { return players.filter(player => player.chance.isAvailable) }
}
export const enhanceChancedRealPlayersWrapper = (wrapper: ChancedRealPlayerWrapper): EnhancedChancedRealPlayerWrapper => ({ ...wrapper,
  playersByRole: ChancedRealPlayerHelper.groupByRole(wrapper.players), injuredPlayers: ChancedRealPlayerHelper.filterInjured(wrapper.players),
  doubtfulPlayers: ChancedRealPlayerHelper.filterDoubtful(wrapper.players), warnedPlayers: ChancedRealPlayerHelper.filterWarned(wrapper.players),
  disqualifiedPlayers: ChancedRealPlayerHelper.filterDisqualified(wrapper.players), availablePlayers: ChancedRealPlayerHelper.filterAvailable(wrapper.players) })
