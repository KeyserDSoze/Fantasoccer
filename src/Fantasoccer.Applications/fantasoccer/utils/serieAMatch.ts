import type { FootballMatch, LineupSource } from '../types/footballMatch'
import type { GameWrapper } from '../types/game'
import { RealCalendarHelper, type RealCalendar, type RealRankedTeam } from '../types/serieA'

const HOUR = 60 * 60 * 1000


// How often a match page asks again. Near the match the server answers from a 15 s cache; far from it nothing changes quickly.
export function matchRefreshInterval(match: Pick<FootballMatch, 'game'> | null | undefined, now = Date.now()): number | null {
  const date = match?.game.date ? new Date(match.game.date).getTime() : null
  if (date === null) return match ? 5 * 60 * 1000 : null
  if (now >= date - 3 * HOUR && now <= date + 5 * HOUR) return 15 * 1000
  if (now < date) return date - now <= 48 * HOUR ? 5 * 60 * 1000 : null
  return now - date <= 36 * HOUR ? 2 * 60 * 1000 : null
}

// The field is read-only here: no live-change counters (the "Controlli partita" box) and nothing editable.
export function toFieldGame(match: FootballMatch): GameWrapper {
  return {
    ...match.lineup,
    canEdit: false,
    isLiveFormationWindow: false,
    liveFormationDeadline: null,
    liveFormationChangesRemaining: null,
    teams: match.lineup.teams.map(team => ({
      ...team, canEdit: false, formationChangesMaximum: null, formationChangesUsed: null, formationChangesRemaining: null,
    })),
  }
}

export function lineupNote(sources: LineupSource[]): string | null {
  if (sources.every(source => source === 'unavailable')) return 'Formazione non disponibile'
  if (sources.some(source => source === 'unavailable')) return 'Formazione non disponibile per una delle due squadre'
  if (sources.every(source => source === 'probable')) return 'Probabili formazioni'
  if (sources.some(source => source === 'probable')) return 'Probabile formazione per una delle due squadre'
  return 'Formazione ricostruita dai voti: panchina e tribuna stimate'
}

// The day to open first: the one in progress, else the next one to play, else the last one.
export function defaultDay(calendar: RealCalendar | null): number | null {
  if (!calendar?.days.length) return null
  const day = RealCalendarHelper.getLiveDay(calendar) ?? RealCalendarHelper.getNextDay(calendar) ?? RealCalendarHelper.getLastDay(calendar)
  return day?.serieADay ?? [...calendar.days].sort((a, b) => b.serieADay - a.serieADay)[0].serieADay
}

// Polling interval for a season: quick while a match may be running, slow while others are coming, none for a closed season.
export function seasonRefreshInterval(calendar: RealCalendar | null | undefined, now = Date.now()): number | null {
  const dates = (calendar?.days ?? []).flatMap(day => day.games).filter(game => !game.delayed && game.date).map(game => new Date(game.date!).getTime())
  if (dates.some(date => now >= date - 3 * HOUR && now <= date + 5 * HOUR)) return 30 * 1000
  if (dates.some(date => date > now || now - date <= 36 * HOUR)) return 5 * 60 * 1000
  return null
}

// Standings as they were after a given day: only matches of days up to it, already played (a match being played is not counted).
// Ties on points go by goal difference, goals scored, then the order of the official standings (head-to-head is not available).
export function rankAtDay(calendar: RealCalendar | null | undefined, day: number, official: RealRankedTeam[] = []): RealRankedTeam[] {
  const table = new Map<string, RealRankedTeam>()
  const entry = (team: { clubId?: string; name: string }) => {
    const key = team.clubId ?? team.name
    let row = table.get(key)
    if (!row) { row = { clubId: team.clubId, name: team.name, points: 0, victories: 0, draws: 0, defeats: 0, goalsFor: 0, goalsAgainst: 0, goalDifference: 0, played: 0 }; table.set(key, row) }
    return row
  }
  for (const value of calendar?.days ?? []) {
    for (const game of value.games) {
      const home = entry(game.home); const away = entry(game.away)
      if (value.serieADay > day || game.delayed || game.homeGoals == null || game.awayGoals == null) continue
      if (!RealCalendarHelper.isGamePlayed(game) || RealCalendarHelper.isGamePlaying(game)) continue
      home.played++; away.played++
      home.goalsFor += game.homeGoals; home.goalsAgainst += game.awayGoals
      away.goalsFor += game.awayGoals; away.goalsAgainst += game.homeGoals
      if (game.homeGoals > game.awayGoals) { home.victories++; home.points += 3; away.defeats++ }
      else if (game.homeGoals < game.awayGoals) { away.victories++; away.points += 3; home.defeats++ }
      else { home.draws++; away.draws++; home.points++; away.points++ }
    }
  }
  const position = (team: RealRankedTeam) => { const index = official.findIndex(value => (value.clubId && value.clubId === team.clubId) || value.name === team.name); return index < 0 ? Number.MAX_SAFE_INTEGER : index }
  return [...table.values()].map(row => ({ ...row, goalDifference: row.goalsFor - row.goalsAgainst }))
    .sort((a, b) => b.points - a.points || b.goalDifference - a.goalDifference || b.goalsFor - a.goalsFor || position(a) - position(b) || a.name.localeCompare(b.name))
}
