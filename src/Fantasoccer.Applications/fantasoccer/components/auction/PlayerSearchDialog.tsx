import { Dialog, Button, Card, ScrollView, Select, Label, Input, Text, XStack, YStack } from 'tamagui'
import { ChevronDown } from '@tamagui/lucide-icons'
import { Role } from 'types/group'
import { StatPlayer } from 'types/auction'
import { getRoleLabel } from 'utils/auctionHelper'
import { Entity } from 'types/default'
import { Team, TeamKey, PlayerInTeamStatus } from 'types/team'
import { getPlayerKey } from 'utils'

export interface PlayerSearchDialogProps {
  open: boolean
  selectedRole: Role
  playerSearchQuery: string
  selectedPlayerForForce: string
  realPlayers: StatPlayer[]
  teams: Entity<Team, TeamKey>[]
  loading: boolean
  onOpenChange: (open: boolean) => void
  onSelectedRoleChange: (role: Role) => void
  onPlayerSearchQueryChange: (query: string) => void
  onSelectedPlayerForForceChange: (playerName: string) => void
  onConfirm: () => Promise<void>
  onCancel: () => void
}

export function PlayerSearchDialog({
  open,
  selectedRole,
  playerSearchQuery,
  selectedPlayerForForce,
  realPlayers,
  teams,
  loading,
  onOpenChange,
  onSelectedRoleChange,
  onPlayerSearchQueryChange,
  onSelectedPlayerForForceChange,
  onConfirm,
  onCancel,
}: PlayerSearchDialogProps) {
  // Collect all player keys that are already owned by teams (only active players)
  const ownedPlayerKeys = new Set<string>()
  teams.forEach(team => {
    team.value.players.forEach(player => {
      // Only exclude players with Active status (status === 0)
      // Sold/Removed players (status > 0) should still be available for auction
      if (player.status === PlayerInTeamStatus.Active) {
        const playerKey = getPlayerKey(player.name)
        ownedPlayerKeys.add(playerKey)
      }
    })
  })
  
  // Filter players by:
  // 1. Selected role
  // 2. Not already owned by any team (with Active status)
  // 3. Search query (name or real team)
  const filteredPlayers = realPlayers
    .filter(p => p.role === selectedRole)
    .filter(p => !ownedPlayerKeys.has(p.key)) // Exclude players already in teams with Active status
    .filter(p => {
      if (!playerSearchQuery) return true
      const query = playerSearchQuery.toLowerCase()
      return p.name.toLowerCase().includes(query) || 
             p.realTeam.name.toLowerCase().includes(query)
    })
    .sort((a, b) => a.name.localeCompare(b.name))
  
  return (
    <Dialog modal open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay 
          key="overlay" 
          animation="quick" 
          opacity={0.5} 
          enterStyle={{ opacity: 0 }} 
          exitStyle={{ opacity: 0 }} 
        />
        <Dialog.Content 
          bordered 
          elevate 
          key="content" 
          animateOnly={['transform', 'opacity']} 
          animation={['quick', { opacity: { overshootClamping: true } }]} 
          enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} 
          exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} 
          gap="$4" 
          width={600} 
          maxWidth="95%" 
          maxHeight="90%"
        >
          <Dialog.Title fontSize="$6" fontWeight="700">Cerca Giocatore</Dialog.Title>
          <Dialog.Description>
            Seleziona un giocatore specifico per il ruolo: {getRoleLabel(selectedRole)}
          </Dialog.Description>
          
          <YStack gap="$3">
            <YStack gap="$2">
              <Label htmlFor="role-select-search" fontSize="$4">Ruolo:</Label>
              <Select 
                id="role-select-search" 
                value={selectedRole.toString()} 
                onValueChange={(val) => {
                  onSelectedRoleChange(Number(val) as Role)
                  onPlayerSearchQueryChange('')
                  onSelectedPlayerForForceChange('')
                }}
              >
                <Select.Trigger iconAfter={ChevronDown}>
                  <Select.Value />
                </Select.Trigger>
                <Select.Content zIndex={200000}>
                  <Select.Viewport>
                    <Select.Item index={0} value={Role.GoalKeeper.toString()}>
                      <Select.ItemText>Portiere</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={1} value={Role.Defensor.toString()}>
                      <Select.ItemText>Difensore</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={2} value={Role.Midfielder.toString()}>
                      <Select.ItemText>Centrocampista</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={3} value={Role.Forward.toString()}>
                      <Select.ItemText>Attaccante</Select.ItemText>
                    </Select.Item>
                  </Select.Viewport>
                </Select.Content>
              </Select>
            </YStack>
            
            <YStack gap="$2">
              <Label htmlFor="player-search-input" fontSize="$4">Cerca per nome:</Label>
              <Input 
                id="player-search-input"
                placeholder="Scrivi per cercare..." 
                value={playerSearchQuery} 
                onChangeText={onPlayerSearchQueryChange}
                size="$4"
                autoFocus
              />
              <Text fontSize="$2" color="$color10">{filteredPlayers.length} giocatori trovati</Text>
            </YStack>
            
            <ScrollView maxHeight={400}>
              <YStack gap="$2">
                {filteredPlayers.map((player) => (
                  <Card
                    key={player.key}
                    padding="$3"
                    backgroundColor={selectedPlayerForForce === player.key ? '$blue2' : '$color1'}
                    borderRadius="$3"
                    borderWidth="$0.5"
                    borderColor={selectedPlayerForForce === player.key ? '$blue8' : '$borderColor'}
                    pressStyle={{ scale: 0.98, opacity: 0.9 }}
                    cursor="pointer"
                    onPress={() => onSelectedPlayerForForceChange(player.key)}
                  >
                    <XStack gap="$3" alignItems="center" justifyContent="space-between">
                      <YStack flex={1}>
                        <Text fontSize="$4" fontWeight="600" color="$color11">{player.name}</Text>
                        <Text fontSize="$3" color="$color10">{player.realTeam.name}</Text>
                      </YStack>
                      <YStack alignItems="flex-end" gap="$1">
                        <XStack gap="$2" alignItems="center">
                          <Text fontSize="$2" color="$color10">Media:</Text>
                          <Text fontSize="$3" fontWeight="600" color="$color11">{player.average.toFixed(1)}</Text>
                        </XStack>
                        <XStack gap="$2" alignItems="center">
                          <Text fontSize="$2" color="$color10">FM:</Text>
                          <Text fontSize="$3" fontWeight="600" color="$color11">{player.fantaAverage.toFixed(1)}</Text>
                        </XStack>
                      </YStack>
                    </XStack>
                  </Card>
                ))}
                {filteredPlayers.length === 0 && (
                  <Card padding="$4" backgroundColor="$color2">
                    <Text fontSize="$3" color="$color10" textAlign="center">
                      Nessun giocatore trovato
                    </Text>
                  </Card>
                )}
              </YStack>
            </ScrollView>
          </YStack>
          
          <XStack justifyContent="flex-end" gap="$2">
            <Dialog.Close asChild>
              <Button variant="outlined" onPress={onCancel}>
                Annulla
              </Button>
            </Dialog.Close>
            <Button 
              theme="orange" 
              disabled={!selectedPlayerForForce || loading}
              onPress={onConfirm}
            >
              Mostra Prossimo
            </Button>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
