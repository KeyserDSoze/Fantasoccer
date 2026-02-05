// DiscardedPlayersPanel - Lista giocatori scartati durante l'asta (visibile a tutti)
import { useState, useMemo, useEffect } from 'react'
import { Button, Card, Input, ScrollView, Separator, Text, XStack, YStack } from 'tamagui'
import { List, ChevronDown, ChevronUp, Calendar, UserX } from '@tamagui/lucide-icons'
import { Role } from 'types/group'
import { AuctionState, StatPlayer } from 'types/auction'
import { Entity } from 'types/default'
import { Team, TeamKey, PlayerInTeamStatus } from 'types/team'
import { getRoleLabel } from 'utils/auctionHelper'
import { auctionService } from 'services/auctionService'

type DiscardedPlayer = StatPlayer & {
  discardedByTeam?: string // Nome del team che ha scartato il giocatore
}

export interface DiscardedPlayersPanelProps {
  auctionState: AuctionState | null
  teams: Entity<Team, TeamKey>[]
  selectedRole: Role
  currentPlayer: StatPlayer | null
  isAuctioneer: boolean
  onForceShowPlayer: (playerKey: string) => void
}

export function DiscardedPlayersPanel({
  auctionState,
  teams,
  selectedRole,
  currentPlayer,
  isAuctioneer,
  onForceShowPlayer,
}: DiscardedPlayersPanelProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [isExpanded, setIsExpanded] = useState(false)
  const [shownPlayers, setShownPlayers] = useState<StatPlayer[]>([])
  const [loading, setLoading] = useState(false)

  // Carica i giocatori mostrati dal server quando cambia il ruolo o l'asta
  useEffect(() => {
    const loadShownPlayers = async () => {
      if (!auctionState) return
      
      setLoading(true)
      try {
        const players = await auctionService.getDiscardedPlayers(auctionState.wrapper.id, selectedRole)
        setShownPlayers(players)
      } catch (error) {
        console.error('❌ Error loading discarded players:', error)
        setShownPlayers([])
      } finally {
        setLoading(false)
      }
    }

    loadShownPlayers()
  }, [auctionState?.wrapper.id, selectedRole, auctionState?.wrapper.winnings.length])

  // Calcola giocatori scartati con info sul team
  const discardedPlayers = useMemo((): DiscardedPlayer[] => {
    if (!auctionState) return []

    // Giocatori dal server (già inclusi sia shown che removed)
    const players: DiscardedPlayer[] = shownPlayers.map(player => {
      // Cerca se è stato rimosso o venduto senza prezzo da qualche team
      // Usiamo il nome per il match perché Player non ha la proprietà 'key'
      const discardedByTeam = teams.find(team =>
        team.value.players.some(p =>
          p.name === player.name &&
          (p.status === PlayerInTeamStatus.Removed || p.status === PlayerInTeamStatus.SoldWithNoReturnedPrice) &&
          p.role === selectedRole
        )
      )

      return {
        ...player,
        discardedByTeam: discardedByTeam?.value.name
      }
    })

    // Ordina per: withVote DESC, fantaAverage DESC
    return players.sort((a, b) => {
      const withVoteA = a.withVote || 0
      const withVoteB = b.withVote || 0
      if (withVoteB !== withVoteA) {
        return withVoteB - withVoteA // Più partite prima
      }
      const fantaAvgA = a.fantaAverage || 0
      const fantaAvgB = b.fantaAverage || 0
      return fantaAvgB - fantaAvgA // Migliore fantamedia prima
    })
  }, [auctionState, teams, selectedRole, shownPlayers])

  // Applica filtro di ricerca separatamente
  const filteredPlayers = useMemo(() => {
    if (!searchTerm.trim()) return discardedPlayers
    
    const searchLower = searchTerm.toLowerCase()
    return discardedPlayers.filter(p =>
      p.name.toLowerCase().includes(searchLower) ||
      p.realTeam?.name?.toLowerCase().includes(searchLower)
    )
  }, [discardedPlayers, searchTerm])

  if (!auctionState) return null

  return (
    <YStack gap="$2">
      <Separator />
      <XStack gap="$2" alignItems="center" pressStyle={{ opacity: 0.7 }} onPress={() => setIsExpanded(!isExpanded)}>
        <List size={20} color="$purple11" />
        <Text fontSize="$4" fontWeight="600" color="$purple11" flex={1}>
          Giocatori Scartati ({filteredPlayers.length})
        </Text>
        {isExpanded ? <ChevronUp size={20} color="$purple11" /> : <ChevronDown size={20} color="$purple11" />}
      </XStack>
      {isExpanded && (
        <>
          <Text fontSize="$2" color="$purple10">
            {getRoleLabel(selectedRole)} già usciti ma non acquistati o rimossi dai team
          </Text>
          <Input
            placeholder="Cerca giocatore scartato..."
            value={searchTerm}
            onChangeText={setSearchTerm}
            size="$3"
          />
          {filteredPlayers.length === 0 ? (
            <Card padding="$3" backgroundColor="$purple1">
              <Text fontSize="$3" color="$purple11" textAlign="center">
                {searchTerm ? 'Nessun risultato' : loading ? 'Caricamento...' : 'Nessun giocatore scartato per questo ruolo'}
              </Text>
            </Card>
          ) : (
            <YStack borderWidth="$0.5" borderColor="$purple8" borderRadius="$3" backgroundColor="$purple1" maxHeight={400}>
              <ScrollView>
                <YStack gap="$2" padding="$2">
                  {filteredPlayers.map((player, idx) => (
                    <Card
                      key={`${player.key}-${idx}`}
                      padding="$2"
                      backgroundColor="$purple3"
                      borderWidth="$0.5"
                      borderColor="$purple8"
                      pressStyle={{ backgroundColor: '$purple4' }}
                    >
                      <YStack gap="$1.5">
                        {/* Nome e team reale */}
                        <Button
                          size="$3"
                          backgroundColor="transparent"
                          disabled={!isAuctioneer}
                          onPress={() => onForceShowPlayer(player.key)}
                          pressStyle={{ opacity: 0.8 }}
                          padding="$0"
                        >
                          <XStack gap="$2" flex={1} justifyContent="flex-start" width="100%">
                            <Text fontSize="$3" color="$purple12" fontWeight="600">{player.name}</Text>
                            <Text fontSize="$2" color="$purple10">({player.realTeam?.name || ''})</Text>
                          </XStack>
                        </Button>
                        
                        {/* Team che ha scartato */}
                        {player.discardedByTeam && (
                          <XStack gap="$1.5" alignItems="center" backgroundColor="$red2" padding="$1.5" borderRadius="$2">
                            <UserX size={14} color="$red10" />
                            <Text fontSize="$2" color="$red11" fontWeight="600">
                              Scartato da: {player.discardedByTeam}
                            </Text>
                          </XStack>
                        )}
                        
                        {/* Statistiche partite */}
                        <XStack gap="$3" flexWrap="wrap">
                          <XStack gap="$1" alignItems="center">
                            <Calendar size={12} color="$purple10" />
                            <Text fontSize="$2" color="$purple10">
                              {player.withVote || 0} partite
                            </Text>
                          </XStack>
                          <Text fontSize="$2" color="$purple10">
                            Media: {player.average?.toFixed(2) || '0.00'}
                          </Text>
                          <Text fontSize="$2" color="$purple10">
                            FantaMedia: {player.fantaAverage?.toFixed(2) || '0.00'}
                          </Text>
                        </XStack>
                        
                        {/* Eventi compatti */}
                        <XStack gap="$1.5" alignItems="center" flexWrap="wrap">
                          {(player.goals || 0) > 0 && (
                            <Text fontSize="$2" color="$green11" fontWeight="600">⚽{player.goals}</Text>
                          )}
                          {(player.assists || 0) > 0 && (
                            <Text fontSize="$2" color="$blue11" fontWeight="600">🎯{player.assists}</Text>
                          )}
                          {(player.penalties || 0) > 0 && (
                            <Text fontSize="$2" color="$purple11" fontWeight="600">🎯R{player.penalties}</Text>
                          )}
                          {(player.stoppedPenalties || 0) > 0 && (
                            <Text fontSize="$2" color="$orange11" fontWeight="600">🧤{player.stoppedPenalties}</Text>
                          )}
                          {(player.yellowCards || 0) > 0 && (
                            <Text fontSize="$2" color="$yellow11" fontWeight="600">🟨{player.yellowCards}</Text>
                          )}
                          {(player.redCards || 0) > 0 && (
                            <Text fontSize="$2" color="$red11" fontWeight="600">🟥{player.redCards}</Text>
                          )}
                        </XStack>
                      </YStack>
                    </Card>
                  ))}
                </YStack>
              </ScrollView>
            </YStack>
          )}
          {!isAuctioneer && (
            <Text fontSize="$2" color="$gray10" textAlign="center" fontStyle="italic">
              Solo il banditore può forzare la visualizzazione di un giocatore
            </Text>
          )}
        </>
      )}
    </YStack>
  )
}
