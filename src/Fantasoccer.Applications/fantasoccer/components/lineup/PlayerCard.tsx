import React, { useMemo, useState, useEffect } from 'react'
import { Card, Image, Text, XStack, YStack, Button } from 'tamagui'
import {
  Trophy,
  Target,
  Star,
  Umbrella,
  Award,
  Shield,
  AlertTriangle,
  Heart,
  Zap,
  Circle,
  Calendar,
  Clock,
  Users,
  Goal,
  Minus,
  Plus,
  TrendingUp,
  TrendingDown,
  Activity,
  AlertCircle,
  Info,
  CheckCircle,
  XCircle,
  Radio,
  Satellite,
  BarChart,
} from '@tamagui/lucide-icons'

import type { LivePlayer } from 'types/game'
import { getPlayerImageUrlFromName } from 'utils/playerImage'
import type { Vote } from 'types/serieA'
import type { Chance, ChanceType, TrendType } from '../../types/chance'
import { ChanceType as ChanceTypeEnum } from '../../types/chance'
import { FantaSoccerRole } from 'types/team'
import { formatGameDateTime, convertUTCToItalianTimezoneAsNotNullableDate } from 'utils/timezoneHelpers'

export interface PlayerCardProps {
  player: LivePlayer
}

// Mappa completa dei bonus/malus con icone migliori e descrizioni
const BONUS_MALUS_MAP: { key: keyof Vote; icon: any; label: string; color: string; description: string }[] = [
  { key: 'goals', icon: Goal, label: 'Gol', color: '#16a34a', description: 'gol segnati' },
  { key: 'assists', icon: Target, label: 'Assist', color: '#06b6d4', description: 'assist forniti' },
  { key: 'penalties', icon: Circle, label: 'Rigori', color: '#8b5cf6', description: 'rigori segnati' },
  { key: 'stoppedPenalties', icon: Shield, label: 'Parate', color: '#f59e0b', description: 'rigori parati' },
  { key: 'ownGoals', icon: Minus, label: 'Autogol', color: '#ef4444', description: 'autogol segnati' },
  { key: 'wrongedPenalties', icon: XCircle, label: 'Rig. Sbagliati', color: '#dc2626', description: 'rigori sbagliati' },
  { key: 'sufferedGoals', icon: AlertCircle, label: 'Gol Subiti', color: '#991b1b', description: 'gol subiti dal portiere' },
]

// Mappa per le informazioni di chance
const CHANCE_INFO_MAP = {
  injury: { icon: AlertTriangle, color: '#ef4444', label: 'Infortunato', text: 'INF' },
  fantagazzetta: { icon: null, color: '#16a34a', label: 'Fantagazzetta', text: 'F' },
  gazzetta: { icon: null, color: '#059669', label: 'Gazzetta', text: 'G' },
  mediaset: { icon: Info, color: '#0ea5e9', label: 'Mediaset', text: 'M' },
  sky: { icon: Star, color: '#f59e0b', label: 'Sky', text: 'S' },
  trend: { icon: TrendingUp, color: '#8b5cf6', label: 'Forma', text: 'T' },
}

