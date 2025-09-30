import React, { useMemo, useState } from 'react'
import { Card, Image, Text, XStack, YStack } from 'tamagui'
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
  const [showTooltip, setShowTooltip] = useState(false)

  const current = (player as any).current ?? (player as any)
  const vote: Vote | null | undefined = (player as any).vote
  const chance: Chance | null | undefined = (player as any).chance
  const injury = (player as any).injury || (current && (current as any).injury)

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

  // Sizing based on variant
  const isField = variant === 'field'
  const cardWidth = isField ? 90 : 120
  const imageSize = isField ? 70 : 90

  return (
    <YStack alignItems="center" width={cardWidth} pointerEvents="auto">
      {/* Vote circle - top left of image */}
      <YStack position="relative" alignItems="center">
        {vote && (
          <YStack
            position="absolute"
            top={-8}
            left={-8}
            width={28}
            height={28}
            borderRadius={14}
            backgroundColor="#ef4444"
            alignItems="center"
            justifyContent="center"
            zIndex={10}
          >
            <Text fontSize={isField ? 10 : 12} fontWeight="800" color="#fff">
              {voteLabel}
            </Text>
          </YStack>
        )}

        {/* Injury icon - top right */}
        {injury && (
          <YStack position="absolute" top={-8} right={-8} zIndex={10}>
            <AlertTriangle size={20} color="#ef4444" />
          </YStack>
        )}

        {/* Circular player image */}
        <YStack
          width={imageSize}
          height={imageSize}
          borderRadius={imageSize / 2}
          borderWidth={3}
          borderColor="#fff"
          backgroundColor="#000"
          overflow="hidden"
          alignItems="center"
          justifyContent="center"
        >
          <Image
            source={{ uri: imageUri }}
            width={imageSize - 6}
            height={imageSize - 6}
            borderRadius={(imageSize - 6) / 2}
            resizeMode="cover"
            onError={() => setUseFallbackImage(true)}
          />
        </YStack>

        {/* Bonus icons - right side stacked */}
        {bonusIcons.length > 0 && (
          <YStack position="absolute" right={-12} top={8} gap={2}>
            {bonusIcons.slice(0, 3).map((b, i) => (
              <YStack
                key={i}
                width={18}
                height={18}
                borderRadius={9}
                backgroundColor={b.color ?? '#111'}
                alignItems="center"
                justifyContent="center"
              >
                <b.Icon size={10} color="#fff" />
              </YStack>
            ))}
          </YStack>
        )}
      </YStack>

      {/* Team label - vertical on left */}
      {showTeamName && teamName && !isField && (
        <YStack
          position="absolute"
          left={-15}
          top={10}
          paddingVertical={4}
          paddingHorizontal={2}
          backgroundColor="rgba(0,0,0,0.7)"
          borderRadius={2}
          transform={[{ rotate: '-90deg' }]}
          transformOrigin="center"
        >
          <Text fontSize={8} color="#fff" fontWeight="600">
            {teamName.slice(0, 3).toUpperCase()}
          </Text>
        </YStack>
      )}

      {/* Player name - bottom */}
      <YStack
        marginTop={4}
        paddingHorizontal={8}
        paddingVertical={2}
        backgroundColor="#16a34a"
        borderRadius={4}
        maxWidth={cardWidth}
      >
        <Text
          fontSize={isField ? 10 : 12}
          fontWeight="700"
          color="#fff"
          textAlign="center"
          numberOfLines={1}
        >
          {name}
        </Text>
      </YStack>

      {/* Role label - small below name */}
      {!isField && (
        <Text fontSize={8} color="$color9" marginTop={2} textAlign="center">
          {roleLabel}
        </Text>
      )}

      {/* Chance info for field variant */}
      {isField && !vote && chanceSummary && (
        <YStack marginTop={2} paddingHorizontal={4} paddingVertical={1} backgroundColor="rgba(0,0,0,0.5)" borderRadius={2}>
          <Text fontSize={8} color="#fff">
            {chanceSummary.sources.join(',')}
          </Text>
        </YStack>
      )}
    </YStack>
  )
}

export default PlayerCard
