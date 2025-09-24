'use client'

// Componente per mostrare la giornata Serie A attuale
import React from 'react'
import {
  Card,
  YStack,
  XStack,
  Text,
  Button,
  Spinner,
} from '@my/ui'
import { Calendar, Play, Clock, RefreshCw } from '@tamagui/lucide-icons'
import { useSerieACalendar, formatGameDate, getDayStatusText } from '../hooks/useSerieACalendar'
import { useTimeAgo } from '../hooks/useTimeAgo'

export function SerieADayCard() {
  const { 
    liveDay, 
    nextDay, 
    liveGames, 
    isLive, 
    isLoading, 
    error, 
    lastUpdated,
    refetch 
  } = useSerieACalendar({
    refreshInterval: 30000, // 30 secondi
    enablePolling: true,
    pollOnlyWhenLive: true // Aggiorna automaticamente solo quando ci sono partite live
  })

  // Hook per aggiornare automaticamente il tempo trascorso
  const timeAgo = useTimeAgo(lastUpdated)

  if (isLoading) {
    return (
      <Card padding="$4" bg="$color2" borderRadius="$4" width="100%">
        <YStack gap="$3" align="center">
          <Spinner size="large" color="$blue10" />
          <Text fontSize="$4" color="$color10">
            Caricamento calendario Serie A...
          </Text>
        </YStack>
      </Card>
    )
  }

  if (error) {
    return (
      <Card padding="$4" bg="$color2" borderRadius="$4" width="100%">
        <YStack gap="$3" align="center">
          <Text fontSize="$4" color="$red10">
            {error}
          </Text>
          <Button 
            size="$3" 
            variant="outlined"
            onPress={refetch}
            icon={RefreshCw}
          >
            Riprova
          </Button>
        </YStack>
      </Card>
    )
  }

  const currentDay = liveDay || nextDay
  const statusText = getDayStatusText(isLive, liveDay, nextDay)

  return (
    <Card padding="$4" bg="$color2" borderRadius="$4" width="100%">
      <YStack gap="$4">
        {/* Header con status giornata */}
        <XStack gap="$3" align="center" justify="center">
          <Calendar size="$1.5" color={isLive ? "$green10" : "$yellow10"} />
          <Text fontSize="$6" fontWeight="bold" color="$color12">
            {statusText}
          </Text>
          {isLive && (
            <XStack gap="$1" align="center">
              <Play size="$1.5" color="$green10" />
              <YStack 
                width="$0.5" 
                height="$0.5" 
                borderRadius="$10" 
                bg="$green10"
                animation="bouncy"
                animateOnly={['scale']}
                // @ts-ignore - Tamagui animation prop
                $animate={{
                  scale: [1, 1.3, 1],
                }}
                style={{
                  animationDuration: '1s',
                  animationIterationCount: 'infinite',
                  animationTimingFunction: 'ease-in-out'
                }}
              />
            </XStack>
          )}
        </XStack>

        {/* Partite live se presenti */}
        {isLive && liveGames.length > 0 && (
          <YStack gap="$2">
            <Text fontSize="$4" fontWeight="600" color="$green10" textAlign="center">
              🔴 Partite in corso
            </Text>
            {liveGames.map((game, index) => (
              <XStack 
                key={index}
                justify="space-between" 
                align="center"
                bg="$color3"
                padding="$2"
                borderRadius="$2"
              >
                <Text fontSize="$3" color="$color11">
                  {game.home.name} vs {game.away.name}
                </Text>
                <XStack gap="$1" align="center">
                  {game.homeGoals !== null && game.awayGoals !== null ? (
                    <Text fontSize="$3" fontWeight="bold" color="$color12">
                      {game.homeGoals} - {game.awayGoals}
                    </Text>
                  ) : (
                    <Clock size="$1" color="$green10" />
                  )}
                </XStack>
              </XStack>
            ))}
          </YStack>
        )}

        {/* Prossime partite se non è live */}
        {!isLive && nextDay && nextDay.games.length > 0 && (
          <YStack gap="$2">
            {nextDay.games.slice(0, 10).map((game, index) => (
              <XStack 
                key={index}
                justify="space-between" 
                align="center"
                bg="$color3"
                padding="$2"
                borderRadius="$2"
              >
                <Text fontSize="$3" color="$color11">
                  {game.home.name} vs {game.away.name}
                </Text>
                <Text fontSize="$2" color="$color10">
                  {formatGameDate(game.date)}
                </Text>
              </XStack>
            ))}
            {nextDay.games.length > 10 && (
              <Text fontSize="$2" color="$color10" textAlign="center">
                ... e altre {nextDay.games.length - 10} partite
              </Text>
            )}
          </YStack>
        )}

        {/* Bottone azione */}
        <Button 
          size="$4" 
          bg={isLive ? "$green10" : "$blue10"} 
          color="white"
          disabled={!currentDay}
          onPress={() => {
            // TODO: Navigazione alla pagina delle formazioni
            console.log('Navigate to formations for day:', currentDay?.serieADay)
          }}
        >
          {isLive ? "⚡ Partite Live" : "🏈 Gestisci Formazione"}
        </Button>

        {/* Info refresh e ultimo aggiornamento */}
        {isLive && (<YStack gap="$1" align="center">
          <XStack justify="center" gap="$2" align="center">
            <Clock size="$0.75" color="$color8" />
            <Text fontSize="$1" color="$color8">
               🔴 Aggiornamento automatico attivo
            </Text>
          </XStack>
          
          <XStack justify="center" gap="$2" align="center">
            <Text fontSize="$1" color="$color8">
              Ultimo aggiornamento: {timeAgo}
            </Text>
            <Button 
              size="$1" 
              variant="outlined" 
              circular
              onPress={() => refetch()}
              opacity={0.7}
              icon={RefreshCw}
            >
            </Button>
          </XStack>
        </YStack>)}
      </YStack>
    </Card>
  )
}