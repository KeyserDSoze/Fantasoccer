// Types matching the C# Group structure
import { convertUTCToItalianTimezone } from '../utils/timezoneHelpers'

// Import IdentityRole from existing identity types
export { IdentityRole } from './identity'

// ============================================
// ENUMS (matching C# enums)
// ============================================

export enum Role {
  Undefined = -1,
  GoalKeeper = 0,
  Defensor = 1,
  Midfielder = 2,
  Forward = 3
}

export const RoleExtensions = {
  asString: (role: Role): string => {
    switch (role) {
      case Role.GoalKeeper:
        return "Portiere"
      case Role.Defensor:
        return "Difensore"
      case Role.Midfielder:
        return "Centrocampista"
      case Role.Forward:
        return "Attaccante"
      default:
        return "Non definito"
    }
  }
}

export enum LeagueType {
  Null = 0,
  League = 1,
  Cup = 2,
  NewCup = 3,
  SuperLeague = 4
}

export const LeagueTypeExtensions = {
  all: Object.values(LeagueType).filter(v => typeof v === 'number') as LeagueType[]
}

export enum MarketType {
  // Add values based on your C# MarketType enum
  // For now, adding a placeholder
  Default = 0
}

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

export interface VoteLeagueSettingRaw {
  g: number    // Goal
  p: number    // Penalty
  s: number    // SufferedGoal
  d: number    // StoppedPenalty
  w: number    // WrongedPenalty
  o: number    // OwnGoal
  a: number    // Assist
  y: number    // YellowCard
  r: number    // RedCard
  j: number    // Injury
  m: number    // ManOfTheMatch
}

export interface LeagueSettingRaw {
  v: Record<number, VoteLeagueSettingRaw>  // Votes (Role -> VoteLeagueSetting)
  s: number    // StartingMoney
  d: number    // DelayedDay
  c: number    // CancelledDay
  g: number    // PointForFirstGoal
  t: number    // PointForNextGoal
  o: number    // PointForOwnGoal
  f: number    // DifferencePointForOwnGoal
  p: number    // PointInHome
  a: number    // PointForVictory
  b: number    // PointForDefeat
  h: number    // PointForDraw
  "3": number  // PointForStrongDefense
  "4": number  // PointForStrongDefense4
  "5": number  // PointForStrongDefense5
  gp: number   // PointForGoodPeople
  l: number    // PointForCleanSheet
  m: number    // MoneyForGoal
  n: number    // MoneyForSufferedGoal
  q: boolean   // RandomAuction
  vp: boolean  // RankWithValuePoints
  mk: MarketType // Market
}

export interface AnnualLeagueRaw {
  y: number           // Year
  t: LeagueType       // Type
  s: LeagueSettingRaw // Settings
}

export interface LeagueRaw {
  i: string              // Id
  n: string              // Name
  m: boolean             // IsMain
  t: LeagueType          // Type
  y: AnnualLeagueRaw[]   // Years
  b: string[]            // BasketsId
}

export interface AnnualTeamRaw {
  n: string    // Name
  o: string    // Owner
  a: string[]  // AdditionalOwners
}

export interface YearlyBasketRaw {
  y: number           // Year
  t: AnnualTeamRaw[]  // Teams
}

export interface BasketRaw {
  i: string              // Id
  n: string              // Name
  y: YearlyBasketRaw[]   // Years
}

export interface UserOfAGroupRaw {
  u: string       // Username
  e: string       // Email
  r: number       // Role (IdentityRole as number)
}

export interface GroupRaw {
  i: string              // Id
  n: string              // Name
  l: LeagueRaw[]         // Leagues
  u: UserOfAGroupRaw[]   // Users
  b: BasketRaw[]         // Baskets
}

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

export interface VoteLeagueSetting {
  goal: number
  penalty: number
  sufferedGoal: number
  stoppedPenalty: number
  wrongedPenalty: number
  ownGoal: number
  assist: number
  yellowCard: number
  redCard: number
  injury: number
  manOfTheMatch: number
}

export interface LeagueSetting {
  votes: Partial<Record<Role, VoteLeagueSetting>>
  startingMoney: number
  delayedDay: number
  cancelledDay: number
  pointForFirstGoal: number
  pointForNextGoal: number
  pointForOwnGoal: number
  differencePointForOwnGoal: number
  pointInHome: number
  pointForVictory: number
  pointForDefeat: number
  pointForDraw: number
  pointForStrongDefense: number
  pointForStrongDefense4: number
  pointForStrongDefense5: number
  pointForGoodPeople: number
  pointForCleanSheet: number
  moneyForGoal: number
  moneyForSufferedGoal: number
  randomAuction: boolean
  rankWithValuePoints: boolean
  market: MarketType
}

