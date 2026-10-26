// AuctioneerPanel - Pannello banditore con controlli asta e pulsantiera offerte
import { Platform } from 'react-native'
import { 
  Button, Card, Select, Separator, Text, XStack, YStack, 
  useMedia, Label
} from 'tamagui'
import { 
  Gavel, Users, TrendingUp, Pause, Play, XCircle, ChevronDown, 
  Clock, RotateCcw, Search, Check, Unlock, Filter 
} from '@tamagui/lucide-icons-2'
import { Role } from 'types/group'
import { AuctionStatus, StatPlayer, AuctionState } from 'types/auction'
import { Entity } from 'types/default'
import { Team, TeamKey } from 'types/team'
import { getRoleLabel } from 'utils/auctionHelper'

export interface AuctioneerPanelProps {
  // Player state
  currentPlayer: StatPlayer | null
  currentBid: number
  currentBidder: string | null
  currentSubstitute: string | null
  
  // Teams
  teams: Entity<Team, TeamKey>[]
  selectedTeamForBid: string
  onSelectedTeamForBidChange: (team: string) => void
  calculateBudgetRemaining: (team: Entity<Team, TeamKey>) => number
  
  // Substitute selection
  auctioneerSelectedSubstitute: string | null
  isRepairAuction: boolean
  onSelectSubstitute: () => void
  
  // Bidding
  areAuctioneerBidButtonsDisabled: () => { disabled: boolean; reason: string | null }
  onIncrementBid: (increment: number, isAuctioneer: boolean) => void
  tempBlockButtons: boolean
  
  // Player selection
  selectedRole: Role
  onSelectedRoleChange: (role: Role) => void
  loading: boolean
  onShowNextPlayer: () => Promise<void>
  onSearchPlayer: () => void
  
  // Assign player
  auctionState: AuctionState | null
  onAssignPlayer: () => Promise<void>
  
  // Auction management
  auctionStatus: AuctionStatus
  offerTimeoutSeconds: number
  onChangeTimer: (seconds: number) => Promise<void>
  onPauseAuction: () => Promise<void>
  onResumeAuction: () => Promise<void>
  onResetTimer: () => Promise<void>
  onCompleteReset: () => Promise<void>
  onCloseAuction: () => Promise<void>
  onCloseCurrentAuction: () => Promise<void>
  onFinalizeAuction: () => Promise<void>
  onReopenAuction: () => Promise<void>
}

