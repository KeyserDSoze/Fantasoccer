import React, { useMemo, useState } from 'react'
import { Card, Image, Text, XStack, YStack, useMedia } from 'tamagui'
import {
  Trophy,
  Target,
  Star,
  Umbrella,
  Award,
  Shield,
  AlertTriangle,
  ArrowUpCircle,
  ArrowDownCircle,
} from '@tamagui/lucide-icons'

import type { LivePlayer } from 'types/game'
import { getPlayerImageSources } from 'utils/playerImage'
import type { Vote } from 'types/serieA'
import type { Chance } from 'types/chance'

export type PlayerCardVariant = 'starter' | 'bench' | 'tribune'

export interface PlayerCardProps {
  player: LivePlayer | any
  variant?: PlayerCardVariant
  showTeamName?: boolean
}

// Small mapping from vote fields to icon + label
const BONUS_ICON_MAP: { key: keyof Vote; icon: any; label: string }[] = [
  { key: 'goals', icon: Trophy, label: 'Gol' },
  { key: 'assists', icon: Target, label: 'Assist' },
  { key: 'penalties', icon: Award, label: 'Rig.' },
  { key: 'stoppedPenalties', icon: Shield, label: 'Par.' },
  { key: 'ownGoals', icon: Umbrella, label: 'Aut.' },
]

export function PlayerCard({ player, variant = 'starter', showTeamName = true }: PlayerCardProps) {
  const media = useMedia()
  const [useFallbackImage, setUseFallbackImage] = useState(false)

  // Support both LivePlayer and the older LineupPlayerInfo shapes
  const live: LivePlayer | undefined = (player && (player.current || player.c)) ? player as LivePlayer : undefined

  const current = live ? live.current : (player as any)
  const vote: Vote | undefined = live ? (live.vote as Vote | undefined) : (player.vote as Vote | undefined)
  const chance: Chance | undefined = live ? (live.chance as Chance | undefined) : (player.chance as Chance | undefined)

  const name = current?.name ?? 'Giocatore'
  const teamName = current?.team?.name ?? current?.realTeam?.name ?? ''
  const roleLabel = current?.roleLabel ?? current?.role ?? ''

  const { src, fallback } = useMemo(() => getPlayerImageSources({ key: current?.key ?? current?.id, name }), [current?.key, current?.id, name])
  const imageUri = useFallbackImage ? fallback : src

  const voteLabel = vote ? (typeof vote.value === 'number' ? vote.value.toFixed(1) : String(vote.value)) : null

  const bonusIcons = useMemo(() => {
    if (!vote) return []
    const icons: { Icon: any; count: number; label: string }[] = []
    for (const m of BONUS_ICON_MAP) {
      const count = (vote as any)[m.key] ?? 0
      if (count && count > 0) icons.push({ Icon: m.icon, count, label: m.label })
    }
    // Add man of the match
    if (vote.manOfTheMatch) icons.push({ Icon: Star, count: 1, label: 'MVP' })
    return icons
  }, [vote])

  // If no vote, show chance summary (sources + status)
  const chanceSummary = useMemo(() => {
    if (!chance) return null
    const sources = (chance.fantagazzetta ? ['F'] : []).concat(chance.gazzetta ? ['G'] : [], chance.mediaset ? ['M'] : [], chance.sky ? ['S'] : [])
    return { status: chance.status, sources, label: chance.description }
  }, [chance])

  return (
    <Card padding="$3" backgroundColor="$color2" borderRadius="$4" borderWidth={1} borderColor="$borderColor">
      <XStack gap="$3" alignItems="center">
        <YStack alignItems="center">
          <Image
            source={{ uri: imageUri }}
            width={variant === 'starter' ? 100 : variant === 'bench' ? 72 : 64}
            height={variant === 'starter' ? 100 : variant === 'bench' ? 72 : 64}
            borderRadius={999}
            resizeMode="cover"
            onError={() => setUseFallbackImage(true)}
          />
        </YStack>

        <YStack flex={1}>
          <Text fontSize="$4" fontWeight="700" numberOfLines={1}>{name}</Text>
          {showTeamName && <Text fontSize="$2" color="$color9">{teamName}</Text>}
          <Text fontSize="$2" color="$color10">{roleLabel}</Text>

          {vote ? (
            <XStack gap="$2" marginTop="$2" alignItems="center">
              <YStack paddingHorizontal="$2" paddingVertical="$1" borderRadius="$3" backgroundColor="$color3">
                <Text fontWeight="700">{voteLabel}</Text>
              </YStack>
              <XStack gap="$1" alignItems="center">
                {bonusIcons.map((b, i) => (
                  <XStack key={i} alignItems="center" gap="$1">
                    <b.Icon size={16} />
                    {b.count > 1 && <Text fontSize="$1">x{b.count}</Text>}
                  </XStack>
                ))}
              </XStack>
            </XStack>
          ) : chanceSummary ? (
            <XStack gap="$2" marginTop="$2" alignItems="center">
              <Text fontSize="$2" color="$color9">{chanceSummary.label ?? 'Probabile formazione'}</Text>
              <Text fontSize="$1" color="$color9">{chanceSummary.sources.join(', ')}</Text>
            </XStack>
          ) : null}
        </YStack>
      </XStack>
    </Card>
  )
}

export default PlayerCard
