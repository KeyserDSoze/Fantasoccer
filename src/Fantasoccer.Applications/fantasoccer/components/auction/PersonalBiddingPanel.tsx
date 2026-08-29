// PersonalBiddingPanel - Pulsantiera personale per le offerte del partecipante/banditore
import { 
  Button, Card, Separator, Text, View, XStack, YStack, 
  useMedia 
} from 'tamagui'
import { DollarSign, Users } from '@tamagui/lucide-icons-2'
import { Entity } from 'types/default'
import { Team, TeamKey, TeamHelper } from 'types/team'
import { StatPlayer } from 'types/auction'
import { Role } from 'types/group'
import { TeamTier } from 'utils/teamTiers'
import type { ChancedRealPlayer } from 'types/chance'
import { calculateTeamStatistics } from 'utils/teamStats'

export interface PersonalBiddingPanelProps {
  // Team info
  myTeam: Entity<Team, TeamKey>
  myTeamBudget: number
  
  // Current player
  currentPlayer: StatPlayer | null
  
  // Bidding actions
  onIncrementBid: (increment: number, isAuctioneer?: boolean) => void
  onFixedBid: (amount: number, isAuctioneer?: boolean) => void
  onCustomBid: () => void
  sendEmoji: (emoji: string) => void
  
  // Substitute selection
  isRepairAuction: boolean
  mySelectedSubstitute: string | null
  onSelectSubstitute: () => void
  
  // Button state
  arePersonalBidButtonsDisabled: () => { disabled: boolean; reason: string | null }
  
  // Current bidder info
  currentBidder: string | null
  userEmail: string | null
  
  // Statistics
  chancedPlayersMap?: Map<string, ChancedRealPlayer>
  showStats?: boolean
}

