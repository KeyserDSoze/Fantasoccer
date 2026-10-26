export interface OpeningCompetitionStanding {
  teamId: string
  basketId: string
  owner: string
  name: string | null
  score: number
  prize: number
  position: number
}
export interface OpeningCompetitionStandings {
  group: string
  league: string
  year: number
  serieADays: number
  teams: OpeningCompetitionStanding[]
  version: number
}
