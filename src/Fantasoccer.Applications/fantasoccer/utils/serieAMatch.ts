import type { FootballMatch, LineupSource } from '../types/footballMatch'
import type { GameWrapper } from '../types/game'
import { RealCalendarHelper, type RealCalendar } from '../types/serieA'

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
