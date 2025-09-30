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

export type PlayerCardVariant = 'starter' | 'bench' | 'tribune'

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

  // compact card sizes
  const imageSize = variant === 'starter' ? 110 : variant === 'bench' ? 80 : 64

  return (
    <Card padding={0} borderRadius="$8" borderWidth={1} borderColor="$borderColor" backgroundColor="$background" width="80%" marginLeft="10%" overflow="hidden">
      <YStack width="100%" height="auto" aspectRatio={1.1} position="relative" alignItems="center" justifyContent="center">
        {/* left vertical team ribbon with stacked letters */}
        {showTeamName && teamName ? (
          <YStack position="absolute" left="-6%" top="12%" width="12%" alignItems="center" justifyContent="center" backgroundColor="$color2" paddingVertical="$2">
            {String(teamName || '')
              .slice(0, 14)
              .split('')
              .map((ch, idx) => (
                <Text key={idx} fontSize="$1" color="$color9" opacity={0.95}>
                  {ch}
                </Text>
              ))}
          </YStack>
        ) : null}

        {/* framed player image */}
        <YStack width="46%" height="46%" borderRadius={999} alignItems="center" justifyContent="center" elevation={4} backgroundColor="$background">
          <YStack borderRadius={999} borderWidth={3} borderColor="$borderColor" overflow="hidden">
            <Image source={{ uri: imageUri }} width="100%" height="100%" resizeMode="cover" onError={() => setUseFallbackImage(true)} />
          </YStack>
        </YStack>

        {/* vote badge top-left */}
        {vote ? (
          <YStack position="absolute" top="4%" left="4%" borderRadius="$6" backgroundColor="#0284c7" paddingHorizontal="$2" paddingVertical="$1">
            <Text fontWeight="800" color="#fff">{voteLabel}</Text>
          </YStack>
        ) : chanceSummary ? (
          <YStack position="absolute" top="4%" left="4%" borderRadius="$6" backgroundColor="#475569" paddingHorizontal="$2" paddingVertical="$1">
            <Text fontSize="$1" color="#fff">P</Text>
          </YStack>
        ) : null}

        {/* injury icon top-right */}
        {injury ? (
          <YStack position="absolute" top="4%" right="4%">
            <YStack
              onHoverIn={() => setShowTooltip(true)}
              onHoverOut={() => setShowTooltip(false)}
              onPress={() => setShowTooltip(s => !s)}
              alignItems="center"
              justifyContent="center"
              padding="$1"
            >
              <AlertTriangle size={18} color="#ef4444" />
            </YStack>

            {showTooltip ? (
              <Card position="absolute" top="-8%" right={-8} padding="$2" borderRadius="$3" elevation={2} backgroundColor="#111">
                <Text color="#fff" fontSize="$1">{String((injury && injury.description) || 'Infortunio')}</Text>
              </Card>
            ) : null}
          </YStack>
        ) : null}

        {/* right stacked stat badges */}
        <YStack position="absolute" right="-4%" top="28%" gap="$2">
          {bonusIcons.slice(0, 3).map((b, i) => (
            <YStack key={i} width="12%" height="12%" borderRadius={999} backgroundColor={b.color ?? '#111'} alignItems="center" justifyContent="center">
              <b.Icon size={12} color="#fff" />
            </YStack>
          ))}
        </YStack>

        {/* bottom name ribbon overlapping */}
        <YStack position="absolute" bottom="4%" left="8%" right="8%" alignItems="center" justifyContent="center">
          <YStack borderRadius="$4" paddingHorizontal="$3" paddingVertical="$1" backgroundColor="#0ea5a4">
            <Text fontWeight="800" color="#fff">{name}</Text>
          </YStack>
        </YStack>
      </YStack>

      {/* small meta row */}
      <XStack padding="$2" gap="$2" alignItems="center" justifyContent="center">
        <Text fontSize="$1" color="$color9">{roleLabel}</Text>
        {!vote && chanceSummary ? <Text fontSize="$1" color="$color9">{(chanceSummary.sources || []).join(', ')}</Text> : null}
      </XStack>
    </Card>
  )
}

export default PlayerCard
