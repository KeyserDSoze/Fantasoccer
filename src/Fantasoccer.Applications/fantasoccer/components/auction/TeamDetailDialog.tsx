import { Dialog, Button, Card, ScrollView, Separator, Text, XStack, YStack } from 'tamagui'
import { Entity } from 'types/default'
import { Team, TeamKey, TeamHelper } from 'types/team'
import { Role } from 'types/group'
import { getRoleLabel, getPlayerStatusLabel, getPlayerStatusColor } from 'utils/auctionHelper'

export interface TeamDetailDialogProps {
  open: boolean
  team: Entity<Team, TeamKey> | null
  user: { email: string } | null
  startingMoney: number
  calculateBudgetRemaining: (team: Entity<Team, TeamKey>) => number
  isAuctioneer: boolean
  onOpenChange: (open: boolean) => void
  onModifyPlayerStatus: (playerKey: string, playerName: string, owner: string) => void
  getPlayerKey: (playerName: string) => string
}

export function TeamDetailDialog({
  open,
  team,
  user,
  startingMoney,
  calculateBudgetRemaining,
  isAuctioneer,
  onOpenChange,
  onModifyPlayerStatus,
  getPlayerKey,
}: TeamDetailDialogProps) {
  if (!team) return null
  
  const budget = calculateBudgetRemaining(team)
  const isMyTeam = user?.email && team?.value?.owner?.toLowerCase() === user.email.toLowerCase()
  
  // Count only active players
  const activePlayers = TeamHelper.getActivePlayers(team.value)
  
  // Sort players: orderBy status, thenBy role
  const sortedPlayers = [...team.value.players].sort((a, b) => {
    if (a.status !== b.status) return (a.status || 0) - (b.status || 0)
    return (a.role || 0) - (b.role || 0)
  })

  return (
    <Dialog modal open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay 
          key="overlay" 
          transition="quick"
          opacity={0.5} 
          enterStyle={{ opacity: 0 }} 
          exitStyle={{ opacity: 0 }} 
        />
        <Dialog.Content 
          bordered 
          elevation="$2"
          key="content" 
          animateOnly={['transform', 'opacity']} 
          transition={['quick', { opacity: { overshootClamping: true } }]}
          enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} 
          exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} 
          gap="$4" 
          width={700} 
          maxWidth="95%" 
          maxHeight="90%"
        >
          <Dialog.Title fontSize="$6" fontWeight="700">{team.value.name}</Dialog.Title>
          <Dialog.Description>Dettaglio squadra - {team.value.owner}</Dialog.Description>
          
          <ScrollView maxHeight={600} showsHorizontalScrollIndicator={false}>
            <YStack gap="$4" padding="$2">
              <Card padding="$3" backgroundColor={isMyTeam ? '$blue2' : '$color2'} borderRadius="$3">
                <YStack gap="$3">
                  <XStack gap="$4" flexWrap="wrap">
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Budget Rimasto</Text>
                      <Text fontSize="$5" fontWeight="700" color={budget < 100 ? '$red11' : '$color11'}>
                        €{budget}
                      </Text>
                    </YStack>
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Giocatori</Text>
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        {activePlayers.length}/25
                      </Text>
                    </YStack>
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Slot Rimasti</Text>
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        {25 - activePlayers.length}
                      </Text>
                    </YStack>
                  </XStack>
                  
                  <Card padding="$3" backgroundColor={isMyTeam ? '$blue1' : '$color3'} borderRadius="$3">
                    <YStack gap="$2">
                      <Text fontSize="$3" color="$color10" fontWeight="600">Dettaglio Budget:</Text>
                      <XStack gap="$3" flexWrap="wrap" justifyContent="space-between">
                        <YStack gap="$1" flex={1} minWidth={120}>
                          <Text fontSize="$2" color="$green10">Budget Iniziale</Text>
                          <Text fontSize="$4" fontWeight="600" color="$green11">+€{startingMoney}</Text>
                        </YStack>
                        <YStack gap="$1" flex={1} minWidth={120}>
                          <Text fontSize="$2" color="$red10">Costo Totale</Text>
                          <Text fontSize="$4" fontWeight="600" color="$red11">
                            -€{TeamHelper.getTotalCostForPlayers(team.value)}
                          </Text>
                        </YStack>
                      </XStack>
                      <XStack gap="$3" flexWrap="wrap" justifyContent="space-between">
                        <YStack gap="$1" flex={1} minWidth={120}>
                          <Text fontSize="$2" color="$green10">Ricavi Vendite</Text>
                          <Text fontSize="$4" fontWeight="600" color="$green11">
                            +€{TeamHelper.getRevenueMoney(team.value)}
                          </Text>
                        </YStack>
                        <YStack gap="$1" flex={1} minWidth={120}>
                          <Text fontSize="$2" color="$green10">Da Classifica</Text>
                          <Text fontSize="$4" fontWeight="600" color="$green11">
                            +€{team.value.moneyFromRank}
                          </Text>
                        </YStack>
                      </XStack>
                      <XStack gap="$3" flexWrap="wrap" justifyContent="space-between">
                        <YStack gap="$1" flex={1} minWidth={120}>
                          <Text fontSize="$2" color="$green10">Infortunati/Ceduti</Text>
                          <Text fontSize="$4" fontWeight="600" color="$green11">
                            +€{TeamHelper.getMoneyFromSoldWithOneHalfReturnedPrice(team.value)}
                          </Text>
                        </YStack>
                      </XStack>
                    </YStack>
                  </Card>
                </YStack>
              </Card>
              
              <YStack gap="$2">
                <Text fontSize="$4" fontWeight="700" color="$color11">
                  Rosa ({sortedPlayers.length})
                </Text>
                {sortedPlayers.length === 0 ? (
                  <Card padding="$3" backgroundColor="$color2">
                    <Text fontSize="$3" color="$color10" textAlign="center">
                      Nessun giocatore ancora
                    </Text>
                  </Card>
                ) : (
                  <YStack gap="$2">
                    {sortedPlayers.map((player) => (
                      <Card key={player.name} padding="$3" backgroundColor="$color1" borderRadius="$3">
                        <XStack gap="$2" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                          <YStack gap="$1" flex={1}>
                            <Text fontSize="$4" fontWeight="600" color="$color11">
                              {player.name}
                            </Text>
                            <XStack gap="$2" flexWrap="wrap">
                              <Text fontSize="$2" color="$color10">
                                {getRoleLabel(player.role)} • {player.team.name}
                              </Text>
                              <Text fontSize="$2" fontWeight="600" color={getPlayerStatusColor(player.status || 0)}>
                                {getPlayerStatusLabel(player.status || 0)}
                              </Text>
                              {(player.status === 3 || player.status === 1) && player.revenue !== undefined && player.revenue !== 0 && (
                                <Text fontSize="$2" fontWeight="600" color={player.revenue > 0 ? '$green11' : '$red11'}>
                                  ({player.revenue > 0 ? '+' : ''}{player.revenue}€)
                                </Text>
                              )}
                            </XStack>
                          </YStack>
                          <XStack gap="$2" alignItems="center">
                            <Text fontSize="$4" fontWeight="700" color="$color11">€{player.price}</Text>
                            {isAuctioneer && (
                              <Button 
                                size="$2" 
                                theme="orange" 
                                variant="outlined"
                                onPress={() => {
                                  onModifyPlayerStatus(getPlayerKey(player.name), player.name, team.value.owner)
                                }}
                              >
                                Modifica
                              </Button>
                            )}
                          </XStack>
                        </XStack>
                      </Card>
                    ))}
                  </YStack>
                )}
              </YStack>
            </YStack>
          </ScrollView>
          
          <XStack justifyContent="flex-end">
            <Dialog.Close asChild>
              <Button>Chiudi</Button>
            </Dialog.Close>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
