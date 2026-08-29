// FloatingPlayerWidget - Widget floating per giocatore in asta (bottom-right)
import { useState } from 'react'
import { Circle, Text, XStack, YStack, Avatar, Image } from 'tamagui'
import { StatPlayer } from 'types/auction'
import { Entity } from 'types/default'
import { Team, TeamKey } from 'types/team'
import { getPlayerImageUrlFromName } from 'utils/playerImage'

export interface FloatingPlayerWidgetProps {
  currentPlayer: StatPlayer | null
  currentBid: number
  currentBidder: string | null
  teams: Entity<Team, TeamKey>[]
  timerSecondsLeft: number
  onIncrementBid: () => void
  disabled?: boolean
}

export function FloatingPlayerWidget({
  currentPlayer,
  currentBid,
  currentBidder,
  teams,
  timerSecondsLeft,
  onIncrementBid,
  disabled = false
}: FloatingPlayerWidgetProps) {
  const [useFallbackImage, setUseFallbackImage] = useState(false)

  if (!currentPlayer) return null

  const winningTeam = teams.find(t => t.value.owner === currentBidder)
  
  // Usa la stessa logica di PlayerCard per le immagini
  const { src, fallback } = getPlayerImageUrlFromName(currentPlayer.name)
  const playerImageUrl = useFallbackImage ? fallback : src

  return (
    <YStack
      style={{
        position: 'fixed',
        bottom: 16,
        right: 16,
        zIndex: 9999,
      }}
      pointerEvents="box-none"
    >
      {/* Container relativo per posizionamento elementi */}
      <YStack
        width={120}
        height={120}
        alignItems="center"
        justifyContent="center"
        pointerEvents="auto"
        position="relative"
      >
        {/* Timer (sopra il cerchio a destra) */}
        <YStack position="absolute" top={5} right={5} zIndex={3}>
          <Circle size={28} backgroundColor="$orange9" borderWidth={2} borderColor="white">
            <Text fontSize="$1" fontWeight="700" color="white">
              {timerSecondsLeft}
            </Text>
          </Circle>
        </YStack>

        {/* Prezzo (sopra il cerchio a sinistra) */}
        <YStack position="absolute" top={5} left={5} zIndex={3}>
          <YStack backgroundColor="$green9" paddingHorizontal="$2" paddingVertical="$1" borderRadius="$3" borderWidth={2} borderColor="white">
            <Text fontSize="$1" fontWeight="700" color="white">
              €{currentBid}
            </Text>
          </YStack>
        </YStack>

        {/* Cerchio con immagine giocatore - cliccabile */}
        <Circle 
          size={110} 
          backgroundColor={disabled ? "$gray5" : "$purple5"}
          borderWidth={3} 
          borderColor={disabled ? "$gray8" : "$purple8"}
          overflow="hidden"
          shadowColor="$shadowColor"
          shadowRadius={15}
          shadowOffset={{ width: 0, height: 4 }}
          elevation={8}
          pressStyle={!disabled ? { scale: 1, opacity: 0.8 } : undefined}
          onPress={disabled ? undefined : onIncrementBid}
          transition="quick"
          cursor={disabled ? 'default' : 'pointer'}
          opacity={disabled ? 0.6 : 1}
        >
          <Image
            source={{ uri: playerImageUrl }}
            width={110}
            height={110}
            resizeMode="cover"
            onError={() => setUseFallbackImage(true)}
          />
        </Circle>

        {/* Nome giocatore (sotto, sopra il cerchio nella parte bassa) */}
        <YStack position="absolute" bottom={8} alignItems="center" width="100%" paddingHorizontal="$2" zIndex={2}>
          <Text 
            fontSize="$2" 
            fontWeight="700" 
            color="white"
            backgroundColor="$purple9"
            paddingHorizontal="$2"
            paddingVertical="$1"
            borderRadius="$2"
            textAlign="center"
            numberOfLines={1}
            ellipsizeMode="tail"
            shadowColor="$shadowColor"
            shadowRadius={5}
            shadowOffset={{ width: 0, height: 2 }}
          >
            {currentPlayer.name} ({currentPlayer.realTeam.abbreviation})
          </Text>
          {/* Squadra vincente */}
          {winningTeam && (
            <Text 
              fontSize="$1" 
              color="white"
              backgroundColor="$blue9"
              paddingHorizontal="$1.5"
              paddingVertical="$0.5"
              borderRadius="$2"
              marginTop="$0.5"
              textAlign="center"
              numberOfLines={1}
              ellipsizeMode="tail"
              shadowColor="$shadowColor"
              shadowRadius={3}
              shadowOffset={{ width: 0, height: 1 }}
            >
              {winningTeam.value.name}
            </Text>
          )}
        </YStack>
      </YStack>
    </YStack>
  )
}
