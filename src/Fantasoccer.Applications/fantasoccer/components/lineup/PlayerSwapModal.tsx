import React, { useMemo } from 'react'
import { YStack, XStack, Text, Button, ScrollView, Sheet, useMedia } from 'tamagui'
import { X, ArrowLeftRight, Plus, AlertTriangle, Ban, TrendingUp, TrendingDown, Minus } from '@tamagui/lucide-icons-2'
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

type PlayerInfoView = {
  hasInjury: boolean; injuryDescription: string; isSuspended: boolean; suspensionDescription: string
  fantagazzettaStarter?: boolean; gazzettaStarter?: boolean
  trend: number | null; trendLabel: string | null; trendArrow: string | null; lastGamePositiveness: number | null
}

// Desktop: le informazioni sul giocatore in un'unica fila di riquadri affiancati, non uno sotto l'altro.
function DesktopInfoChips({ info }: { info: PlayerInfoView }) {
  const tone = (value: number | null, high: number, low: number) =>
    value === null ? 'gray' : value >= high ? 'green' : value <= low ? 'red' : 'yellow'
  const chips: React.ReactNode[] = []
  const chip = (key: string, color: string, title: string, body: React.ReactNode) => (
    <YStack key={key} flexGrow={1} flexShrink={1} flexBasis={230} gap={2} paddingHorizontal={10} paddingVertical={6} borderRadius={8}
      backgroundColor={`$${color}2` as any} borderLeftWidth={3} borderLeftColor={`$${color}9` as any}>
      <Text fontSize={11} fontWeight="800" color={`$${color}11` as any}>{title}</Text>
      {body}
    </YStack>
  )
  if (info.hasInjury) chips.push(chip('injury', 'red', '⚠️ INFORTUNATO', <Text fontSize={12} color="$red10" numberOfLines={3}>{info.injuryDescription}</Text>))
  if (info.isSuspended) chips.push(chip('suspended', 'red', '🚫 SQUALIFICATO', <Text fontSize={12} color="$red10" numberOfLines={3}>{info.suspensionDescription}</Text>))
  if (info.fantagazzettaStarter !== undefined || info.gazzettaStarter !== undefined) {
    const row = (label: string, value: boolean | undefined) => value === undefined ? null : (
      <XStack key={label} gap={4}>
        <Text fontSize={12} color="$blue10" fontWeight="600">{label}</Text>
        <Text fontSize={12} fontWeight="700" color={value ? '$green11' : '$red11'}>{value ? '✓ Titolare' : '✗ Panchina'}</Text>
      </XStack>
    )
    chips.push(chip('chance', 'blue', '📊 PROBABILITÀ TITOLARE', <XStack gap={14} flexWrap="wrap">{row('Fantacalcio.it', info.fantagazzettaStarter)}{row('Gazzetta', info.gazzettaStarter)}</XStack>))
  }
  if (info.trend !== null) {
    const color = tone(info.trend, 3, 1)
    chips.push(chip('trend', color, `${info.trendArrow ?? ''} FORMA RECENTE: ${(info.trendLabel ?? '').toUpperCase()}`,
      <Text fontSize={12} color={`$${color}10` as any}>{info.trend >= 3 ? 'Sta giocando molto bene' : info.trend <= 1 ? 'Prestazioni negative' : 'Prestazioni altalenanti'}</Text>))
  }
  if (info.lastGamePositiveness !== null && info.lastGamePositiveness !== undefined) {
    const value = info.lastGamePositiveness
    const color = value > 0 ? 'green' : value < 0 ? 'red' : 'gray'
    chips.push(chip('last', color, '⚽ ULTIMA PARTITA',
      <Text fontSize={12} color={`$${color}10` as any}>{value > 0 ? '+' : ''}{value} • {value > 0 ? 'Ha fatto bonus' : value < 0 ? 'Ha preso malus' : 'Neutra'}</Text>))
  }
  if (chips.length === 0) return null
  return <XStack gap={8} flexWrap="wrap" alignItems="stretch">{chips}</XStack>
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
  const media = useMedia()
  const isDesktop = !!media.gtSm

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

  type SwapGroup = {
    key: string; title: string; origin: string; players: LivePlayer[]; action: string
    headerBg: string; headerText: string; headerCount: string; border: string; button: string
  }
  const swapGroups: SwapGroup[] = [
    { key: 'starter', title: '⭐ TITOLARI', origin: 'In campo', players: groupedPlayers.starters, action: 'SCAMBIA',
      headerBg: '$green2', headerText: '$green11', headerCount: '$green10', border: '$green7', button: '$green10' },
    { key: 'bench', title: '🪑 PANCHINA', origin: 'In panchina', players: groupedPlayers.bench, action: 'SCAMBIA',
      headerBg: '$orange2', headerText: '$orange11', headerCount: '$orange10', border: '$orange7', button: '$orange10' },
    { key: 'tribune', title: '🏟️ TRIBUNA', origin: 'Fuori dai convocati', players: groupedPlayers.tribune, action: 'PORTA IN CAMPO',
      headerBg: '$gray3', headerText: '$gray11', headerCount: '$gray10', border: '$gray7', button: '$blue10' },
  ]

  const renderSwapTile = (group: SwapGroup, player: LivePlayer, key: string) => (
    <Button
      key={key}
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
          backgroundColor={group.button as any}
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
            {group.action}
          </Text>
        </XStack>
      </YStack>
    </Button>
  )

  const actionButtons = (
  <YStack gap={8} flexDirection={isDesktop ? 'row' : 'column'} flexWrap="wrap" alignItems={isDesktop ? 'center' : 'stretch'}>
    {canAddToFieldFromTribune && onAddToField && (
      <Button
        backgroundColor="$green10"
        color="#fff"
        size={isDesktop ? '$3' : '$4'}
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
        size={isDesktop ? '$3' : '$4'}
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
        size={isDesktop ? '$3' : '$4'}
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
        size={isDesktop ? '$3' : '$4'}
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
  )

  return (
    <Sheet
      modal
      open={isOpen}
      onOpenChange={onClose}
      dismissOnSnapToBottom
      snapPointsMode={isDesktop ? 'fit' : 'percent'}
      snapPoints={isDesktop ? undefined : [85]}
      position={0}
      zIndex={100000}
    >
      <Sheet.Overlay backgroundColor="rgba(0,0,0,0.7)" transition="quick" enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
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
              {isDesktop && actionButtons}
              <Button
                circular
                size="$4"
                onPress={onClose}
                chromeless
                icon={<X size={24} />}
              />
            </XStack>

            {/* Informazioni dettagliate giocatore */}
            {playerInfo && isDesktop && <DesktopInfoChips info={playerInfo} />}
            {playerInfo && !isDesktop && (
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

            {!isDesktop && actionButtons}
          </YStack>

          {/* Lista giocatori papabili - Scrollabile */}
          <ScrollView 
            showsVerticalScrollIndicator={false}
            contentContainerStyle={{ paddingTop: 16 }}
          >
            <YStack gap={20} paddingBottom={16}>
              {isDesktop ? (
                // Desktop: i tre gruppi affiancati; ogni pannello dice da dove arriva il giocatore e quale azione si fa
                <XStack gap={12} flexWrap="wrap" alignItems="stretch">
                  {swapGroups.filter((group) => group.players.length > 0).map((group) => (
                    <YStack
                      key={group.key}
                      flexGrow={group.players.length}
                      flexShrink={1}
                      flexBasis={Math.min(group.players.length, 4) * 132 + 24}
                      minWidth={168}
                      borderWidth={1}
                      borderColor={group.border}
                      borderRadius={12}
                      overflow="hidden"
                      backgroundColor="$color1"
                    >
                      <XStack backgroundColor={group.headerBg} paddingHorizontal={12} paddingVertical={8} alignItems="center" gap={8}
                        borderBottomWidth={2} borderBottomColor={group.button}>
                        <Text fontSize={14} fontWeight="800" color={group.headerText}>{group.title}</Text>
                        <Text fontSize={12} fontWeight="600" color={group.headerCount}>({group.players.length})</Text>
                        <Text fontSize={11} color={group.headerCount} marginLeft="auto">{group.origin}</Text>
                      </XStack>
                      <XStack gap={10} flexWrap="wrap" padding={10} justifyContent="flex-start">
                        {group.players.map((player, i) => renderSwapTile(group, player, `${group.key}-${i}`))}
                      </XStack>
                    </YStack>
                  ))}
                </XStack>
              ) : (
                swapGroups.filter((group) => group.players.length > 0).map((group) => (
                  <YStack key={group.key} gap={12}>
                    <XStack backgroundColor={group.headerBg} paddingHorizontal={12} paddingVertical={8} borderRadius={8} alignItems="center" gap={8}>
                      <Text fontSize={15} fontWeight="700" color={group.headerText}>{group.title}</Text>
                      <Text fontSize={13} color={group.headerCount} fontWeight="600">({group.players.length})</Text>
                    </XStack>
                    <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                      <XStack gap={12} paddingVertical={8} paddingHorizontal={4}>
                        {group.players.map((player, i) => renderSwapTile(group, player, `${group.key}-${i}`))}
                      </XStack>
                    </ScrollView>
                  </YStack>
                ))
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
