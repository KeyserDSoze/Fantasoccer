import type { LeagueSource } from './group'

// Source: 0 basket enrolment, 1 manual, 2 qualification, 3 legacy import.
export interface Participant {
  teamId: string; basketId: string; name: string; isDerived: boolean; authoritativeTeamId: string; authoritativeBasketId: string
  owner: string; additionalOwners: string[]; source: number; sourceLeagueId: string | null; qualificationRank: number | null; isArchived: boolean; logoUrl: string | null
}
export interface LeagueParticipants { leagueId: string; seasonIndex: number; mode: number; sources: LeagueSource[]; participants: Participant[] }
export interface QualificationCandidate {
  sourceLeagueId: string; sourceLeagueName: string; rank: number; originTeamId: string; name: string; owner: string
  qualified: boolean; alreadyAdmitted: boolean; targetBasketId: string | null; problem: string | null
}
export interface QualificationPreview { leagueId: string; seasonIndex: number; candidates: QualificationCandidate[] }
export interface QualifiedTeam { sourceLeagueId: string; originTeamId: string }
