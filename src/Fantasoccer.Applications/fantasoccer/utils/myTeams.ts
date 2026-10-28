import type { AnnualTeam, Group } from '../types/group'

// A user may own or co-own several teams in one group (one per basket, derived
// copies included). Screens must act on an explicitly selected team, never on
// "the first team found".
export interface MyTeam {
  teamId: string
  basketId: string
  basketName: string
  year: number
  name: string
  owner: string
  additionalOwners: string[]
  logoUrl?: string | null
  isDerived: boolean
  originTeamId?: string | null
}

export function listMyTeams(group: Group | null | undefined, email: string | null | undefined, year: number | null | undefined, options?: { includeDerived?: boolean; teamIds?: string[] | null }): MyTeam[] {
  if (!group || !email || year == null) return []
  const allowed = options?.teamIds ? new Set(options.teamIds) : null
  return group.baskets.flatMap(basket => (basket.years ?? []).filter(season => season.year === year).flatMap(season => season.teams
    .filter(team => !!team.teamId && isOwner(team, email))
    .filter(team => options?.includeDerived !== false || !team.originTeamId)
    .filter(team => !allowed || allowed.has(team.teamId!))
    .map(team => ({ teamId: team.teamId!, basketId: basket.id, basketName: basket.name, year, name: team.name, owner: team.owner,
      additionalOwners: team.additionalOwners ?? [], logoUrl: team.logoUrl, isDerived: !!team.originTeamId, originTeamId: team.originTeamId }))))
    .sort((a, b) => Number(a.isDerived) - Number(b.isDerived) || a.basketName.localeCompare(b.basketName) || a.name.localeCompare(b.name))
}


const isOwner = (team: AnnualTeam, email: string) =>
  [team.owner, ...(team.additionalOwners ?? [])].some(value => value?.toLowerCase() === email.toLowerCase())
