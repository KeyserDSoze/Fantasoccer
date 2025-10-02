import React, { useMemo } from 'react'
import { Dialog, YStack, XStack, Text, Button, ScrollView, Sheet } from 'tamagui'
import { X, ArrowLeftRight } from '@tamagui/lucide-icons'
import { Dimensions } from 'react-native'
import type { LivePlayer } from 'types/game'
import { FantaSoccerRole } from 'types/team'
import { Role } from 'types/group'
import { PlayerCard } from './PlayerCard'

interface PlayerSwapModalProps {
  isOpen: boolean
  onClose: () => void
  selectedPlayer: LivePlayer | null
  allPlayers: LivePlayer[]
  onSwap: (targetPlayer: LivePlayer) => void
  onRemove?: () => void // Opzione per mandare in tribuna (se permesso)
  canRemove: boolean
}

const ROLE_LABELS: Record<Role, string> = {
  [Role.GoalKeeper]: 'Portiere',
  [Role.Defensor]: 'Difensore',
  [Role.Midfielder]: 'Centrocampista',
  [Role.Forward]: 'Attaccante',
  [Role.Undefined]: 'Non definito'
}

export function PlayerSwapModal({
  isOpen,
  onClose,
  selectedPlayer,
  allPlayers,
  onSwap,
  onRemove,
  canRemove
}: PlayerSwapModalProps) {
  // Filtra giocatori papabili per ruolo (stesso ruolo principale)
  const eligiblePlayers = useMemo(() => {
    if (!selectedPlayer) return []
    
    const selectedRole = (selectedPlayer.current as any)?.role ?? Role.Undefined
    
    return allPlayers.filter(p => {
      // Escludi il giocatore selezionato stesso
      if ((p.current as any)?.name === (selectedPlayer.current as any)?.name) return false
      
      // Stesso ruolo principale
      const pRole = (p.current as any)?.role ?? Role.Undefined
      return pRole === selectedRole
    })
  }, [selectedPlayer, allPlayers])

  // Raggruppa per categoria (titolari, panchina, tribuna)
  const groupedPlayers = useMemo(() => {
    const starters: LivePlayer[] = []
    const bench: LivePlayer[] = []
    const tribune: LivePlayer[] = []

    eligiblePlayers.forEach(p => {
      const pos = p.currentPosition
      if (pos >= FantaSoccerRole.GoalKeeper && pos <= FantaSoccerRole.Forward) {
        starters.push(p)
      } else if (pos >= FantaSoccerRole.BackupGoalKeeper && pos <= FantaSoccerRole.SecondBackupForward) {
        bench.push(p)
      } else if (pos === FantaSoccerRole.Tribune) {
        tribune.push(p)
      }
    })

    return { starters, bench, tribune }
  }, [eligiblePlayers])

  const selectedPlayerName = (selectedPlayer?.current as any)?.name ?? 'Giocatore'
  const selectedPlayerRole = (selectedPlayer?.current as any)?.role ?? Role.Undefined
  const selectedRoleLabel = ROLE_LABELS[selectedPlayerRole as Role] ?? 'Non definito'
  const screenHeight = Dimensions.get('window').height

  return (
    <Sheet
      modal
      open={isOpen}
      onOpenChange={onClose}
      dismissOnSnapToBottom
      snapPoints={[85]}
      position={0}
      zIndex={100000}
    >
      <Sheet.Overlay backgroundColor="rgba(0,0,0,0.7)" animation="quick" enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
      <Sheet.Frame 
        padding={0} 
        backgroundColor="$background" 
        borderTopLeftRadius={20} 
        borderTopRightRadius={20}
        maxHeight={screenHeight * 0.85}
      >
        <Sheet.Handle backgroundColor="$gray8" marginVertical={8} />
        
        <YStack flex={1} paddingHorizontal={16} paddingBottom={16}>
          {/* Header Fisso */}
          <YStack gap={12} paddingBottom={12} borderBottomWidth={1} borderBottomColor="$borderColor">
            <XStack justifyContent="space-between" alignItems="center">
              <YStack gap={4} flex={1}>
                <Text fontSize={20} fontWeight="800" color="$color">
                  Scambia Giocatore
                </Text>
                <Text fontSize={14} color="$gray11">
                  {selectedPlayerName} • {selectedRoleLabel}
                </Text>
              </YStack>
              <Button
                circular
                size="$4"
                onPress={onClose}
                chromeless
                icon={<X size={24} />}
              />
            </XStack>

            {/* Opzione rimozione (tribuna) */}
            {canRemove && onRemove && (
              <Button
                backgroundColor="$red10"
                color="#fff"
                size="$4"
                fontWeight="700"
                onPress={() => {
                  onRemove()
                  onClose()
                }}
                borderRadius={12}
                pressStyle={{ opacity: 0.8, scale: 0.98 }}
              >
                🏟️ Manda in Tribuna
              </Button>
            )}
          </YStack>

          {/* Lista giocatori papabili - Scrollabile */}
          <ScrollView 
            showsVerticalScrollIndicator={false}
            contentContainerStyle={{ paddingTop: 16 }}
          >
            <YStack gap={20} paddingBottom={16}>
              {/* Titolari */}
              {groupedPlayers.starters.length > 0 && (
                <YStack gap={12}>
                  <XStack 
                    backgroundColor="$green2" 
                    paddingHorizontal={12} 
                    paddingVertical={8} 
                    borderRadius={8}
                    alignItems="center"
                    gap={8}
                  >
                    <Text fontSize={15} fontWeight="700" color="$green11">
                      ⭐ TITOLARI
                    </Text>
                    <Text fontSize={13} color="$green10" fontWeight="600">
                      ({groupedPlayers.starters.length})
                    </Text>
                  </XStack>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <XStack gap={12} paddingVertical={4}>
                      {groupedPlayers.starters.map((player, i) => (
                        <Button
                          key={`starter-${i}`}
                          padding={0}
                          backgroundColor="transparent"
                          borderWidth={0}
                          onPress={() => {
                            onSwap(player)
                            onClose()
                          }}
                          pressStyle={{ opacity: 0.7, scale: 0.95 }}
                        >
                          <YStack gap={4} alignItems="center">
                            <PlayerCard player={player} />
                            <XStack 
                              backgroundColor="$green10" 
                              paddingHorizontal={8} 
                              paddingVertical={4} 
                              borderRadius={6}
                              alignItems="center"
                              gap={4}
                            >
                              <ArrowLeftRight size={12} color="#fff" />
                              <Text fontSize={11} fontWeight="700" color="#fff">
                                SCAMBIA
                              </Text>
                            </XStack>
                          </YStack>
                        </Button>
                      ))}
                    </XStack>
                  </ScrollView>
                </YStack>
              )}

              {/* Panchina */}
              {groupedPlayers.bench.length > 0 && (
                <YStack gap={12}>
                  <XStack 
                    backgroundColor="$orange2" 
                    paddingHorizontal={12} 
                    paddingVertical={8} 
                    borderRadius={8}
                    alignItems="center"
                    gap={8}
                  >
                    <Text fontSize={15} fontWeight="700" color="$orange11">
                      🪑 PANCHINA
                    </Text>
                    <Text fontSize={13} color="$orange10" fontWeight="600">
                      ({groupedPlayers.bench.length})
                    </Text>
                  </XStack>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <XStack gap={12} paddingVertical={4}>
                      {groupedPlayers.bench.map((player, i) => (
                        <Button
                          key={`bench-${i}`}
                          padding={0}
                          backgroundColor="transparent"
                          borderWidth={0}
                          onPress={() => {
                            onSwap(player)
                            onClose()
                          }}
                          pressStyle={{ opacity: 0.7, scale: 0.95 }}
                        >
                          <YStack gap={4} alignItems="center">
                            <PlayerCard player={player} />
                            <XStack 
                              backgroundColor="$orange10" 
                              paddingHorizontal={8} 
                              paddingVertical={4} 
                              borderRadius={6}
                              alignItems="center"
                              gap={4}
                            >
                              <ArrowLeftRight size={12} color="#fff" />
                              <Text fontSize={11} fontWeight="700" color="#fff">
                                SCAMBIA
                              </Text>
                            </XStack>
                          </YStack>
                        </Button>
                      ))}
                    </XStack>
                  </ScrollView>
                </YStack>
              )}

              {/* Tribuna */}
              {groupedPlayers.tribune.length > 0 && (
                <YStack gap={12}>
                  <XStack 
                    backgroundColor="$gray3" 
                    paddingHorizontal={12} 
                    paddingVertical={8} 
                    borderRadius={8}
                    alignItems="center"
                    gap={8}
                  >
                    <Text fontSize={15} fontWeight="700" color="$gray11">
                      🏟️ TRIBUNA
                    </Text>
                    <Text fontSize={13} color="$gray10" fontWeight="600">
                      ({groupedPlayers.tribune.length})
                    </Text>
                  </XStack>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <XStack gap={12} paddingVertical={4}>
                      {groupedPlayers.tribune.map((player, i) => (
                        <Button
                          key={`tribune-${i}`}
                          padding={0}
                          backgroundColor="transparent"
                          borderWidth={0}
                          onPress={() => {
                            onSwap(player)
                            onClose()
                          }}
                          pressStyle={{ opacity: 0.7, scale: 0.95 }}
                        >
                          <YStack gap={4} alignItems="center">
                            <PlayerCard player={player} />
                            <XStack 
                              backgroundColor="$blue10" 
                              paddingHorizontal={8} 
                              paddingVertical={4} 
                              borderRadius={6}
                              alignItems="center"
                              gap={4}
                            >
                              <ArrowLeftRight size={12} color="#fff" />
                              <Text fontSize={11} fontWeight="700" color="#fff">
                                PORTA IN CAMPO
                              </Text>
                            </XStack>
                          </YStack>
                        </Button>
                      ))}
                    </XStack>
                  </ScrollView>
                </YStack>
              )}

              {eligiblePlayers.length === 0 && (
                <YStack 
                  backgroundColor="$gray2" 
                  padding={32} 
                  borderRadius={12} 
                  alignItems="center"
                  gap={8}
                >
                  <Text fontSize={40}>🤷‍♂️</Text>
                  <Text fontSize={16} fontWeight="600" color="$gray11" textAlign="center">
                    Nessun giocatore disponibile
                  </Text>
                  <Text fontSize={14} color="$gray10" textAlign="center">
                    Non ci sono altri {selectedRoleLabel.toLowerCase()} nella rosa
                  </Text>
                </YStack>
              )}
            </YStack>
          </ScrollView>
        </YStack>
      </Sheet.Frame>
    </Sheet>
  )
}
