import { memo, useMemo } from 'react'
import { XStack, YStack } from 'tamagui'
import type { GameWrapper, LiveTeam, LivePlayer } from 'types/game'
import { PlayerCard } from './PlayerCard'

// SVG field background as a React component (scaled for mobile)
const SoccerFieldSVGMobile = memo(() => (
  <svg width="100%" height="100%" viewBox="0 0 700 900" xmlns="http://www.w3.org/2000/svg" aria-label="Campo da calcio">
    <rect x="0" y="0" width="700" height="900" fill="#0a0a0a"/>
    <g id="campo">
      <rect fill="#15803d" x="50" y="80" width="600" height="740" rx="20"/>
      <rect stroke="#ffffff" strokeWidth="8" fill="none" x="80" y="110" width="540" height="680"/>
      <line stroke="#ffffff" strokeWidth="6" x1="350" y1="110" x2="350" y2="790"/>
      <circle stroke="#ffffff" strokeWidth="6" fill="none" cx="350" cy="450" r="90"/>
      <circle fill="#ffffff" cx="350" cy="450" r="6"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="80" y="280" width="90" height="340"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="80" y="360" width="30" height="180"/>
      <circle fill="#ffffff" cx="145" cy="450" r="6"/>
      <path stroke="#ffffff" strokeWidth="3" fill="none" d="M 160 390 A 60 60 0 0 1 160 510"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="540" y="280" width="90" height="340"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="none" x="610" y="360" width="30" height="180"/>
      <circle fill="#ffffff" cx="555" cy="450" r="6"/>
      <path stroke="#ffffff" strokeWidth="3" fill="none" d="M 540 510 A 60 60 0 0 1 540 390"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="62" y="410" width="18" height="80"/>
      <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="670" y="410" width="18" height="80"/>
    </g>
  </svg>
))

export interface SoccerFieldMobileProps {
  gameWrapper?: GameWrapper | null
  style?: any
}

function pickHomeAwayTeamsMobile(gameWrapper?: GameWrapper | null): { home?: LiveTeam; away?: LiveTeam } {
  const teams = gameWrapper?.teams ?? []
  if (!teams.length) return {}

  const game = gameWrapper?.game
  if (game) {
    const homeName = (game.home ?? '').toLowerCase()
    const awayName = (game.away ?? '').toLowerCase()
    const foundHome = teams.find(t => (t.name ?? '').toLowerCase() === homeName)
    const foundAway = teams.find(t => (t.name ?? '').toLowerCase() === awayName)
    if (foundHome || foundAway) return { home: foundHome ?? teams[0], away: foundAway ?? teams[1] ?? teams[0] }
  }

  return { home: teams[0], away: teams[1] }
}

export const SoccerFieldMobile = memo(({ gameWrapper, style }: SoccerFieldMobileProps) => {
  const { home, away } = useMemo(() => pickHomeAwayTeamsMobile(gameWrapper), [gameWrapper])

  const homeStarters: LivePlayer[] = (home?.players ?? []).slice(0, 11)
  const awayStarters: LivePlayer[] = (away?.players ?? []).slice(0, 11)

  return (
    <YStack position="relative" width="100%" height={700} {...style}>
      <SoccerFieldSVGMobile />

      {/* Home team - top */}
      <YStack position="absolute" left={0} top={40} width="100%" height="45%" justifyContent="center" pointerEvents="none">
        <YStack pointerEvents="auto" space={10} alignItems="center">
          {homeStarters.map((p, i) => (
            <PlayerCard key={(p.current?.name ?? '') + '-' + i} player={p} variant="starter" />
          ))}
        </YStack>
      </YStack>

      {/* Away team - bottom */}
      <YStack position="absolute" left={0} bottom={40} width="100%" height="45%" justifyContent="center" pointerEvents="none">
        <YStack pointerEvents="auto" space={10} alignItems="center">
          {awayStarters.map((p, i) => (
            <PlayerCard key={(p.current?.name ?? '') + '-' + i} player={p} variant="starter" showTeamName={true} />
          ))}
        </YStack>
      </YStack>
    </YStack>
  )
})

export default SoccerFieldMobile
