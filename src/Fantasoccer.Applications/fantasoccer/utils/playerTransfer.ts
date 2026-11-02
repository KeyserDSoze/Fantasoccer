import type { RealPlayer } from '../types/serieA'

type TransferFields = Pick<RealPlayer, 'leftSerieA' | 'previousTeam' | 'transferredAtUtc'>

const date = (value?: string | null) => {
  const parsed = value ? new Date(value) : null
  return parsed && !Number.isNaN(parsed.getTime()) ? parsed : null
}

export const formatTransferDate = (value?: string | null, locale = 'it-IT') =>
  date(value)?.toLocaleDateString(locale, { day: '2-digit', month: '2-digit', year: 'numeric' }) ?? '—'

// Text for the last move of the season. inactiveInRoster: a fantasy roster flags the
// player as no longer listed in Serie A even when no statistics row is available.
export function transferNotice(player: TransferFields | null | undefined, inactiveInRoster = false, locale = 'it-IT'): string | null {
  const when = date(player?.transferredAtUtc)
  const suffix = when ? ` il ${when.toLocaleDateString(locale, { day: '2-digit', month: '2-digit' })}` : ''
  if (player?.leftSerieA || inactiveInRoster) return `Ha lasciato la Serie A${suffix}`
  if (!when) return null
  return player?.previousTeam ? `Trasferito dal ${player.previousTeam.name}${suffix}` : `Arrivato in Serie A${suffix}`
}
