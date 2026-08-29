// RemainingPlayersPanel - Lista giocatori che non sono ancora usciti nell'asta (visibile a tutti)
import { useState, useMemo, useEffect } from 'react'
import { Button, Card, Input, ScrollView, Separator, Text, XStack, YStack } from 'tamagui'
import { List, ChevronDown, ChevronUp, Calendar } from '@tamagui/lucide-icons-2'
import { Role } from 'types/group'
import { AuctionState, StatPlayer } from 'types/auction'
import { Entity } from 'types/default'
import { Team, TeamKey, PlayerInTeamStatus } from 'types/team'
import { getRoleLabel } from 'utils/auctionHelper'
import { auctionService } from 'services/auctionService'
import { getTeamTierInfo } from 'utils/teamTiers'
import { getPlayerKey } from 'utils/keyUtils'

export interface RemainingPlayersPanelProps {
  auctionState: AuctionState | null
  teams: Entity<Team, TeamKey>[]
  selectedRole: Role
  currentPlayer: StatPlayer | null
  isAuctioneer: boolean
  onForceShowPlayer: (playerKey: string) => void
}

export function RemainingPlayersPanel({
  auctionState,
  teams,
  selectedRole,
  currentPlayer,
  isAuctioneer,
  onForceShowPlayer,
}: RemainingPlayersPanelProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [isExpanded, setIsExpanded] = useState(false)
  const [discardedPlayers, setDiscardedPlayers] = useState<StatPlayer[]>([])
  const [loading, setLoading] = useState(false)

  // Carica i giocatori scartati dal server
  useEffect(() => {
    const loadDiscardedPlayers = async () => {
      if (!auctionState) return
      
      setLoading(true)
      try {
        const players = await auctionService.getDiscardedPlayers(auctionState.wrapper.id, selectedRole)
        setDiscardedPlayers(players)
      } catch (error) {
        console.error('❌ Error loading discarded players:', error)
        setDiscardedPlayers([])
      } finally {
        setLoading(false)
      }
    }

    loadDiscardedPlayers()
  }, [auctionState?.wrapper.id, selectedRole, auctionState?.wrapper.winnings.length])

  // Calcola giocatori rimanenti
  const remainingPlayers = useMemo((): StatPlayer[] => {
    if (!auctionState) return []

    // Tutti i giocatori del ruolo corrente
    const allPlayersForRole = auctionState.realPlayers.filter(p => p.role === selectedRole)

    // TUTTI i giocatori nei team (qualsiasi status) - normalizza nome in key
    const playersInTeamsKeys = new Set(
      teams.flatMap(team =>
        team.value.players
          .filter(p => p.role === selectedRole)
          .map(p => getPlayerKey(p.name))
      )
    )

    // Giocatori scartati (dal server) - usa key diretta
    const discardedPlayerKeys = new Set(discardedPlayers.map(p => p.key))

    // Filtra giocatori rimanenti: tutti tranne quelli già gestiti
    // Formula: allPlayers - (playersInTeams + discardedPlayers)
    const remaining = allPlayersForRole.filter(
      player =>
        !playersInTeamsKeys.has(player.key) &&
        !discardedPlayerKeys.has(player.key) &&
        player.key !== currentPlayer?.key // Escludi il giocatore corrente in asta
    )

    // Ordina per: withVote DESC, fantaAverage DESC
    return remaining.sort((a, b) => {
      const withVoteA = a.withVote || 0
      const withVoteB = b.withVote || 0
      if (withVoteB !== withVoteA) {
        return withVoteB - withVoteA // Più partite prima
      }
      const fantaAvgA = a.fantaAverage || 0
      const fantaAvgB = b.fantaAverage || 0
      return fantaAvgB - fantaAvgA // Migliore fantamedia prima
    })
  }, [auctionState, teams, selectedRole, discardedPlayers, currentPlayer])

  // Applica filtro di ricerca
  const filteredPlayers = useMemo(() => {
    if (!searchTerm.trim()) return remainingPlayers
    
    const searchLower = searchTerm.toLowerCase()
    return remainingPlayers.filter(p =>
      p.name.toLowerCase().includes(searchLower) ||
      p.realTeam?.name?.toLowerCase().includes(searchLower)
    )
  }, [remainingPlayers, searchTerm])

  if (!auctionState) return null

  return (
    <YStack gap="$2">
      <Separator />
      <XStack gap="$2" alignItems="center" pressStyle={{ opacity: 0.7 }} onPress={() => setIsExpanded(!isExpanded)}>
        <List size={20} color="$blue11" />
        <Text fontSize="$4" fontWeight="600" color="$blue11" flex={1}>
          Giocatori Rimanenti ({filteredPlayers.length})
        </Text>
        {isExpanded ? <ChevronUp size={20} color="$blue11" /> : <ChevronDown size={20} color="$blue11" />}
      </XStack>
      {isExpanded && (
        <>
          <Text fontSize="$2" color="$blue10">
            {getRoleLabel(selectedRole)} che non sono ancora usciti nell&apos;asta
          </Text>
          <Input
            placeholder="Cerca giocatore rimanente..."
            value={searchTerm}
            onChangeText={setSearchTerm}
            size="$3"
          />
          {filteredPlayers.length === 0 ? (
            <Card padding="$3" backgroundColor="$blue1">
              <Text fontSize="$3" color="$blue11" textAlign="center">
                {searchTerm ? 'Nessun risultato' : loading ? 'Caricamento...' : 'Tutti i giocatori di questo ruolo sono già usciti'}
              </Text>
            </Card>
          ) : (
            <YStack borderWidth="$0.5" borderColor="$blue8" borderRadius="$3" backgroundColor="$blue1" maxHeight={400}>
              <ScrollView>
                <YStack gap="$1" padding="$2">
                  {filteredPlayers.map((player, idx) => {
                    const tierInfo = getTeamTierInfo(player.realTeam?.name)
                    
                    return (
                      <Button
                        key={`${player.key}-${idx}`}
                        size="$2"
                        backgroundColor="$blue2"
                        borderWidth="$0.5"
                        borderColor="$blue7"
                        disabled={!isAuctioneer}
                        onPress={() => onForceShowPlayer(player.key)}
                        pressStyle={{ backgroundColor: '$blue3' }}
                        padding="$2"
                        height="auto"
                      >
                        <XStack gap="$2" alignItems="center" justifyContent="space-between" width="100%">
                          {/* Nome giocatore e team */}
                          <XStack gap="$1" alignItems="center" flex={1}>
                            <Text fontSize="$3" color="$blue12" fontWeight="600" numberOfLines={1}>
                              {player.name}
                            </Text>
                            <Text fontSize="$2" color="$blue10" numberOfLines={1}>
                              ({player.realTeam?.name || ''})
                            </Text>
                          </XStack>

                          {/* Team tier badge */}
                          <Card 
                            paddingVertical="$0.5" 
                            paddingHorizontal="$1.5" 
                            backgroundColor={tierInfo.backgroundColor}
                            borderRadius="$2"
                            borderWidth={1}
                            borderColor={tierInfo.color}
                          >
                            <Text fontSize="$1" fontWeight="700" color={tierInfo.color}>
                              {tierInfo.label}
                            </Text>
                          </Card>

                          {/* Statistiche compatte */}
                          <XStack gap="$2" alignItems="center">
                            <XStack gap="$1" alignItems="center">
                              <Calendar size={12} color="$blue10" />
                              <Text fontSize="$2" color="$blue11" fontWeight="600">
                                {player.withVote || 0}
                              </Text>
                            </XStack>
                            <Text fontSize="$2" color="$blue10">
                              M: {player.average?.toFixed(1) || '0.0'}
                            </Text>
                            <Text fontSize="$2" color="$blue10" fontWeight="600">
                              FM: {player.fantaAverage?.toFixed(1) || '0.0'}
                            </Text>
                          </XStack>

                          {/* Eventi compatti */}
                          <XStack gap="$1.5" alignItems="center" flexWrap="wrap">
                            {(player.goals || 0) > 0 && (
                              <Text fontSize="$1" color="$green11" fontWeight="600">⚽{player.goals}</Text>
                            )}
                            {(player.assists || 0) > 0 && (
                              <Text fontSize="$1" color="$blue11" fontWeight="600">🎯{player.assists}</Text>
                            )}
                            {(player.penalties || 0) > 0 && (
                              <Text fontSize="$1" color="$purple11" fontWeight="600">🎯R{player.penalties}</Text>
                            )}
                            {(player.stoppedPenalties || 0) > 0 && (
                              <Text fontSize="$1" color="$orange11" fontWeight="600">🧤{player.stoppedPenalties}</Text>
                            )}
                            {(player.yellowCards || 0) > 0 && (
                              <Text fontSize="$1" color="$yellow11" fontWeight="600">🟨{player.yellowCards}</Text>
                            )}
                            {(player.redCards || 0) > 0 && (
                              <Text fontSize="$1" color="$red11" fontWeight="600">🟥{player.redCards}</Text>
                            )}
                          </XStack>
                        </XStack>
                      </Button>
                    )
                  })}
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
