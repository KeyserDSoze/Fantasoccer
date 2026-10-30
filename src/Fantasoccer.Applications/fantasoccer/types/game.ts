import type { CalendarGame } from './calendar'
import type { FantaSoccerRole, Player } from './team'
import type { RealGame, Vote } from './serieA'
import type { Chance } from './chance'

export interface VoteValue { value: number; special: boolean }
export interface LivePlayer {
  current: Player
  vote?: Vote | null
  chance?: Chance | null
  realGame?: RealGame | null
  finalValue?: VoteValue | null
  currentPosition: FantaSoccerRole
}
export interface LiveFormationPlayerChange { name: string; fromPosition: FantaSoccerRole; toPosition: FantaSoccerRole }
export interface LiveFormationChange { date: string | null; author: string; players: LiveFormationPlayerChange[] }
export interface LiveTeam {
  teamId: string
  basketId: string
  version: number
  sourceVersion: number | null
  name: string
  owner: string
  additionalOwners: string[]
  players: LivePlayer[]
  lastUpdate?: string | null
  formationChangesMaximum?: number | null
  formationChangesUsed?: number | null
  formationChangesRemaining?: number | null
  formationChangeHistory: LiveFormationChange[]
  logoUrl?: string | null
  canEdit: boolean
  // Team owning roster and economy; differs from teamId only for a derived team.
  authoritativeTeamId?: string
  isDerived?: boolean
  // Lineup entries whose player left the roster after saving (never auto-corrected).
  unavailablePlayerIds?: string[]
  // Day whose lineup is proposed (not saved yet) because this day has none.
  proposedFromDay?: number | null
  // The owner chose a different lineup per competition (team settings).
  separateLineups?: boolean
}
export interface GameWrapper {
  teams: LiveTeam[]
  game: CalendarGame
  serieADay: number
  canEdit: boolean
  isLiveFormationWindow: boolean
  liveFormationDeadline: string | null
  liveFormationChangesRemaining: number | null
  resultIsPreview: boolean
  awaitingPostponedMatches?: boolean
}
export default GameWrapper
