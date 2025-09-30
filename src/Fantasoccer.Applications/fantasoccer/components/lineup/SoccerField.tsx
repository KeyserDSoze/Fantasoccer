import { memo, useMemo } from 'react'
import { YStack, XStack, Text, ScrollView } from 'tamagui'
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

export interface SoccerFieldProps {
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

export const SoccerField = memo(({ gameWrapper, style }: SoccerFieldProps) => {
  const { width, height, isMobile, orientation, cardScale } = useResponsiveField()
  const { home, away } = useMemo(() => pickHomeAwayTeams(gameWrapper), [gameWrapper])
console.log(away?.players)
  const homeStarters: LivePlayer[] = (home?.players ?? []).filter(p => 
    p.currentPosition >= FantaSoccerRole.GoalKeeper && p.currentPosition <= FantaSoccerRole.Forward
  )
  const awayStarters: LivePlayer[] = (away?.players ?? []).filter(p => 
    p.currentPosition >= FantaSoccerRole.GoalKeeper && p.currentPosition <= FantaSoccerRole.Forward
  )

  // Panchine - giocatori backup (riserve)
  const homeBench: LivePlayer[] = (home?.players ?? []).filter(p => 
    p.currentPosition >= FantaSoccerRole.BackupGoalKeeper && p.currentPosition <= FantaSoccerRole.SecondBackupForward
  )
  const awayBench: LivePlayer[] = (away?.players ?? []).filter(p => 
    p.currentPosition >= FantaSoccerRole.BackupGoalKeeper && p.currentPosition <= FantaSoccerRole.SecondBackupForward
  )

  // Tribuna - giocatori non utilizzati
  const homeTribune: LivePlayer[] = (home?.players ?? []).filter(p => 
    p.currentPosition === FantaSoccerRole.Tribune
  )
  const awayTribune: LivePlayer[] = (away?.players ?? []).filter(p => 
    p.currentPosition === FantaSoccerRole.Tribune
  )

  // Funzione per ottenere il label del ruolo
  const getRoleLabel = (role: FantaSoccerRole): string => {
    switch (role) {
      case FantaSoccerRole.GoalKeeper:
      case FantaSoccerRole.BackupGoalKeeper:
        return 'P'
      case FantaSoccerRole.Defensor:
      case FantaSoccerRole.FirstBackupDefensor:
      case FantaSoccerRole.SecondBackupDefensor:
        return 'D'
      case FantaSoccerRole.Midfielder:
      case FantaSoccerRole.FirstBackupMidfielder:
      case FantaSoccerRole.SecondBackupMidfielder:
        return 'C'
      case FantaSoccerRole.Forward:
      case FantaSoccerRole.FirstBackupForward:
      case FantaSoccerRole.SecondBackupForward:
        return 'A'
      default:
        return 'T'
    }
  }

  const getRoleColor = (role: FantaSoccerRole): string => {
    switch (role) {
      case FantaSoccerRole.GoalKeeper:
      case FantaSoccerRole.BackupGoalKeeper:
        return '#f59e0b'
      case FantaSoccerRole.Defensor:
      case FantaSoccerRole.FirstBackupDefensor:
      case FantaSoccerRole.SecondBackupDefensor:
        return '#3b82f6'
      case FantaSoccerRole.Midfielder:
      case FantaSoccerRole.FirstBackupMidfielder:
      case FantaSoccerRole.SecondBackupMidfielder:
        return '#10b981'
      case FantaSoccerRole.Forward:
      case FantaSoccerRole.FirstBackupForward:
      case FantaSoccerRole.SecondBackupForward:
        return '#ef4444'
      default:
        return '#6b7280'
    }
  }
  // Group players by FantaSoccerRole numeric enum (titolari: 0..3)
  function groupByPosition(players: LivePlayer[]) {
    const groups: { GK: LivePlayer[]; DEF: LivePlayer[]; MID: LivePlayer[]; FWD: LivePlayer[] } = { GK: [], DEF: [], MID: [], FWD: [] }
    players.forEach(p => {
      // position can be on player.position or player.current.position depending on shape
      const pos = p.currentPosition
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
  
  return (
    <YStack space={16} {...style}>
      {/* CAMPO DA CALCIO */}
      <YStack 
        position="relative" 
        width="100%" 
        height={height}
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

      {/* PANCHINE */}
      <XStack space={16} flex={1} justifyContent="space-between">
        {/* Panchina Casa */}
        <YStack flex={1} space={8}>
          <YStack 
            backgroundColor="#16a34a" 
            paddingHorizontal={16} 
            paddingVertical={8} 
            borderRadius={8}
            alignItems="center"
          >
            <Text fontSize={16} fontWeight="700" color="#fff">
              🏠 PANCHINA {home?.name?.toUpperCase() || 'CASA'}
            </Text>
          </YStack>
          <ScrollView horizontal showsHorizontalScrollIndicator={false}>
            <XStack space={8} paddingHorizontal={8}>
              {homeBench.map((player, i) => (
                <YStack key={`home-bench-${i}`} alignItems="center" space={4}>
                  {/* Badge ruolo sopra la card */}
                  <YStack
                    backgroundColor={getRoleColor(player.currentPosition)}
                    paddingHorizontal={8}
                    paddingVertical={4}
                    borderRadius={12}
                    minWidth={24}
                    alignItems="center"
                  >
                    <Text fontSize={12} fontWeight="700" color="#fff">
                      {getRoleLabel(player.currentPosition)}
                    </Text>
                  </YStack>
                  <PlayerCard player={player} variant="bench" />
                </YStack>
              ))}
              {homeBench.length === 0 && (
                <Text fontSize={14} color="$gray10" fontStyle="italic">
                  Nessun giocatore in panchina
                </Text>
              )}
            </XStack>
          </ScrollView>
        </YStack>

        {/* Panchina Trasferta */}
        <YStack flex={1} space={8}>
          <YStack 
            backgroundColor="#dc2626" 
            paddingHorizontal={16} 
            paddingVertical={8} 
            borderRadius={8}
            alignItems="center"
          >
            <Text fontSize={16} fontWeight="700" color="#fff">
              ✈️ PANCHINA {away?.name?.toUpperCase() || 'TRASFERTA'}
            </Text>
          </YStack>
          <ScrollView horizontal showsHorizontalScrollIndicator={false}>
            <XStack space={8} paddingHorizontal={8}>
              {awayBench.map((player, i) => (
                <YStack key={`away-bench-${i}`} alignItems="center" space={4}>
                  {/* Badge ruolo sopra la card */}
                  <YStack
                    backgroundColor={getRoleColor(player.currentPosition)}
                    paddingHorizontal={8}
                    paddingVertical={4}
                    borderRadius={12}
                    minWidth={24}
                    alignItems="center"
                  >
                    <Text fontSize={12} fontWeight="700" color="#fff">
                      {getRoleLabel(player.currentPosition)}
                    </Text>
                  </YStack>
                  <PlayerCard player={player} variant="bench" />
                </YStack>
              ))}
              {awayBench.length === 0 && (
                <Text fontSize={14} color="$gray10" fontStyle="italic">
                  Nessun giocatore in panchina
                </Text>
              )}
            </XStack>
          </ScrollView>
        </YStack>
      </XStack>

      {/* TRIBUNA */}
      {(homeTribune.length > 0 || awayTribune.length > 0) && (
        <YStack space={12}>
          <YStack 
            backgroundColor="#6b7280" 
            paddingHorizontal={16} 
            paddingVertical={8} 
            borderRadius={8}
            alignItems="center"
          >
            <Text fontSize={16} fontWeight="700" color="#fff">
              🏟️ TRIBUNA
            </Text>
          </YStack>
          
          <XStack space={16} flex={1}>
            {/* Tribuna Casa */}
            <YStack flex={1} space={8}>
              <Text fontSize={14} fontWeight="600" color="$color" textAlign="center">
                {home?.name || 'Casa'}
              </Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <XStack space={8} paddingHorizontal={8}>
                  {homeTribune.map((player, i) => (
                    <PlayerCard key={`home-tribune-${i}`} player={player} variant="tribune" />
                  ))}
                  {homeTribune.length === 0 && (
                    <Text fontSize={12} color="$gray10" fontStyle="italic">
                      Nessuno in tribuna
                    </Text>
                  )}
                </XStack>
              </ScrollView>
            </YStack>

            {/* Tribuna Trasferta */}
            <YStack flex={1} space={8}>
              <Text fontSize={14} fontWeight="600" color="$color" textAlign="center">
                {away?.name || 'Trasferta'}
              </Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <XStack space={8} paddingHorizontal={8}>
                  {awayTribune.map((player, i) => (
                    <PlayerCard key={`away-tribune-${i}`} player={player} variant="tribune" />
                  ))}
                  {awayTribune.length === 0 && (
                    <Text fontSize={12} color="$gray10" fontStyle="italic">
                      Nessuno in tribuna
                    </Text>
                  )}
                </XStack>
              </ScrollView>
            </YStack>
          </XStack>
        </YStack>
      )}
    </YStack>
  )
})

export default SoccerField

