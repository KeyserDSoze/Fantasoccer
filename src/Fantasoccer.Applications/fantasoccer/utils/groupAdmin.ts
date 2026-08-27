import type {
  AnnualLeague,
  AnnualTeam,
  League,
  LeagueSetting,
  LeagueSettingRaw,
  VoteLeagueSettingRaw
} from '../types/group'

export const cloneLeagueSetting = (setting: LeagueSetting): LeagueSetting =>
  JSON.parse(JSON.stringify(setting)) as LeagueSetting

export const getAnnualLeagueForYear = (
  league: League,
  year: number,
  defaultSettings: LeagueSetting
): AnnualLeague =>
  league.years.find(annualLeague => annualLeague.year === year) || {
    year,
    type: league.type,
    settings: cloneLeagueSetting(defaultSettings)
  }

export const upsertAnnualLeague = (
  years: AnnualLeague[],
  annualLeague: AnnualLeague
): AnnualLeague[] =>
  years.some(year => year.year === annualLeague.year)
    ? years.map(year => year.year === annualLeague.year ? annualLeague : year)
    : [...years, annualLeague]

export const copyAnnualLeagueToYear = (
  source: AnnualLeague,
  targetYear: number,
  fallbackType: League['type']
): AnnualLeague => ({
  year: targetYear,
  type: source.type || fallbackType,
  settings: cloneLeagueSetting(source.settings)
})

export const copyMissingTeams = (
  sourceTeams: AnnualTeam[],
  existingTeams: AnnualTeam[]
): AnnualTeam[] =>
  sourceTeams
    .filter(sourceTeam => !existingTeams.some(team => team.owner === sourceTeam.owner))
    .map(team => ({
      ...team,
      additionalOwners: [...(team.additionalOwners || [])]
    }))

export const preserveRawLeagueSetting = (
  setting: LeagueSetting,
  votes: Record<number, VoteLeagueSettingRaw>
): LeagueSettingRaw => ({
  ...setting.raw,
  v: votes,
  s: setting.startingMoney,
  d: setting.delayedDay,
  c: setting.cancelledDay,
  g: setting.pointForFirstGoal,
  t: setting.pointForNextGoal,
  o: setting.pointForOwnGoal,
  f: setting.differencePointForOwnGoal,
  p: setting.pointInHome,
  a: setting.pointForVictory,
  b: setting.pointForDefeat,
  h: setting.pointForDraw,
  "3": setting.pointForStrongDefense,
  "4": setting.pointForStrongDefense4,
  "5": setting.pointForStrongDefense5,
  gp: setting.pointForGoodPeople,
  l: setting.pointForCleanSheet,
  m: setting.moneyForGoal,
  n: setting.moneyForSufferedGoal,
  q: setting.randomAuction,
  vp: setting.rankWithValuePoints,
  mk: setting.market
})
