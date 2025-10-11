import { Card, Text, XStack, YStack } from 'tamagui'
import { Users } from '@tamagui/lucide-icons'
import { Entity } from 'types/default'
import { Team, TeamKey, TeamHelper } from 'types/team'
import { Role } from 'types/group'
import { useMedia } from 'tamagui'
import { getTeamTier, TeamTier } from 'utils/teamTiers'
import type { ChancedRealPlayer } from 'types/chance'
import { getPlayerKey } from 'utils'

export interface TeamsPanelProps {
  teams: Entity<Team, TeamKey>[]
  user: { email: string } | null
  startingMoney: number
  calculateBudgetRemaining: (team: Entity<Team, TeamKey>) => number
  onTeamClick: (team: Entity<Team, TeamKey>) => void
  chancedPlayersMap?: Map<string, ChancedRealPlayer>
  showTeamDetails?: boolean
}

const ROLE_LIMITS = {
  [Role.GoalKeeper]: 3,
  [Role.Defensor]: 8,
  [Role.Midfielder]: 8,
  [Role.Forward]: 6,
}

export function TeamsPanel({ teams, user, startingMoney, calculateBudgetRemaining, onTeamClick, chancedPlayersMap, showTeamDetails = true }: TeamsPanelProps) {
  const media = useMedia()
  
  if (teams.length === 0) return null
  
  // Responsive grid: 5 teams per row on desktop, 2 on mobile
  const maxCardWidth = media.gtMd ? 240 : 180
  
  // Sort teams by budget (descending - from highest to lowest)
  const sortedTeams = [...teams].sort((a, b) => {
    const budgetA = calculateBudgetRemaining(a)
    const budgetB = calculateBudgetRemaining(b)
    return budgetB - budgetA
  })
  
  // Calculate starters statistics per role (based on Fantagazzetta)
  const startersStats = {
    [Role.GoalKeeper]: { starters: 0, total: 0 },
    [Role.Defensor]: { starters: 0, total: 0 },
    [Role.Midfielder]: { starters: 0, total: 0 },
    [Role.Forward]: { starters: 0, total: 0 },
  }
  
  // Calculate real team distribution
  const realTeamDistribution = new Map<string, number>()
  
  // Iterate through all teams and their active players
  teams.forEach(team => {
    const activePlayers = TeamHelper.getActivePlayers(team.value)
    activePlayers.forEach(player => {
      // Count starters per role
      if (chancedPlayersMap) {
        const playerKey = getPlayerKey(player.name)
        const chancedPlayer = chancedPlayersMap.get(playerKey)
        
        if (player.role !== Role.Undefined) {
          startersStats[player.role].total++
          if (chancedPlayer?.chance?.fantagazzetta) {
            startersStats[player.role].starters++
          }
        }
      }
      
      // Count players per real team
      const teamName = player.team.name
      realTeamDistribution.set(teamName, (realTeamDistribution.get(teamName) || 0) + 1)
    })
  })
  
  // Sort real teams by player count (descending)
  const sortedRealTeams = Array.from(realTeamDistribution.entries())
    .sort((a, b) => b[1] - a[1])
  
  return (
    <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
      <YStack gap="$4">
        <XStack gap="$2" alignItems="center">
          <Users size={20} color="$color11" />
          <Text fontSize="$5" fontWeight="700" color="$color11">
            Squadre Partecipanti ({teams.length})
          </Text>
        </XStack>
        
        <XStack gap="$3" flexWrap="wrap" justifyContent="flex-start">
          {sortedTeams.map((team) => {
            const isMyTeam = user?.email && team?.value?.owner?.toLowerCase() === user.email.toLowerCase()
            const budget = calculateBudgetRemaining(team)
            const hasLowBudget = budget < 100
            
            // Count only active players (status === 0)
            const activePlayers = TeamHelper.getActivePlayers(team.value)
            const roleCount = {
              [Role.GoalKeeper]: activePlayers.filter(p => p.role === Role.GoalKeeper).length,
              [Role.Defensor]: activePlayers.filter(p => p.role === Role.Defensor).length,
              [Role.Midfielder]: activePlayers.filter(p => p.role === Role.Midfielder).length,
              [Role.Forward]: activePlayers.filter(p => p.role === Role.Forward).length,
            }
            
            // Count players by tier
            const tierCount = {
              [TeamTier.T1]: 0,
              [TeamTier.T2]: 0,
              [TeamTier.T3]: 0,
              [TeamTier.T4]: 0,
            }
            activePlayers.forEach(player => {
              const tier = getTeamTier(player.team.name)
              tierCount[tier]++
            })
            
            // Count starters per role for this team (based on Fantagazzetta)
            const teamStartersStats = {
              [Role.GoalKeeper]: { starters: 0, total: 0 },
              [Role.Defensor]: { starters: 0, total: 0 },
              [Role.Midfielder]: { starters: 0, total: 0 },
              [Role.Forward]: { starters: 0, total: 0 },
            }
            
            // Count players per real team for this team
            const teamRealTeamDistribution = new Map<string, number>()
            
            activePlayers.forEach(player => {
              // Count starters
              if (chancedPlayersMap && player.role !== Role.Undefined) {
                const playerKey = getPlayerKey(player.name)
                const chancedPlayer = chancedPlayersMap.get(playerKey)
                teamStartersStats[player.role].total++
                if (chancedPlayer?.chance?.fantagazzetta) {
                  teamStartersStats[player.role].starters++
                }
              }
              
              // Count real teams
              const teamName = player.team.name
              teamRealTeamDistribution.set(teamName, (teamRealTeamDistribution.get(teamName) || 0) + 1)
            })
            
            // Sort real teams by count
            const teamSortedRealTeams = Array.from(teamRealTeamDistribution.entries())
              .sort((a, b) => b[1] - a[1])
            
            return (
              <Card 
                key={team.value.owner}
                flex={media.gtMd ? 1 : 0}
                flexBasis={media.gtMd ? undefined : "47.5%"}
                minWidth={media.gtMd ? 180 : undefined}
                maxWidth={media.gtMd ? maxCardWidth : "47.5%"}
                padding="$3" 
                backgroundColor={isMyTeam ? '$blue2' : '$color1'} 
                borderRadius="$3" 
                borderWidth="$0.5" 
                borderColor={isMyTeam ? '$blue8' : '$borderColor'}
                pressStyle={{ scale: 0.98, opacity: 0.9 }}
                cursor="pointer"
                onPress={() => onTeamClick(team)}
              >
                <YStack gap="$2">
                  <XStack gap="$2" alignItems="center" justifyContent="space-between">
                    <Text fontSize="$4" fontWeight="700" color="$color11" numberOfLines={1} flex={1}>
                      {team.value.name}
                    </Text>
                    {isMyTeam && (
                      <Card padding="$1" paddingHorizontal="$2" backgroundColor="$blue8" borderRadius="$2">
                        <Text fontSize="$1" color="$blue1" fontWeight="700">TUA</Text>
                      </Card>
                    )}
                  </XStack>
                  
                  <XStack gap="$2" flexWrap="wrap">
                    <YStack gap="$0.5" flex={1}>
                      <Text fontSize="$1" color="$color10">Budget</Text>
                      <Text fontSize="$3" fontWeight="600" color={hasLowBudget ? '$red11' : '$color11'}>
                        €{budget}
                      </Text>
                    </YStack>
                    <YStack gap="$0.5" flex={1}>
                      <Text fontSize="$1" color="$color10">Gioc.</Text>
                      <Text fontSize="$3" fontWeight="600" color="$color11">
                        {activePlayers.length}/25
                      </Text>
                    </YStack>
                  </XStack>
                  
                  {/* Budget Breakdown */}
                  {showTeamDetails && (
                    <Card padding="$2" backgroundColor="$color3" borderRadius="$2">
                      <YStack gap="$1">
                        <Text fontSize="$1" color="$color10" fontWeight="600">Dettaglio Budget:</Text>
                        <XStack gap="$2" flexWrap="wrap" justifyContent="space-between">
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Budget Iniziale</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">+€{startingMoney}</Text>
                          </YStack>
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$red10">Costo Totale</Text>
                            <Text fontSize="$2" fontWeight="600" color="$red11">
                              -€{TeamHelper.getTotalCostForPlayers(team.value)}
                            </Text>
                          </YStack>
                        </XStack>
                        <XStack gap="$2" flexWrap="wrap" justifyContent="space-between">
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Ricavi Vendite</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">
                              +€{TeamHelper.getRevenueMoney(team.value)}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Da Classifica</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">
                              +€{team.value.moneyFromRank}
                            </Text>
                          </YStack>
                        </XStack>
                        <XStack gap="$2" flexWrap="wrap" justifyContent="space-between">
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Infortunati/Ceduti</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">
                              +€{TeamHelper.getMoneyFromSoldWithOneHalfReturnedPrice(team.value)}
                            </Text>
                          </YStack>
                        </XStack>
                      </YStack>
                    </Card>
                  )}
                  
                  {/* Role counts */}
                  <Card padding="$2" backgroundColor="$color2" borderRadius="$2">
                    <XStack gap="$2" justifyContent="space-between">
                      <YStack gap="$0.5" alignItems="center">
                        <Text fontSize="$1" color="$color10">P</Text>
                        <Text 
                          fontSize="$2" 
                          fontWeight="600" 
                          color={roleCount[Role.GoalKeeper] >= ROLE_LIMITS[Role.GoalKeeper] ? '$red11' : '$color11'}
                        >
                          {roleCount[Role.GoalKeeper]}/{ROLE_LIMITS[Role.GoalKeeper]}
                        </Text>
                      </YStack>
                      <YStack gap="$0.5" alignItems="center">
                        <Text fontSize="$1" color="$color10">D</Text>
                        <Text 
                          fontSize="$2" 
                          fontWeight="600" 
                          color={roleCount[Role.Defensor] >= ROLE_LIMITS[Role.Defensor] ? '$red11' : '$color11'}
                        >
                          {roleCount[Role.Defensor]}/{ROLE_LIMITS[Role.Defensor]}
                        </Text>
                      </YStack>
                      <YStack gap="$0.5" alignItems="center">
                        <Text fontSize="$1" color="$color10">C</Text>
                        <Text 
                          fontSize="$2" 
                          fontWeight="600" 
                          color={roleCount[Role.Midfielder] >= ROLE_LIMITS[Role.Midfielder] ? '$red11' : '$color11'}
                        >
                          {roleCount[Role.Midfielder]}/{ROLE_LIMITS[Role.Midfielder]}
                        </Text>
                      </YStack>
                      <YStack gap="$0.5" alignItems="center">
                        <Text fontSize="$1" color="$color10">A</Text>
                        <Text 
                          fontSize="$2" 
                          fontWeight="600" 
                          color={roleCount[Role.Forward] >= ROLE_LIMITS[Role.Forward] ? '$red11' : '$color11'}
                        >
                          {roleCount[Role.Forward]}/{ROLE_LIMITS[Role.Forward]}
                        </Text>
                      </YStack>
                    </XStack>
                  </Card>
                  
                  {/* Tier distribution */}
                  {showTeamDetails && (
                    <Card padding="$2" backgroundColor="$color2" borderRadius="$2">
                      <YStack gap="$1">
                        <Text fontSize="$1" color="$color10" fontWeight="600">Tier Squadre:</Text>
                        <XStack gap="$2" justifyContent="space-between">
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$yellow10" fontWeight="700">T1</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {tierCount[TeamTier.T1]}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$blue10" fontWeight="700">T2</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {tierCount[TeamTier.T2]}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$green10" fontWeight="700">T3</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {tierCount[TeamTier.T3]}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$gray10" fontWeight="700">T4</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {tierCount[TeamTier.T4]}
                            </Text>
                          </YStack>
                        </XStack>
                      </YStack>
                    </Card>
                  )}

                  {/* Real Team Distribution Card */}
                  {showTeamDetails && (
                    <Card padding="$2" backgroundColor="$color2" borderRadius="$2">
                      <YStack gap="$1">
                        <Text fontSize="$1" color="$color10" fontWeight="600">Squadre Serie A:</Text>
                        <XStack gap="$1" flexWrap="wrap">
                          {teamSortedRealTeams.map(([teamName, count]) => (
                            <Card 
                              key={teamName} 
                              padding="$1" 
                              paddingHorizontal="$2" 
                              backgroundColor="$blue8" 
                              borderRadius="$2"
                            >
                              <Text fontSize="$1" color="white" fontWeight="600">
                                {teamName} {count}
                              </Text>
                            </Card>
                          ))}
                        </XStack>
                      </YStack>
                    </Card>
                  )}

                  {/* Starters by Role Card */}
                  {showTeamDetails && (
                    <Card padding="$2" backgroundColor="$color2" borderRadius="$2">
                      <YStack gap="$1">
                        <Text fontSize="$1" color="$color10" fontWeight="600">Titolari (Fantagazzetta):</Text>
                        <XStack gap="$2" justifyContent="space-around">
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$color10">P</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {teamStartersStats[Role.GoalKeeper].starters}/{teamStartersStats[Role.GoalKeeper].total}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$color10">D</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {teamStartersStats[Role.Defensor].starters}/{teamStartersStats[Role.Defensor].total}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$color10">C</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {teamStartersStats[Role.Midfielder].starters}/{teamStartersStats[Role.Midfielder].total}
                            </Text>
                          </YStack>
                          <YStack gap="$0.5" alignItems="center">
                            <Text fontSize="$1" color="$color10">A</Text>
                            <Text fontSize="$2" fontWeight="600" color="$color11">
                              {teamStartersStats[Role.Forward].starters}/{teamStartersStats[Role.Forward].total}
                            </Text>
                          </YStack>
                        </XStack>
                      </YStack>
                    </Card>
                  )}
                </YStack>
              </Card>
            )
          })}
        </XStack>

        {/* Starters Statistics per Role */}
        {chancedPlayersMap && chancedPlayersMap.size > 0 && (
          <Card padding="$3" backgroundColor="$blue2" borderRadius="$3" borderWidth="$0.5" borderColor="$blue8">
            <YStack gap="$2">
              <Text fontSize="$4" fontWeight="700" color="$blue11">
                📊 Titolari
              </Text>
              <XStack gap="$3" justifyContent="space-around" flexWrap="wrap">
                <YStack gap="$1" alignItems="center" minWidth={80}>
                  <Text fontSize="$2" color="$blue10">Portieri</Text>
                  <Text fontSize="$5" fontWeight="700" color="$blue11">
                    {startersStats[Role.GoalKeeper].starters}/{startersStats[Role.GoalKeeper].total}
                  </Text>
                  {startersStats[Role.GoalKeeper].total > 0 && (
                    <Text fontSize="$1" color="$blue10">
                      {Math.round((startersStats[Role.GoalKeeper].starters / startersStats[Role.GoalKeeper].total) * 100)}%
                    </Text>
                  )}
                </YStack>
                <YStack gap="$1" alignItems="center" minWidth={80}>
                  <Text fontSize="$2" color="$blue10">Difensori</Text>
                  <Text fontSize="$5" fontWeight="700" color="$blue11">
                    {startersStats[Role.Defensor].starters}/{startersStats[Role.Defensor].total}
                  </Text>
                  {startersStats[Role.Defensor].total > 0 && (
                    <Text fontSize="$1" color="$blue10">
                      {Math.round((startersStats[Role.Defensor].starters / startersStats[Role.Defensor].total) * 100)}%
                    </Text>
                  )}
                </YStack>
                <YStack gap="$1" alignItems="center" minWidth={80}>
                  <Text fontSize="$2" color="$blue10">Centrocampisti</Text>
                  <Text fontSize="$5" fontWeight="700" color="$blue11">
                    {startersStats[Role.Midfielder].starters}/{startersStats[Role.Midfielder].total}
                  </Text>
                  {startersStats[Role.Midfielder].total > 0 && (
                    <Text fontSize="$1" color="$blue10">
                      {Math.round((startersStats[Role.Midfielder].starters / startersStats[Role.Midfielder].total) * 100)}%
                    </Text>
                  )}
                </YStack>
                <YStack gap="$1" alignItems="center" minWidth={80}>
                  <Text fontSize="$2" color="$blue10">Attaccanti</Text>
                  <Text fontSize="$5" fontWeight="700" color="$blue11">
                    {startersStats[Role.Forward].starters}/{startersStats[Role.Forward].total}
                  </Text>
                  {startersStats[Role.Forward].total > 0 && (
                    <Text fontSize="$1" color="$blue10">
                      {Math.round((startersStats[Role.Forward].starters / startersStats[Role.Forward].total) * 100)}%
                    </Text>
                  )}
                </YStack>
              </XStack>
            </YStack>
          </Card>
        )}
        
        {/* Real Team Distribution */}
        {sortedRealTeams.length > 0 && (
          <Card padding="$3" backgroundColor="$green2" borderRadius="$3" borderWidth="$0.5" borderColor="$green8">
            <YStack gap="$2">
              <Text fontSize="$4" fontWeight="700" color="$green11">
                ⚽ Distribuzione Squadre Serie A ({sortedRealTeams.length} squadre)
              </Text>
              <XStack gap="$2" flexWrap="wrap">
                {sortedRealTeams.map(([teamName, count]) => (
                  <Card 
                    key={teamName}
                    padding="$2" 
                    paddingHorizontal="$3" 
                    backgroundColor="$green1" 
                    borderRadius="$3"
                    borderWidth={1}
                    borderColor="$green7"
                  >
                    <XStack gap="$2" alignItems="center">
                      <Text fontSize="$3" fontWeight="600" color="$green11">
                        {teamName}
                      </Text>
                      <Card 
                        padding="$1" 
                        paddingHorizontal="$2" 
                        backgroundColor="$green8" 
                        borderRadius="$2"
                      >
                        <Text fontSize="$2" fontWeight="700" color="white">
                          {count}
                        </Text>
                      </Card>
                    </XStack>
                  </Card>
                ))}
              </XStack>
            </YStack>
          </Card>
        )}

      </YStack>
    </Card>
  )
}
