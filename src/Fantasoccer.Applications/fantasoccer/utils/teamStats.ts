// Team Statistics Utilities - Shared calculation logic for team stats
import { Role } from 'types/group'
import { TeamTier, getTeamTier } from './teamTiers'
import { TeamHelper } from 'types/team'
import type { Entity } from 'types/default'
import type { Team, TeamKey } from 'types/team'
import type { ChancedRealPlayer } from 'types/chance'
import { getPlayerKey } from './index'

export interface StartersByRole {
  [Role.GoalKeeper]: { starters: number; total: number }
  [Role.Defensor]: { starters: number; total: number }
  [Role.Midfielder]: { starters: number; total: number }
  [Role.Forward]: { starters: number; total: number }
}

export interface TierCount {
  [TeamTier.T1]: number
  [TeamTier.T2]: number
  [TeamTier.T3]: number
  [TeamTier.T4]: number
}

export interface RoleCount {
  [Role.GoalKeeper]: number
  [Role.Defensor]: number
  [Role.Midfielder]: number
  [Role.Forward]: number
}

/**
 * Calculate tier distribution for a team
 */
export function calculateTierCount(team: Entity<Team, TeamKey>): TierCount {
  const activePlayers = TeamHelper.getActivePlayers(team.value)
  const tierCount: TierCount = {
    [TeamTier.T1]: 0,
    [TeamTier.T2]: 0,
    [TeamTier.T3]: 0,
    [TeamTier.T4]: 0,
  }
  
  activePlayers.forEach(player => {
    const tier = getTeamTier(player.team.name)
    tierCount[tier]++
  })
  
  return tierCount
}

/**
 * Calculate role distribution for a team
 */
export function calculateRoleCount(team: Entity<Team, TeamKey>): RoleCount {
  const activePlayers = TeamHelper.getActivePlayers(team.value)
  return {
    [Role.GoalKeeper]: activePlayers.filter(p => p.role === Role.GoalKeeper).length,
    [Role.Defensor]: activePlayers.filter(p => p.role === Role.Defensor).length,
    [Role.Midfielder]: activePlayers.filter(p => p.role === Role.Midfielder).length,
    [Role.Forward]: activePlayers.filter(p => p.role === Role.Forward).length,
  }
}

/**
 * Calculate starters by role for a team based on Fantagazzetta predictions
 */
export function calculateStartersByRole(
  team: Entity<Team, TeamKey>,
  chancedPlayersMap?: Map<string, ChancedRealPlayer>
): StartersByRole {
  const activePlayers = TeamHelper.getActivePlayers(team.value)
  const startersByRole: StartersByRole = {
    [Role.GoalKeeper]: { starters: 0, total: 0 },
    [Role.Defensor]: { starters: 0, total: 0 },
    [Role.Midfielder]: { starters: 0, total: 0 },
    [Role.Forward]: { starters: 0, total: 0 },
  }
  
  if (!chancedPlayersMap) return startersByRole
  
  activePlayers.forEach(player => {
    if (player.role !== Role.Undefined) {
      const playerKey = getPlayerKey(player.name)
      const chancedPlayer = chancedPlayersMap.get(playerKey)
      startersByRole[player.role].total++
      if (chancedPlayer?.chance?.fantagazzetta) {
        startersByRole[player.role].starters++
      }
    }
  })
  
  return startersByRole
}

/**
 * Calculate real team distribution for a team
 * Returns a Map of team name -> player count, sorted by count descending
 */
export function calculateRealTeamDistribution(
  team: Entity<Team, TeamKey>
): [string, number][] {
  const activePlayers = TeamHelper.getActivePlayers(team.value)
  const teamDistribution = new Map<string, number>()
  
  activePlayers.forEach(player => {
    const teamName = player.team.name
    teamDistribution.set(teamName, (teamDistribution.get(teamName) || 0) + 1)
  })
  
  // Sort by count descending
  return Array.from(teamDistribution.entries()).sort((a, b) => b[1] - a[1])
}

/**
 * Calculate all statistics for a team at once
 */
export interface TeamStatistics {
  tierCount: TierCount
  roleCount: RoleCount
  startersByRole: StartersByRole
  realTeamDistribution: [string, number][]
}

export function calculateTeamStatistics(
  team: Entity<Team, TeamKey>,
  chancedPlayersMap?: Map<string, ChancedRealPlayer>
): TeamStatistics {
  return {
    tierCount: calculateTierCount(team),
    roleCount: calculateRoleCount(team),
    startersByRole: calculateStartersByRole(team, chancedPlayersMap),
    realTeamDistribution: calculateRealTeamDistribution(team),
  }
}
