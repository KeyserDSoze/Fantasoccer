import React, { useMemo } from 'react'
import { Dimensions } from 'react-native'
import { Sheet, YStack, XStack, Text, Button, ScrollView, Card } from 'tamagui'
import { X, AlertTriangle, Ban, TrendingUp, TrendingDown, Minus, Clock, CheckCircle, Circle } from '@tamagui/lucide-icons'
import type { LivePlayer } from 'types/game'
import type { StatPlayer } from 'types/stat'
import type { Vote } from 'types/serieA'
import type { Chance } from '../../types/chance'
import type { RealGame } from 'types/serieA'
import { Behaviour } from 'types/serieA'
import { ChanceType as ChanceTypeEnum } from '../../types/chance'
import { Role } from 'types/group'
import { AssistSVG, SoccerBallSVG, BehaviourCardSVG } from './icons'
import { convertUTCToItalianTimezoneAsNotNullableDate, formatGameDateTime } from 'utils/timezoneHelpers'
import { calculatePlayerStats } from 'utils/statPlayerCalculator'
import { getPlayerKey } from 'utils/keyUtils'

interface PlayerViewModalProps {
  isOpen: boolean
  onClose: () => void
  player: LivePlayer | null
  statPlayersMap?: Map<string, StatPlayer>
}

const ROLE_LABELS: Record<Role, string> = {
  [Role.GoalKeeper]: 'Portiere',
  [Role.Defensor]: 'Difensore',
  [Role.Midfielder]: 'Centrocampista',
  [Role.Forward]: 'Attaccante',
  [Role.Undefined]: 'Non definito'
}

