import React, { useMemo } from 'react'
import { Dialog, YStack, XStack, Text, Button, ScrollView, Sheet } from 'tamagui'
import { X } from '@tamagui/lucide-icons'
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

  return (
    <Sheet
      modal
      open={isOpen}
      onOpenChange={onClose}
      dismissOnSnapToBottom
      snapPointsMode="fit"
    >
      <Sheet.Overlay backgroundColor="rgba(0,0,0,0.5)" />
      <Sheet.Frame padding={16} backgroundColor="$background" borderTopLeftRadius={16} borderTopRightRadius={16}>
        <Sheet.Handle backgroundColor="$gray8" />
        
        <YStack gap={16} maxHeight="80vh">
          {/* Header */}
          <XStack justifyContent="space-between" alignItems="center">
            <Text fontSize={18} fontWeight="700">
              Scambia {selectedPlayerName}
            </Text>
            <Button
              circular
              size="$3"
              onPress={onClose}
              icon={<X size={20} />}
            />
          </XStack>

          {/* Opzione rimozione (tribuna) */}
          {canRemove && onRemove && (
            <Button
              backgroundColor="$red9"
              color="#fff"
              onPress={() => {
                onRemove()
                onClose()
              }}
            >
              🏟️ Manda in Tribuna
            </Button>
          )}

          {/* Lista giocatori papabili */}
          <ScrollView maxHeight="60vh">
            <YStack gap={24}>
              {/* Titolari */}
              {groupedPlayers.starters.length > 0 && (
                <YStack gap={12}>
                  <Text fontSize={16} fontWeight="600" color="$green10">
                    ⭐ Titolari
                  </Text>
                  <XStack gap={8} flexWrap="wrap">
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
                      >
                        <PlayerCard player={player} />
                      </Button>
                    ))}
                  </XStack>
                </YStack>
              )}

              {/* Panchina */}
              {groupedPlayers.bench.length > 0 && (
                <YStack gap={12}>
                  <Text fontSize={16} fontWeight="600" color="$orange10">
                    🪑 Panchina
                  </Text>
                  <XStack gap={8} flexWrap="wrap">
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
                      >
                        <PlayerCard player={player} />
                      </Button>
                    ))}
                  </XStack>
                </YStack>
              )}

              {/* Tribuna */}
              {groupedPlayers.tribune.length > 0 && (
                <YStack gap={12}>
                  <Text fontSize={16} fontWeight="600" color="$gray10">
                    🏟️ Tribuna
                  </Text>
                  <XStack gap={8} flexWrap="wrap">
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
                      >
                        <PlayerCard player={player} />
                      </Button>
                    ))}
                  </XStack>
                </YStack>
              )}

              {eligiblePlayers.length === 0 && (
                <Text fontSize={14} color="$gray10" textAlign="center" paddingVertical={32}>
                  Nessun giocatore disponibile per lo scambio
                </Text>
              )}
            </YStack>
          </ScrollView>
        </YStack>
      </Sheet.Frame>
    </Sheet>
  )
}