export function PersonalBiddingPanel({
  myTeam,
  myTeamBudget,
  currentPlayer,
  onIncrementBid,
  onFixedBid,
  onCustomBid,
  sendEmoji,
  isRepairAuction,
  mySelectedSubstitute,
  onSelectSubstitute,
  arePersonalBidButtonsDisabled,
  currentBidder,
  userEmail,
  chancedPlayersMap,
  showStats = true,
}: PersonalBiddingPanelProps) {
  const media = useMedia()
  const bidStatus = arePersonalBidButtonsDisabled()
  const isDisabled = bidStatus.disabled
  const isCurrentBidder = currentBidder?.toLowerCase() === userEmail?.toLowerCase()
  
  // Calculate team statistics
  const stats = calculateTeamStatistics(myTeam, chancedPlayersMap)
  const activePlayers = TeamHelper.getActivePlayers(myTeam.value)
  
  const ROLE_LIMITS = {
    [Role.GoalKeeper]: 3,
    [Role.Defensor]: 8,
    [Role.Midfielder]: 8,
    [Role.Forward]: 6,
  }
  
  // Check if disabled ONLY because of role limit (not other reasons like locked/expired)
  const isDisabledForRoleLimit = bidStatus.reason?.includes('Limite ruolo raggiunto') || false
  
  // Substitute button should be disabled only when budget is 0
  const isSubstituteButtonDisabled = isRepairAuction ? myTeamBudget <= 0 : isDisabled
  const shouldShowGlow = isRepairAuction && isDisabledForRoleLimit && !mySelectedSubstitute
  
  return (
    <Card padding="$4" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8">
      <YStack gap="$3">
        <XStack gap="$2" alignItems="center">
          <DollarSign size={20} color="$blue11" />
          <Text fontSize="$5" fontWeight="700" color="$blue11">Mia Squadra</Text>
        </XStack>
        <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
          <Text fontSize="$3" color="$blue11">{myTeam.value.name} • Budget: €{myTeamBudget}</Text>
        </Card>
        
        {/* Tier distribution */}
        {showStats && (
          <Card padding="$2" backgroundColor="$blue1" borderRadius="$3">
            <YStack gap="$1">
              <Text fontSize="$2" color="$blue11" fontWeight="600">Tier Squadre:</Text>
              <XStack gap="$2" justifyContent="space-between">
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$yellow10" fontWeight="700">T1</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.tierCount[TeamTier.T1]}
                  </Text>
                </YStack>
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$blue10" fontWeight="700">T2</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.tierCount[TeamTier.T2]}
                  </Text>
                </YStack>
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$green10" fontWeight="700">T3</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.tierCount[TeamTier.T3]}
                  </Text>
                </YStack>
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$gray10" fontWeight="700">T4</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.tierCount[TeamTier.T4]}
                  </Text>
                </YStack>
              </XStack>
            </YStack>
          </Card>
        )}
        
        {/* Real Team Distribution */}
        {showStats && stats.realTeamDistribution.length > 0 && (
          <Card padding="$2" backgroundColor="$blue1" borderRadius="$3">
            <YStack gap="$1">
              <Text fontSize="$2" color="$blue11" fontWeight="600">Squadre Serie A:</Text>
              <XStack gap="$1" flexWrap="wrap">
                {stats.realTeamDistribution.map(([teamName, count]) => (
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
        
        {/* Starters by Role */}
        {showStats && chancedPlayersMap && (
          <Card padding="$2" backgroundColor="$blue1" borderRadius="$3">
            <YStack gap="$1">
              <Text fontSize="$2" color="$blue11" fontWeight="600">Titolari (Fantagazzetta):</Text>
              <XStack gap="$2" justifyContent="space-around">
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$blue10">P</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.startersByRole[Role.GoalKeeper].starters}/{stats.roleCount[Role.GoalKeeper]}
                  </Text>
                </YStack>
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$blue10">D</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.startersByRole[Role.Defensor].starters}/{stats.roleCount[Role.Defensor]}
                  </Text>
                </YStack>
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$blue10">C</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.startersByRole[Role.Midfielder].starters}/{stats.roleCount[Role.Midfielder]}
                  </Text>
                </YStack>
                <YStack gap="$0.5" alignItems="center">
                  <Text fontSize="$1" color="$blue10">A</Text>
                  <Text fontSize="$2" fontWeight="600" color="$blue11">
                    {stats.startersByRole[Role.Forward].starters}/{stats.roleCount[Role.Forward]}
                  </Text>
                </YStack>
              </XStack>
            </YStack>
          </Card>
        )}
        
        <Separator />
        
        <XStack gap="$2" opacity={isDisabled ? 0.5 : 1}>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onIncrementBid(1)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">Incremento</Text>
          </Button>
        </XStack>
        <XStack gap="$2" opacity={isDisabled ? 0.5 : 1}>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onIncrementBid(2)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+2</Text>
          </Button>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onIncrementBid(3)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+3</Text>
          </Button>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onIncrementBid(5)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+5</Text>
          </Button>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onIncrementBid(7)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+7</Text>
          </Button>
        </XStack>
        <Separator />
        <XStack gap="$2" opacity={isDisabled ? 0.5 : 1}>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onFixedBid(50)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">50</Text>
          </Button>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onFixedBid(100)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">100</Text>
          </Button>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onFixedBid(150)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">150</Text>
          </Button>
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onFixedBid(200)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">200</Text>
          </Button>
        </XStack>
        
        {/* Emoji Reactions */}
        <Separator />
        <YStack gap="$2">
          <Text fontSize="$2" color="$blue11" fontWeight="600">Reazioni:</Text>
          <XStack gap="$2" flexWrap="wrap">
            <Button size="$4" chromeless onPress={() => sendEmoji('🔥')}>
              <Text fontSize={32}>🔥</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('😂')}>
              <Text fontSize={32}>😂</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('💰')}>
              <Text fontSize={32}>💰</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('👍')}>
              <Text fontSize={32}>👍</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('❤️')}>
              <Text fontSize={32}>❤️</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('😱')}>
              <Text fontSize={32}>😱</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('🤔')}>
              <Text fontSize={32}>🤔</Text>
            </Button>
            <Button size="$4" chromeless onPress={() => sendEmoji('🎉')}>
              <Text fontSize={32}>🎉</Text>
            </Button>
          </XStack>
        </YStack>
        
        <Button icon={DollarSign} size="$5" theme="blue" variant="outlined" onPress={onCustomBid} disabled={isDisabled} opacity={isDisabled ? 0.5 : 1}>
          Offerta Personalizzata
        </Button>
        {isRepairAuction && (
          <Button 
            icon={Users} 
            size="$5" 
            theme={mySelectedSubstitute ? "green" : "orange"} 
            variant="outlined" 
            onPress={onSelectSubstitute}
            disabled={isSubstituteButtonDisabled}
            transition="bouncy"
            style={{
              ...(shouldShowGlow && {
                borderColor: '#f97316',
                borderWidth: 2,
                shadowColor: '#f97316',
                shadowRadius: 12,
                shadowOpacity: 0.8,
              })
            }}
          >
            {mySelectedSubstitute 
              ? `Sostituisce: ${myTeam.value.players.find(p => p.name === mySelectedSubstitute)?.name || mySelectedSubstitute}` 
              : 'Seleziona giocatore da sostituire'}
          </Button>
        )}
        {isCurrentBidder && !isDisabled && (
          <Card padding="$2" backgroundColor="$green2" borderColor="$green8" borderWidth="$0.5">
            <Text fontSize="$3" color="$green11">✅ Sei il miglior offerente</Text>
          </Card>
        )}
        {bidStatus.reason && (
          <Card padding="$2" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
            <Text fontSize="$3" color="$red11">❌ {bidStatus.reason}</Text>
          </Card>
        )}
      </YStack>
    </Card>
  )
}