export interface AnnualLeague {
  year: number
  type: LeagueType
  settings: LeagueSetting
}

export interface League {
  id: string
  name: string
  isMain: boolean
  type: LeagueType
  years: AnnualLeague[]
  basketsId: string[]
}

export interface AnnualTeam {
  name: string
  owner: string
  additionalOwners: string[]
}

export interface YearlyBasket {
  year: number
  teams: AnnualTeam[]
}

export interface Basket {
  id: string
  name: string
  years: YearlyBasket[]
}

export interface UserOfAGroup {
  username: string
  email: string
  role: number // IdentityRole
}

export interface Group {
  id: string
  name: string
  leagues: League[]
  users: UserOfAGroup[]
  baskets: Basket[]
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

export function mapRawVoteLeagueSettingToVoteLeagueSetting(raw: VoteLeagueSettingRaw): VoteLeagueSetting {
  return {
    goal: raw.g,
    penalty: raw.p,
    sufferedGoal: raw.s,
    stoppedPenalty: raw.d,
    wrongedPenalty: raw.w,
    ownGoal: raw.o,
    assist: raw.a,
    yellowCard: raw.y,
    redCard: raw.r,
    injury: raw.j,
    manOfTheMatch: raw.m
  }
}

export function mapRawLeagueSettingToLeagueSetting(raw: LeagueSettingRaw): LeagueSetting {
  const votes: Partial<Record<Role, VoteLeagueSetting>> = {}
  
  // Map the votes dictionary
  for (const [roleKey, voteRaw] of Object.entries(raw.v)) {
    const role = parseInt(roleKey) as Role
    votes[role] = mapRawVoteLeagueSettingToVoteLeagueSetting(voteRaw)
  }

  return {
    votes,
    startingMoney: raw.s,
    delayedDay: raw.d,
    cancelledDay: raw.c,
    pointForFirstGoal: raw.g,
    pointForNextGoal: raw.t,
    pointForOwnGoal: raw.o,
    differencePointForOwnGoal: raw.f,
    pointInHome: raw.p,
    pointForVictory: raw.a,
    pointForDefeat: raw.b,
    pointForDraw: raw.h,
    pointForStrongDefense: raw["3"],
    pointForStrongDefense4: raw["4"],
    pointForStrongDefense5: raw["5"],
    pointForGoodPeople: raw.gp,
    pointForCleanSheet: raw.l,
    moneyForGoal: raw.m,
    moneyForSufferedGoal: raw.n,
    randomAuction: raw.q,
    rankWithValuePoints: raw.vp,
    market: raw.mk
  }
}

export function mapRawAnnualLeagueToAnnualLeague(raw: AnnualLeagueRaw): AnnualLeague {
  return {
    year: raw.y,
    type: raw.t,
    settings: mapRawLeagueSettingToLeagueSetting(raw.s)
  }
}

export function mapRawLeagueToLeague(raw: LeagueRaw): League {
  return {
    id: raw.i,
    name: raw.n,
    isMain: raw.m,
    type: raw.t,
    years: raw.y.map(mapRawAnnualLeagueToAnnualLeague),
    basketsId: raw.b
  }
}

export function mapRawAnnualTeamToAnnualTeam(raw: AnnualTeamRaw): AnnualTeam {
  return {
    name: raw.n,
    owner: raw.o,
    additionalOwners: raw.a
  }
}

export function mapRawYearlyBasketToYearlyBasket(raw: YearlyBasketRaw): YearlyBasket {
  return {
    year: raw.y,
    teams: raw.t.map(mapRawAnnualTeamToAnnualTeam)
  }
}

export function mapRawBasketToBasket(raw: BasketRaw): Basket {
  return {
    id: raw.i,
    name: raw.n,
    years: raw.y.map(mapRawYearlyBasketToYearlyBasket)
  }
}

export function mapRawUserOfAGroupToUserOfAGroup(raw: UserOfAGroupRaw): UserOfAGroup {
  return {
    username: raw.u,
    email: raw.e,
    role: raw.r
  }
}

export function mapRawGroupToGroup(raw: GroupRaw): Group {
  return {
    id: raw.i,
    name: raw.n,
    leagues: raw.l.map(mapRawLeagueToLeague),
    users: raw.u.map(mapRawUserOfAGroupToUserOfAGroup),
    baskets: raw.b.map(mapRawBasketToBasket)
  }
}

// ============================================
// HELPER FUNCTIONS (matching C# computed properties)
// ============================================

export class GroupHelper {
  // AnnualTeam helper methods
  static isOwner(team: AnnualTeam, email: string): boolean {
    return email != null && (team.owner === email || team.additionalOwners?.includes(email) === true)
  }

