// CurrentPlayerCard - Card che mostra il giocatore corrente in asta con statistiche
import { 
  Card, Separator, Text, XStack, YStack, Image
} from 'tamagui'
import { Users, Clock, ArrowRightLeft } from '@tamagui/lucide-icons'
import { StatPlayer } from 'types/auction'
import { Entity } from 'types/default'
import { Team, TeamKey } from 'types/team'
import { getRoleLabel } from 'utils/auctionHelper'
import { getPlayerImageUrlFromName } from 'utils/playerImage'
import { getTeamTierInfo } from 'utils/teamTiers'

export interface CurrentPlayerCardProps {
  currentPlayer: StatPlayer | null
  previousYearStats?: StatPlayer
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
  previousYearStats,
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
  
  // Get team tier info
  const tierInfo = getTeamTierInfo(currentPlayer?.realTeam.name)
  
  // Check if player changed team from previous year
  const hasChangedTeam = currentPlayer && previousYearStats 
    && currentPlayer.realTeam.name !== previousYearStats.realTeam.name
  
  // Helper component to display stat with previous year comparison
  const StatWithComparison = ({ 
    current, 
    previous, 
    decimals = 0 
  }: { 
    current: number | undefined | null; 
    previous: number | undefined | null; 
    decimals?: number 
  }) => {
    const currentValue = current ?? 0
    const displayCurrent = decimals > 0 ? currentValue.toFixed(decimals) : currentValue.toString()
    
    // Show "/-" when no previous year data
    const displayPrevious = (previous === undefined || previous === null) 
      ? '-' 
      : (decimals > 0 ? previous.toFixed(decimals) : previous.toString())
    
    return (
      <XStack gap="$1" alignItems="center">
        <Text fontSize="$4" fontWeight="600" color="$color11">{displayCurrent}</Text>
        <Text fontSize="$3" fontWeight="500" color="$gray10">/ {displayPrevious}</Text>
      </XStack>
    )
  }
  
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
            <XStack gap="$2" alignItems="center">
              <Text fontSize="$4" color="$green10">{displayTeam}</Text>
              {currentPlayer && (
                <Card 
                  paddingVertical="$1" 
                  paddingHorizontal="$2" 
                  backgroundColor={tierInfo.backgroundColor}
                  borderRadius="$2"
                  borderWidth={1}
                  borderColor={tierInfo.color}
                >
                  <Text fontSize="$2" fontWeight="700" color={tierInfo.color}>
                    {tierInfo.label}
                  </Text>
                </Card>
              )}
            </XStack>
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
                <StatWithComparison 
                  current={currentPlayer?.average} 
                  previous={previousYearStats?.average}
                  decimals={2}
                />
              </YStack>
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Fantamedia</Text>
                <StatWithComparison 
                  current={currentPlayer?.fantaAverage} 
                  previous={previousYearStats?.fantaAverage}
                  decimals={2}
                />
              </YStack>
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Partite</Text>
                <StatWithComparison 
                  current={currentPlayer?.withVote} 
                  previous={previousYearStats?.withVote}
                />
              </YStack>
              <YStack gap="$1" minWidth={80}>
                <Text fontSize="$2" color="$color10">Senza Voto</Text>
                <StatWithComparison 
                  current={currentPlayer?.withoutVote} 
                  previous={previousYearStats?.withoutVote}
                />
              </YStack>
            </XStack>
            {hasChangedTeam && (
              <XStack gap="$2" alignItems="center" paddingTop="$2">
                <ArrowRightLeft size={16} color="$orange10" />
                <Text fontSize="$2" color="$orange10" fontWeight="600">
                  Ha cambiato squadra ({previousYearStats?.realTeam.name} → {currentPlayer?.realTeam.name})
                </Text>
              </XStack>
            )}
            <Separator />
            <Text fontSize="$3" fontWeight="700" color="$color11">Eventi</Text>
            <XStack gap="$4" flexWrap="wrap" justifyContent="space-between">
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Gol</Text>
                <StatWithComparison 
                  current={currentPlayer?.goals} 
                  previous={previousYearStats?.goals}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Rigori</Text>
                <StatWithComparison 
                  current={currentPlayer?.penalties} 
                  previous={previousYearStats?.penalties}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Assist</Text>
                <StatWithComparison 
                  current={currentPlayer?.assists} 
                  previous={previousYearStats?.assists}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Rig. Parati</Text>
                <StatWithComparison 
                  current={currentPlayer?.stoppedPenalties} 
                  previous={previousYearStats?.stoppedPenalties}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Gol Subiti</Text>
                <StatWithComparison 
                  current={currentPlayer?.sufferedGoals} 
                  previous={previousYearStats?.sufferedGoals}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Rig. Sbagliati</Text>
                <StatWithComparison 
                  current={currentPlayer?.wrongedPenalties} 
                  previous={previousYearStats?.wrongedPenalties}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Autogol</Text>
                <StatWithComparison 
                  current={currentPlayer?.ownGoals} 
                  previous={previousYearStats?.ownGoals}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Ammonizioni</Text>
                <StatWithComparison 
                  current={currentPlayer?.yellowCards} 
                  previous={previousYearStats?.yellowCards}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">Espulsioni</Text>
                <StatWithComparison 
                  current={currentPlayer?.redCards} 
                  previous={previousYearStats?.redCards}
                />
              </YStack>
              <YStack gap="$1" minWidth={70}>
                <Text fontSize="$2" color="$color10">MVP</Text>
                <StatWithComparison 
                  current={currentPlayer?.manOfTheMatch} 
                  previous={previousYearStats?.manOfTheMatch}
                />
              </YStack>
            </XStack>
          </YStack>
        </Card>
      </YStack>
    </Card>
  )
}
