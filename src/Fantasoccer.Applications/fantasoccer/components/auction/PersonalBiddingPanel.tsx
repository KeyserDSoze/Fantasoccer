// PersonalBiddingPanel - Pulsantiera personale per le offerte del partecipante/banditore
import { 
  Button, Card, Separator, Text, XStack, YStack, 
  useMedia 
} from 'tamagui'
import { DollarSign, Users } from '@tamagui/lucide-icons'
import { Entity } from 'types/default'
import { Team, TeamKey } from 'types/team'
import { StatPlayer } from 'types/auction'

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
  
  // Substitute selection
  isRepairAuction: boolean
  mySelectedSubstitute: string | null
  onSelectSubstitute: () => void
  
  // Button state
  arePersonalBidButtonsDisabled: () => { disabled: boolean; reason: string | null }
  
  // Current bidder info
  currentBidder: string | null
  userEmail: string | null
}

export function PersonalBiddingPanel({
  myTeam,
  myTeamBudget,
  currentPlayer,
  onIncrementBid,
  onFixedBid,
  onCustomBid,
  isRepairAuction,
  mySelectedSubstitute,
  onSelectSubstitute,
  arePersonalBidButtonsDisabled,
  currentBidder,
  userEmail,
}: PersonalBiddingPanelProps) {
  const media = useMedia()
  const bidStatus = arePersonalBidButtonsDisabled()
  const isDisabled = bidStatus.disabled
  const isCurrentBidder = currentBidder?.toLowerCase() === userEmail?.toLowerCase()
  
  return (
    <Card padding="$4" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8" opacity={isDisabled ? 0.5 : 1}>
      <YStack gap="$3">
        <XStack gap="$2" alignItems="center">
          <DollarSign size={20} color="$blue11" />
          <Text fontSize="$5" fontWeight="700" color="$blue11">Mia Squadra</Text>
        </XStack>
        <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
          <Text fontSize="$3" color="$blue11">{myTeam.value.name} • Budget: €{myTeamBudget}</Text>
        </Card>
        <XStack gap="$2">
          <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="blue" onPress={() => onIncrementBid(1)} disabled={isDisabled}>
            <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">Incremento</Text>
          </Button>
        </XStack>
        <XStack gap="$2">
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
        <XStack gap="$2">
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
        <Button icon={DollarSign} size="$5" theme="blue" variant="outlined" onPress={onCustomBid} disabled={isDisabled}>
          Offerta Personalizzata
        </Button>
        {isRepairAuction && currentPlayer && (
          <Button 
            icon={Users} 
            size="$5" 
            theme={mySelectedSubstitute ? "green" : "orange"} 
            variant="outlined" 
            onPress={onSelectSubstitute}
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
