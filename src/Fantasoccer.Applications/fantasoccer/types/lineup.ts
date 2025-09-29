import { Role } from './group'

export type PlayerLineupStatus = 'starter' | 'bench' | 'tribune' | 'out' | 'injured'
export type MatchStatus = 'scheduled' | 'live' | 'finished'

export type PlayerEventType =
  | 'goal'
  | 'assist'
  | 'penaltyScored'
  | 'penaltySaved'
  | 'penaltyMissed'
  | 'yellowCard'
  | 'redCard'
  | 'ownGoal'
  | 'cleanSheet'
  | 'substitutionIn'
  | 'substitutionOut'
  | 'injury'
  | 'manOfTheMatch'
  | 'other'

export type PlayerAvailabilityStatus = 'starter' | 'probable' | 'doubtful' | 'out' | 'bench' | 'unknown'

export interface PlayerAvailabilitySourceInfo {
  source: 'fantacalcio' | 'gazzetta' | (string & {})
  label?: string
  status: PlayerAvailabilityStatus
  probability?: number | null
  note?: string | null
}

export interface PlayerAvailabilityInfo {
  description?: string | null
  status?: PlayerAvailabilityStatus
  statusNote?: string | null
  sources?: PlayerAvailabilitySourceInfo[]
  trend?: number | null
  lastGamePositiveness?: number | null
}

export interface PlayerParticipationInfo {
  played?: boolean
  reason?: string | null
}

export interface PlayerEventIndicator {
  type: PlayerEventType
  label?: string
  count?: number
  minute?: number
  isPositive?: boolean
}

export interface PlayerMatchResult {
  goalsFor: number
  goalsAgainst: number
  minute?: number
  isLive?: boolean
}

export interface PlayerMatchInfo {
  opponent: string
  isHome: boolean
  status: MatchStatus
  kickoff?: string
  venueName?: string
  result?: PlayerMatchResult | null
}

export interface LineupPlayerInfo {
  id: string
  name: string
  key?: string | null
  role: Role
  roleLabel: string
  fantasyRoleLabel?: string
  teamName: string
  jerseyNumber?: number | null
  isCaptain?: boolean
  status: PlayerLineupStatus
  vote?: number | null
  voteIsFinal?: boolean
  match?: PlayerMatchInfo | null
  events?: PlayerEventIndicator[]
  note?: string | null
  imageKey?: string | null
  availability?: PlayerAvailabilityInfo | null
  participation?: PlayerParticipationInfo | null
}

export interface LineupPlayerSlot {
  slotId: string
  player: LineupPlayerInfo | null
}

export interface LineupRow {
  rowId: string
  slots: LineupPlayerSlot[]
}

export interface LineupSide {
  teamName: string
  moduleLabel: string
  rows: LineupRow[]
  bench: LineupPlayerSlot[]
  tribune: LineupPlayerSlot[]
  color?: string
}

export interface LineupFieldData {
  home: LineupSide
  away: LineupSide
}
