import type { CalendarDay } from '../types/calendar'
import type { LiveGroup } from '../types/liveGroup'

export function homeMatchDisplay(days: CalendarDay[], live: LiveGroup | null, leagueId: string | null, year: number | null) {
  const league = live?.year === year ? live.leagues.find(item => item.id === leagueId) : undefined
  // A cup may have no fixtures on the current Serie A day. In that case (or a
  // cold group cache), keep the last played day instead of replacing it with an empty Live.
  const hasFixtures = Object.values(league?.rounds ?? {}).some(day => day.games.length > 0)
  const isLive = Boolean(live?.isInLiveWindow && league && hasFixtures && !league.isConsolidated)
  if (isLive && league) {
    return { isLive: true, days: Object.values(league.rounds), rank: league.rank,
      pulseResults: Boolean(live?.isPlaying), serieADay: live?.serieADay ?? null }
  }
  const played = days.filter(day => day.games.some(game => game.result != null))
  const lastDay = played.length ? Math.max(...played.map(day => day.serieADay)) : null
  return { isLive: false, days: played.filter(day => day.serieADay === lastDay), rank: null, pulseResults: false, serieADay: lastDay }
}
