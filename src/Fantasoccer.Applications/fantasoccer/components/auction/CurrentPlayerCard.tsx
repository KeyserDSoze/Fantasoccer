// CurrentPlayerCard - Card che mostra il giocatore corrente in asta con statistiche
import { 
  Card, Separator, Text, XStack, YStack, Image
} from 'tamagui'
import { Users, Clock } from '@tamagui/lucide-icons'
import { StatPlayer } from 'types/auction'
import { Entity } from 'types/default'
import { Team, TeamKey } from 'types/team'
import { getRoleLabel } from 'utils/auctionHelper'
import { getPlayerImageUrlFromName } from 'utils/playerImage'

export interface CurrentPlayerCardProps {
  currentPlayer: StatPlayer | null
  currentBid: number
  currentBidder: string | null
  currentSubstitute: string | null
  teams: Entity<Team, TeamKey>[]
  timerRunning: boolean
  timerExpired: boolean
  timerSecondsLeft: number
  realPlayers?: Array<{ key: string; name: string }>
}

export function CurrentPlayerCard({
  currentPlayer,
  currentBid,
  currentBidder,
  currentSubstitute,
  teams,
  timerRunning,
  timerExpired,
  timerSecondsLeft,
  realPlayers,
}: CurrentPlayerCardProps) {
  // Placeholder values when no player in auction
  const displayName = currentPlayer?.name || '---'
  const displayTeam = currentPlayer?.realTeam.name || '---'
  const displayRole = currentPlayer ? getRoleLabel(currentPlayer.role) : '---'
  const { src: playerImageUrl, fallback: fallbackImageUrl } = getPlayerImageUrlFromName(currentPlayer?.name)
  
  return (
    <Card padding="$4" backgroundColor="$green2" borderRadius="$4" borderWidth="$0.5" borderColor="$green8">
      <YStack gap="$3">
        <XStack gap="$2" alignItems="center" justifyContent="space-between" flexWrap="wrap">
          <XStack gap="$2" alignItems="center">
            <Users size={20} color="$green11" />
            <Text fontSize="$5" fontWeight="700" color="$green11">Giocatore in Asta</Text>
          </XStack>
          <Text fontSize="$4" color="$green10">{displayRole}</Text>
        </XStack>
        <XStack gap="$3" alignItems="center">
          <Image
            source={{ uri: playerImageUrl }}
            defaultSource={{ uri: fallbackImageUrl }}
            width={80}
            height={80}
            borderRadius="$3"
            backgroundColor="$gray5"
          />
          <YStack gap="$2" flex={1}>
            <Text fontSize="$7" fontWeight="700" color="$green11">{displayName}</Text>
            <Text fontSize="$4" color="$green10">{displayTeam}</Text>
          </YStack>
        </XStack>
        <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
          <XStack gap="$4" justifyContent="space-between" alignItems="center" flexWrap="wrap">
            <YStack gap="$1" flex={1}>
              <Text fontSize="$2" color="$color10">Offerta Corrente</Text>
              <Text fontSize="$6" fontWeight="700" color="$color11">€{currentBid}</Text>
            </YStack>
            {currentBidder && (
              <YStack gap="$1" flex={1}>
                <Text fontSize="$2" color="$color10">Offerente</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">
                  {teams.find(t => t.value.owner === currentBidder)?.value.name || currentBidder}
                </Text>
                {currentSubstitute && (
                  <Text fontSize="$3" color="$orange11" marginTop="$1">
                    Sostituisce: {(() => {
                      const substitutePlayer = realPlayers?.find(p => p.key === currentSubstitute)
                      return substitutePlayer?.name || currentSubstitute
                    })()}
                  </Text>
                )}
              </YStack>
            )}
            {(timerRunning || timerExpired) && (
              <YStack gap="$1" alignItems="center" justifyContent="center" minWidth={100}>
                <Text fontSize="$2" color="$color10">Timer</Text>
                <Card 
                  padding="$2" 
                  paddingHorizontal="$3" 
                  backgroundColor={timerExpired ? '$red8' : timerSecondsLeft <= 3 ? '$orange8' : '$blue8'} 
                  borderRadius="$4"
                >
                  <XStack gap="$2" alignItems="center">
                    <Clock size={20} color="white" />
                    <Text fontSize="$6" fontWeight="700" color="white">
                      {timerExpired ? 'SCADUTO' : `${timerSecondsLeft}s`}
                    </Text>
                  </XStack>
                </Card>
              </YStack>
            )}
          </XStack>
        </Card>
        <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
          <YStack gap="$3">
            <Text fontSize="$3" fontWeight="700" color="$color11">Statistiche Stagione</Text>
            <XStack gap="$4" flexWrap="wrap" justifyContent="space-between">
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Media Voto</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.average?.toFixed(2) || '0.00'}</Text>
              </YStack>
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Fantamedia</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.fantaAverage?.toFixed(2) || '0.00'}</Text>
              </YStack>
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Partite</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.withVote || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Senza Voto</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.withoutVote || 0}</Text>
              </YStack>
            </XStack>
            <Separator />
            <Text fontSize="$3" fontWeight="700" color="$color11">Eventi</Text>
            <XStack gap="$4" flexWrap="wrap" justifyContent="space-between">
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Gol</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.goals || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Rigori</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.penalties || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Assist</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.assists || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Rig. Parati</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.stoppedPenalties || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Gol Subiti</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.sufferedGoals || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Rig. Sbagliati</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.wrongedPenalties || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Autogol</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.ownGoals || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Ammonizioni</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.yellowCards || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Espulsioni</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.redCards || 0}</Text>
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">MVP</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer?.manOfTheMatch || 0}</Text>
              </YStack>
            </XStack>
          </YStack>
        </Card>
      </YStack>
    </Card>
  )
}