export function AuctioneerPanel({
  currentPlayer,
  currentBid,
  currentBidder,
  currentSubstitute,
  teams,
  selectedTeamForBid,
  onSelectedTeamForBidChange,
  calculateBudgetRemaining,
  auctioneerSelectedSubstitute,
  isRepairAuction,
  onSelectSubstitute,
  areAuctioneerBidButtonsDisabled,
  onIncrementBid,
  tempBlockButtons,
  selectedRole,
  onSelectedRoleChange,
  loading,
  onShowNextPlayer,
  onSearchPlayer,
  auctionState,
  onAssignPlayer,
  auctionStatus,
  offerTimeoutSeconds,
  onChangeTimer,
  onPauseAuction,
  onResumeAuction,
  onResetTimer,
  onCompleteReset,
  onCloseAuction,
  onCloseCurrentAuction,
  onFinalizeAuction,
  onReopenAuction,
}: AuctioneerPanelProps) {
  const media = useMedia()
  
  const renderAuctioneerBidding = () => {
    const bidStatus = areAuctioneerBidButtonsDisabled()
    const isDisabled = !currentPlayer || bidStatus.disabled
    
    return (
      <YStack gap="$3" opacity={!currentPlayer ? 0.5 : 1}>
        {!currentPlayer && (
          <Card padding="$3" backgroundColor="$orange1" borderRadius="$3">
            <Text fontSize="$3" color="$orange11" textAlign="center">
              Nessun giocatore in asta - Seleziona il prossimo giocatore
            </Text>
          </Card>
        )}
        
        <YStack gap="$2">
          <Label htmlFor="team-select" fontSize="$4" color="$orange11">Fai offerta per squadra:</Label>
          <Select id="team-select" value={selectedTeamForBid} onValueChange={onSelectedTeamForBidChange}>
            <Select.Trigger iconAfter={ChevronDown} disabled={!currentPlayer}><Select.Value placeholder="Seleziona Squadra" /></Select.Trigger>
            <Select.Content>
              <Select.Viewport>
                {teams.map((team, idx) => (
                  <Select.Item key={team.value.teamId} index={idx} value={team.value.teamId ?? ''}>
                    <Select.ItemText>{team.value.name} (€{calculateBudgetRemaining(team)})</Select.ItemText>
                  </Select.Item>
                ))}
              </Select.Viewport>
            </Select.Content>
          </Select>
          {selectedTeamForBid && (
            <Card padding="$2" backgroundColor="$orange1" borderRadius="$2">
              <Text fontSize="$3" color="$orange11">Selezionato: {teams.find(t => t.value.teamId === selectedTeamForBid)?.value.name}</Text>
            </Card>
          )}
        </YStack>
        <Separator />
        {isRepairAuction && selectedTeamForBid && (
          <Button 
            icon={Users} 
            size="$5" 
            theme={auctioneerSelectedSubstitute ? "green" : "orange"} 
            variant="outlined" 
            onPress={onSelectSubstitute}
            disabled={!currentPlayer}
          >
            {auctioneerSelectedSubstitute 
              ? `Sostituisce: ${teams.find(t => t.value.teamId === selectedTeamForBid)?.value.players.find(p => p.playerId === auctioneerSelectedSubstitute)?.name || auctioneerSelectedSubstitute}`
              : 'Seleziona giocatore da sostituire'}
          </Button>
        )}
        <Separator />
        <YStack gap="$2">
          <XStack gap="$2">
            <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="orange" onPress={() => onIncrementBid(1, true)} disabled={isDisabled}>
              <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">Incremento</Text>
            </Button>
          </XStack>
          <XStack gap="$2">
            <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="orange" onPress={() => onIncrementBid(2, true)} disabled={isDisabled}>
              <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+2</Text>
            </Button>
            <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="orange" onPress={() => onIncrementBid(3, true)} disabled={isDisabled}>
              <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+3</Text>
            </Button>
            <Button flex={1} size={media.gtSm ? "$6" : "$5"} theme="orange" onPress={() => onIncrementBid(5, true)} disabled={isDisabled}>
              <Text fontSize={media.gtSm ? "$7" : "$5"} fontWeight="700" color="white">+5</Text>
            </Button>
          </XStack>
        </YStack>
        {currentPlayer && bidStatus.reason && (
          <Card padding="$2" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
            <Text fontSize="$3" color="$red11">❌ {bidStatus.reason}</Text>
          </Card>
        )}
      </YStack>
    )
  }

  const renderAuctioneerControls = () => (
    <YStack gap="$3">
      <XStack gap="$2" alignItems="center">
        <Gavel size={20} color="$orange11" />
        <Text fontSize="$5" fontWeight="700" color="$orange11">Pulsantiera Banditore</Text>
      </XStack>
      <Text fontSize="$4" marginTop="$2" fontWeight="600" color="$orange11">Gestione Giocatore</Text>
      <XStack gap="$2">
        <YStack flex={1}>
          <Select value={selectedRole.toString()} onValueChange={(val) => onSelectedRoleChange(Number(val) as Role)}>
            <Select.Trigger iconAfter={ChevronDown}><Select.Value /></Select.Trigger>
            <Select.Content>
              <Select.Viewport>
                {[Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward].map((role, idx) => (
                  <Select.Item key={role} index={idx} value={role.toString()}>
                    <Select.ItemText>{getRoleLabel(role)}</Select.ItemText>
                  </Select.Item>
                ))}
              </Select.Viewport>
            </Select.Content>
          </Select>
        </YStack>
        <Button icon={TrendingUp} theme="orange" flex={1} disabled={loading} onPress={onShowNextPlayer}>
          Prossimo
        </Button>
      </XStack>
      <Button icon={Search} theme="orange" variant="outlined" onPress={onSearchPlayer}>
        Cerca Giocatore
      </Button>
      {currentPlayer && (
        <Button 
          icon={XCircle} 
          theme="red" 
          variant="outlined" 
          onPress={async () => {
            if (Platform.OS === 'web' && !confirm('Vuoi chiudere l\'asta corrente e aprire per chiamata?')) return
            await onCloseCurrentAuction()
          }}
        >
          Chiudi Asta Corrente (Apri a Chiamata)
        </Button>
      )}
      <Separator />
      {currentBidder && currentPlayer && (
        <>
          <Text fontSize="$4" fontWeight="600" color="$orange11">Aggiudica Giocatore</Text>
          <Card padding="$3" backgroundColor="$orange1">
            <YStack gap="$1">
              <Text fontSize="$3" color="$orange11">
                {currentPlayer.name} a {teams.find(t => t.value.owner === currentBidder)?.value.name} per €{currentBid}
              </Text>
              {currentSubstitute && (
                <Text fontSize="$2" color="$orange10" fontStyle="italic">
                  in sostituzione di {(() => {
                    const substitutePlayer = auctionState?.realPlayers.find(p => p.key === currentSubstitute)
                    return substitutePlayer?.name || currentSubstitute
                  })()}
                </Text>
              )}
            </YStack>
          </Card>
          <Button icon={Gavel} theme="green" disabled={loading} onPress={onAssignPlayer}>
            Aggiudica
          </Button>
          <Separator />
        </>
      )}
      <Text fontSize="$4" fontWeight="600" color="$orange11">Controlli Asta</Text>
      <XStack gap="$2" flexWrap="wrap" alignItems="center">
        <YStack gap="$1">
          <Select 
            value={offerTimeoutSeconds.toString()} 
            onValueChange={(val) => onChangeTimer(Number(val))}
          >
            <Select.Trigger width={70} size="$3"><Select.Value /></Select.Trigger>
            <Select.Content>
              <Select.Viewport>
                {[5, 10, 15, 20, 30, 45, 60].map((secs, idx) => (
                  <Select.Item key={secs} index={idx} value={secs.toString()}>
                    <Select.ItemText>{secs}s</Select.ItemText>
                  </Select.Item>
                ))}
              </Select.Viewport>
            </Select.Content>
          </Select>
        </YStack>
        {auctionStatus === AuctionStatus.InProgress ? (
          <Button icon={Pause} theme="orange" flex={1} minWidth={90} size="$3" disabled={loading} onPress={onPauseAuction}>
            Pausa
          </Button>
        ) : (
          <Button icon={Play} theme="green" flex={1} minWidth={90} size="$3" disabled={loading} onPress={onResumeAuction}>
            Riprendi
          </Button>
        )}
        <Button 
          icon={Clock} 
          theme="blue" 
          variant="outlined" 
          flex={1} 
          minWidth={90} 
          size="$3" 
          disabled={!currentPlayer || loading}
          onPress={onResetTimer}
        >
          Riavvia Timer
        </Button>
        <Button 
          icon={RotateCcw} 
          theme="orange" 
          variant="outlined" 
          flex={1} 
          minWidth={90} 
          size="$3" 
          disabled={!currentPlayer || loading}
          onPress={onCompleteReset}
        >
          Reset Completo
        </Button>
      </XStack>
      <Separator />
      {auctionStatus === AuctionStatus.Finished ? (
        <>
          <Text fontSize="$4" fontWeight="600" color="$green11">Asta Finalizzata</Text>
          <Button 
            icon={Unlock} 
            theme="blue" 
            size="$4"
            disabled={loading}
            onPress={async () => {
              if (Platform.OS === 'web' && !confirm('Sei sicuro di voler riaprire l\'asta?')) return
              await onReopenAuction()
            }}
          >
            Riapri Asta
          </Button>
          <Text fontSize="$2" color="$blue10" textAlign="center">
            Riapre l&apos;asta in stato Pausa per poterla modificare e ri-finalizzare
          </Text>
        </>
      ) : (
        <>
          <Text fontSize="$4" fontWeight="600" color="$red11">Finalizza Asta</Text>
          <Button 
            icon={Check} 
            theme="red" 
            size="$4"
            disabled={loading}
            onPress={async () => {
              if (Platform.OS === 'web' && !confirm('Sei sicuro di voler finalizzare l\'asta? Questo chiuderà l\'asta e salverà tutte le squadre con la formazione della prossima giornata.')) return
              await onFinalizeAuction()
            }}
          >
            Finalizza e Salva Tutte le Squadre
          </Button>
          <Text fontSize="$2" color="$red10" textAlign="center">
            ⚠️ Chiude l&apos;asta e salva automaticamente tutte le squadre con la formazione della prossima giornata
          </Text>
        </>
      )}
    </YStack>
  )

  return (
    <Card padding="$4" backgroundColor="$orange2" borderRadius="$4" borderWidth="$0.5" borderColor="$orange8" opacity={tempBlockButtons ? 0.6 : 1}>
      {renderAuctioneerControls()}
      {renderAuctioneerBidding()}
    </Card>
  )
}
