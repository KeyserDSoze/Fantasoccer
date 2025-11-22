import React, { useMemo } from 'react'
import { Dialog, YStack, XStack, Text, Button, ScrollView, Sheet } from 'tamagui'
import { X, ArrowLeftRight, Plus, AlertTriangle, Ban, TrendingUp, TrendingDown, Minus } from '@tamagui/lucide-icons'
import { Dimensions } from 'react-native'
import type { LivePlayer } from 'types/game'
import { FantaSoccerRole } from 'types/team'
import { Role } from 'types/group'
import { PlayerCard } from './PlayerCard'
import { getAvailableFieldPosition, getAvailableBenchPosition } from '../../utils/teamValidation'
import { ChanceType } from '../../types/chance'
import type { StatPlayer } from '../../types/stat'
import { calculatePlayerStats } from '../../utils/statPlayerCalculator'
import { getPlayerKey } from '../../utils/keyUtils'

interface PlayerSwapModalProps {
  isOpen: boolean
  onClose: () => void
  selectedPlayer: LivePlayer | null
  allPlayers: LivePlayer[]
  onSwap: (targetPlayer: LivePlayer) => void
  onRemove?: () => void // Opzione per mandare in tribuna (se permesso)
  canRemove: boolean
  onAddToField?: (player: LivePlayer) => void // Aggiunge giocatore dalla tribuna/panchina in campo
  onAddToBench?: (player: LivePlayer) => void // Aggiunge giocatore dalla tribuna in panchina
  statPlayersMap?: Map<string, StatPlayer> // Mappa per statistiche giocatori
}

const ROLE_LABELS: Record<Role, string> = {
  [Role.GoalKeeper]: 'Portiere',
  [Role.Defensor]: 'Difensore',
  [Role.Midfielder]: 'Centrocampista',
  [Role.Forward]: 'Attaccante',
  [Role.Undefined]: 'Non definito'
}

