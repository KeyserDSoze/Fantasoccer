import { StatPlayer, StatPlayerGame } from 'types/stat'
import { TrendType } from 'types/chance'

/**
 * Utility per calcolare metriche derivate da StatPlayer,
 * sostituendo i dati precedentemente ottenuti da Chance (trend, positiveness, lastGame)
 */

// ============================================
// CONFIGURAZIONE
// ============================================

const TREND_GAMES_COUNT = 5 // Numero di partite da considerare per il trend
const POSITIVENESS_GAMES_COUNT = 3 // Numero di partite per calcolare positiveness media

// ============================================
// CALCOLO TREND (0-4)
// ============================================

/**
 * Calcola il trend del giocatore basato sulle ultime N partite.
 * Logica CORRETTA:
 * - Prende le ultime TREND_GAMES_COUNT giornate (ordine decrescente)
 * - Se il giocatore NON ha giocato (vote === null), conta come -2.0 di positiveness
 * - Calcola la SOMMA del positiveness (incluse le partite non giocate)
 * - Mappa la somma su scala 0-4 (TrendType)
 * 
 * Mapping soglie (SOMMA di 5 partite):
 * - < -3: Bad (0) - molto negativo - freccia ↓ rossa
 * - -3 to 0: NotBad (1) - negativo - freccia ↘ arancione
 * - 1 to 3: Normal (2) - neutro - freccia → gialla
 * - 3 to 6: VeryGood (3) - positivo - freccia ↗ verde chiaro
 * - > 6: Excellent (4) - molto positivo - freccia ↑ verde scuro
 */
export function calculateTrend(player: StatPlayer): TrendType {
  if (!player.games || player.games.length === 0) {
    return TrendType.Normal // Default per giocatori senza storico
  }

  // Prendi le ultime TREND_GAMES_COUNT giornate (ordinate per serieADay decrescente)
  const recentGames = player.games
    .slice()
    .sort((a, b) => b.serieADay - a.serieADay)
    .slice(0, TREND_GAMES_COUNT)

  if (recentGames.length === 0) {
    return TrendType.Normal
  }

  // Calcola SOMMA positiveness - IMPORTANTE: se non ha giocato (vote === null) = -2.0
  const totalPositiveness = recentGames.reduce((sum, game) => {
    return sum + (game.vote !== null ? game.positiveness : -2.0)
  }, 0)

  // Mappa su scala trend con soglie basate su SOMMA di 5 partite
  if (totalPositiveness < -3) {
    return TrendType.Bad // 0 - Molto negativo (< -3)
  } else if (totalPositiveness <= 0) {
    return TrendType.NotBad // 1 - Negativo (-3 to 0)
  } else if (totalPositiveness < 3) {
    return TrendType.Normal // 2 - Neutro (>0 to <3, quindi 1 e 2)
  } else if (totalPositiveness <= 6) {
    return TrendType.VeryGood // 3 - Positivo (3 to 6)
  } else {
    return TrendType.Excellent // 4 - Molto positivo (> 6)
  }
}

// ============================================
// CALCOLO POSITIVENESS MEDIA
// ============================================

/**
 * Calcola la media del positiveness delle ultime N partite.
 * Usato per mostrare un valore numerico invece di solo il trend categorico.
 */
export function calculateAveragePositiveness(player: StatPlayer): number {
  if (!player.games || player.games.length === 0) {
    return 0
  }

  const recentGames = player.games
    .filter(game => game.vote !== null)
    .slice(-POSITIVENESS_GAMES_COUNT)

  if (recentGames.length === 0) {
    return 0
  }

  return recentGames.reduce((sum, game) => sum + game.positiveness, 0) / recentGames.length
}

// ============================================
// LAST GAME
// ============================================

/**
 * Ritorna l'ultimo gioco del giocatore (più recente per serieADay)
 */
export function getLastGame(player: StatPlayer): StatPlayerGame | null {
  if (!player.games || player.games.length === 0) {
    return null
  }

  // Ordina per serieADay decrescente e prendi il primo
  const sorted = [...player.games].sort((a, b) => b.serieADay - a.serieADay)
  return sorted[0] ?? null
}

// ============================================
// VISUAL MAPPING (da PlayerCard)
// ============================================

/**
 * Ritorna informazioni visive per il trend badge (colore, freccia e label)
 * Portato qui da PlayerCard per centralizzare la logica
 */
export function getTrendVisual(trend: TrendType): { backgroundColor: string; arrow: string; label: string } {
  const colorScale = [
    '#dc2626', // 0 Bad - rosso
    '#f97316', // 1 NotBad - arancione
    '#facc15', // 2 Normal - giallo
    '#4ade80', // 3 VeryGood - verde chiaro
    '#16a34a'  // 4 Excellent - verde scuro
  ]
  const arrows = ['↓', '↘', '→', '↗', '↑']
  const labels = ['Pessima', 'Negativa', 'Neutra', 'Positiva', 'Ottima']
  
  return {
    backgroundColor: colorScale[trend],
    arrow: arrows[trend],
    label: labels[trend]
  }
}

// ============================================
// HELPER PER CREARE MAPPA KEY->STATPLAYER
// ============================================

/**
 * Crea una mappa per lookup veloce dei giocatori per key (nome normalizzato)
 */
export function createStatPlayerMap(players: StatPlayer[]): Map<string, StatPlayer> {
  const map = new Map<string, StatPlayer>()
  
  for (const player of players) {
    if (player.key) {
      map.set(player.key, player)
    }
  }
  
  return map
}

// ============================================
// TIPO PER AGGREGARE TUTTI I DATI CALCOLATI
// ============================================

export interface CalculatedPlayerStats {
  trend: TrendType
  avgPositiveness: number
  lastGame: StatPlayerGame | null
  trendVisual: { backgroundColor: string; arrow: string; label: string }
  trendAverage: number // Media positiveness ultime 5 partite (usata per il trend)
}

/**
 * Funzione all-in-one per calcolare tutte le metriche da un StatPlayer
 */
export function calculatePlayerStats(player: StatPlayer): CalculatedPlayerStats {
  const trend = calculateTrend(player)
  const avgPositiveness = calculateAveragePositiveness(player)
  const lastGame = getLastGame(player)
  const trendVisual = getTrendVisual(trend)
  
  // Calcola la media delle ultime 5 partite per mostrare nel badge
  const recentGames = player.games
    .slice()
    .sort((a, b) => b.serieADay - a.serieADay)
    .slice(0, 5) // Ultime 5 giornate
  
  // Somma (non media) delle ultime 5 partite per il badge
  const trendAverage = recentGames.reduce(
    (sum, game) => sum + (game.vote !== null ? game.positiveness : -2.0),
    0
  )

  return {
    trend,
    avgPositiveness,
    lastGame,
    trendVisual,
    trendAverage
  }
}
