import { memo } from 'react'
import { XStack, YStack } from 'tamagui'
import type { LineupFieldData, LineupPlayerSlot, LineupRow, LineupSide } from 'types/lineup'
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

// Helper to get player slots by role
function getRoleRows(side: LineupSide): { [role: string]: LineupPlayerSlot[] } {
  const roleRows: { [role: string]: LineupPlayerSlot[] } = {}
  side.rows.forEach(row => {
    row.slots.forEach(slot => {
      if (slot.player) {
        const role = slot.player.roleLabel
        if (!roleRows[role]) roleRows[role] = []
        roleRows[role].push(slot)
      }
    })
  })
  return roleRows
}

export interface SoccerFieldHorizontalProps {
  data: LineupFieldData
  style?: any
}

export const SoccerFieldHorizontal = memo(({ data, style }: SoccerFieldHorizontalProps) => {
  // Home team (top/left), Away team (bottom/right)
  const homeRoles = getRoleRows(data.home)
  const awayRoles = getRoleRows(data.away)

  // For now, use fixed role order: GK, DEF, MID, ATT
  const roleOrder = ['POR', 'DIF', 'CEN', 'ATT']

  return (
    <YStack position="relative" width="100%" height={500} {...style}>
      <SoccerFieldSVG />
      {/* Home team: top, left */}
      <YStack position="absolute" left={0} top={0} width="100%" height="50%" justifyContent="flex-start" alignItems="flex-start" pointerEvents="none">
        {roleOrder.map((role, i) => (
          <XStack key={role} gap="$2" marginTop={40 + i * 60} marginLeft={120 + i * 40} pointerEvents="auto">
            {homeRoles[role]?.map((slot, idx) => (
              slot.player && <PlayerCard key={slot.slotId} player={slot.player} variant="starter" />
            ))}
          </XStack>
        ))}
      </YStack>
      {/* Away team: bottom, right */}
      <YStack position="absolute" right={0} bottom={0} width="100%" height="50%" justifyContent="flex-end" alignItems="flex-end" pointerEvents="none">
        {roleOrder.map((role, i) => (
          <XStack key={role} gap="$2" marginBottom={40 + i * 60} marginRight={120 + i * 40} pointerEvents="auto">
            {awayRoles[role]?.map((slot, idx) => (
              slot.player && <PlayerCard key={slot.slotId} player={slot.player} variant="starter" />
            ))}
          </XStack>
        ))}
      </YStack>
    </YStack>
  )
})
