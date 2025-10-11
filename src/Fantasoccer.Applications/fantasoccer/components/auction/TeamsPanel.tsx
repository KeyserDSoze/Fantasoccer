import { Card, Text, XStack, YStack } from 'tamagui'
import { Users } from '@tamagui/lucide-icons'
import { Entity } from 'types/default'
import { Team, TeamKey, TeamHelper } from 'types/team'
import { Role } from 'types/group'
import { useMedia } from 'tamagui'
import { getTeamTier, TeamTier } from 'utils/teamTiers'

export interface TeamsPanelProps {
  teams: Entity<Team, TeamKey>[]
  user: { email: string } | null
  startingMoney: number
  calculateBudgetRemaining: (team: Entity<Team, TeamKey>) => number
  onTeamClick: (team: Entity<Team, TeamKey>) => void
}

const ROLE_LIMITS = {
  [Role.GoalKeeper]: 3,
  [Role.Defensor]: 8,
  [Role.Midfielder]: 8,
  [Role.Forward]: 6,
}

export function TeamsPanel({ teams, user, startingMoney, calculateBudgetRemaining, onTeamClick }: TeamsPanelProps) {
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
                </YStack>
              </Card>
            )
          })}
        </XStack>
      </YStack>
    </Card>
  )
}