// Componente per bonus/malus con tooltip
function BonusIcon({ 
  Icon, 
  count, 
  color, 
  label, 
  description, 
  showTooltip, 
  onToggleTooltip 
}: { 
  Icon: any
  count: number
  color: string
  label: string
  description: string
  showTooltip: boolean
  onToggleTooltip: () => void
}) {
  return (
    <YStack alignItems="center">
      <YStack
        width={28}
        height={28}
        borderRadius={14}
        backgroundColor={`${color}E6`}
        alignItems="center"
        justifyContent="center"
        borderWidth={2}
        borderColor="#fff"
        elevation={5}
        position="relative"
        onPress={onToggleTooltip}
        onHoverIn={onToggleTooltip}
        onHoverOut={() => showTooltip && onToggleTooltip()}
        pressStyle={{ scale: 0.95 }}
      >
        <Icon size={14} color="#fff" />
        {count > 1 && (
          <YStack
            position="absolute"
            top={-8}
            right={-8}
            width={16}
            height={16}
            borderRadius={8}
            backgroundColor="#fff"
            alignItems="center"
            justifyContent="center"
            borderWidth={1}
            borderColor={color}
            elevation={3}
          >
            <Text fontSize={8} color={color} fontWeight="800">
              {count}
            </Text>
          </YStack>
        )}
      </YStack>
      
      {/* Tooltip */}
      {showTooltip && (
        <YStack
          position="absolute"
          top={35}
          right={-20}
          backgroundColor="rgba(0, 0, 0, 0.95)"
          padding={6}
          borderRadius={6}
          zIndex={35}
          elevation={15}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
          minWidth={100}
        >
          <Text fontSize={10} color="#fff" textAlign="center" fontWeight="600">
            {count} {description}
          </Text>
        </YStack>
      )}
    </YStack>
  )
}

// Componente per chance information con tooltip
function ChanceIcon({ 
  type, 
  value, 
  showTooltip, 
  onToggleTooltip 
}: { 
  type: keyof typeof CHANCE_INFO_MAP
  value: boolean | string | number
  showTooltip: boolean
  onToggleTooltip: () => void
}) {
  const config = CHANCE_INFO_MAP[type]
  const IconComponent = config.icon
  
  return (
    <YStack alignItems="center">
      <YStack
        width={28}
        height={28}
        borderRadius={14}
        backgroundColor={`${config.color}E6`}
        alignItems="center"
        justifyContent="center"
        borderWidth={2}
        borderColor="#fff"
        elevation={5}
        onPress={onToggleTooltip}
        onHoverIn={onToggleTooltip}
        onHoverOut={() => showTooltip && onToggleTooltip()}
        pressStyle={{ scale: 0.95 }}
      >
        {IconComponent ? (
          <IconComponent size={14} color="#fff" />
        ) : (
          <Text fontSize={12} fontWeight="bold" color="#fff">
            {config.text}
          </Text>
        )}
      </YStack>
      
      {/* Tooltip */}
      {showTooltip && (
        <YStack
          position="absolute"
          top={35}
          right={-20}
          backgroundColor="rgba(0, 0, 0, 0.95)"
          padding={6}
          borderRadius={6}
          zIndex={35}
          elevation={15}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
          minWidth={100}
        >
          <Text fontSize={10} color="#fff" textAlign="center" fontWeight="600">
            {type === 'fantagazzetta' ? 'Fantagazzetta' : 
             type === 'gazzetta' ? 'Gazzetta' : 
             config.label}: {typeof value === 'boolean' ? (value ? 'Titolare' : 'Panchina') : value}
          </Text>
        </YStack>
      )}
    </YStack>
  )
}

