import { Dialog, Button, Card, ScrollView, Separator, Text, XStack, YStack } from 'tamagui'
import { Entity } from 'types/default'
import { Team, TeamKey } from 'types/team'
import { StatPlayer } from 'types/auction'
import { Role } from 'types/group'
import { getRoleLabel } from 'utils/auctionHelper'
import { getPlayerKey } from 'utils'

export interface SubstituteSelectionDialogProps {
  open: boolean
  currentPlayer: StatPlayer | null
  selectedRole: Role
  team: Entity<Team, TeamKey> | null
  currentSelection: string | null
  realPlayers: StatPlayer[] | undefined
  onOpenChange: (open: boolean) => void
  onSelectPlayer: (playerName: string) => void
  onRemoveSelection: () => void
}

export function SubstituteSelectionDialog({
  open,
  currentPlayer,
  selectedRole,
  team,
  currentSelection,
  realPlayers,
  onOpenChange,
  onSelectPlayer,
  onRemoveSelection,
}: SubstituteSelectionDialogProps) {
  if (!team) return null
  
  // Filter players by selected role (the role currently being auctioned)
  const eligiblePlayers = team.value.players.filter(p => {
    if (p.status !== 0) return false // Only active players
    return p.role === selectedRole // Filter by selected role
  })
  
  return (
    <Dialog modal open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay 
          key="overlay" 
          animation="quick" 
          opacity={0.5} 
          enterStyle={{ opacity: 0 }} 
          exitStyle={{ opacity: 0 }} 
        />
        <Dialog.Content 
          bordered 
          elevate 
          key="content" 
          animateOnly={['transform', 'opacity']} 
          animation={['quick', { opacity: { overshootClamping: true } }]} 
          enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} 
          exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} 
          gap="$4" 
          width={800} 
          maxWidth="95%" 
          maxHeight="90%"
        >
          <Dialog.Title fontSize="$6" fontWeight="700">Seleziona Giocatore da Sostituire</Dialog.Title>
          <Dialog.Description>
            {currentPlayer 
              ? `Scegli un ${getRoleLabel(selectedRole)} da sostituire con ${currentPlayer.name}`
              : `Scegli un ${getRoleLabel(selectedRole)} da sostituire (pre-selezione)`
            }
          </Dialog.Description>
          
          <ScrollView maxHeight={500}>
            <YStack gap="$3" padding="$2">
              {eligiblePlayers.length === 0 ? (
                <Card padding="$4" backgroundColor="$orange2" borderRadius="$3">
                  <Text fontSize="$4" color="$orange11" textAlign="center">
                    Nessun {getRoleLabel(selectedRole)} disponibile per la sostituzione
                  </Text>
                </Card>
              ) : (
                <YStack gap="$3">
                  {eligiblePlayers.map((player) => {
                    // Get stats from realPlayers using playerKey
                    const playerKey = getPlayerKey(player.name)
                    const playerStats = realPlayers?.find(p => p.key === playerKey)
                    
                    return (
                      <Card
                        key={player.name}
                        padding="$4"
                        backgroundColor={currentSelection === player.name ? '$blue2' : '$color1'}
                        borderRadius="$4"
                        borderWidth="$0.5"
                        borderColor={currentSelection === player.name ? '$blue8' : '$borderColor'}
                        pressStyle={{ scale: 0.98, opacity: 0.9 }}
                        cursor="pointer"
                        onPress={() => {
                          onSelectPlayer(player.name)
                          onOpenChange(false)
                        }}
                      >
                        <YStack gap="$3">
                          <XStack gap="$2" alignItems="center" justifyContent="space-between">
                            <YStack gap="$1" flex={1}>
                              <Text fontSize="$6" fontWeight="700" color="$color11">{player.name}</Text>
                              <Text fontSize="$3" color="$color10">{player.team.name}</Text>
                            </YStack>
                            {currentSelection === player.name && (
                              <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue8" borderRadius="$3">
                                <Text fontSize="$3" color="white" fontWeight="700">✓ SELEZIONATO</Text>
                              </Card>
                            )}
                          </XStack>
                          
                          <XStack gap="$4" flexWrap="wrap">
                            <YStack gap="$1">
                              <Text fontSize="$2" color="$color10">Ruolo</Text>
                              <Text fontSize="$4" fontWeight="600" color="$color11">{getRoleLabel(player.role)}</Text>
                            </YStack>
                            <YStack gap="$1">
                              <Text fontSize="$2" color="$color10">Prezzo Acquisto</Text>
                              <Text fontSize="$4" fontWeight="600" color="$color11">€{player.price}</Text>
                            </YStack>
                            {player.revenue !== 0 && (
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Revenue</Text>
                                <Text fontSize="$4" fontWeight="600" color={player.revenue > 0 ? '$green11' : '$red11'}>
                                  {player.revenue > 0 ? '+' : ''}€{player.revenue}
                                </Text>
                              </YStack>
                            )}
                            <YStack gap="$1">
                              <Text fontSize="$2" color="$color10">Costo Effettivo</Text>
                              <Text fontSize="$4" fontWeight="600" color="$color11">€{player.price + player.revenue}</Text>
                            </YStack>
                          </XStack>

                          {playerStats && (
                            <>
                              <Separator />
                              <YStack gap="$2">
                                <Text fontSize="$3" fontWeight="600" color="$color11">Statistiche Stagione</Text>
                                <XStack gap="$4" flexWrap="wrap" justifyContent="space-between">
                                  <YStack gap="$1" minWidth={80}>
                                    <Text fontSize="$2" color="$color10">Media Voto</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">
                                      {playerStats.average?.toFixed(2) || 'N/A'}
                                    </Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={80}>
                                    <Text fontSize="$2" color="$color10">Fantamedia</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">
                                      {playerStats.fantaAverage?.toFixed(2) || 'N/A'}
                                    </Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={80}>
                                    <Text fontSize="$2" color="$color10">Partite</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.withVote || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={80}>
                                    <Text fontSize="$2" color="$color10">Senza Voto</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.withoutVote || 0}</Text>
                                  </YStack>
                                </XStack>
                              </YStack>

                              <Separator />

                              <YStack gap="$2">
                                <Text fontSize="$3" fontWeight="600" color="$color11">Eventi</Text>
                                <XStack gap="$3" flexWrap="wrap" justifyContent="space-between">
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Gol</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.goals || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Rigori</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.penalties || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Assist</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.assists || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Rig. Parati</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.stoppedPenalties || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Gol Subiti</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.sufferedGoals || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Rig. Sbag.</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.wrongedPenalties || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Autogol</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.ownGoals || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Ammon.</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.yellowCards || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">Espuls.</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.redCards || 0}</Text>
                                  </YStack>
                                  <YStack gap="$1" minWidth={60}>
                                    <Text fontSize="$2" color="$color10">MVP</Text>
                                    <Text fontSize="$3" fontWeight="600" color="$color11">{playerStats.manOfTheMatch || 0}</Text>
                                  </YStack>
                                </XStack>
                              </YStack>
                            </>
                          )}

                          {!playerStats && (
                            <Card padding="$2" backgroundColor="$orange1" marginTop="$2">
                              <Text fontSize="$2" color="$orange11" textAlign="center">
                                ℹ️ Statistiche non disponibili per questo giocatore
                              </Text>
                            </Card>
                          )}
                        </YStack>
                      </Card>
                    )
                  })}
                </YStack>
              )}
            </YStack>
          </ScrollView>
          
          <XStack justifyContent="flex-end" gap="$2">
            {currentSelection && (
              <Button variant="outlined" theme="orange" onPress={onRemoveSelection}>
                Rimuovi Selezione
              </Button>
            )}
            <Dialog.Close asChild>
              <Button>Chiudi</Button>
            </Dialog.Close>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
