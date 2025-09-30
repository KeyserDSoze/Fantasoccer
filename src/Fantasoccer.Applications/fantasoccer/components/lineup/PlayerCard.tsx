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
  const cardWidth = isField ? 110 : 140
  const cardHeight = isField ? 150 : 180
  const imageSize = isField ? 60 : 80

  return (
    <Card
      width={cardWidth}
      height={cardHeight}
      padding={0}
      backgroundColor="#fff"
      borderRadius={8}
      borderWidth={2}
      borderColor="#e5e5e5"
      overflow="hidden"
      position="relative"
      elevation={3}
    >
      {/* Header - Team name and game info */}
      <XStack
        height={isField ? 20 : 24}
        backgroundColor="#1f2937"
        alignItems="center"
        justifyContent="space-between"
        paddingHorizontal={4}
      >
        <Text fontSize={isField ? 8 : 10} color="#fff" fontWeight="600" numberOfLines={1} flex={1}>
          {teamName.slice(0, 10).toUpperCase()}
        </Text>
        {gameInfo && (
          <XStack alignItems="center" gap={2}>
            {gameInfo.type === 'result' ? (
              <Trophy size={isField ? 10 : 12} color="#fbbf24" />
            ) : (
              <Calendar size={isField ? 10 : 12} color="#60a5fa" />
            )}
            <Text fontSize={isField ? 7 : 8} color="#fff" fontWeight="500">
              {gameInfo.text}
            </Text>
          </XStack>
        )}
      </XStack>

      {/* Main content area */}
      <YStack flex={1} alignItems="center" justifyContent="center" paddingTop={8}>
        {/* Vote badge - top left corner */}
        {vote && (
          <YStack
            position="absolute"
            top={isField ? 24 : 28}
            left={4}
            width={isField ? 24 : 28}
            height={isField ? 24 : 28}
            borderRadius={isField ? 12 : 14}
            backgroundColor="#ef4444"
            alignItems="center"
            justifyContent="center"
            zIndex={10}
            borderWidth={2}
            borderColor="#fff"
          >
            <Text fontSize={isField ? 8 : 10} fontWeight="800" color="#fff">
              {voteLabel}
            </Text>
          </YStack>
        )}

        {/* Injury indicator - top right corner */}
        {injury && (
          <Button
            position="absolute"
            top={isField ? 24 : 28}
            right={4}
            width={isField ? 24 : 28}
            height={isField ? 24 : 28}
            borderRadius={isField ? 12 : 14}
            backgroundColor="#dc2626"
            padding={0}
            onPress={() => setShowInjuryTooltip(!showInjuryTooltip)}
            zIndex={10}
          >
            <AlertTriangle size={isField ? 12 : 16} color="#fff" />
          </Button>
        )}

        {/* Injury tooltip */}
        {injury && showInjuryTooltip && (
          <YStack
            position="absolute"
            top={isField ? 50 : 60}
            right={4}
            backgroundColor="#000"
            padding={8}
            borderRadius={4}
            maxWidth={cardWidth - 16}
            zIndex={20}
          >
            <Text fontSize={8} color="#fff" textAlign="center">
              {injury.reason || 'Infortunato'}
            </Text>
          </YStack>
        )}

        {/* Player image - center */}
        <YStack
          width={imageSize}
          height={imageSize}
          borderRadius={6}
          backgroundColor="#f3f4f6"
          overflow="hidden"
          alignItems="center"
          justifyContent="center"
          borderWidth={2}
          borderColor="#e5e7eb"
        >
          <Image
            source={{ uri: imageUri }}
            width={imageSize - 4}
            height={imageSize - 4}
            resizeMode="cover"
            onError={() => setUseFallbackImage(true)}
          />
        </YStack>

        {/* Bonus icons - right side */}
        {bonusIcons.length > 0 && (
          <YStack position="absolute" right={4} bottom={isField ? 40 : 50} gap={2}>
            {bonusIcons.slice(0, isField ? 2 : 3).map((b, i) => (
              <YStack
                key={i}
                width={isField ? 16 : 20}
                height={isField ? 16 : 20}
                borderRadius={isField ? 8 : 10}
                backgroundColor={b.color ?? '#111'}
                alignItems="center"
                justifyContent="center"
                borderWidth={1}
                borderColor="#fff"
              >
                <b.Icon size={isField ? 8 : 10} color="#fff" />
                {b.count > 1 && (
                  <Text
                    position="absolute"
                    top={-4}
                    right={-4}
                    fontSize={6}
                    color="#fff"
                    backgroundColor="#ef4444"
                    borderRadius={6}
                    paddingHorizontal={2}
                    paddingVertical={1}
                    minWidth={12}
                    textAlign="center"
                  >
                    {b.count}
                  </Text>
                )}
              </YStack>
            ))}
          </YStack>
        )}

        {/* Chance info - left side for field variant without vote */}
        {isField && !vote && chanceSummary && (
          <YStack
            position="absolute"
            left={4}
            bottom={isField ? 40 : 50}
            paddingHorizontal={4}
            paddingVertical={2}
            backgroundColor={injury ? "#dc2626" : "#0891b2"}
            borderRadius={3}
            borderWidth={1}
            borderColor="#fff"
          >
            <Text fontSize={7} color="#fff" fontWeight="600">
              {injury ? "INF" : chanceSummary.sources.join(',')}
            </Text>
          </YStack>
        )}
      </YStack>

      {/* Footer - Player name and role */}
      <YStack
        backgroundColor="#16a34a"
        paddingHorizontal={6}
        paddingVertical={isField ? 4 : 6}
        alignItems="center"
      >
        <Text
          fontSize={isField ? 9 : 11}
          fontWeight="700"
          color="#fff"
          textAlign="center"
          numberOfLines={1}
        >
          {name}
        </Text>
        {!isField && roleLabel && (
          <Text fontSize={7} color="#dcfce7" marginTop={1} textAlign="center">
            {roleLabel}
          </Text>
        )}
      </YStack>
    </Card>
  )
}

export default PlayerCard
