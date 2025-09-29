import { memo, useMemo } from 'react'
import { YStack } from 'tamagui'
import type { GameWrapper, LiveTeam, LivePlayer } from 'types/game'
import { PlayerCard } from './PlayerCard'

// SVG field background as a React component
const SoccerFieldSVG = memo(() => (
  <svg width="100%" height="100%" viewBox="0 0 1400 900" xmlns="http://www.w3.org/2000/svg" aria-label="Campo da calcio">
    <rect x="0" y="0" width="1400" height="900" fill="#0a0a0a"/>
    <g id="campo">
      <rect fill="#15803d" x="100" y="80" width="1200" height="740" rx="20"/>
      <rect stroke="#ffffff" strokeWidth="8" fill="none" x="130" y="110" width="1140" height="680"/>
      <line stroke="#ffffff" strokeWidth="6" x1="700" y1="110" x2="700" y2="790"/>
      <circle stroke="#ffffff" strokeWidth="6" fill="none" cx="700" cy="450" r="90"/>
      <circle fill="#ffffff" cx="700" cy="450" r="6"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="130" y="280" width="190" height="340"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="130" y="360" width="60" height="180"/>
      <circle fill="#ffffff" cx="295" cy="450" r="6"/>
      <path stroke="#ffffff" strokeWidth="3" fill="none" d="M 320 390 A 60 60 0 0 1 320 510"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="1080" y="280" width="190" height="340"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="1210" y="360" width="60" height="180"/>
      <circle fill="#ffffff" cx="1105" cy="450" r="6"/>
      <path stroke="#ffffff" strokeWidth="3" fill="none" d="M 1080 510 A 60 60 0 0 1 1080 390"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="112" y="410" width="18" height="80"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="1270" y="410" width="18" height="80"/>
    </g>
  </svg>
))

export interface SoccerFieldHorizontalProps {
  gameWrapper?: GameWrapper | null
  style?: any
}

const PLAYER_GAP = 12

function pickHomeAwayTeams(gameWrapper?: GameWrapper | null): { home?: LiveTeam; away?: LiveTeam } {
  const teams = gameWrapper?.teams ?? []
  if (!teams.length) return {}

  // Try to match by game home/away names
  const game = gameWrapper?.game
  if (game) {
    const homeName = (game.home ?? '').toLowerCase()
    const awayName = (game.away ?? '').toLowerCase()
    const foundHome = teams.find(t => (t.name ?? '').toLowerCase() === homeName)
    const foundAway = teams.find(t => (t.name ?? '').toLowerCase() === awayName)
    if (foundHome || foundAway) return { home: foundHome ?? teams[0], away: foundAway ?? teams[1] ?? teams[0] }
  }

  // Fallback to first/second
  return { home: teams[0], away: teams[1] }
}

export const SoccerFieldHorizontal = memo(({ gameWrapper, style }: SoccerFieldHorizontalProps) => {
  const { home, away } = useMemo(() => pickHomeAwayTeams(gameWrapper), [gameWrapper])

  const homeStarters: LivePlayer[] = (home?.players ?? []).slice(0, 11)
  const awayStarters: LivePlayer[] = (away?.players ?? []).slice(0, 11)

  return (
    <YStack position="relative" width="100%" height={700} {...style}>
      <SoccerFieldSVG />

      {/* Home column - left */}
      <YStack position="absolute" left={40} top={80} height="100%" justifyContent="center" pointerEvents="none">
        <YStack pointerEvents="auto" space={PLAYER_GAP}>
          {homeStarters.map((p, i) => (
            <PlayerCard key={(p.current?.name ?? '') + '-' + i} player={p} variant="starter" />
          ))}
        </YStack>
      </YStack>

      {/* Away column - right */}
      <YStack position="absolute" right={40} top={80} height="100%" justifyContent="center" pointerEvents="none">
        <YStack pointerEvents="auto" space={PLAYER_GAP} alignItems="flex-end">
          {awayStarters.map((p, i) => (
            <PlayerCard key={(p.current?.name ?? '') + '-' + i} player={p} variant="starter" showTeamName={true} />
          ))}
        </YStack>
      </YStack>
    </YStack>
  )
})

export default SoccerFieldHorizontal

