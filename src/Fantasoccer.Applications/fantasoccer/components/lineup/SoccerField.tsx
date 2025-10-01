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

  // Panchine - giocatori backup (riserve) ordinati per currentPosition
  const homeBench: LivePlayer[] = (home?.players ?? [])
    .filter(p => 
      p.currentPosition >= FantaSoccerRole.BackupGoalKeeper && p.currentPosition <= FantaSoccerRole.SecondBackupForward
    )
    .sort((a, b) => a.currentPosition - b.currentPosition)
    
  const awayBench: LivePlayer[] = (away?.players ?? [])
    .filter(p => 
      p.currentPosition >= FantaSoccerRole.BackupGoalKeeper && p.currentPosition <= FantaSoccerRole.SecondBackupForward
    )
    .sort((a, b) => a.currentPosition - b.currentPosition)

  // Tribuna - giocatori non utilizzati
  const homeTribune: LivePlayer[] = (home?.players ?? []).filter(p => 
    p.currentPosition === FantaSoccerRole.Tribune
  )
  const awayTribune: LivePlayer[] = (away?.players ?? []).filter(p => 
    p.currentPosition === FantaSoccerRole.Tribune
  )

  // Funzione per raggruppare giocatori della panchina per ruolo
  const groupBenchByRole = (benchPlayers: LivePlayer[]) => {
    const groups = {
      goalkeepers: benchPlayers.filter(p => p.currentPosition === FantaSoccerRole.BackupGoalKeeper),
      defenders: benchPlayers.filter(p => 
        p.currentPosition === FantaSoccerRole.FirstBackupDefensor ||
        p.currentPosition === FantaSoccerRole.SecondBackupDefensor
      ).sort((a, b) => a.currentPosition - b.currentPosition),
      midfielders: benchPlayers.filter(p => 
        p.currentPosition === FantaSoccerRole.FirstBackupMidfielder ||
        p.currentPosition === FantaSoccerRole.SecondBackupMidfielder
      ).sort((a, b) => a.currentPosition - b.currentPosition),
      forwards: benchPlayers.filter(p => 
        p.currentPosition === FantaSoccerRole.FirstBackupForward ||
        p.currentPosition === FantaSoccerRole.SecondBackupForward
      ).sort((a, b) => a.currentPosition - b.currentPosition)
    }
    return groups
  }

  const homeBenchGroups = groupBenchByRole(homeBench)
  const awayBenchGroups = groupBenchByRole(awayBench)

  /**
   * Componente riutilizzabile per sezioni panchina
   * @param players - Array di giocatori da mostrare
   * @param sectionKey - Chiave univoca per le key React (es. 'home-gk', 'away-def')
   * @param roleLabel - Etichetta del ruolo da mostrare (es. 'PORTIERI', 'DIFENSORI')
   * @param backgroundColor - Colore hex di sfondo per il badge del ruolo (es. '#f59e0b')
   * @param roleIcon - Emoji icona del ruolo (es. '🥅', '🛡️')
   * @returns Layout mobile: YStack con ruolo sopra e cards sotto | Layout desktop: XStack con |ruolo verticale|cards orizzontali| o null se nessun giocatore
   */
  const renderBenchSection = (
    players: LivePlayer[],
    sectionKey: string,
    roleLabel: string,
    backgroundColor: string,
    roleIcon: string
  ) => {
    if (players.length === 0) return null

    // Converte hex color in rgba con opacità 0.1
    const hexToRgba = (hex: string, alpha: number = 0.1) => {
      const r = parseInt(hex.slice(1, 3), 16)
      const g = parseInt(hex.slice(3, 5), 16)
      const b = parseInt(hex.slice(5, 7), 16)
      return `rgba(${r}, ${g}, ${b}, ${alpha})`
    }

    // Layout mobile: colonna tradizionale
    if (isMobile) {
      return (
        <YStack gap={8} backgroundColor={hexToRgba(backgroundColor)} borderRadius={8} padding={12}>
          {/* Ruolo orizzontale sopra */}
          <Text 
            fontSize={14} 
            fontWeight="700" 
            color={backgroundColor}
            textAlign="center"
            paddingBottom={4}
          >
            {roleIcon} {roleLabel}
          </Text>
          
          {/* Cards giocatori in colonna */}
          <YStack gap={8} alignItems="center" style={{
            transform: `scale(${cardScale})`,
            transformOrigin: 'center top'
          }}>
            {players.map((player, i) => (
              <PlayerCard key={`${sectionKey}-${i}`} player={player} />
            ))}
          </YStack>
        </YStack>
      )
    }

    // Layout desktop: riga con ruolo verticale a sinistra
    return (
      <XStack gap={8} backgroundColor={hexToRgba(backgroundColor)} borderRadius={8} padding={12}>
        {/* Ruolo in verticale */}
        <YStack 
          minWidth={60} 
          height={180}
          justifyContent="center" 
          alignItems="center"
          backgroundColor={backgroundColor}
          borderRadius={8}
          paddingHorizontal={8}
          paddingVertical={2}
        >
          <Text 
            fontSize={14} 
            fontWeight="700" 
            color="#fff"
            style={{ 
              writingMode: 'vertical-lr',
              textOrientation: 'mixed'
            }}
          >
            {roleIcon} {roleLabel}
          </Text>
        </YStack>
        
        {/* Cards giocatori in riga */}
        <XStack gap={gapBetweenCards} flexWrap="wrap" flex={1} style={{
          transform: `scale(${cardScale})`,
          transformOrigin: 'left top'
        }}>
          {players.map((player, i) => (
            <PlayerCard key={`${sectionKey}-${i}`} player={player} />
          ))}
        </XStack>
      </XStack>
    )
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
  const paddingBottomForRole = 9
  const gapBetweenCards = 8
  return (
    <YStack gap={16} {...style}>
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
                  gap={gapBetweenCards}
                  alignItems="center" 
                  justifyContent="center"
                  style={{
                    transform: `scale(${cardScale})`,
                    transformOrigin: 'center center'
                  }}
                >
                  {playersInStripe.map((p, i) => (
                    <PlayerCard key={`${p.current?.name ?? ''}-${i}`} player={p} />
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
                  gap={gapBetweenCards}
                  alignItems="center" 
                  justifyContent="center"
                  style={{
                    transform: `scale(${cardScale})`,
                    transformOrigin: 'center center'
                  }}
                >
                  {playersInStripe.map((p, i) => (
                    <PlayerCard key={`${p.current?.name ?? ''}-${i}`} player={p} />
                  ))}
                </YStack>
              </YStack>
            )
          }
        })}
      </YStack>
      {/* PANCHINE ORGANIZZATE PER RUOLO */}
      <XStack gap={16} flex={1} justifyContent="space-between">
        {/* Panchina Casa */}
        <YStack flex={1} gap={12}>
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

          {/* Sezioni ruoli con componente riutilizzabile */}
          {renderBenchSection(homeBenchGroups.goalkeepers, 'home-gk', 'PORTIERI', '#f59e0b', '🥅')}
          {renderBenchSection(homeBenchGroups.defenders, 'home-def', 'DIFENSORI', '#3b82f6', '🛡️')}
          {renderBenchSection(homeBenchGroups.midfielders, 'home-mid', 'CENTROCAMPISTI', '#10b981', '⚽')}
          {renderBenchSection(homeBenchGroups.forwards, 'home-fwd', 'ATTACCANTI', '#ef4444', '🎯')}

          {homeBench.length === 0 && (
            <Text fontSize={14} color="$gray10" fontStyle="italic" textAlign="center">
              Nessun giocatore in panchina
            </Text>
          )}
        </YStack>

        {/* Panchina Trasferta */}
        <YStack flex={1} gap={12}>
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

          {/* Sezioni ruoli con componente riutilizzabile */}
          {renderBenchSection(awayBenchGroups.goalkeepers, 'away-gk', 'PORTIERI', '#f59e0b', '🥅')}
          {renderBenchSection(awayBenchGroups.defenders, 'away-def', 'DIFENSORI', '#3b82f6', '🛡️')}
          {renderBenchSection(awayBenchGroups.midfielders, 'away-mid', 'CENTROCAMPISTI', '#10b981', '⚽')}
          {renderBenchSection(awayBenchGroups.forwards, 'away-fwd', 'ATTACCANTI', '#ef4444', '🎯')}

          {awayBench.length === 0 && (
            <Text fontSize={14} color="$gray10" fontStyle="italic" textAlign="center">
              Nessun giocatore in panchina
            </Text>
          )}
        </YStack>
      </XStack>

      {/* TRIBUNA */}
      {(homeTribune.length > 0 || awayTribune.length > 0) && (
        <YStack gap={12}>
          <YStack 
            backgroundColor="#6b7280" 
            paddingHorizontal={16} 
            paddingVertical={8} 
            borderRadius={8}
            alignItems="center"
          >
            <Text fontSize={16} fontWeight="700" color="#fff" paddingBottom={paddingBottomForRole}>
              🏟️ TRIBUNA
            </Text>
          </YStack>
          
          <XStack gap={16} flex={1}>
            {/* Tribuna Casa */}
            <YStack flex={1} gap={8}>
              <Text fontSize={14} fontWeight="600" color="$color" textAlign="center" paddingBottom={paddingBottomForRole}>
                {home?.name || 'Casa'}
              </Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <XStack gap={gapBetweenCards}paddingHorizontal={8}>
                  {homeTribune.map((player, i) => (
                    <PlayerCard key={`home-tribune-${i}`} player={player} />
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
            <YStack flex={1} gap={8}>
              <Text fontSize={14} fontWeight="600" color="$color" textAlign="center" paddingBottom={paddingBottomForRole}>
                {away?.name || 'Trasferta'}
              </Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <XStack gap={gapBetweenCards}paddingHorizontal={8}>
                  {awayTribune.map((player, i) => (
                    <PlayerCard key={`away-tribune-${i}`} player={player} />
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

