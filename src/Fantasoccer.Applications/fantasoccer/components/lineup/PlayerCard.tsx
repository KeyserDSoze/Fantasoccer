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
} from '@tamagui/lucide-icons'

import type { LivePlayer } from 'types/game'
import { getPlayerImageUrlFromName } from 'utils/playerImage'
import type { Vote } from 'types/serieA'
import type { Chance } from 'types/chance'
import { FantaSoccerRole } from 'types/team'
import { formatGameDateTime, convertUTCToItalianTimezoneAsNotNullableDate } from 'utils/timezoneHelpers'

export type PlayerCardVariant = 'starter' | 'bench' | 'tribune' | 'field'

export interface PlayerCardProps {
  player: LivePlayer
  variant?: PlayerCardVariant
  showTeamName?: boolean
}

// prettier, friendlier icons map
const BONUS_ICON_MAP: { key: keyof Vote; icon: any; label: string; color?: string }[] = [
  { key: 'goals', icon: Trophy, label: 'Gol', color: '#f59e0b' },
  { key: 'assists', icon: Target, label: 'Assist', color: '#06b6d4' },
  { key: 'penalties', icon: Award, label: 'Rig.', color: '#ef4444' },
  { key: 'stoppedPenalties', icon: Shield, label: 'Par.', color: '#10b981' },
  { key: 'ownGoals', icon: Umbrella, label: 'Aut.', color: '#64748b' },
]

function BonusBadge({ Icon, count, color, label }: { Icon: any; count: number; color?: string; label?: string }) {
  return (
    <XStack alignItems="center" gap="$1" paddingHorizontal="$2" paddingVertical="$1" borderRadius="$3" backgroundColor={color ?? '$color3'}>
      <Icon size={14} color="#fff" />
      {count > 1 ? <Text color="#fff" fontSize="$1">x{count}</Text> : null}
    </XStack>
  )
}

