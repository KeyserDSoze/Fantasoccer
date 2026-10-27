export interface RankedTeam {
  teamId?: string; basketId?: string; name: string; owner: string; point: number; victories: number; draws: number; defeats: number
  goal: number; sufferedGoal: number; valuePoint: number; sufferedValuePoint: number; plusMoney: number; money: number | null; valueAssets: number | null
}
export interface Rank { serieADay: number; rounds: Record<string, RankedTeam[]>; snapshotDay?: number; isReconstructed?: boolean }
export interface LeagueKey { group: string; league: string; year: number }
export interface YearDayLeague extends LeagueKey { day: number }
export class RankHelper {
  static getRankedTeamByOwner(rank: Rank, stage: string, owner: string) { return rank.rounds[stage]?.find(team => team.owner.toLowerCase() === owner.toLowerCase()) ?? null }
  static getAvailableRounds(rank: Rank) { return Object.keys(rank.rounds) }
  static getTeamCount(rank: Rank, stage: string) { return rank.rounds[stage]?.length ?? 0 }
  static getTeamsSortedByPoints(rank: Rank, stage: string) { return [...(rank.rounds[stage] ?? [])].sort((a, b) => b.point - a.point) }
  static getTeamsSortedByValueAssets(rank: Rank, stage: string) { return [...(rank.rounds[stage] ?? [])].sort((a, b) => (b.valueAssets ?? -Infinity) - (a.valueAssets ?? -Infinity)) }
  static getTeamPosition(rank: Rank, stage: string, owner: string) { const index = this.getTeamsSortedByPoints(rank, stage).findIndex(team => team.owner.toLowerCase() === owner.toLowerCase()); return index < 0 ? -1 : index + 1 }
  static getGoalDifference(team: RankedTeam) { return team.goal - team.sufferedGoal }
  static getTotalGamesPlayed(team: RankedTeam) { return team.victories + team.draws + team.defeats }
  static getPointsPerGame(team: RankedTeam) { const games = this.getTotalGamesPlayed(team); return games > 0 ? team.point / games : 0 }
  static addRankedTeams(a: RankedTeam, b: RankedTeam): RankedTeam { const money = a.money === null || b.money === null ? null : a.money + b.money; const plusMoney = a.plusMoney + b.plusMoney; return { name: a.name, owner: a.owner, point: a.point + b.point, defeats: a.defeats + b.defeats,
    goal: a.goal + b.goal, draws: a.draws + b.draws, money, plusMoney,
    sufferedGoal: a.sufferedGoal + b.sufferedGoal, sufferedValuePoint: a.sufferedValuePoint + b.sufferedValuePoint,
    valuePoint: a.valuePoint + b.valuePoint, victories: a.victories + b.victories, valueAssets: money === null ? null : money + plusMoney } }
  static createLeagueKey(group: string, league: string, year: number): LeagueKey { return { group, league, year } }
  static createYearDayLeague(group: string, league: string, year: number, day: number): YearDayLeague { return { group, league, year, day } }
  static leagueKeyToString(key: LeagueKey) { return JSON.stringify([key.group, key.league, key.year]) }
  static yearDayLeagueToString(key: YearDayLeague) { return JSON.stringify([key.group, key.league, key.year, key.day]) }
}
export interface EnhancedRank extends Rank { availableRounds: string[]; totalTeamsCount: number; roundTeamCounts: Record<string, number> }
export interface EnhancedRankedTeam extends RankedTeam { goalDifference: number; totalGamesPlayed: number; pointsPerGame: number; position?: number }
export const enhanceRank = (rank: Rank): EnhancedRank => ({ ...rank, availableRounds: Object.keys(rank.rounds),
  totalTeamsCount: Object.values(rank.rounds).reduce((sum, teams) => sum + teams.length, 0),
  roundTeamCounts: Object.fromEntries(Object.entries(rank.rounds).map(([stage, teams]) => [stage, teams.length])) })
export const enhanceRankedTeam = (team: RankedTeam, position?: number): EnhancedRankedTeam => ({ ...team, goalDifference: RankHelper.getGoalDifference(team),
  totalGamesPlayed: RankHelper.getTotalGamesPlayed(team), pointsPerGame: RankHelper.getPointsPerGame(team), position })
export const enhanceRankWithTeamPositions = (rank: Rank): Record<string, EnhancedRankedTeam[]> => Object.fromEntries(Object.keys(rank.rounds)
  .map(stage => [stage, RankHelper.getTeamsSortedByPoints(rank, stage).map((team, index) => enhanceRankedTeam(team, index + 1))]))
export const DefaultRankedTeam: RankedTeam = { name: '', owner: '', point: 0, victories: 0, draws: 0, defeats: 0, goal: 0, sufferedGoal: 0,
  valuePoint: 0, sufferedValuePoint: 0, plusMoney: 0, money: 0, valueAssets: 0 }
export const DefaultRank: Rank = { serieADay: 0, rounds: {} }
export type LuckEventType = 'win-vs-worst' | 'win-vs-second-worst' | 'win-narrow' | 'draw-wide-gap' | 'loss-narrow' | 'loss-as-best' | 'loss-as-top3' | 'win-as-worst'
export interface LuckEvent { type: LuckEventType; points: number; gameId: string; gameDay: number; opponent: string; myScore: number; opponentScore: number; result: 'win' | 'draw' | 'loss'; detail: string }
export interface TeamLuck { owner: string; name: string; totalLuck: number; gamesPlayed: number; avgLuck: number; events: LuckEvent[] }
export interface EnhancedRankedTeamWithLuck extends EnhancedRankedTeam { luck?: TeamLuck }