// Configurazione bonus/malus con icone
const BONUS_MALUS_CONFIG = [
  { key: 'goals', label: 'Gol', description: 'Gol segnati', component: () => <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#000000" />, order: 1, isPositive: true },
  { key: 'penalties', label: 'Rigori', description: 'Rigori segnati', component: () => <SoccerBallSVG primaryColor="#000000" secondaryColor="#ffffff" />, order: 2, isPositive: true },
  { key: 'assists', label: 'Assist', description: 'Assist', component: () => <AssistSVG color="#000" />, order: 3, isPositive: true },
  { key: 'stoppedPenalties', label: 'Parate', description: 'Rigori parati', component: () => <SoccerBallSVG primaryColor="#3b82f6" secondaryColor="#ffffff" />, order: 4, isPositive: true },
  { key: 'sufferedGoals', label: 'Gol Subiti', description: 'Gol subiti', component: () => <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" />, order: 5, isPositive: false },
  { key: 'wrongedPenalties', label: 'Rig. Sbagliati', description: 'Rigori sbagliati', component: () => <SoccerBallSVG primaryColor="#c113d1ff" secondaryColor="#ffffff" />, order: 6, isPositive: false },
  { key: 'ownGoals', label: 'Autogol', description: 'Autogol', component: () => <SoccerBallSVG primaryColor="#fbbf24" secondaryColor="#ffffff" />, order: 7, isPositive: false },
]

const BEHAVIOUR_CONFIG = {
  [Behaviour.YellowCard]: { label: 'Ammonito', icon: () => <BehaviourCardSVG color="#facc15" />, color: '#facc15' },
  [Behaviour.RedCard]: { label: 'Espulso', icon: () => <BehaviourCardSVG color="#dc2626" />, color: '#dc2626' },
  [Behaviour.Nothing]: { label: '', icon: null, color: '' }
}

export function PlayerViewModal({ isOpen, onClose, player, statPlayersMap }: PlayerViewModalProps) {
  const screenHeight = Dimensions.get('window').height

  if (!player) return null

  const current = player.current
  const vote: Vote | null | undefined = player.vote
  const chance: Chance | null | undefined = player.chance
  const realGame: RealGame | null | undefined = player.realGame

  const playerName = current?.name ?? 'Giocatore'
  const playerRole = current?.role ?? Role.Undefined
  const roleLabel = ROLE_LABELS[playerRole as Role] ?? 'Non definito'
  const teamName = current?.team?.name ?? ''
  const playerKey = getPlayerKey(playerName)

  // Lookup StatPlayer per calcoli e timeline
  const statPlayer = useMemo(() => {
    return statPlayersMap?.get(playerKey) ?? null
  }, [statPlayersMap, playerKey])

  // Calcola metriche da StatPlayer (sostituisce chance.trend e chance.lastGame)
  const playerStats = useMemo(() => {
    if (!statPlayer) {
      return null
    }
    return calculatePlayerStats(statPlayer)
  }, [statPlayer])

  // Determina se la partita è iniziata, in corso o terminata
  const gameStatus = useMemo(() => {
    if (!realGame?.date) return 'not_started'
    
    const now = new Date()
    const gameStart = realGame.date
    const twoHoursAfterStart = new Date(gameStart.getTime() + 2 * 60 * 60 * 1000)
    
    if (now < gameStart) return 'not_started'
    if (now >= gameStart && now <= twoHoursAfterStart) return 'live'
    return 'finished'
  }, [realGame])

  // Informazioni voto e prestazione
  const voteInfo = useMemo(() => {
    if (!vote) return null

    const voteValue = typeof vote.value === 'number' ? vote.value.toFixed(1) : String(vote.value)
    const isFinal = vote.isFinal
    const hasVote = vote.hasVote
    const isOut = vote.isOut

    // Raccogli bonus/malus
    const bonusGroups: {
      config: typeof BONUS_MALUS_CONFIG[0]
      count: number
    }[] = []

    for (const bonusConfig of BONUS_MALUS_CONFIG.sort((a, b) => a.order - b.order)) {
      const count = (vote as any)[bonusConfig.key] ?? 0
      if (count > 0) {
        bonusGroups.push({ config: bonusConfig, count })
      }
    }

    // Man of the Match
    const hasManOfTheMatch = (vote as any)?.manOfTheMatch ?? false

    // Comportamento (cartellini)
    const behaviour = vote.status !== Behaviour.Nothing ? {
      type: vote.status,
      config: BEHAVIOUR_CONFIG[vote.status]
    } : null

    return {
      voteValue,
      isFinal,
      hasVote,
      isOut,
      bonusGroups,
      hasManOfTheMatch,
      behaviour
    }
  }, [vote])

  // Informazioni chance (pre-partita)
  const chanceInfo = useMemo(() => {
    if (!chance) return null

    // Infortunio
    const hasInjury = chance.status === ChanceTypeEnum.Injury
    const injuryDescription = chance.description || 'Infortunato'

    // Squalifica
    const isSuspended = chance.status === ChanceTypeEnum.Disqualified
    const suspensionDescription = chance.description || 'Squalificato'

    // Probabilità titolare
    const fantagazzettaStarter = chance.fantagazzetta
    const gazzettaStarter = chance.gazzetta

    // Trend e lastGame ora vengono da playerStats (StatPlayer) invece di chance
    // Non ci sono più in Chance - rimossi dal backend

    return {
      hasInjury,
      injuryDescription,
      isSuspended,
      suspensionDescription,
      fantagazzettaStarter,
      gazzettaStarter,
    }
  }, [chance])

  // Informazioni partita di Serie A
  const gameInfo = useMemo(() => {
    if (!realGame) return null

    const actualDate = new Date()
    const gameDate = realGame.date ? realGame.date : null
    const isPlayed = gameDate && gameDate < actualDate

    const homeTeamName = realGame.home?.name || realGame.home
    const awayTeamName = realGame.away?.name || realGame.away
    const playerTeamName = teamName

    // Determina quale squadra è quella del giocatore
    const isPlayerInHomeTeam = String(homeTeamName).toLowerCase().includes(String(playerTeamName).toLowerCase()) ||
                               String(playerTeamName).toLowerCase().includes(String(homeTeamName).toLowerCase())

    return {
      homeTeam: homeTeamName,
      awayTeam: awayTeamName,
      homeGoals: realGame.homeGoals ?? null,
      awayGoals: realGame.awayGoals ?? null,
      isPlayed,
      gameDate,
      isPlayerInHomeTeam
    }
  }, [realGame, teamName])

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
                  {playerName}
                </Text>
                <Text fontSize={14} color="$gray11">
                  {roleLabel} • {teamName}
                </Text>
              </YStack>
              <Button
                circular
                size="$4"
                onPress={onClose}
                chromeless
                icon={<X size={24} />}
              />
            </XStack>
          </YStack>

          {/* Contenuto Scrollabile */}
          <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={{ paddingTop: 16 }}>
            <YStack gap={16} paddingBottom={16}>
              
              {/* PARTITA DI SERIE A */}
              {gameInfo && (
                <Card backgroundColor="$blue2" padding={12} borderRadius={12}>
                  <YStack gap={8}>
                    <XStack alignItems="center" gap={6}>
                      <Clock size={16} color="#3b82f6" />
                      <Text fontSize={14} fontWeight="700" color="$blue11">
                        {gameStatus === 'not_started' ? '⏳ PARTITA DA GIOCARE' : 
                         gameStatus === 'live' ? '🔴 PARTITA IN CORSO' : 
                         '✅ PARTITA TERMINATA'}
                      </Text>
                    </XStack>
                    
                    <YStack gap={4}>
                      <XStack justifyContent="space-between" alignItems="center">
                        <Text 
                          fontSize={15} 
                          fontWeight={gameInfo.isPlayerInHomeTeam ? "800" : "600"}
                          color="$blue12"
                        >
                          {gameInfo.homeTeam} {gameInfo.isPlayerInHomeTeam ? '🔵' : ''}
                        </Text>
                        <Text fontSize={18} fontWeight="bold" color="$blue12">
                          {gameInfo.homeGoals !== null ? gameInfo.homeGoals : '-'}
                        </Text>
                      </XStack>
                      
                      <XStack justifyContent="space-between" alignItems="center">
                        <Text 
                          fontSize={15} 
                          fontWeight={!gameInfo.isPlayerInHomeTeam ? "800" : "600"}
                          color="$blue12"
                        >
                          {gameInfo.awayTeam} {!gameInfo.isPlayerInHomeTeam ? '🔵' : ''}
                        </Text>
                        <Text fontSize={18} fontWeight="bold" color="$blue12">
                          {gameInfo.awayGoals !== null ? gameInfo.awayGoals : '-'}
                        </Text>
                      </XStack>
                      
                      {gameInfo.gameDate && (
                        <Text fontSize={12} color="$blue10" textAlign="center" marginTop={4}>
                          {formatGameDateTime(gameInfo.gameDate)}
                        </Text>
                      )}
                    </YStack>
                  </YStack>
                </Card>
              )}

              {/* VOTO E PRESTAZIONE (se partita iniziata) */}
              {gameStatus !== 'not_started' && voteInfo && (
                <Card backgroundColor="$green2" padding={12} borderRadius={12}>
                  <YStack gap={12}>
                    {/* Voto principale */}
                    <XStack justifyContent="space-between" alignItems="center">
                      <YStack gap={2}>
                        <Text fontSize={14} fontWeight="700" color="$green11">
                          {voteInfo.isFinal ? '✅ VOTO FINALE' : 
                           voteInfo.hasVote ? '⏳ VOTO PROVVISORIO' : 
                           '⚪ NON SUFFICIENTE'}
                        </Text>
                        {voteInfo.isOut && (
                          <Text fontSize={12} color="$red10" fontWeight="600">
                            🔴 Uscito dal campo
                          </Text>
                        )}
                      </YStack>
                      <Text 
                        fontSize={32} 
                        fontWeight="bold" 
                        color={voteInfo.isFinal && voteInfo.hasVote ? "$green11" : 
                               voteInfo.hasVote ? "$orange11" : 
                               "$gray11"}
                      >
                        {voteInfo.voteValue}
                      </Text>
                    </XStack>

                    {/* Man of the Match */}
                    {voteInfo.hasManOfTheMatch && (
                      <XStack 
                        gap={8} 
                        alignItems="center" 
                        backgroundColor="$yellow2" 
                        padding={8} 
                        borderRadius={8}
                      >
                        <Text fontSize={20}>⭐</Text>
                        <Text fontSize={13} fontWeight="700" color="$yellow11">
                          MIGLIORE IN CAMPO (Man of the Match)
                        </Text>
                      </XStack>
                    )}

                    {/* Comportamento (cartellini) */}
                    {voteInfo.behaviour && (
                      <XStack 
                        gap={8} 
                        alignItems="center" 
                        backgroundColor={voteInfo.behaviour.type === Behaviour.RedCard ? "$red2" : "$yellow2"}
                        padding={8} 
                        borderRadius={8}
                      >
                        {voteInfo.behaviour.config.icon && (
                          <voteInfo.behaviour.config.icon />
                        )}
                        <Text 
                          fontSize={13} 
                          fontWeight="700" 
                          color={voteInfo.behaviour.type === Behaviour.RedCard ? "$red11" : "$yellow11"}
                        >
                          {voteInfo.behaviour.config.label.toUpperCase()}
                        </Text>
                      </XStack>
                    )}

                    {/* Bonus e Malus */}
                    {voteInfo.bonusGroups.length > 0 && (
                      <YStack gap={8}>
                        <Text fontSize={13} fontWeight="700" color="$green11">
                          📊 STATISTICHE PARTITA
                        </Text>
                        {voteInfo.bonusGroups.map((group, idx) => (
                          <XStack 
                            key={idx}
                            justifyContent="space-between" 
                            alignItems="center"
                            backgroundColor={group.config.isPositive ? "$green3" : "$red3"}
                            padding={8}
                            borderRadius={8}
                          >
                            <XStack gap={8} alignItems="center">
                              <group.config.component />
                              <Text 
                                fontSize={13} 
                                fontWeight="600"
                                color={group.config.isPositive ? "$green11" : "$red11"}
                              >
                                {group.config.description}
                              </Text>
                            </XStack>
                            <Text 
                              fontSize={16} 
                              fontWeight="bold"
                              color={group.config.isPositive ? "$green11" : "$red11"}
                            >
                              {group.count}
                            </Text>
                          </XStack>
                        ))}
                      </YStack>
                    )}

                    {/* Messaggio se nessuna statistica */}
                    {voteInfo.bonusGroups.length === 0 && !voteInfo.hasManOfTheMatch && !voteInfo.behaviour && (
                      <Text fontSize={12} color="$gray10" textAlign="center" fontStyle="italic">
                        Nessuna prestazione significativa registrata
                      </Text>
                    )}
                  </YStack>
                </Card>
              )}

              {/* INFORMAZIONI CHANCE (se partita non iniziata) */}
              {gameStatus === 'not_started' && chanceInfo && (
                <YStack gap={12}>
                  {/* Infortunio */}
                  {chanceInfo.hasInjury && (
                    <XStack gap={8} alignItems="center" backgroundColor="$red2" padding={12} borderRadius={12}>
                      <AlertTriangle size={20} color="#dc2626" />
                      <YStack flex={1}>
                        <Text fontSize={14} fontWeight="700" color="$red11">
                          🤕 INFORTUNATO
                        </Text>
                        <Text fontSize={12} color="$red10">
                          {chanceInfo.injuryDescription}
                        </Text>
                      </YStack>
                    </XStack>
                  )}

                  {/* Squalifica */}
                  {chanceInfo.isSuspended && (
                    <XStack gap={8} alignItems="center" backgroundColor="$red2" padding={12} borderRadius={12}>
                      <Ban size={20} color="#dc2626" />
                      <YStack flex={1}>
                        <Text fontSize={14} fontWeight="700" color="$red11">
                          🚫 SQUALIFICATO
                        </Text>
                        <Text fontSize={12} color="$red10">
                          {chanceInfo.suspensionDescription}
                        </Text>
                      </YStack>
                    </XStack>
                  )}

                  {/* Probabilità titolare */}
                  {(chanceInfo.fantagazzettaStarter !== undefined || chanceInfo.gazzettaStarter !== undefined) && (
                    <Card backgroundColor="$blue2" padding={12} borderRadius={12}>
                      <YStack gap={8}>
                        <Text fontSize={14} fontWeight="700" color="$blue11">
                          📊 PROBABILITÀ TITOLARE
                        </Text>
                        <XStack gap={12}>
                          {chanceInfo.fantagazzettaStarter !== undefined && (
                            <XStack gap={6} alignItems="center" flex={1} backgroundColor="$blue3" padding={8} borderRadius={8}>
                              {chanceInfo.fantagazzettaStarter ? (
                                <CheckCircle size={16} color="#16a34a" />
                              ) : (
                                <Circle size={16} color="#dc2626" />
                              )}
                              <YStack flex={1}>
                                <Text fontSize={11} fontWeight="600" color="$blue12">
                                  Fantacalcio.it
                                </Text>
                                <Text fontSize={10} color="$blue11">
                                  {chanceInfo.fantagazzettaStarter ? 'Titolare' : 'Panchina'}
                                </Text>
                              </YStack>
                            </XStack>
                          )}
                          {chanceInfo.gazzettaStarter !== undefined && (
                            <XStack gap={6} alignItems="center" flex={1} backgroundColor="$blue3" padding={8} borderRadius={8}>
                              {chanceInfo.gazzettaStarter ? (
                                <CheckCircle size={16} color="#16a34a" />
                              ) : (
                                <Circle size={16} color="#dc2626" />
                              )}
                              <YStack flex={1}>
                                <Text fontSize={11} fontWeight="600" color="$blue12">
                                  Gazzetta
                                </Text>
                                <Text fontSize={10} color="$blue11">
                                  {chanceInfo.gazzettaStarter ? 'Titolare' : 'Panchina'}
                                </Text>
                              </YStack>
                            </XStack>
                          )}
                        </XStack>
                      </YStack>
                    </Card>
                  )}
                </YStack>
              )}

              {/* STATISTICHE GIOCATORE (sempre visibili se disponibili) */}
              {playerStats && (
                <YStack gap={12}>
                  {/* Forma recente (trend) - usa playerStats da StatPlayer */}
                  {playerStats.trend !== null && (
                    <Card 
                      backgroundColor={
                        playerStats.trend >= 3 ? "$green2" : 
                        playerStats.trend <= 1 ? "$red2" : 
                        "$yellow2"
                      }
                      padding={12} 
                      borderRadius={12}
                    >
                      <XStack gap={8} alignItems="center">
                        {playerStats.trend >= 3 ? (
                          <TrendingUp size={20} color="#16a34a" />
                        ) : playerStats.trend <= 1 ? (
                          <TrendingDown size={20} color="#dc2626" />
                        ) : (
                          <Minus size={20} color="#eab308" />
                        )}
                        <YStack flex={1}>
                          <Text fontSize={14} fontWeight="700" color={
                            playerStats.trend >= 3 ? "$green11" : 
                            playerStats.trend <= 1 ? "$red11" : 
                            "$yellow11"
                          }>
                            {playerStats.trendVisual.arrow} FORMA RECENTE: {playerStats.trendVisual.label?.toUpperCase()} ({Math.round(playerStats.trendAverage)})
                          </Text>
                          <Text fontSize={12} color={
                            playerStats.trend >= 3 ? "$green10" : 
                            playerStats.trend <= 1 ? "$red10" : 
                            "$yellow10"
                          }>
                            {playerStats.trend >= 3 ? 'Sta giocando molto bene' : 
                             playerStats.trend <= 1 ? 'Prestazioni negative' : 
                             'Prestazioni altalenanti'}
                          </Text>
                        </YStack>
                      </XStack>
                    </Card>
                  )}

                  {/* Ultima partita (positiveness) - usa playerStats da StatPlayer */}
                  {playerStats?.lastGame && (
                    <Card 
                      backgroundColor={
                        playerStats.lastGame.vote === null ? "$gray2" :
                        playerStats.lastGame.positiveness > 0 ? "$green2" : 
                        playerStats.lastGame.positiveness < 0 ? "$red2" : 
                        "$gray2"
                      }
                      padding={12} 
                      borderRadius={12}
                    >
                      <YStack gap={4}>
                        <Text fontSize={14} fontWeight="700" color={
                          playerStats.lastGame.vote === null ? "$gray11" :
                          playerStats.lastGame.positiveness > 0 ? "$green11" : 
                          playerStats.lastGame.positiveness < 0 ? "$red11" : 
                          "$gray11"
                        }>
                          ⚽ ULTIMA PARTITA (G{playerStats.lastGame.serieADay})
                        </Text>
                        <Text fontSize={12} color={
                          playerStats.lastGame.vote === null ? "$gray10" :
                          playerStats.lastGame.positiveness > 0 ? "$green10" : 
                          playerStats.lastGame.positiveness < 0 ? "$red10" : 
                          "$gray10"
                        }>
                          {playerStats.lastGame.vote === null 
                            ? 'Non ha giocato' 
                            : `Valore: ${playerStats.lastGame.positiveness > 0 ? '' : ''}${Math.round(playerStats.lastGame.positiveness)} • ${
                                playerStats.lastGame.positiveness > 0 ? 'Ha fatto bonus!' : 
                                playerStats.lastGame.positiveness < 0 ? 'Ha preso malus' : 
                                'Prestazione neutra'
                              }`
                          }
                        </Text>
                      </YStack>
                    </Card>
                  )}
                </YStack>
              )}

              {/* Timeline partite recenti (sempre visibile se disponibile) */}
              {statPlayer && statPlayer.games && statPlayer.games.length > 0 && (
                <Card backgroundColor="$background" padding={12} borderRadius={12} borderWidth={1} borderColor="$borderColor">
                  <YStack gap={8}>
                    <Text fontSize={14} fontWeight="700" color="$color12">
                      📊 ULTIME PARTITE
                    </Text>
                    <Text fontSize={11} color="$color11" marginBottom={4}>
                      Ordine: dalla più recente • Verde: positivo | Rosso: negativo | Grigio: non giocata
                    </Text>
                    
                    {/* Griglia pallini - INVERTITI: trend dentro, giornata sotto - ORDINE DECRESCENTE */}
                    <XStack gap={6} flexWrap="wrap">
                      {statPlayer.games
                        .slice()
                        .sort((a, b) => b.serieADay - a.serieADay) // Ordine decrescente: dalla più recente
                        .slice(0, 15)
                        .map((game, index) => {
                          const hasVote = game.vote !== null
                          const ballColor = !hasVote 
                            ? '#6b7280' 
                            : game.positiveness > 0 
                            ? '#16a34a' 
                            : '#dc2626'
                          
                          return (
                            <YStack key={`game-${game.serieADay}-${index}`} alignItems="center" gap={2}>
                              <YStack
                                width={36}
                                height={36}
                                borderRadius={18}
                                backgroundColor={ballColor}
                                alignItems="center"
                                justifyContent="center"
                                borderWidth={2}
                                borderColor="rgba(255,255,255,0.3)"
                              >
                                {/* INVERTITO: Trend dentro la palla - ARROTONDATO senza virgola */}
                                <Text fontSize={11} fontWeight="bold" color="#fff">
                                  {hasVote ? `${game.positiveness > 0 ? '+' : ''}${Math.round(game.positiveness)}` : '-'}
                                </Text>
                              </YStack>
                              {/* INVERTITO: Giornata di Serie A sotto */}
                              <Text fontSize={9} fontWeight="600" color="$color11">
                                G{game.serieADay}
                              </Text>
                            </YStack>
                          )
                        })}
                    </XStack>
                  </YStack>
                </Card>
              )}

              {/* Messaggio se nessuna informazione disponibile */}
              {!voteInfo && !chanceInfo && !gameInfo && (
                <YStack 
                  backgroundColor="$gray2" 
                  padding={32} 
                  borderRadius={12} 
                  alignItems="center"
                  gap={8}
                >
                  <Text fontSize={40}>🤷‍♂️</Text>
                  <Text fontSize={16} fontWeight="600" color="$gray11" textAlign="center">
                    Nessuna informazione disponibile
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
