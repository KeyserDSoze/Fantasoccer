import { GameResultHelper, type CalendarDay, type CalendarGame } from './calendar'
import { enhanceRank, type Rank, type EnhancedRank } from './rank'

export interface LiveLeagueRound { key: string; day: CalendarDay }
export interface LiveLeague { id: string; name: string; rounds: Record<string, CalendarDay>; rank: Rank | null; previewUnavailable?: boolean }
// isStale: the shared projection is being rebuilt by another instance.
// liveVotesStale: the matchday is being played and the provisional votes were not confirmed by the live job in time;
// liveVotesUpdatedAtUtc: when they were last confirmed (null if the job never published them).
export interface LiveGroup { name: string; year?: number; leagues: LiveLeague[]; serieADay?: number | null; generatedAtUtc?: string | null; isStale?: boolean;
  liveVotesUpdatedAtUtc?: string | null; liveVotesStale?: boolean }
export interface EnhancedLiveLeague extends LiveLeague { roundKeys: string[]; roundsList: LiveLeagueRound[]; latestRoundKey: string | null; pendingGames: CalendarGame[]; enhancedRank: EnhancedRank | null }
export interface EnhancedLiveGroup extends LiveGroup { leaguesWithRounds: EnhancedLiveLeague[]; totalPendingGames: number }
export class LiveLeagueHelper {
  static getRoundKeys(league: LiveLeague) { return Object.keys(league.rounds).sort((a, b) => a.localeCompare(b, undefined, { numeric: true })) }
  static getRoundsList(league: LiveLeague) { return this.getRoundKeys(league).map(key => ({ key, day: league.rounds[key] })).filter(round => round.day != null) }
  static getRound(league: LiveLeague, key: string) { return league.rounds[key] ?? null }
  static getLatestRoundKey(league: LiveLeague) { return this.getRoundKeys(league).at(-1) ?? null }
  static getPendingGames(league: LiveLeague) { return Object.values(league.rounds).flatMap(day => day.games ?? []).filter(game => !GameResultHelper.hasValue(game.result)) }
  static enhance(league: LiveLeague): EnhancedLiveLeague { return { ...league, roundKeys: this.getRoundKeys(league), roundsList: this.getRoundsList(league),
    latestRoundKey: this.getLatestRoundKey(league), pendingGames: this.getPendingGames(league), enhancedRank: league.rank ? enhanceRank(league.rank) : null } }
}
export class LiveGroupHelper {
  // Null when the provisional votes are up to date (or no matchday is being played).
  static getVotesDelayNotice(group: Pick<LiveGroup, 'liveVotesStale' | 'liveVotesUpdatedAtUtc'> | null | undefined, locale = 'it-IT'): string | null {
    if (!group?.liveVotesStale) return null
    const updatedAt = group.liveVotesUpdatedAtUtc ? new Date(group.liveVotesUpdatedAtUtc) : null
    if (!updatedAt || Number.isNaN(updatedAt.getTime())) return 'Voti live non ancora disponibili: i punteggi mostrati non li includono.'
    return `Voti live non aggiornati dalle ${updatedAt.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })}: stai vedendo l'ultimo dato disponibile.`
  }
  static getLeagueById(group: LiveGroup, id: string) { return group.leagues.find(league => league.id === id) ?? null }
  static getLeaguesWithRounds(group: LiveGroup) { return group.leagues.filter(league => Object.keys(league.rounds).length > 0) }
  static enhance(group: LiveGroup): EnhancedLiveGroup { const leaguesWithRounds = group.leagues.map(league => LiveLeagueHelper.enhance(league));
    return { ...group, leaguesWithRounds, totalPendingGames: leaguesWithRounds.reduce((sum, league) => sum + league.pendingGames.length, 0) } }
}
export const enhanceLiveLeague = (league: LiveLeague) => LiveLeagueHelper.enhance(league)
export const enhanceLiveGroup = (group: LiveGroup) => LiveGroupHelper.enhance(group)
