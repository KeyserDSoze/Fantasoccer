export interface OpeningCompetitionStanding {
  owner: string
  name: string | null
  score: number
  prize: number
}

export interface OpeningCompetitionStandings {
  group: string
  league: string
  year: number
  serieADays: number
  teams: OpeningCompetitionStanding[]
}

export interface OpeningCompetitionStandingsRaw {
  g: string
  l: string
  y: number
  d: number
  t: Array<{ o: string; n?: string | null; s: number; p: number }>
}

export const mapOpeningCompetitionStandings = (raw: OpeningCompetitionStandingsRaw): OpeningCompetitionStandings => ({
  group: raw.g,
  league: raw.l,
  year: raw.y,
  serieADays: raw.d,
  teams: raw.t.map(team => ({ owner: team.o, name: team.n ?? null, score: team.s, prize: team.p }))
})
