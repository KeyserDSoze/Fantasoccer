import { FantaSoccerRole } from '../types/team'
import { Role } from '../types/group'

export interface EmptySlot {
  key: string
  area: 'bench' | 'tribune'
  position: FantaSoccerRole
  label: string
}

// Bench places of each role: one goalkeeper, two for every other role.
const BENCH_PLACES: Partial<Record<Role, FantaSoccerRole[]>> = {
  [Role.GoalKeeper]: [FantaSoccerRole.BackupGoalKeeper],
  [Role.Defensor]: [FantaSoccerRole.FirstBackupDefensor, FantaSoccerRole.SecondBackupDefensor],
  [Role.Midfielder]: [FantaSoccerRole.FirstBackupMidfielder, FantaSoccerRole.SecondBackupMidfielder],
  [Role.Forward]: [FantaSoccerRole.FirstBackupForward, FantaSoccerRole.SecondBackupForward],
}

// Free places the selected player can be moved to, shown as empty cards: the stands, and the bench places of his role nobody has.
// A starter leaves the field only if the formation can do without him (canLeaveField).
export function emptySlotsFor(role: Role, position: FantaSoccerRole, occupied: number[], canLeaveField: boolean): EmptySlot[] {
  const isStarter = position >= FantaSoccerRole.GoalKeeper && position <= FantaSoccerRole.Forward
  if (isStarter && !canLeaveField) return []
  const slots: EmptySlot[] = []
  const places = BENCH_PLACES[role] ?? []
  places.forEach((place, index) => {
    if (place === position || occupied.includes(place)) return
    slots.push({ key: `bench-${place}`, area: 'bench', position: place, label: places.length === 1 ? 'Panchinaro' : `${index + 1}° panchinaro` })
  })
  if (position !== FantaSoccerRole.Tribune) slots.push({ key: 'tribune', area: 'tribune', position: FantaSoccerRole.Tribune, label: 'In tribuna' })
  return slots
}
