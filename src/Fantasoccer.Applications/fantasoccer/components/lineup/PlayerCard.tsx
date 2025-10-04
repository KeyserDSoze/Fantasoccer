import React, { useMemo, useState, useEffect } from 'react'
import { Pressable } from 'react-native'
import { Card, Image, Text, XStack, YStack, AnimatePresence } from 'tamagui'
import {
  Star,
  AlertTriangle,
  TrendingUp,
  Info,
} from '@tamagui/lucide-icons'

import type { LivePlayer } from 'types/game'
import { getPlayerImageUrlFromName } from 'utils/playerImage'
import type { Vote } from 'types/serieA'
import { Behaviour } from 'types/serieA'
import type { Chance } from '../../types/chance'
import { ChanceType as ChanceTypeEnum } from '../../types/chance'
import { FantaSoccerRole } from 'types/team'
import { formatGameDateTime, convertUTCToItalianTimezoneAsNotNullableDate } from 'utils/timezoneHelpers'
import { AssistSVG, BehaviourCardSVG, SoccerBallSVG } from './icons'


// Dimensioni base esportate della PlayerCard per allineare altri componenti (es. badge ruoli panchina)
export const PLAYER_CARD_WIDTH = 112
export const PLAYER_CARD_HEIGHT = 150

export interface PlayerCardProps {
  player: LivePlayer
  onPress?: () => void // Callback opzionale per rendere la card cliccabile
}

// ============================================
// CONFIGURAZIONE BONUS/MALUS CON ICONE SVG PERSONALIZZATE
// ============================================

// Configurazione per i comportamenti (cartellini)
const BEHAVIOUR_CONFIG = {
  [Behaviour.YellowCard]: { color: '#fbbf24', label: 'Cartellino Giallo' },
  [Behaviour.RedCard]: { color: '#ef4444', label: 'Cartellino Rosso' },
  [Behaviour.Nothing]: null
}

