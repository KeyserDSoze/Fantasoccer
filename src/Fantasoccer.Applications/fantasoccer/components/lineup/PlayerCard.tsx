import React, { useMemo, useState } from 'react'
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

  // Game info helper
  const gameInfo = useMemo(() => {
    if (!realGame) return null
    const actualDate = new Date()
    const isPlayed = realGame.date ?? new Date() < actualDate
    if (isPlayed) {
      return {
        type: 'result' as const,
        text: `${realGame.homeGoals}-${realGame.awayGoals}`,
        subtitle: `${realGame.home} vs ${realGame.away}`
      }
    } else {
      const gameDate = realGame.date ? new Date(realGame.date) : null
      return {
        type: 'upcoming' as const,
        text: gameDate ? gameDate.toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' }) : 'TBD',
        subtitle: `${realGame.home} vs ${realGame.away}`
      }
    }
  }, [realGame])

  // Sizing based on variant
  const isField = variant === 'field'
  const cardWidth = isField ? 120 : 150
  const cardHeight = isField ? 160 : 200
  const imageSize = isField ? 80 : 110 // Immagine molto più grande!

  return (
    <Card
      width={cardWidth}
      height={cardHeight}
      padding={0}
      backgroundColor="#fff"
      borderRadius={12}
      borderWidth={2}
      borderColor="#e5e5e5"
      overflow="hidden"
      position="relative"
      elevation={4}
    >
      {/* IMMAGINE GIOCATORE - SFONDO COMPLETO della card */}
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
      {/* Vote badge - posizionato sopra l'immagine, angolo top-left */}
      {vote && (
        <YStack
          position="absolute"
          top={8}
          left={8}
          width={isField ? 32 : 36}
          height={isField ? 32 : 36}
          borderRadius={isField ? 16 : 18}
          backgroundColor="#dc2626"
          alignItems="center"
          justifyContent="center"
          zIndex={15}
          borderWidth={3}
          borderColor="#fff"
          elevation={5}
        >
          <Text fontSize={isField ? 10 : 12} fontWeight="800" color="#fff">
            {voteLabel}
          </Text>
        </YStack>
      )}

      {/* Team name badge - angolo top-right */}
      <YStack
        position="absolute"
        top={8}
        right={8}
        paddingHorizontal={isField ? 6 : 8}
        paddingVertical={isField ? 3 : 4}
        backgroundColor="rgba(31, 41, 55, 0.9)"
        borderRadius={6}
        zIndex={15}
        borderWidth={1}
        borderColor="rgba(255, 255, 255, 0.3)"
      >
        <Text fontSize={isField ? 8 : 9} color="#fff" fontWeight="700">
          {teamName.slice(0, 3).toUpperCase()}
        </Text>
      </YStack>

      {/* Game info badge - sotto il team name */}
      {gameInfo && (
        <XStack
          position="absolute"
          top={isField ? 35 : 42}
          right={8}
          paddingHorizontal={isField ? 4 : 6}
          paddingVertical={isField ? 2 : 3}
          backgroundColor={gameInfo.type === 'result' ? "rgba(5, 150, 105, 0.9)" : "rgba(2, 132, 199, 0.9)"}
          borderRadius={4}
          alignItems="center"
          gap={2}
          zIndex={15}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
        >
          {gameInfo.type === 'result' ? (
            <Trophy size={isField ? 8 : 10} color="#fff" />
          ) : (
            <Calendar size={isField ? 8 : 10} color="#fff" />
          )}
          <Text fontSize={isField ? 7 : 8} color="#fff" fontWeight="600">
            {gameInfo.text}
          </Text>
        </XStack>
      )}

      {/* Injury indicator - angolo bottom-right dell'immagine */}
      {injury && (
        <Button
          position="absolute"
          bottom={isField ? 45 : 55}
          right={8}
          width={isField ? 28 : 32}
          height={isField ? 28 : 32}
          borderRadius={isField ? 14 : 16}
          backgroundColor="rgba(220, 38, 38, 0.95)"
          padding={0}
          onPress={() => setShowInjuryTooltip(!showInjuryTooltip)}
          zIndex={15}
          borderWidth={2}
          borderColor="#fff"
          elevation={5}
        >
          <AlertTriangle size={isField ? 14 : 16} color="#fff" />
        </Button>
      )}

      {/* Injury tooltip */}
      {injury && showInjuryTooltip && (
        <YStack
          position="absolute"
          bottom={isField ? 80 : 95}
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
          <Text fontSize={isField ? 8 : 9} color="#fff" textAlign="center">
            {injury.reason || 'Infortunato'}
          </Text>
        </YStack>
      )}

      {/* Rimuoviamo la sezione dell'immagine centrale - ora è lo sfondo */}

      {/* Bonus icons - lato sinistro dell'immagine */}
      {bonusIcons.length > 0 && (
        <YStack 
          position="absolute" 
          left={4} 
          top={isField ? 40 : 55} 
          gap={isField ? 3 : 4}
          zIndex={12}
        >
          {bonusIcons.slice(0, isField ? 3 : 4).map((b, i) => (
            <YStack
              key={i}
              width={isField ? 20 : 24}
              height={isField ? 20 : 24}
              borderRadius={isField ? 10 : 12}
              backgroundColor={`${b.color ?? '#111'}E6`}
              alignItems="center"
              justifyContent="center"
              borderWidth={2}
              borderColor="#fff"
              elevation={5}
            >
              <b.Icon size={isField ? 10 : 12} color="#fff" />
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
      {isField && !vote && chanceSummary && (
        <YStack
          position="absolute"
          left={8}
          bottom={isField ? 45 : 55}
          paddingHorizontal={6}
          paddingVertical={4}
          backgroundColor={injury ? "rgba(220, 38, 38, 0.9)" : "rgba(8, 145, 178, 0.9)"}
          borderRadius={6}
          borderWidth={2}
          borderColor="#fff"
          zIndex={15}
          elevation={5}
        >
          <Text fontSize={isField ? 8 : 9} color="#fff" fontWeight="700">
            {injury ? "INF" : chanceSummary.sources.join(',')}
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
        paddingHorizontal={8}
        paddingVertical={isField ? 6 : 8}
        alignItems="center"
        borderBottomLeftRadius={12}
        borderBottomRightRadius={12}
        zIndex={10}
        borderTopWidth={1}
        borderTopColor="rgba(255, 255, 255, 0.3)"
      >
        <Text
          fontSize={isField ? 11 : 13}
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
        {!isField && roleLabel && (
          <Text 
            fontSize={isField ? 8 : 9} 
            color="rgba(255, 255, 255, 0.9)" 
            marginTop={2} 
            fontWeight="600"
            textShadowColor="rgba(0, 0, 0, 0.5)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
          >
            {roleLabel}
          </Text>
        )}
      </YStack>
    </Card>
  )
}

export default PlayerCard
