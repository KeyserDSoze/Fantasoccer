import { IdentityRole } from './identity'
export { IdentityRole } from './identity'

export enum Role { Undefined = -1, GoalKeeper = 0, Defensor = 1, Midfielder = 2, Forward = 3 }
export const RoleExtensions = { asString: (role: Role) => ({ [-1]: 'Non definito', 0: 'Portiere', 1: 'Difensore', 2: 'Centrocampista', 3: 'Attaccante' })[role] }
export enum LeagueType { Null = 0, League = 1, Cup = 2, NewCup = 3, SuperLeague = 4, FutsalLeague = 5 }
export const LeagueTypeExtensions = { all: Object.values(LeagueType).filter(value => typeof value === 'number') as LeagueType[] }
export enum MarketType { WithVote = 0, WithoutVote = 1, Denied = 2 }
export enum FormationType { Normal = 0, Best = 1 }
export enum LeagueCalendarType { Random = 0, Cup = 1 }
export enum RoundType { Rank = 0, Elimination = 1 }
export interface VoteLeagueSetting { goal: number; penalty: number; sufferedGoal: number; stoppedPenalty: number; wrongedPenalty: number; ownGoal: number; assist: number; yellowCard: number; redCard: number; injury: number; manOfTheMatch: number }
export interface LeagueRound { name: string | null; type: RoundType; fromStart: boolean; fromRankingStartTeam: number | null; fromRankingEndTeam: number | null }
export interface LeagueTypeNumberSettings { maxPlayersInTeam: number; maxGoalKeepersInTeam: number; maxDefendersInTeam: number; maxMidfieldersInTeam: number; maxForwardsInTeam: number; maxGoalKeepersInBench: number; maxDefendersInBench: number; maxMidfieldersInBench: number; maxForwardsInBench: number }
export interface FromPreviousYearSettings { leaguesId: string[]; maxTeamsPerLeague: number; roundType: RoundType }
export interface CardTrainerSettings { maxCardsPerType: Record<number, number> }
export interface LeagueTypeSettings { calendarType: LeagueCalendarType; rounds: LeagueRound[]; numbers: LeagueTypeNumberSettings; fromPreviousYear: FromPreviousYearSettings | null; cardTrainer: CardTrainerSettings }
export interface OpeningCompetitionPrize { position: number; credits: number }
export interface OpeningCompetitionSettings { enabled: boolean; serieADays: number; prizes: OpeningCompetitionPrize[] }
export interface PostponementSettings { waitForRecovery: boolean; goalkeeper: number; defender: number; midfielder: number; forward: number }
export interface LeagueSetting {
  votes: Partial<Record<Role, VoteLeagueSetting>>; formation: FormationType; typeSettings: LeagueTypeSettings | null
  startingMoney: number; delayedDay: number; cancelledDay: number; pointForFirstGoal: number; pointForNextGoal: number
  pointForOwnGoal: number; differencePointForOwnGoal: number; pointInHome: number; pointForVictory: number; pointForDefeat: number
  pointForDraw: number; pointForStrongDefense: number; pointForStrongDefense4: number; pointForStrongDefense5: number
  pointForGoodPeople: number; pointForCleanSheet: number; moneyForGoal: number; moneyForSufferedGoal: number
  randomAuction: boolean; rankWithValuePoints: boolean; market: MarketType; liveFormationChanges: number
  allowLiveModuleChange: boolean; openingCompetition: OpeningCompetitionSettings; postponements: PostponementSettings
}
export interface LeagueSource { sourceLeagueId: string; qualifiedTeams: number; stage?: string | null }
export interface AnnualLeague { year: number; type: LeagueType; settings: LeagueSetting; sources?: LeagueSource[] | null; participantTeamIds?: string[] | null }
// mode: 0 autonomous, 1 derived (participants qualified from source leagues).
export interface League { id: string; name: string; isMain: boolean; type: LeagueType; years: AnnualLeague[]; basketsId: string[]; mode?: number }
// originTeamId is set for a derived team: roster and owners come from that team.
export interface AnnualTeam { teamId?: string; logoUrl?: string | null; name: string; owner: string; additionalOwners: string[]; originTeamId?: string | null }
export interface YearlyBasket { year: number; teams: AnnualTeam[] }
// kind: 0 autonomous, 1 derived from originBasketId.
export interface Basket { id: string; name: string; years: YearlyBasket[]; kind?: number; originBasketId?: string | null }
export interface UserOfAGroup { username: string; email: string; role: IdentityRole }
export interface Group { id: string; name: string; revision: number; leagues: League[]; users: UserOfAGroup[]; baskets: Basket[] }