export function PlayerCard({ player, variant = 'starter', showTeamName = true }: PlayerCardProps) {
  const [useFallbackImage, setUseFallbackImage] = useState(false)
  const [showInjuryTooltip, setShowInjuryTooltip] = useState(false)
  const [showGameTooltip, setShowGameTooltip] = useState(false)
  const [isBlinking, setIsBlinking] = useState(false)

  const current = (player as any).current ?? (player as any)
  const vote: Vote | null | undefined = (player as any).vote
  const chance: Chance | null | undefined = (player as any).chance
  const injury = (player as any).injury || (current && (current as any).injury)
  const realGame = player.realGame

  const name = current?.name ?? 'Giocatore'
  const teamName = current?.team?.name ?? ''
  const roleLabel = current?.role ?? ''

  const { src, fallback } = useMemo(() => getPlayerImageUrlFromName(current?.name), [current?.name])
  const imageUri = useFallbackImage ? fallback : src

  const voteLabel = vote ? (typeof vote.value === 'number' ? vote.value.toFixed(1) : String(vote.value)) : null

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
    // Una partita è considerata "live" se siamo nel range di 2 ore dall'inizio
    // Il lampeggiamento si ferma se il giocatore è uscito (isOut=true)
    if (realGame?.date && vote.hasVote && !vote.isOut) {
      const gameStart = convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString())
      const twoHoursAfterStart = new Date(gameStart.getTime() + 2 * 60 * 60 * 1000)
      
      // La partita è live se siamo tra l'inizio e 2 ore dopo
      if (now >= gameStart && now <= twoHoursAfterStart) {
        shouldBlink = true
      }
    }

    return {
      backgroundColor,
      shouldBlink,
      borderColor: vote.isFinal ? '#fff' : 'rgba(255, 255, 255, 0.7)'
    }
  }, [vote, realGame])

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
    const icons: { Icon: any; count: number; label: string; color?: string }[] = []
    for (const m of BONUS_ICON_MAP) {
      const count = (vote as any)[m.key] ?? 0
      if (count && count > 0) icons.push({ Icon: m.icon, count, label: m.label, color: m.color })
    }
    if ((vote as any)?.manOfTheMatch) icons.push({ Icon: Star, count: 1, label: 'MVP', color: '#f97316' })
    return icons
  }, [vote])

  const chanceSummary = useMemo(() => {
    if (!chance) return null
    const sources = (chance.fantagazzetta ? ['F'] : []).concat(chance.gazzetta ? ['G'] : [], chance.mediaset ? ['M'] : [], chance.sky ? ['S'] : [])
    return { status: chance.status, sources, label: chance.description }
  }, [chance])

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
    >
      {/* IMMAGINE GIOCATORE - SFONDO COMPLETO della card con bordi arrotondati */}
      <Image
        source={{ uri: imageUri }}
        width={cardWidth}
        height={cardHeight}
        resizeMode="cover"
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
      {/* Vote badge - sporgente dall'angolo con effetto 3D pronunciato */}
      {vote && voteDisplayInfo && (
        <YStack
          position="absolute"
          top={-12}
          left={-12}
          width={40}
          height={40}
          borderRadius={22}
          backgroundColor={voteDisplayInfo.shouldBlink && isBlinking ? 'rgba(255, 255, 255, 0.3)' : voteDisplayInfo.backgroundColor}
          alignItems="center"
          justifyContent="center"
          zIndex={25}
          borderWidth={4}
          borderColor="#fff"
          elevation={12}
          shadowColor="#000"
          shadowOffset={{ width: 3, height: 6 }}
          shadowOpacity={0.4}
          shadowRadius={8}
          opacity={voteDisplayInfo.shouldBlink && isBlinking ? 0.7 : 1}
          transform={[
            { scale: voteDisplayInfo.shouldBlink && isBlinking ? 1.1 : 1.0 }
          ]}
        >
          <Text fontSize={14} fontWeight="900" color="#fff">
            {voteLabel}
          </Text>
          
          {/* Indicatori di stato sotto il voto */}
          <XStack position="absolute" bottom={-8} gap={2}>
            {!vote.hasVote && (
              <YStack
                width={6}
                height={6}
                borderRadius={3}
                backgroundColor="#ef4444"
                borderWidth={1}
                borderColor="#fff"
                elevation={3}
              />
            )}
            {!vote.isFinal && vote.hasVote && (
              <YStack
                width={6}
                height={6}
                borderRadius={3}
                backgroundColor="#f59e0b"
                borderWidth={1}
                borderColor="#fff"
                elevation={3}
              />
            )}
            {vote.isOut && (
              <YStack
                width={6}
                height={6}
                borderRadius={3}
                backgroundColor="#6b7280"
                borderWidth={1}
                borderColor="#fff"
                elevation={3}
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

      {/* Rimuoviamo la sezione dell'immagine centrale - ora è lo sfondo */}

      {/* Bonus icons - lato sinistro dell'immagine, riposizionati per evitare sovrapposizione con voto sporgente */}
      {bonusIcons.length > 0 && (
        <YStack 
          position="absolute" 
          left={4} 
          top={60} 
          gap={4}
          zIndex={12}
        >
          {bonusIcons.slice(0, 4).map((b, i) => (
            <YStack
              key={i}
              width={24}
              height={24}
              borderRadius={12}
              backgroundColor={`${b.color ?? '#111'}E6`}
              alignItems="center"
              justifyContent="center"
              borderWidth={2}
              borderColor="#fff"
              elevation={5}
            >
              <b.Icon size={12} color="#fff" />
              {b.count > 1 && (
                <YStack
                  position="absolute"
                  top={-6}
                  right={-6}
                  width={14}
                  height={14}
                  borderRadius={7}
                  backgroundColor="#ef4444"
                  alignItems="center"
                  justifyContent="center"
                  borderWidth={1}
                  borderColor="#fff"
                >
                  <Text fontSize={7} color="#fff" fontWeight="700">
                    {b.count}
                  </Text>
                </YStack>
              )}
            </YStack>
          ))}
        </YStack>
      )}

      {/* Chance info - lato sinistro basso per field variant senza vote */}
      {!vote && chanceSummary && (
        <YStack
          position="absolute"
          left={8}
          bottom={55}
          paddingHorizontal={6}
          paddingVertical={4}
          backgroundColor={injury ? "rgba(220, 38, 38, 0.9)" : "rgba(8, 145, 178, 0.9)"}
          borderRadius={6}
          borderWidth={2}
          borderColor="#fff"
          zIndex={15}
          elevation={5}
        >
          <Text fontSize={9} color="#fff" fontWeight="700">
            {injury ? "INF" : chanceSummary.sources.join(',')}
          </Text>
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