  // League helper methods
  static getAnnualType(league: League, year: number): LeagueType {
    const annualLeague = league.years?.find(x => x.year === year)
    if (annualLeague != null && annualLeague.type !== LeagueType.Null) {
      return annualLeague.type
    }
    return league.type
  }

  // Group helper methods
  static getActiveUsers(group: Group): UserOfAGroup[] {
    return group.users.filter(x => x.role !== 0) // IdentityRole.None = 0
  }

  static getRemovedUsers(group: Group): UserOfAGroup[] {
    return group.users.filter(x => x.role === 0) // IdentityRole.None = 0
  }

  static getAdminUsers(group: Group): UserOfAGroup[] {
    // Assuming IdentityRole.Admin has a specific bit flag value
    return group.users.filter(x => (x.role & 2) !== 0) // Adjust based on your IdentityRole enum
  }

  static getSuperAdminUsers(group: Group): UserOfAGroup[] {
    // Assuming IdentityRole.SuperAdmin has a specific bit flag value
    return group.users.filter(x => (x.role & 4) !== 0) // Adjust based on your IdentityRole enum
  }

  static getReaderUsers(group: Group): UserOfAGroup[] {
    // Assuming IdentityRole.Reader has a specific bit flag value
    return group.users.filter(x => (x.role & 8) !== 0) // Adjust based on your IdentityRole enum
  }

  static getParticipants(group: Group): UserOfAGroup[] {
    // Assuming IdentityRole.Participant has a specific bit flag value
    return group.users.filter(x => (x.role & 1) !== 0) // Adjust based on your IdentityRole enum
  }

  static getBasketId(group: Group, owner: string, year?: number): string | null {
    for (const basket of group.baskets) {
      if (basket.years != null) {
        for (const yearly of basket.years) {
          if (year == null || yearly.year === year) {
            if (yearly.teams.some(x => x.owner === owner)) {
              return basket.id
            }
          }
        }
      }
    }
    return null
  }

  static getAnnualLeague(group: Group, id: string, year: number): AnnualLeague | null {
    const league = group.leagues?.find(x => x.id === id)
    return league?.years?.find(x => x.year === year) || null
  }

  static getAnnualLeagues(group: Group, year: number): AnnualLeague[] {
    return group.leagues?.flatMap(x => x.years)?.filter(x => x.year === year) || []
  }
}

// Default LeagueSetting (matching C# Default property)
export const DefaultLeagueSetting: LeagueSetting = {
  startingMoney: 1000,
  votes: {
    [Role.Undefined]: {
      assist: 1,
      goal: 3,
      ownGoal: -3,
      penalty: 3,
      stoppedPenalty: 3,
      redCard: -1,
      yellowCard: -0.5,
      sufferedGoal: -1,
      wrongedPenalty: -3,
      injury: 0,
      manOfTheMatch: 2
    }
  },
  delayedDay: 2,
  cancelledDay: 1,
  pointForFirstGoal: 66,
  pointForNextGoal: 6,
  pointForOwnGoal: 6,
  differencePointForOwnGoal: 6,
  pointInHome: 0,
  pointForVictory: 3,
  pointForDefeat: 0,
  pointForDraw: 1,
  pointForStrongDefense: 2,
  pointForStrongDefense4: 4,
  pointForStrongDefense5: 6,
  moneyForGoal: 5,
  moneyForSufferedGoal: 3,
  pointForCleanSheet: 1,
  pointForGoodPeople: 2,
  randomAuction: false,
  rankWithValuePoints: false,
  market: MarketType.Default
}

// Enhanced Group interface with computed properties
export interface EnhancedGroup extends Group {
  activeUsers: UserOfAGroup[]
  removedUsers: UserOfAGroup[]
  adminUsers: UserOfAGroup[]
  superAdminUsers: UserOfAGroup[]
  readerUsers: UserOfAGroup[]
  participants: UserOfAGroup[]
}

// Function to enhance a Group with computed properties
export const enhanceGroup = (group: Group): EnhancedGroup => ({
  ...group,
  activeUsers: GroupHelper.getActiveUsers(group),
  removedUsers: GroupHelper.getRemovedUsers(group),
  adminUsers: GroupHelper.getAdminUsers(group),
  superAdminUsers: GroupHelper.getSuperAdminUsers(group),
  readerUsers: GroupHelper.getReaderUsers(group),
  participants: GroupHelper.getParticipants(group)
})