export class GroupHelper {
  static isOwner(team: AnnualTeam, email: string) { return !!email && [team.owner, ...(team.additionalOwners ?? [])].some(value => value.toLowerCase() === email.toLowerCase()) }
  static getAnnualType(league: League, year: number) { return league.years.find(value => value.year === year && value.type !== LeagueType.Null)?.type ?? league.type }
  static getActiveUsers(group: Group) { return group.users.filter(user => user.role !== IdentityRole.None) }
  static getRemovedUsers(group: Group) { return group.users.filter(user => user.role === IdentityRole.None) }
  static getAdminUsers(group: Group) { return group.users.filter(user => (user.role & IdentityRole.Admin) !== 0) }
  static getSuperAdminUsers(group: Group) { return group.users.filter(user => (user.role & IdentityRole.SuperAdmin) !== 0) }
  static getReaderUsers(group: Group) { return group.users.filter(user => (user.role & IdentityRole.Reader) !== 0) }
  static getParticipants(group: Group) { return group.users.filter(user => (user.role & IdentityRole.Participant) !== 0) }
  static getBasketId(group: Group, owner: string, year?: number) { return group.baskets.find(basket => basket.years.some(season => (year == null || season.year === year) && season.teams.some(team => team.owner.toLowerCase() === owner.toLowerCase())))?.id ?? null }
  static getAnnualLeague(group: Group, id: string, year: number) { return group.leagues.find(league => league.id === id)?.years.find(value => value.year === year) ?? null }
  static getAnnualLeagues(group: Group, year: number) { return group.leagues.flatMap(league => league.years).filter(value => value.year === year) }
}
export const DefaultVoteLeagueSetting: VoteLeagueSetting = { assist: 1, goal: 3, ownGoal: -3, penalty: 3, stoppedPenalty: 3, redCard: -1, yellowCard: -0.5, sufferedGoal: -1, wrongedPenalty: -3, injury: 0, manOfTheMatch: 2 }
export const DefaultLeagueSetting: LeagueSetting = {
  startingMoney: 1000, formation: FormationType.Normal, typeSettings: null, votes: { [-1]: DefaultVoteLeagueSetting },
  delayedDay: 2, cancelledDay: 1, pointForFirstGoal: 66, pointForNextGoal: 6, pointForOwnGoal: 6, differencePointForOwnGoal: 6,
  pointInHome: 0, pointForVictory: 3, pointForDefeat: 0, pointForDraw: 1, pointForStrongDefense: 2, pointForStrongDefense4: 4,
  pointForStrongDefense5: 6, moneyForGoal: 5, moneyForSufferedGoal: 3, pointForCleanSheet: 1, pointForGoodPeople: 2,
  randomAuction: false, rankWithValuePoints: false, market: MarketType.WithVote, liveFormationChanges: 0, allowLiveModuleChange: false,
  openingCompetition: { enabled: false, serieADays: 2, prizes: [{ position: 1, credits: 50 }, { position: 2, credits: 30 }, { position: 3, credits: 20 }] },
  postponements: { waitForRecovery: false, goalkeeper: 6, defender: 6, midfielder: 6, forward: 6 },
}
export interface EnhancedGroup extends Group { activeUsers: UserOfAGroup[]; removedUsers: UserOfAGroup[]; adminUsers: UserOfAGroup[]; superAdminUsers: UserOfAGroup[]; readerUsers: UserOfAGroup[]; participants: UserOfAGroup[] }
export const enhanceGroup = (group: Group): EnhancedGroup => ({ ...group, activeUsers: GroupHelper.getActiveUsers(group), removedUsers: GroupHelper.getRemovedUsers(group), adminUsers: GroupHelper.getAdminUsers(group), superAdminUsers: GroupHelper.getSuperAdminUsers(group), readerUsers: GroupHelper.getReaderUsers(group), participants: GroupHelper.getParticipants(group) })
