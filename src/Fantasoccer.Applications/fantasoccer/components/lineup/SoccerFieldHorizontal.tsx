import { memo, useMemo } from 'react'
import { YStack, XStack } from 'tamagui'
import type { GameWrapper, LiveTeam, LivePlayer } from 'types/game'
import { FantaSoccerRole } from 'types/team'
import { PlayerCard } from './PlayerCard'
import { useResponsiveField } from '../../hooks/useResponsiveField'

// SVG field background as a React component with responsive orientation
interface SoccerFieldSVGProps {
  orientation: 'horizontal' | 'vertical'
}

const SoccerFieldSVG = memo(({ orientation }: SoccerFieldSVGProps) => {
  // Usiamo sempre lo stesso campo orizzontale e lo ruotiamo con CSS per mobile
  return (
    <svg 
      width="100%" 
      height="100%" 
      viewBox="0 0 1400 900" 
      xmlns="http://www.w3.org/2000/svg" 
      aria-label="Campo da calcio"
      style={{
        transform: orientation === 'vertical' ? 'rotate(90deg)' : 'none',
        transformOrigin: 'center center'
      }}
    >
      <g id="campo">
        <rect fill="#15803d" x="0" y="0" width="1400" height="900"/>
        <rect stroke="#ffffff" strokeWidth="8" fill="none" x="40" y="40" width="1320" height="820"/>
        <line stroke="#ffffff" strokeWidth="6" x1="700" y1="40" x2="700" y2="860"/>
        <circle stroke="#ffffff" strokeWidth="6" fill="none" cx="700" cy="450" r="90"/>
        <circle fill="#ffffff" cx="700" cy="450" r="6"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="40" y="250" width="200" height="400"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="40" y="340" width="80" height="220"/>
        <circle fill="#ffffff" cx="200" cy="450" r="6"/>
        <path stroke="#ffffff" strokeWidth="5" fill="none" d="M 240 380 A 60 60 0 0 1 240 520"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="1160" y="250" width="200" height="400"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="1280" y="340" width="80" height="220"/>
        <circle fill="#ffffff" cx="1200" cy="450" r="6"/>
        <path stroke="#ffffff" strokeWidth="5" fill="none" d="M 1160 520 A 60 60 0 0 1 1160 380"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="0" y="390" width="40" height="120"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="1360" y="390" width="40" height="120"/>
      </g>
    </svg>
  )
})

export interface SoccerFieldHorizontalProps {
  gameWrapper?: GameWrapper | null
  style?: any
}

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
    console.log(gameWrapper)
  const { width, height, isMobile, orientation, cardScale } = useResponsiveField()
  const { home, away } = useMemo(() => pickHomeAwayTeams(gameWrapper), [gameWrapper])

  const homeStarters: LivePlayer[] = (home?.players ?? [])
  const awayStarters: LivePlayer[] = (away?.players ?? [])

  // Group players by FantaSoccerRole numeric enum (titolari: 0..3)
  function groupByPosition(players: LivePlayer[]) {
    const groups: { GK: LivePlayer[]; DEF: LivePlayer[]; MID: LivePlayer[]; FWD: LivePlayer[] } = { GK: [], DEF: [], MID: [], FWD: [] }
    players.forEach(p => {
      // position can be on player.position or player.current.position depending on shape
      const pos = p.currentPosition
      console.log(p.current.name + ' pos: ' + pos)
      switch (pos) {
        case FantaSoccerRole.GoalKeeper:
          groups.GK.push(p)
          break
        case FantaSoccerRole.Defensor:
          groups.DEF.push(p)
          break
        case FantaSoccerRole.Midfielder:
          groups.MID.push(p)
          break
        case FantaSoccerRole.Forward:
          groups.FWD.push(p)
          break
      }
    })
    return groups
  }

  const homeGroups = groupByPosition(homeStarters)
  const awayGroups = groupByPosition(awayStarters)

  // Stripes order: home GK, home DEF, home MID, home FWD, then mirrored away FWD, MID, DEF, GK
  const stripes: LivePlayer[][] = [
    homeGroups.GK,
    homeGroups.DEF,
    homeGroups.MID,
    homeGroups.FWD,
    awayGroups.FWD,
    awayGroups.MID,
    awayGroups.DEF,
    awayGroups.GK,
  ]
    console.log(stripes)
  return (
    <YStack 
      position="relative" 
      width="100%" 
      height={height}
      {...style}
    >
      {/* SVG field background with responsive orientation */}
      <SoccerFieldSVG orientation={orientation} />

      {/* 8 equal containers positioned absolutely over the field */}
      {stripes.map((playersInStripe, stripeIndex) => {
        if (isMobile) {
          // Mobile: vertical layout with horizontal rows
          const topPercent = stripeIndex * 12.5
          
          return (
            <XStack
              key={`stripe-${stripeIndex}`}
              position="absolute"
              top={`${topPercent}%`}
              left={0}
              width="100%"
              height="12.5%"
              justifyContent="center"
              alignItems="center"
              pointerEvents="none"
            >
              <XStack 
                pointerEvents="auto" 
                space={8} 
                alignItems="center" 
                justifyContent="center"
                style={{
                  transform: `scale(${cardScale})`,
                  transformOrigin: 'center center'
                }}
              >
                {playersInStripe.map((p, i) => (
                  <PlayerCard key={`${p.current?.name ?? ''}-${i}`} player={p} variant="field" />
                ))}
              </XStack>
            </XStack>
          )
        } else {
          // Desktop/Tablet: horizontal layout with vertical columns
          const leftPercent = stripeIndex * 12.5
          
          return (
            <YStack
              key={`stripe-${stripeIndex}`}
              position="absolute"
              left={`${leftPercent}%`}
              top={0}
              width="12.5%"
              height="100%"
              justifyContent="center"
              alignItems="center"
              pointerEvents="none"
            >
              <YStack 
                pointerEvents="auto" 
                space={8} 
                alignItems="center" 
                justifyContent="center"
                style={{
                  transform: `scale(${cardScale})`,
                  transformOrigin: 'center center'
                }}
              >
                {playersInStripe.map((p, i) => (
                  <PlayerCard key={`${p.current?.name ?? ''}-${i}`} player={p} variant="field" />
                ))}
              </YStack>
            </YStack>
          )
        }
      })}
    </YStack>
  )
})

export default SoccerFieldHorizontal