// Configurazione per i bonus/malus con ordine specifico e icone SVG
const BONUS_MALUS_CONFIG: {
  key: keyof Vote
  component: React.ComponentType<{ size?: number }>
  label: string
  description: string
  order: number
}[] = [
  { key: 'goals', component: () => <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#000000" />, label: 'Gol', description: 'gol segnati', order: 1 },
  { key: 'penalties', component: () => <SoccerBallSVG primaryColor="#000000" secondaryColor="#ffffff" />, label: 'Rigori', description: 'rigori segnati', order: 2 },
  { key: 'assists', component: () => <AssistSVG color="#000" />, label: 'Assist', description: 'assist forniti', order: 3 },
  { key: 'stoppedPenalties', component: () => <SoccerBallSVG primaryColor="#3b82f6" secondaryColor="#ffffff" />, label: 'Parate', description: 'rigori parati', order: 4 },
  { key: 'sufferedGoals', component: () => <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" />, label: 'Gol Subiti', description: 'gol subiti', order: 5 },
  { key: 'wrongedPenalties', component: () => <SoccerBallSVG primaryColor="#c113d1ff" secondaryColor="#ffffff" />, label: 'Rig. Sbagliati', description: 'rigori sbagliati', order: 6 },
  { key: 'ownGoals', component: () => <SoccerBallSVG primaryColor="#fbbf24" secondaryColor="#ffffff" />, label: 'Autogol', description: 'autogol fatti', order: 7 },
]

// Mappa per le informazioni di chance
const CHANCE_INFO_MAP = {
  injury: { icon: AlertTriangle, color: '#ef4444', error: '#ef4444', label: 'Infortunato', text: 'INF' },
  fantagazzetta: { icon: null, color: '#16a34a', error: '#ef4444', label: 'Fantagazzetta', text: 'F' },
  gazzetta: { icon: null, color: '#16a34a', error: '#ef4444', label: 'Gazzetta', text: 'G' },
  mediaset: { icon: Info, color: '#16a34a', error: '#ef4444',label: 'Mediaset', text: 'M' },
  sky: { icon: Star, color: '#16a34a', error: '#ef4444', label: 'Sky', text: 'S' },
  trend: { icon: TrendingUp, color: '#8b5cf6', error: '#ef4444', label: 'Forma', text: 'T' },
}



export function PlayerCard({ player, onPress }: PlayerCardProps) {
  const [useFallbackImage, setUseFallbackImage] = useState(false)
  const [isBlinking, setIsBlinking] = useState(false)

  const current = (player as any).current ?? (player as any)
  const vote: Vote | null | undefined = (player as any).vote
  const chance: Chance | null | undefined = (player as any).chance
  const injury = (player as any).injury || (current && (current as any).injury)
  const realGame = player.realGame

  const name = current?.name ?? 'Giocatore'
  const teamName = current?.team?.name ?? ''

  const { src, fallback } = useMemo(() => getPlayerImageUrlFromName(current?.name), [current?.name])
  const imageUri = useFallbackImage ? fallback : src

  const voteLabel = vote ? (typeof vote.value === 'number' ? vote.value.toFixed(1) : String(vote.value)) : null

  // Determina se è durante una partita live per lampeggiamento voti
  const isGameLive = useMemo(() => {
    if (!realGame?.date || !vote) return false
    const now = new Date()
    const gameStart = realGame.date
    const twoHoursAfterStart = new Date(gameStart.getTime() + 2 * 60 * 60 * 1000)
    return now >= gameStart && now <= twoHoursAfterStart
  }, [realGame, vote])

  // Determina se la partita non è ancora iniziata per mostrare chance info
  const isGameNotStarted = useMemo(() => {
    if (!realGame?.date) return true // Se non c'è data, consideriamo non iniziata
    const now = new Date()
    const gameStart = realGame.date
    return now < gameStart
  }, [realGame])

  // Logica per determinare il colore del voto e se deve lampeggiare
  const voteDisplayInfo = useMemo(() => {
    if (!vote) return null

    const now = new Date()
    let backgroundColor = '#dc2626' // Rosso di default
    let shouldBlink = false

    // Determina il colore basato su isFinal e hasVote
    // VERDE: voto finale confermato (isFinal=true, hasVote=true)
    // ARANCIONE: voto live ma non finale (isFinal=false, hasVote=true) 
    // GRIGIO: voto non valido - non ha giocato abbastanza (hasVote=false)
    // ROSSO: casi edge/errore
    if (vote.isFinal && vote.hasVote) {
      backgroundColor = '#16a34a' // Verde - voto finale confermato
    } else if (!vote.isFinal && vote.hasVote) {
      if(!vote.isOut) {
        backgroundColor = '#f59e0b' // Arancione - voto live ma non finale
      }
      else {
        backgroundColor = '#dc2626' // Rosso - voto non valido (giocatore uscito)
      }
    } else if (!vote.hasVote) {
      backgroundColor = '#6b7280' // Grigio - voto non valido (non ha giocato abbastanza)
    }

    // Determina se deve lampeggiare (solo se la partita è live e il giocatore non è uscito)
    // Il lampeggiamento si ferma se il giocatore è uscito (isOut=true)
    if (isGameLive && vote.hasVote && !vote.isOut) {
      shouldBlink = true
    }

    return {
      backgroundColor,
      shouldBlink,
      borderColor: vote.isFinal ? '#fff' : 'rgba(255, 255, 255, 0.7)'
    }
  }, [vote, isGameLive])

  // Effetto per gestire il lampeggiamento
  useEffect(() => {
    let interval: NodeJS.Timeout
    
    if (voteDisplayInfo?.shouldBlink) {
      interval = setInterval(() => {
        setIsBlinking(prev => !prev)
      }, 600) // Lampeggia ogni 600ms (più veloce per essere più visibile)
    } else {
      setIsBlinking(false)
    }

    return () => {
      if (interval) clearInterval(interval)
    }
  }, [voteDisplayInfo?.shouldBlink])

  // Visual mapping per trend (0..4) -> colore e freccia direzionale
  const trendVisual = useMemo(() => {
    const trendValue = typeof (chance as any)?.trend === 'number' ? (chance as any).trend : 2
    // Clamp di sicurezza
    const t = Math.min(4, Math.max(0, trendValue))
    const colorScale = [
      '#dc2626', // 0 molto negativo
      '#f97316', // 1 negativo
      '#facc15', // 2 neutro
      '#4ade80', // 3 positivo
      '#16a34a'  // 4 molto positivo (Excellent)
    ]
    const arrows = ['↓', '↘', '→', '↗', '↑']
    return { backgroundColor: colorScale[t], arrow: arrows[t] }
  }, [chance?.trend])

  // Sistema organizzato per comportamenti e bonus/malus
  const playerBehaviourAndBonus = useMemo(() => {
    if (!vote) return { behaviour: null, bonusGroups: [], hasManOfTheMatch: false }

    // Comportamenti (cartellini)
    const behaviour = vote.status !== Behaviour.Nothing ? {
      type: vote.status,
      config: BEHAVIOUR_CONFIG[vote.status]
    } : null

    // Bonus/Malus raggruppati per tipo
    const bonusGroups: {
      config: typeof BONUS_MALUS_CONFIG[0]
      count: number
      items: React.ReactNode[]
    }[] = []

    for (const bonusConfig of BONUS_MALUS_CONFIG.sort((a, b) => a.order - b.order)) {
      const count = (vote as any)[bonusConfig.key] ?? 0
      if (count > 0) {
        // Crea array di icone per lo stesso tipo (se count > 1, mostra multiple icone)
        const items: React.ReactNode[] = []
        for (let i = 0; i < Math.min(count, 4); i++) { // Max 4 icone per tipo per evitare overflow
          items.push(
            <bonusConfig.component key={`${bonusConfig.key}-${i}`} size={14} />
          )
        }
        
        bonusGroups.push({
          config: bonusConfig,
          count,
          items
        })
      }
    }

    // Man of the Match
    const hasManOfTheMatch = (vote as any)?.manOfTheMatch ?? false

    return {
      behaviour,
      bonusGroups,
      hasManOfTheMatch
    }
  }, [vote])

  // Informazioni chance 
  const chanceInfo = useMemo(() => {
    if (!chance) return []
    
    const info: { type: keyof typeof CHANCE_INFO_MAP; value: any; label: string }[] = []
    
    // Solo se la partita non è ancora iniziata mostriamo F/G
    if (isGameNotStarted) {
      // Fantagazzetta
      if (chance.fantagazzetta !== undefined) {
        info.push({ 
          type: 'fantagazzetta', 
          value: chance.fantagazzetta,
          label: chance.fantagazzetta ? 'Titolare Fantalcacio.it' : 'Panchina Fantacalcio.it'
        })
      }
      
      // Gazzetta
      if (chance.gazzetta !== undefined) {
        info.push({ 
          type: 'gazzetta', 
          value: chance.gazzetta,
          label: chance.gazzetta ? 'Titolare Gazzetta' : 'Panchina Gazzetta'
        })
      }
    }
    
    // Infortunio sempre visibile
    if (chance.status === ChanceTypeEnum.Injury) {
      info.push({ 
        type: 'injury', 
        value: chance.description || 'Infortunato',
        label: 'Infortunio'
      })
    }
    
    return info
  }, [chance, isGameNotStarted])

  // Funzione per determinare il colore di background del nome basato sulla posizione originale
  const getPlayerNameBackgroundColor = useMemo(() => {
    const originalPosition = current?.position ?? player.currentPosition

    // Titolari (0-3) - Verde (come era)
    if (originalPosition >= FantaSoccerRole.GoalKeeper && originalPosition <= FantaSoccerRole.Forward) {
      return "#16a34a" // Verde per titolari
    }

    // Primi panchinari (4, First Backup roles)
    if (originalPosition === FantaSoccerRole.BackupGoalKeeper ||
        originalPosition === FantaSoccerRole.FirstBackupDefensor ||
        originalPosition === FantaSoccerRole.FirstBackupMidfielder ||
        originalPosition === FantaSoccerRole.FirstBackupForward) {
      return "#f59e0b" // Arancione per primi panchinari
    }

    // Secondi panchinari (Second Backup roles)
    if (originalPosition === FantaSoccerRole.SecondBackupDefensor ||
        originalPosition === FantaSoccerRole.SecondBackupMidfielder ||
        originalPosition === FantaSoccerRole.SecondBackupForward) {
      return "#3b82f6" // Blu per secondi panchinari
    }

    // Tribuna e altri (11 e < 0)
    if (originalPosition === FantaSoccerRole.Tribune || 
        originalPosition < FantaSoccerRole.GoalKeeper) {
      return "#dc2626" // Rosso per tribuna/altri
    }

    // Default fallback
    return "#6b7280" // Grigio per casi non gestiti
  }, [current?.position, player.currentPosition])

  // Game info helper - informazioni complete della partita
  const gameInfo = useMemo(() => {
    if (!realGame) return null
    const actualDate = new Date()
    const gameDate = realGame.date ? convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString()) : null
    const isPlayed = gameDate && gameDate < actualDate
    
    const homeTeamName = realGame.home?.name || realGame.home
    const awayTeamName = realGame.away?.name || realGame.away
    const playerTeamName = teamName // Squadra del giocatore
    
    // Determina quale squadra è quella del giocatore
    const isPlayerInHomeTeam = String(homeTeamName).toLowerCase().includes(String(playerTeamName).toLowerCase()) ||
                               String(playerTeamName).toLowerCase().includes(String(homeTeamName).toLowerCase())
    
    // Crea versioni con case differenziato
    const homeTeamDisplay = isPlayerInHomeTeam ? 
      String(homeTeamName).toUpperCase() : 
      String(homeTeamName).toLowerCase()
    const awayTeamDisplay = !isPlayerInHomeTeam ? 
      String(awayTeamName).toUpperCase() : 
      String(awayTeamName).toLowerCase()
    
    // Versioni abbreviate con case differenziato
    const homeTeamShort = isPlayerInHomeTeam ? 
      String(homeTeamName).substring(0, 3).toUpperCase() : 
      String(homeTeamName).substring(0, 3).toLowerCase()
    const awayTeamShort = !isPlayerInHomeTeam ? 
      String(awayTeamName).substring(0, 3).toUpperCase() : 
      String(awayTeamName).substring(0, 3).toLowerCase()
    
    if (isPlayed) {
      return {
        type: 'result' as const,
        homeTeam: homeTeamName,
        awayTeam: awayTeamName,
        homeGoals: realGame.homeGoals,
        awayGoals: realGame.awayGoals,
        resultText: `${realGame.homeGoals ?? 0}-${realGame.awayGoals ?? 0}`,
        dateText: formatGameDateTime(gameDate),
        matchText: `${homeTeamDisplay} vs ${awayTeamDisplay}`,
        matchTextShort: `${homeTeamShort} vs ${awayTeamShort}`
      }
    } else {
      return {
        type: 'upcoming' as const,
        homeTeam: homeTeamName,
        awayTeam: awayTeamName,
        homeGoals: null,
        awayGoals: null,
        resultText: null,
        dateText: formatGameDateTime(gameDate),
        matchText: `${homeTeamDisplay} vs ${awayTeamDisplay}`,
        matchTextShort: `${homeTeamShort} vs ${awayTeamShort}`
      }
    }
  }, [realGame, teamName])

  // Dimensioni base riferite alle costanti esportate
  const cardWidth = PLAYER_CARD_WIDTH
  const cardHeight = PLAYER_CARD_HEIGHT 

  // Controlla se il giocatore è Man of the Match
  const isManOfTheMatch = useMemo(() => {
    return vote && (vote as any)?.manOfTheMatch
  }, [vote])

  // Stato per l'animazione arcobaleno
  const [rainbowPhase, setRainbowPhase] = useState(0)

  // Effetto per animazione arcobaleno continua
  useEffect(() => {
    if (!isManOfTheMatch) return

    const interval = setInterval(() => {
      setRainbowPhase(prev => (prev + 1) % 6) // 6 colori arcobaleno
    }, 500) // Cambia colore ogni 500ms

    return () => clearInterval(interval)
  }, [isManOfTheMatch])

  // Colori arcobaleno
  const rainbowColors = [
    '#ff0000', // Rosso
    '#ff7f00', // Arancione
    '#ffff00', // Giallo
    '#00ff00', // Verde
    '#0000ff', // Blu
    '#9400d3'  // Violetto
  ]

  const currentRainbowColor = rainbowColors[rainbowPhase]

  const cardContent = (
    <YStack position="relative">
      {/* Overlay arcobaleno animato per Man of the Match - contenuto nel YStack */}
      {isManOfTheMatch && (
        <YStack
          position="absolute"
          top={-3}
          left={-3}
          width={cardWidth + 6}
          height={cardHeight + 6}
          borderRadius={15}
          borderWidth={3}
          borderColor={currentRainbowColor}
          zIndex={0}
          animation="bouncy"
          animateOnly={['borderColor']}
          style={{
            shadowColor: currentRainbowColor,
            shadowOffset: { width: 0, height: 0 },
            shadowOpacity: 0.6,
            shadowRadius: 6,
            elevation: 8,
          }}
        />
      )}
      
      <Card
        width={cardWidth}
        height={cardHeight}
        padding={0}
        backgroundColor="#fff"
        borderRadius={12}
        borderWidth={2}
        borderColor={isManOfTheMatch ? "#fff" : "#e5e5e5"}
        position="relative"
        elevation={isManOfTheMatch ? 6 : 4}
        overflow="hidden"
        zIndex={1}
      >
      {/* IMMAGINE GIOCATORE - SFONDO COMPLETO della card con bordi arrotondati */}
      <Image
        source={{ uri: imageUri }}
        width={cardWidth}
        height={cardHeight}
        onError={() => setUseFallbackImage(true)}
        position="absolute"
        top={0}
        left={0}
        zIndex={1}
        borderRadius={12}
      />

      {/* Overlay semitrasparente per migliorare leggibilità */}
      <YStack
        position="absolute"
        top={0}
        left={0}
        right={0}
        bottom={0}
        backgroundColor="rgba(0,0,0,0.1)"
        zIndex={2}
      />
      {/* Vote badge - quadrato in alto a sinistra interno alla card */}
      {vote && voteDisplayInfo && (
        <YStack
          position="absolute"
          top={-4}
          left={-4}
          width={36}
          height={36}
          borderRadius={6}
          backgroundColor={voteDisplayInfo.shouldBlink && isBlinking ? '#ffffff' : voteDisplayInfo.backgroundColor}
          alignItems="center"
          justifyContent="center"
          zIndex={25}
          borderWidth={2}
          borderColor={voteDisplayInfo.shouldBlink && isBlinking ? voteDisplayInfo.backgroundColor : 'rgba(255, 255, 255, 0.9)'}
          elevation={8}
          opacity={1}
          transform={[
            { scale: voteDisplayInfo.shouldBlink && isBlinking ? 1.15 : 1.0 }
          ]}
        >
          <Text 
            fontSize={13} 
            fontWeight="900" 
            color={voteDisplayInfo.shouldBlink && isBlinking ? voteDisplayInfo.backgroundColor : '#fff'}
          >
            {voteLabel}
          </Text>
          
        </YStack>
      )}

      {/* Chance badge (pre-gara): mostra positiveness + freccia trend con scala colori */}
      {isGameNotStarted && chance && (
        <YStack
          position="absolute"
          top={vote ? 34 : -4} /* se c'è già il voto lo spostiamo sotto per non sovrapporre */
          left={-4}
          width={40}
          height={40}
          borderRadius={8}
          backgroundColor={trendVisual.backgroundColor}
          alignItems="center"
          justifyContent="center"
          zIndex={25}
          borderWidth={2}
          borderColor="rgba(255, 255, 255, 0.9)"
          elevation={8}
          paddingHorizontal={2}
          gap={2}
        >
          <XStack alignItems="center" gap={4}>
            <Text fontSize={14} fontWeight="900" color="#fff">
              {trendVisual.arrow}
            </Text>
            <Text fontSize={13} fontWeight="900" color="#fff">
              {chance.lastGame?.positiveness ?? '?'}
            </Text>
          </XStack>
        </YStack>
      )}

      {/* Injury indicator - angolo bottom-right dell'immagine */}
      {injury && (
        <YStack
          position="absolute"
          bottom={55}
          right={8}
          width={32}
          height={32}
          borderRadius={16}
          backgroundColor="rgba(220, 38, 38, 0.95)"
          alignItems="center"
          justifyContent="center"
          zIndex={15}
          borderWidth={2}
          borderColor="#fff"
          elevation={5}
        >
          <AlertTriangle size={16} color="#fff" />
        </YStack>
      )}

      {/* Cartellino comportamentale - sotto il voto a sinistra */}
      {vote && playerBehaviourAndBonus.behaviour && (
        <YStack
          position="absolute"
          top={42}
          left={6}
          zIndex={24}
        >
          <BehaviourCardSVG 
            color={playerBehaviourAndBonus.behaviour.config!.color} 
            size={16}
          />
        </YStack>
      )}

      {/* Area bonus/malus organizzata - a destra */}
      {vote && playerBehaviourAndBonus.bonusGroups.length > 0 && (
        <YStack
          position="absolute"
          top={0}
          right={-4}
          maxWidth={48}
          borderRadius={6}
          paddingVertical={4}
          paddingHorizontal={4}
          gap={4}
          alignItems="center"
          zIndex={20}
        >
          {/* Bonus/Malus organizzati per tipo */}
          {playerBehaviourAndBonus.bonusGroups.map((group, groupIndex) => (
            <YStack key={group.config.key} alignItems="center" gap={2}>
              {/* Riga di icone dello stesso tipo */}
              <XStack gap={2} flexWrap="wrap" justifyContent="center" maxWidth={40}>
                {group.items}
              </XStack>
              
              {/* Contatore se ci sono più di 4 elementi */}
              {group.count > 4 && (
                <Text fontSize={8} color="#fff" fontWeight="600">
                  +{group.count - 4}
                </Text>
              )}
            </YStack>
          ))}
        </YStack>
      )}

      {/* Area chance organizzata - a destra */}
      {isGameNotStarted && chance && chanceInfo.length > 0 && (
        <YStack
          position="absolute"
          top={0}
          right={-4}
          maxWidth={48}
          borderRadius={6}
          paddingVertical={0}
          paddingHorizontal={4}
          gap={2}
          alignItems="center"
          zIndex={20}
        >
          {/* Chance Info Icons - versione compatta */}
          {chanceInfo.map((info, index) => {
            const config = CHANCE_INFO_MAP[info.type]
            const IconComponent = config.icon
            
            return (
              <YStack
                key={`${info.type}-${index}`}
                width={20}
                height={20}
                borderRadius={4}
                backgroundColor={typeof info.value === 'boolean' ? (info.value ? config.color : config.error) : config.color}
                alignItems="center"
                justifyContent="center"
                borderWidth={1}
                borderColor="rgba(255, 255, 255, 0.8)"
              >
                {IconComponent ? (
                  <IconComponent size={12} color="#fff" />
                ) : (
                  <Text fontSize={10} fontWeight="bold" color="#fff">
                    {config.text}
                  </Text>
                )}
              </YStack>
            )
          })}
        </YStack>
      )}

      {/* Banda informazioni partita Serie A - sopra il nome */}
      {gameInfo && (
        <YStack
          position="absolute"
          bottom={20}
          left={0}
          right={0}
          backgroundColor="rgba(0, 0, 0, 0.85)"
          paddingHorizontal={4}
          paddingVertical={1}
          alignItems="center"
          zIndex={9}
          borderTopWidth={1}
          borderBottomWidth={1}
          borderColor="rgba(255, 255, 255, 0.2)"
        >
          {/* Squadre della partita - versione abbreviata */}
          <Text
            fontSize={11}
            fontWeight="700"
            color="#fff"
            textAlign="center"
            numberOfLines={1}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
          >
            {gameInfo.matchTextShort}
          </Text>
          
          {/* Data o risultato */}
          <Text
            fontSize={12}
            fontWeight="600"
            color={isGameLive && gameInfo.type === 'result' && isBlinking ? "#fff" : (gameInfo.type === 'result' ? "#10b981" : "#60a5fa")}
            textAlign="center"
            marginTop={1}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
            opacity={1}
            transform={[
              { scale: isGameLive && gameInfo.type === 'result' && isBlinking ? 1.2 : 1.0 }
            ]}
          >
            {gameInfo.type === 'result' ? `${gameInfo.resultText}` : gameInfo.dateText}
          </Text>
        </YStack>
      )}

      {/* Nome giocatore - footer prominente con colore dinamico basato su posizione originale */}
      <YStack
        position="absolute"
        bottom={0}
        left={0}
        right={0}
        backgroundColor={`${getPlayerNameBackgroundColor}E6`} // Aggiunge trasparenza (90%)
        paddingHorizontal={2}
        paddingVertical={2}
        alignItems="center"
        borderBottomLeftRadius={12}
        borderBottomRightRadius={12}
        zIndex={10}
        borderTopWidth={1}
        borderTopColor="rgba(255, 255, 255, 0.3)"
      >
        <Text
          fontSize={13}
          fontWeight="800"
          color="#fff"
          textAlign="center"
          numberOfLines={1}
          textTransform="uppercase"
          letterSpacing={0.5}
          textShadowColor="rgba(0, 0, 0, 0.5)"
          textShadowOffset={{ width: 1, height: 1 }}
          textShadowRadius={2}
        >
          {name}
        </Text>
      </YStack>
    </Card>
    </YStack>
  )

  // Render condizionale: se c'è onPress, wrappa con Pressable
  return onPress ? (
    <Pressable onPress={onPress}>
      {cardContent}
    </Pressable>
  ) : (
    cardContent
  )
}

export default PlayerCard