export function PlayerSwapModal({
  isOpen,
  onClose,
  selectedPlayer,
  allPlayers,
  onSwap,
  onRemove,
  canRemove,
  onAddToField,
  onAddToBench,
  statPlayersMap
}: PlayerSwapModalProps) {
  // Filtra giocatori papabili per ruolo (stesso ruolo principale)
  const eligiblePlayers = useMemo(() => {
    if (!selectedPlayer) return []
    
    const selectedRole = selectedPlayer.current?.role ?? Role.Undefined
    const selectedPosition = selectedPlayer.currentPosition
    
    return allPlayers.filter(p => {
      // Escludi il giocatore selezionato stesso
      if (p.current.name === selectedPlayer.current.name) return false

      // Escludi giocatori con la stessa posizione esatta (FantaSoccerRole)
      // Es: se seleziono un Midfielder titolare (pos=2), escludo gli altri Midfielder titolari
      if (p.currentPosition === selectedPosition) return false
      
      // Stesso ruolo principale (per permettere scambi tra titolare/panchina/tribuna dello stesso ruolo)
      const pRole = p.current?.role ?? Role.Undefined
      return pRole === selectedRole
    })
  }, [selectedPlayer, allPlayers])

  // Determina se il giocatore selezionato è in tribuna o panchina (quindi può essere aggiunto in campo/panchina)
  const selectedIsInTribune = selectedPlayer?.currentPosition === FantaSoccerRole.Tribune
  const selectedIsInBench = selectedPlayer && 
    selectedPlayer.currentPosition >= FantaSoccerRole.BackupGoalKeeper && 
    selectedPlayer.currentPosition <= FantaSoccerRole.SecondBackupForward
  
  // Calcola se ci sono spazi disponibili in campo e panchina per il ruolo del giocatore selezionato
  const selectedRole = (selectedPlayer?.current as any)?.role ?? Role.Undefined
  const availableFieldPosition = selectedPlayer ? getAvailableFieldPosition(selectedRole, allPlayers) : undefined
  const availableBenchPosition = selectedPlayer ? getAvailableBenchPosition(selectedRole, allPlayers) : undefined
  
  const canAddToFieldFromTribune = selectedIsInTribune && availableFieldPosition !== undefined
  const canAddToBenchFromTribune = selectedIsInTribune && availableBenchPosition !== undefined
  const canAddToFieldFromBench = selectedIsInBench && availableFieldPosition !== undefined

  // Raggruppa per categoria (titolari, panchina, tribuna)
  const groupedPlayers = useMemo(() => {
    const starters: LivePlayer[] = []
    const bench: LivePlayer[] = []
    const tribune: LivePlayer[] = []

    eligiblePlayers.forEach(p => {
      const pos = p.currentPosition
      if (pos >= FantaSoccerRole.GoalKeeper && pos <= FantaSoccerRole.Forward) {
        starters.push(p)
      } else if (pos >= FantaSoccerRole.BackupGoalKeeper && pos <= FantaSoccerRole.SecondBackupForward) {
        bench.push(p)
      } else if (pos === FantaSoccerRole.Tribune) {
        tribune.push(p)
      }
    })

    // Ordina gli array per posizione (enum value crescente)
    starters.sort((a, b) => a.currentPosition - b.currentPosition)
    bench.sort((a, b) => a.currentPosition - b.currentPosition)

    return { starters, bench, tribune }
  }, [eligiblePlayers])

  const selectedPlayerName = (selectedPlayer?.current as any)?.name ?? 'Giocatore'
  const selectedPlayerRole = (selectedPlayer?.current as any)?.role ?? Role.Undefined
  const selectedRoleLabel = ROLE_LABELS[selectedPlayerRole as Role] ?? 'Non definito'
  const selectedPlayerPrice = (selectedPlayer?.current as any)?.price ?? null
  const screenHeight = Dimensions.get('window').height

  // Estrai informazioni chance del giocatore selezionato
  const selectedPlayerChance = selectedPlayer?.chance
  
  // Calcola statistiche da StatPlayer per il giocatore selezionato
  const selectedPlayerStats = useMemo(() => {
    if (!selectedPlayer || !statPlayersMap) return null
    const playerName = (selectedPlayer.current as any)?.name ?? ''
    const playerKey = getPlayerKey(playerName)
    const statPlayer = statPlayersMap.get(playerKey)
    return statPlayer ? calculatePlayerStats(statPlayer) : null
  }, [selectedPlayer, statPlayersMap])
  
  // Determina le informazioni da mostrare
  const playerInfo = useMemo(() => {
    if (!selectedPlayer) return null
    
    const chance = selectedPlayerChance
    
    // Infortunio
    const hasInjury = chance?.status === ChanceType.Injury
    const injuryDescription = chance?.description || 'Infortunato'
    
    // Squalifica
    const isSuspended = chance?.status === ChanceType.Disqualified
    const suspensionDescription = chance?.description || 'Squalificato'
    
    // Probabilità titolare
    const fantagazzettaStarter = chance?.fantagazzetta
    const gazzettaStarter = chance?.gazzetta
    
    // Trend e lastGame ora vengono da selectedPlayerStats (StatPlayer)
    const trend = selectedPlayerStats?.trend ?? null
    const trendLabel = selectedPlayerStats?.trendVisual?.label ?? null
    const trendArrow = selectedPlayerStats?.trendVisual?.arrow ?? null
    const lastGamePositiveness = selectedPlayerStats?.lastGame?.positiveness ?? null
    
    return {
      hasInjury,
      injuryDescription,
      isSuspended,
      suspensionDescription,
      fantagazzettaStarter,
      gazzettaStarter,
      trend,
      trendLabel,
      trendArrow,
      lastGamePositiveness
    }
  }, [selectedPlayer, selectedPlayerChance, selectedPlayerStats])

  return (
    <Sheet
      modal
      open={isOpen}
      onOpenChange={onClose}
      dismissOnSnapToBottom
      snapPoints={[85]}
      position={0}
      zIndex={100000}
    >
      <Sheet.Overlay backgroundColor="rgba(0,0,0,0.7)" animation="quick" enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
      <Sheet.Frame 
        padding={0} 
        backgroundColor="$background" 
        borderTopLeftRadius={20} 
        borderTopRightRadius={20}
        maxHeight={screenHeight * 0.85}
      >
        <Sheet.Handle backgroundColor="$gray8" marginVertical={8} />
        
        <YStack flex={1} paddingHorizontal={16} paddingBottom={16}>
          {/* Header Fisso */}
          <YStack gap={12} paddingBottom={12} borderBottomWidth={1} borderBottomColor="$borderColor">
            <XStack justifyContent="space-between" alignItems="center">
              <YStack gap={4} flex={1}>
                <Text fontSize={20} fontWeight="800" color="$color">
                  {selectedIsInTribune || selectedIsInBench ? 'Aggiungi Giocatore' : 'Scambia Giocatore'}
                </Text>
                <XStack gap={8} alignItems="center">
                  <Text fontSize={14} color="$gray11">
                    {selectedPlayerName} • {selectedRoleLabel}
                  </Text>
                  {selectedPlayerPrice !== null && (
                    <XStack gap={4} alignItems="center" paddingHorizontal={8} paddingVertical={2} backgroundColor="$green3" borderRadius={6}>
                      <Text fontSize={13} fontWeight="700" color="$green11">
                        💰 {selectedPlayerPrice}
                      </Text>
                    </XStack>
                  )}
                </XStack>
              </YStack>
              <Button
                circular
                size="$4"
                onPress={onClose}
                chromeless
                icon={<X size={24} />}
              />
            </XStack>

            {/* Informazioni dettagliate giocatore */}
            {playerInfo && (
              <YStack gap={8} backgroundColor="$color2" padding={12} borderRadius={12}>
                {/* Infortunio */}
                {playerInfo.hasInjury && (
                  <XStack gap={8} alignItems="center" backgroundColor="$red2" padding={8} borderRadius={8}>
                    <AlertTriangle size={18} color="#dc2626" />
                    <YStack flex={1}>
                      <Text fontSize={12} fontWeight="700" color="$red11">
                        INFORTUNATO
                      </Text>
                      <Text fontSize={11} color="$red10">
                        {playerInfo.injuryDescription}
                      </Text>
                    </YStack>
                  </XStack>
                )}

                {/* Squalifica */}
                {playerInfo.isSuspended && (
                  <XStack gap={8} alignItems="center" backgroundColor="$red2" padding={8} borderRadius={8}>
                    <Ban size={18} color="#dc2626" />
                    <YStack flex={1}>
                      <Text fontSize={12} fontWeight="700" color="$red11">
                        SQUALIFICATO
                      </Text>
                      <Text fontSize={11} color="$red10">
                        {playerInfo.suspensionDescription}
                      </Text>
                    </YStack>
                  </XStack>
                )}

                {/* Probabilità titolare */}
                {(playerInfo.fantagazzettaStarter !== undefined || playerInfo.gazzettaStarter !== undefined) && (
                  <YStack gap={6} backgroundColor="$blue2" padding={8} borderRadius={8}>
                    <Text fontSize={12} fontWeight="700" color="$blue11">
                      📊 PROBABILITÀ TITOLARE
                    </Text>
                    <XStack gap={12}>
                      {playerInfo.fantagazzettaStarter !== undefined && (
                        <XStack gap={4} alignItems="center" flex={1}>
                          <Text fontSize={11} color="$blue10" fontWeight="600">
                            Fantacalcio.it:
                          </Text>
                          <Text 
                            fontSize={11} 
                            fontWeight="700" 
                            color={playerInfo.fantagazzettaStarter ? "$green11" : "$red11"}
                          >
                            {playerInfo.fantagazzettaStarter ? '✓ Titolare' : '✗ Panchina'}
                          </Text>
                        </XStack>
                      )}
                      {playerInfo.gazzettaStarter !== undefined && (
                        <XStack gap={4} alignItems="center" flex={1}>
                          <Text fontSize={11} color="$blue10" fontWeight="600">
                            Gazzetta:
                          </Text>
                          <Text 
                            fontSize={11} 
                            fontWeight="700" 
                            color={playerInfo.gazzettaStarter ? "$green11" : "$red11"}
                          >
                            {playerInfo.gazzettaStarter ? '✓ Titolare' : '✗ Panchina'}
                          </Text>
                        </XStack>
                      )}
                    </XStack>
                  </YStack>
                )}

                {/* Forma recente (trend) */}
                {playerInfo.trend !== null && (
                  <XStack 
                    gap={8} 
                    alignItems="center" 
                    backgroundColor={
                      playerInfo.trend >= 3 ? "$green2" : 
                      playerInfo.trend <= 1 ? "$red2" : 
                      "$yellow2"
                    } 
                    padding={8} 
                    borderRadius={8}
                  >
                    {playerInfo.trend >= 3 ? (
                      <TrendingUp size={18} color="#16a34a" />
                    ) : playerInfo.trend <= 1 ? (
                      <TrendingDown size={18} color="#dc2626" />
                    ) : (
                      <Minus size={18} color="#eab308" />
                    )}
                    <YStack flex={1}>
                      <Text fontSize={12} fontWeight="700" color={
                        playerInfo.trend >= 3 ? "$green11" : 
                        playerInfo.trend <= 1 ? "$red11" : 
                        "$yellow11"
                      }>
                        {playerInfo.trendArrow} FORMA RECENTE: {playerInfo.trendLabel?.toUpperCase()}
                      </Text>
                      <Text fontSize={11} color={
                        playerInfo.trend >= 3 ? "$green10" : 
                        playerInfo.trend <= 1 ? "$red10" : 
                        "$yellow10"
                      }>
                        {playerInfo.trend >= 3 ? 'Sta giocando molto bene nelle ultime partite' : 
                         playerInfo.trend <= 1 ? 'Prestazioni negative nelle ultime partite' : 
                         'Prestazioni altalenanti nelle ultime partite'}
                      </Text>
                    </YStack>
                  </XStack>
                )}

                {/* Ultima partita (positiveness) */}
                {playerInfo.lastGamePositiveness !== undefined && playerInfo.lastGamePositiveness !== null && (
                  <XStack 
                    gap={8} 
                    alignItems="center" 
                    backgroundColor={
                      playerInfo.lastGamePositiveness > 0 ? "$green2" : 
                      playerInfo.lastGamePositiveness < 0 ? "$red2" : 
                      "$gray2"
                    } 
                    padding={8} 
                    borderRadius={8}
                  >
                    <YStack flex={1}>
                      <Text fontSize={12} fontWeight="700" color={
                        playerInfo.lastGamePositiveness > 0 ? "$green11" : 
                        playerInfo.lastGamePositiveness < 0 ? "$red11" : 
                        "$gray11"
                      }>
                        ⚽ ULTIMA PARTITA
                      </Text>
                      <Text fontSize={11} color={
                        playerInfo.lastGamePositiveness > 0 ? "$green10" : 
                        playerInfo.lastGamePositiveness < 0 ? "$red10" : 
                        "$gray10"
                      }>
                        Valore: {playerInfo.lastGamePositiveness > 0 ? '+' : ''}{playerInfo.lastGamePositiveness} • 
                        {playerInfo.lastGamePositiveness > 0 ? ' Ha fatto bonus!' : 
                         playerInfo.lastGamePositiveness < 0 ? ' Ha preso malus' : 
                         ' Prestazione neutra'}
                      </Text>
                    </YStack>
                  </XStack>
                )}
              </YStack>
            )}

            {/* Pulsanti azione diretta per tribuna/panchina */}
            <YStack gap={8}>
              {canAddToFieldFromTribune && onAddToField && (
                <Button
                  backgroundColor="$green10"
                  color="#fff"
                  size="$4"
                  fontWeight="700"
                  onPress={() => {
                    onAddToField(selectedPlayer!)
                    onClose()
                  }}
                  borderRadius={12}
                  pressStyle={{ opacity: 0.8, scale: 0.98 }}
                  icon={<Plus size={20} />}
                >
                  ⚽ Aggiungi in Campo
                </Button>
              )}
              
              {canAddToBenchFromTribune && onAddToBench && (
                <Button
                  backgroundColor="$orange10"
                  color="#fff"
                  size="$4"
                  fontWeight="700"
                  onPress={() => {
                    onAddToBench(selectedPlayer!)
                    onClose()
                  }}
                  borderRadius={12}
                  pressStyle={{ opacity: 0.8, scale: 0.98 }}
                  icon={<Plus size={20} />}
                >
                  🪑 Aggiungi in Panchina
                </Button>
              )}
              
              {canAddToFieldFromBench && onAddToField && (
                <Button
                  backgroundColor="$green10"
                  color="#fff"
                  size="$4"
                  fontWeight="700"
                  onPress={() => {
                    onAddToField(selectedPlayer!)
                    onClose()
                  }}
                  borderRadius={12}
                  pressStyle={{ opacity: 0.8, scale: 0.98 }}
                  icon={<Plus size={20} />}
                >
                  ⚽ Porta in Campo
                </Button>
              )}

              {/* Opzione rimozione (tribuna) */}
              {canRemove && onRemove && (
                <Button
                  backgroundColor="$red10"
                  color="#fff"
                  size="$4"
                  fontWeight="700"
                  onPress={() => {
                    onRemove()
                    onClose()
                  }}
                  borderRadius={12}
                  pressStyle={{ opacity: 0.8, scale: 0.98 }}
                >
                  🏟️ Manda in Tribuna
                </Button>
              )}
            </YStack>
          </YStack>

          {/* Lista giocatori papabili - Scrollabile */}
          <ScrollView 
            showsVerticalScrollIndicator={false}
            contentContainerStyle={{ paddingTop: 16 }}
          >
            <YStack gap={20} paddingBottom={16}>
              {/* Titolari */}
              {groupedPlayers.starters.length > 0 && (
                <YStack gap={12}>
                  <XStack 
                    backgroundColor="$green2" 
                    paddingHorizontal={12} 
                    paddingVertical={8} 
                    borderRadius={8}
                    alignItems="center"
                    gap={8}
                  >
                    <Text fontSize={15} fontWeight="700" color="$green11">
                      ⭐ TITOLARI
                    </Text>
                    <Text fontSize={13} color="$green10" fontWeight="600">
                      ({groupedPlayers.starters.length})
                    </Text>
                  </XStack>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <XStack gap={12} paddingVertical={8} paddingHorizontal={4}>
                      {groupedPlayers.starters.map((player, i) => (
                        <Button
                          key={`starter-${i}`}
                          padding={0}
                          backgroundColor="transparent"
                          borderWidth={0}
                          onPress={() => {
                            onSwap(player)
                            onClose()
                          }}
                          pressStyle={{ opacity: 0.7, scale: 0.95 }}
                          height={200}
                        >
                          <YStack gap={6} alignItems="center" width={120}>
                            <PlayerCard player={player} statPlayersMap={statPlayersMap} />
                            <XStack 
                              backgroundColor="$green10" 
                              paddingHorizontal={10} 
                              paddingVertical={6} 
                              borderRadius={8}
                              alignItems="center"
                              gap={4}
                              width="100%"
                              justifyContent="center"
                            >
                              <ArrowLeftRight size={14} color="#fff" />
                              <Text fontSize={12} fontWeight="700" color="#fff">
                                SCAMBIA
                              </Text>
                            </XStack>
                          </YStack>
                        </Button>
                      ))}
                    </XStack>
                  </ScrollView>
                </YStack>
              )}

              {/* Panchina */}
              {groupedPlayers.bench.length > 0 && (
                <YStack gap={12}>
                  <XStack 
                    backgroundColor="$orange2" 
                    paddingHorizontal={12} 
                    paddingVertical={8} 
                    borderRadius={8}
                    alignItems="center"
                    gap={8}
                  >
                    <Text fontSize={15} fontWeight="700" color="$orange11">
                      🪑 PANCHINA
                    </Text>
                    <Text fontSize={13} color="$orange10" fontWeight="600">
                      ({groupedPlayers.bench.length})
                    </Text>
                  </XStack>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <XStack gap={12} paddingVertical={8} paddingHorizontal={4}>
                      {groupedPlayers.bench.map((player, i) => (
                        <Button
                          key={`bench-${i}`}
                          padding={0}
                          backgroundColor="transparent"
                          borderWidth={0}
                          onPress={() => {
                            onSwap(player)
                            onClose()
                          }}
                          pressStyle={{ opacity: 0.7, scale: 0.95 }}
                          height={200}
                        >
                          <YStack gap={6} alignItems="center" width={120}>
                            <PlayerCard player={player} statPlayersMap={statPlayersMap} />
                            <XStack 
                              backgroundColor="$orange10" 
                              paddingHorizontal={10} 
                              paddingVertical={6} 
                              borderRadius={8}
                              alignItems="center"
                              gap={4}
                              width="100%"
                              justifyContent="center"
                            >
                              <ArrowLeftRight size={14} color="#fff" />
                              <Text fontSize={12} fontWeight="700" color="#fff">
                                SCAMBIA
                              </Text>
                            </XStack>
                          </YStack>
                        </Button>
                      ))}
                    </XStack>
                  </ScrollView>
                </YStack>
              )}

              {/* Tribuna */}
              {groupedPlayers.tribune.length > 0 && (
                <YStack gap={12}>
                  <XStack 
                    backgroundColor="$gray3" 
                    paddingHorizontal={12} 
                    paddingVertical={8} 
                    borderRadius={8}
                    alignItems="center"
                    gap={8}
                  >
                    <Text fontSize={15} fontWeight="700" color="$gray11">
                      🏟️ TRIBUNA
                    </Text>
                    <Text fontSize={13} color="$gray10" fontWeight="600">
                      ({groupedPlayers.tribune.length})
                    </Text>
                  </XStack>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <XStack gap={12} paddingVertical={8} paddingHorizontal={4}>
                      {groupedPlayers.tribune.map((player, i) => (
                        <Button
                          key={`tribune-${i}`}
                          padding={0}
                          backgroundColor="transparent"
                          borderWidth={0}
                          onPress={() => {
                            onSwap(player)
                            onClose()
                          }}
                          pressStyle={{ opacity: 0.7, scale: 0.95 }}
                          height={200}
                        >
                          <YStack gap={6} alignItems="center" width={120}>
                            <PlayerCard player={player} statPlayersMap={statPlayersMap} />
                            <XStack 
                              backgroundColor="$blue10" 
                              paddingHorizontal={10} 
                              paddingVertical={6} 
                              borderRadius={8}
                              alignItems="center"
                              gap={4}
                              width="100%"
                              justifyContent="center"
                            >
                              <ArrowLeftRight size={14} color="#fff" />
                              <Text fontSize={12} fontWeight="700" color="#fff">
                                PORTA IN CAMPO
                              </Text>
                            </XStack>
                          </YStack>
                        </Button>
                      ))}
                    </XStack>
                  </ScrollView>
                </YStack>
              )}

              {eligiblePlayers.length === 0 && (
                <YStack 
                  backgroundColor="$gray2" 
                  padding={32} 
                  borderRadius={12} 
                  alignItems="center"
                  gap={8}
                >
                  <Text fontSize={40}>🤷‍♂️</Text>
                  <Text fontSize={16} fontWeight="600" color="$gray11" textAlign="center">
                    Nessun giocatore disponibile
                  </Text>
                  <Text fontSize={14} color="$gray10" textAlign="center">
                    Non ci sono altri {selectedRoleLabel.toLowerCase()} nella rosa
                  </Text>
                </YStack>
              )}
            </YStack>
          </ScrollView>
        </YStack>
      </Sheet.Frame>
    </Sheet>
  )
}