export function PlayerCard({ player}: PlayerCardProps) {
  const [useFallbackImage, setUseFallbackImage] = useState(false)
  const [showInjuryTooltip, setShowInjuryTooltip] = useState(false)
  const [showGameTooltip, setShowGameTooltip] = useState(false)
  const [showBonusTooltip, setShowBonusTooltip] = useState<string | null>(null)
  const [showChanceTooltip, setShowChanceTooltip] = useState<string | null>(null)
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
    const gameStart = convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString())
    const twoHoursAfterStart = new Date(gameStart.getTime() + 2 * 60 * 60 * 1000)
    return now >= gameStart && now <= twoHoursAfterStart
  }, [realGame, vote])

  // Determina se la partita non è ancora iniziata per mostrare chance info
  const isGameNotStarted = useMemo(() => {
    if (!realGame?.date) return true // Se non c'è data, consideriamo non iniziata
    const now = new Date()
    const gameStart = convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString())
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
      backgroundColor = '#f59e0b' // Arancione - voto live ma non finale
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
      }, 800) // Lampeggia ogni 800ms
    } else {
      setIsBlinking(false)
    }

    return () => {
      if (interval) clearInterval(interval)
    }
  }, [voteDisplayInfo?.shouldBlink])

  const bonusIcons = useMemo(() => {
    if (!vote) return []
    const icons: { 
      Icon: any
      count: number
      label: string
      color: string
      description: string
      key: string
    }[] = []
    
    for (const m of BONUS_MALUS_MAP) {
      const count = (vote as any)[m.key] ?? 0
      if (count && count > 0) {
        icons.push({ 
          Icon: m.icon, 
          count, 
          label: m.label, 
          color: m.color,
          description: m.description,
          key: m.key
        })
      }
    }
    
    if ((vote as any)?.manOfTheMatch) {
      icons.push({ 
        Icon: Star, 
        count: 1, 
        label: 'MVP', 
        color: '#f97316',
        description: 'migliore in campo',
        key: 'manOfTheMatch'
      })
    }
    
    return icons
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

  // Dimensioni base fisse - il scaling viene gestito dal contenitore via cardScale
  const cardWidth = 112
  const cardHeight = 150 

  return (
    <Card
      width={cardWidth}
      height={cardHeight}
      padding={0}
      backgroundColor="#fff"
      borderRadius={12}
      borderWidth={2}
      borderColor="#e5e5e5"
      position="relative"
      elevation={4}
      overflow="hidden"
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
          backgroundColor={voteDisplayInfo.shouldBlink && isBlinking ? 'rgba(255, 255, 255, 0.3)' : voteDisplayInfo.backgroundColor}
          alignItems="center"
          justifyContent="center"
          zIndex={25}
          borderWidth={2}
          borderColor="rgba(255, 255, 255, 0.9)"
          elevation={8}
          opacity={voteDisplayInfo.shouldBlink && isBlinking ? 0.8 : 1}
          transform={[
            { scale: voteDisplayInfo.shouldBlink && isBlinking ? 1.05 : 1.0 }
          ]}
        >
          <Text fontSize={13} fontWeight="900" color="#fff">
            {voteLabel}
          </Text>
          
          {/* Indicatori di stato - piccoli pallini nell'angolo in basso a destra del quadrato */}
          <XStack position="absolute" bottom={2} right={2} gap={1}>
            {!vote.hasVote && (
              <YStack
                width={4}
                height={4}
                borderRadius={2}
                backgroundColor="#ef4444"
                borderWidth={0.5}
                borderColor="#fff"
              />
            )}
            {!vote.isFinal && vote.hasVote && (
              <YStack
                width={4}
                height={4}
                borderRadius={2}
                backgroundColor="#f59e0b"
                borderWidth={0.5}
                borderColor="#fff"
              />
            )}
            {vote.isOut && (
              <YStack
                width={4}
                height={4}
                borderRadius={2}
                backgroundColor="#6b7280"
                borderWidth={0.5}
                borderColor="#fff"
              />
            )}
          </XStack>
        </YStack>
      )}

      {/* Injury indicator - angolo bottom-right dell'immagine */}
      {injury && (
        <Button
          position="absolute"
          bottom={55}
          right={8}
          width={32}
          height={32}
          borderRadius={16}
          backgroundColor="rgba(220, 38, 38, 0.95)"
          padding={0}
          onPress={() => setShowInjuryTooltip(!showInjuryTooltip)}
          zIndex={15}
          borderWidth={2}
          borderColor="#fff"
          elevation={5}
        >
          <AlertTriangle size={16} color="#fff" />
        </Button>
      )}

      {/* Injury tooltip */}
      {injury && showInjuryTooltip && (
        <YStack
          position="absolute"
          bottom={95}
          right={8}
          backgroundColor="rgba(0, 0, 0, 0.9)"
          padding={8}
          borderRadius={6}
          maxWidth={cardWidth - 16}
          zIndex={20}
          elevation={10}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
        >
          <Text fontSize={9} color="#fff" textAlign="center">
            {injury.reason || 'Infortunato'}
          </Text>
        </YStack>
      )}

      {/* Sistema bonus/malus - in alto a destra, spostato leggermente più in basso */}
      {vote && bonusIcons.length > 0 && (
        <YStack
          position="absolute"
          top={6}
          right={-14}
          gap={3}
          zIndex={20}
        >
          {bonusIcons.map((bonus, index) => (
            <BonusIcon
              key={bonus.key}
              Icon={bonus.Icon}
              count={bonus.count}
              label={bonus.label}
              color={bonus.color}
              description={bonus.description}
              showTooltip={showBonusTooltip === bonus.key}
              onToggleTooltip={() => setShowBonusTooltip(showBonusTooltip === bonus.key ? null : bonus.key)}
            />
          ))}
        </YStack>
      )}

      {/* Informazioni chance */}
      {chanceInfo.length > 0 && (
        <YStack
          position="absolute"
          top={8}
          right={-4}
          gap={3}
          zIndex={20}
        >
          {chanceInfo.map((info, index) => (
            <ChanceIcon
              key={`${info.type}-${index}`}
              type={info.type}
              value={info.value}
              showTooltip={showChanceTooltip === `${info.type}-${index}`}
              onToggleTooltip={() => setShowChanceTooltip(showChanceTooltip === `${info.type}-${index}` ? null : `${info.type}-${index}`)}
            />
          ))}
        </YStack>
      )}

      {/* Banda informazioni partita Serie A - sopra il nome */}
      {gameInfo && (
        <YStack
          position="absolute"
          bottom={32}
          left={0}
          right={0}
          backgroundColor="rgba(0, 0, 0, 0.85)"
          paddingHorizontal={8}
          paddingVertical={6}
          alignItems="center"
          zIndex={9}
          borderTopWidth={1}
          borderBottomWidth={1}
          borderColor="rgba(255, 255, 255, 0.2)"
          onPress={() => setShowGameTooltip(!showGameTooltip)}
          onHoverIn={() => setShowGameTooltip(true)}
          onHoverOut={() => setShowGameTooltip(false)}
          pressStyle={{ backgroundColor: "rgba(0, 0, 0, 0.95)" }}
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
            fontSize={9}
            fontWeight="600"
            color={gameInfo.type === 'result' ? "#10b981" : "#60a5fa"}
            textAlign="center"
            marginTop={1}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
            opacity={isGameLive && gameInfo.type === 'result' && isBlinking ? 0.7 : 1}
            transform={[
              { scale: isGameLive && gameInfo.type === 'result' && isBlinking ? 1.05 : 1.0 }
            ]}
          >
            {gameInfo.type === 'result' ? `${gameInfo.resultText}` : gameInfo.dateText}
          </Text>
        </YStack>
      )}

      {/* Tooltip nomi completi partita */}
      {gameInfo && showGameTooltip && (
        <YStack
          position="absolute"
          bottom={85}
          left={8}
          right={8}
          backgroundColor="rgba(0, 0, 0, 0.95)"
          padding={8}
          borderRadius={8}
          alignItems="center"
          zIndex={30}
          elevation={12}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
        >
          <Text
            fontSize={12}
            fontWeight="700"
            color="#fff"
            textAlign="center"
            numberOfLines={2}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
          >
            {gameInfo.matchText}
          </Text>
          {gameInfo.type === 'result' && (
            <Text
              fontSize={10}
              fontWeight="600"
              color="#10b981"
              textAlign="center"
              marginTop={4}
              opacity={isGameLive && isBlinking ? 0.7 : 1}
              transform={[
                { scale: isGameLive && isBlinking ? 1.05 : 1.0 }
              ]}
            >
              {gameInfo.resultText}
            </Text>
          )}
        </YStack>
      )}

      {/* Nome giocatore - footer prominente con colore dinamico basato su posizione originale */}
      <YStack
        position="absolute"
        bottom={0}
        left={0}
        right={0}
        backgroundColor={`${getPlayerNameBackgroundColor}E6`} // Aggiunge trasparenza (90%)
        paddingHorizontal={8}
        paddingVertical={8}
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
  )
}

export default PlayerCard
