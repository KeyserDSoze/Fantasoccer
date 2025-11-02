/**
 * Formation Service
 * 
 * Servizio dedicato alla gestione delle formazioni automatiche.
 * Calcola la formazione ottimale basandosi su un algoritmo che valuta:
 * - Fantagazzetta (+5/-5)
 * - Status giocatore (Normal +1, Injury/Disqualified -99, Warned/Maybe -5)
 * - Trend (0-4 calcolato da StatPlayer)
 * - Positiveness dell'ultima partita (da StatPlayer)
 */

import type { LivePlayer, LiveTeam } from 'types/game'
import type { StatPlayer } from 'types/stat'
import type { Chance, ChanceType, TrendType } from 'types/chance'
import type { RealGame } from 'types/serieA'
import { FantaSoccerRole } from 'types/team'
import { Role } from 'types/group'
import { calculatePlayerStats } from 'utils/statPlayerCalculator'
import { getPlayerKey } from 'utils/keyUtils'

// ============================================
// CONSTANTS
// ============================================

// Squadre top della Serie A (non retrocederanno mai)
const TOP_SERIE_A_TEAMS = [
  'INTER',
  'MILAN', 
  'JUVENTUS',
  'NAPOLI',
  'ATALANTA',
  'ROMA'
]

// ============================================
// TYPES
// ============================================

export interface PlayerScore {
  player: LivePlayer
  score: number
  role: Role
}

export interface FormationLimits {
  minDefenders: number
  maxDefenders: number
  minMidfielders: number
  maxMidfielders: number
  minForwards: number
  maxForwards: number
}

// Moduli validi del fantacalcio
export const VALID_FORMATIONS: FormationLimits[] = [
  { minDefenders: 3, maxDefenders: 3, minMidfielders: 4, maxMidfielders: 4, minForwards: 3, maxForwards: 3 }, // 3-4-3
  { minDefenders: 3, maxDefenders: 3, minMidfielders: 5, maxMidfielders: 5, minForwards: 2, maxForwards: 2 }, // 3-5-2
  { minDefenders: 4, maxDefenders: 4, minMidfielders: 5, maxMidfielders: 5, minForwards: 1, maxForwards: 1 }, // 4-5-1
  { minDefenders: 5, maxDefenders: 5, minMidfielders: 4, maxMidfielders: 4, minForwards: 1, maxForwards: 1 }, // 5-4-1
  { minDefenders: 5, maxDefenders: 5, minMidfielders: 3, maxMidfielders: 3, minForwards: 2, maxForwards: 2 }, // 5-3-2
  { minDefenders: 4, maxDefenders: 4, minMidfielders: 4, maxMidfielders: 4, minForwards: 2, maxForwards: 2 }, // 4-4-2
  { minDefenders: 4, maxDefenders: 4, minMidfielders: 3, maxMidfielders: 3, minForwards: 3, maxForwards: 3 }, // 4-3-3
]

// Limiti assoluti per la formazione base (3-3-1)
const BASE_FORMATION = {
  goalkeepers: 1,
  defenders: 3,
  midfielders: 3,
  forwards: 1,
}

// Limiti massimi per ruolo
const MAX_STARTERS = {
  goalkeepers: 1,
  defenders: 5,
  midfielders: 5,
  forwards: 3,
}

// Numero di panchinari per ruolo
const BENCH_COUNT = {
  goalkeepers: 1, // Solo un portiere di riserva
  defenders: 2,
  midfielders: 2,
  forwards: 2,
}

// ============================================
// HELPER FUNCTIONS
// ============================================

/**
 * Verifica se una squadra è tra le top della Serie A
 */
const isTopTeam = (teamName: string): boolean => {
  const normalized = teamName.toUpperCase().trim()
  return TOP_SERIE_A_TEAMS.some(top => normalized.includes(top) || top.includes(normalized))
}

/**
 * Calcola il bonus/malus casa/trasferta e difficoltà avversario
 */
const calculateHomeAwayBonus = (realGame: RealGame | null | undefined, playerTeamName: string): number => {
  if (!realGame) return 0

  const playerTeamNormalized = playerTeamName.toUpperCase().trim()
  const homeTeamNormalized = realGame.home.name.toUpperCase().trim()
  const awayTeamNormalized = realGame.away.name.toUpperCase().trim()

  // Determina se il giocatore gioca in casa o trasferta
  const isHome = homeTeamNormalized.includes(playerTeamNormalized) || playerTeamNormalized.includes(homeTeamNormalized)
  const isAway = awayTeamNormalized.includes(playerTeamNormalized) || playerTeamNormalized.includes(awayTeamNormalized)

  if (!isHome && !isAway) return 0 // Team non trovato

  // Determina l'avversario
  const opponentName = isHome ? realGame.away.name : realGame.home.name
  const isOpponentTop = isTopTeam(opponentName)

  // Calcolo bonus/malus
  if (isHome) {
    // Gioca in casa
    if (isOpponentTop) {
      return -1 // Casa contro top team: -1
    } else {
      return +2 // Casa contro squadra normale: +2
    }
  } else {
    // Gioca in trasferta
    if (isOpponentTop) {
      return -2 // Trasferta contro top team: -2
    } else {
      return 0 // Trasferta contro squadra normale: 0 (nessun bonus)
    }
  }
}

// ============================================
// ALGORITMO DI CALCOLO SCORE
// ============================================

/**
 * Calcola lo score di un giocatore basandosi sui dati Chance e StatPlayer
 * AGGIORNATO: usa StatPlayer per trend e lastGame.positiveness invece di Chance
 */
export const calculatePlayerScore = (player: LivePlayer, statPlayersMap?: Map<string, StatPlayer>): number => {
  const chance = player.chance
  if (!chance) return 0

  let score = 0

  // Lookup StatPlayer per trend e positiveness
  const playerName = (player.current as any)?.name ?? ''
  const playerKey = getPlayerKey(playerName)
  const statPlayer = statPlayersMap?.get(playerKey) ?? null
  const playerStats = statPlayer ? calculatePlayerStats(statPlayer) : null

  // 1) Fantagazzetta: +5 se true, -5 se false
  score += chance.fantagazzetta ? 5 : -5

  // 2) Status: Normal +1, Injury -99, Warned -5, Disqualified -99, Maybe -5
  switch (chance.status) {
    case 0: // ChanceType.Normal
      score += 1
      break
    case 1: // ChanceType.Injury
      score -= 99
      break
    case 2: // ChanceType.Warned
      score -= 5
      break
    case 3: // ChanceType.Disqualified
      score -= 99
      break
    case 4: // ChanceType.Maybe
      score -= 5
      break
  }

  // 3) Trend: usa trendAverage (somma diretta ultime 5 partite) invece del TrendType mappato
  const trendValue = playerStats?.trendAverage ?? 0
  score += trendValue

  // 4) LastGame positiveness: usa StatPlayer - nessun fallback necessario
  const lastGamePositiveness = playerStats?.lastGame?.positiveness ?? 0
  score += lastGamePositiveness

  // 5) Casa/Trasferta e difficoltà avversario
  const playerTeamName = (player.current as any)?.realTeam?.name ?? ''
  const homeAwayBonus = calculateHomeAwayBonus(player.realGame, playerTeamName)
  score += homeAwayBonus

  return score
}

/**
 * Ottiene il ruolo principale di un giocatore
 */
const getPlayerRole = (player: LivePlayer): Role => {
  return (player.current as any)?.role ?? Role.Undefined
}

/**
 * Ordina i giocatori per score decrescente
 */
const sortPlayersByScore = (players: PlayerScore[]): PlayerScore[] => {
  return [...players].sort((a, b) => b.score - a.score)
}

// ============================================
// ALGORITMO DI FORMAZIONE OTTIMALE
// ============================================

/**
 * Calcola la formazione ottimale per un team
 * 
 * Algoritmo:
 * 1. Calcola score per tutti i giocatori
 * 2. Seleziona i portieri (primo titolare, secondo in panchina, resto in tribuna)
 * 3. Seleziona la formazione base minima (3-3-1)
 * 4. Completa con i migliori giocatori disponibili rispettando i limiti
 * 5. Assegna panchinari (primi 2 per ruolo)
 * 6. Manda tutti i restanti in tribuna
 */
export const calculateOptimalFormation = (team: LiveTeam, statPlayersMap?: Map<string, StatPlayer>): LivePlayer[] => {
  // 1. Calcola score per tutti i giocatori
  const scoredPlayers: PlayerScore[] = team.players.map(player => ({
    player,
    score: calculatePlayerScore(player, statPlayersMap),
    role: getPlayerRole(player),
  }))

  // Separa per ruolo
  const goalkeepers = sortPlayersByScore(scoredPlayers.filter(p => p.role === Role.GoalKeeper))
  const defenders = sortPlayersByScore(scoredPlayers.filter(p => p.role === Role.Defensor))
  const midfielders = sortPlayersByScore(scoredPlayers.filter(p => p.role === Role.Midfielder))
  const forwards = sortPlayersByScore(scoredPlayers.filter(p => p.role === Role.Forward))

  // Risultato finale
  const updatedPlayers: LivePlayer[] = []

  // ========================================
  // 2. PORTIERI: primo titolare, secondo panchina, resto tribuna
  // ========================================
  goalkeepers.forEach((gk, index) => {
    let position: FantaSoccerRole
    if (index === 0) {
      position = FantaSoccerRole.GoalKeeper
    } else if (index === 1) {
      position = FantaSoccerRole.BackupGoalKeeper
    } else {
      position = FantaSoccerRole.Tribune
    }
    updatedPlayers.push({ ...gk.player, currentPosition: position })
  })

  // ========================================
  // 3. FORMAZIONE BASE (3-3-1)
  // ========================================
  const starterDefenders: PlayerScore[] = []
  const starterMidfielders: PlayerScore[] = []
  const starterForwards: PlayerScore[] = []

  // Prendi i migliori 3 difensori
  for (let i = 0; i < BASE_FORMATION.defenders && i < defenders.length; i++) {
    starterDefenders.push(defenders[i])
  }

  // Prendi i migliori 3 centrocampisti
  for (let i = 0; i < BASE_FORMATION.midfielders && i < midfielders.length; i++) {
    starterMidfielders.push(midfielders[i])
  }

  // Prendi il miglior attaccante
  for (let i = 0; i < BASE_FORMATION.forwards && i < forwards.length; i++) {
    starterForwards.push(forwards[i])
  }

  // ========================================
  // 4. COMPLETA LA FORMAZIONE CON I MIGLIORI
  // ========================================
  // Crea pool di giocatori rimanenti ordinati per score
  const remainingDefenders = defenders.slice(BASE_FORMATION.defenders)
  const remainingMidfielders = midfielders.slice(BASE_FORMATION.midfielders)
  const remainingForwards = forwards.slice(BASE_FORMATION.forwards)

  // Pool combinato di tutti i restanti
  const remainingPool = sortPlayersByScore([
    ...remainingDefenders,
    ...remainingMidfielders,
    ...remainingForwards,
  ])

  // Aggiungi i migliori rispettando i limiti di ruolo
  for (const scored of remainingPool) {
    if (scored.role === Role.Defensor && starterDefenders.length < MAX_STARTERS.defenders) {
      starterDefenders.push(scored)
    } else if (scored.role === Role.Midfielder && starterMidfielders.length < MAX_STARTERS.midfielders) {
      starterMidfielders.push(scored)
    } else if (scored.role === Role.Forward && starterForwards.length < MAX_STARTERS.forwards) {
      starterForwards.push(scored)
    }

    // Verifica se abbiamo raggiunto 11 giocatori (escludendo portiere)
    const totalStarters = starterDefenders.length + starterMidfielders.length + starterForwards.length
    if (totalStarters >= 10) break
  }

  // ========================================
  // 5. ASSEGNA POSIZIONI AI TITOLARI
  // ========================================
  starterDefenders.forEach(def => {
    updatedPlayers.push({ ...def.player, currentPosition: FantaSoccerRole.Defensor })
  })

  starterMidfielders.forEach(mid => {
    updatedPlayers.push({ ...mid.player, currentPosition: FantaSoccerRole.Midfielder })
  })

  starterForwards.forEach(fwd => {
    updatedPlayers.push({ ...fwd.player, currentPosition: FantaSoccerRole.Forward })
  })

  // ========================================
  // 6. PANCHINARI (primi 2 per ruolo dopo i titolari)
  // ========================================
  const benchDefenders = defenders.filter(d => !starterDefenders.includes(d)).slice(0, BENCH_COUNT.defenders)
  const benchMidfielders = midfielders.filter(m => !starterMidfielders.includes(m)).slice(0, BENCH_COUNT.midfielders)
  const benchForwards = forwards.filter(f => !starterForwards.includes(f)).slice(0, BENCH_COUNT.forwards)

  // Assegna posizioni panchina
  benchDefenders.forEach((def, index) => {
    const position = index === 0 ? FantaSoccerRole.FirstBackupDefensor : FantaSoccerRole.SecondBackupDefensor
    updatedPlayers.push({ ...def.player, currentPosition: position })
  })

  benchMidfielders.forEach((mid, index) => {
    const position = index === 0 ? FantaSoccerRole.FirstBackupMidfielder : FantaSoccerRole.SecondBackupMidfielder
    updatedPlayers.push({ ...mid.player, currentPosition: position })
  })

  benchForwards.forEach((fwd, index) => {
    const position = index === 0 ? FantaSoccerRole.FirstBackupForward : FantaSoccerRole.SecondBackupForward
    updatedPlayers.push({ ...fwd.player, currentPosition: position })
  })

  // ========================================
  // 7. TRIBUNA (tutti i restanti)
  // ========================================
  const allAssigned = new Set(updatedPlayers.map(p => (p.current as any)?.name))
  
  team.players.forEach(player => {
    const playerName = (player.current as any)?.name
    if (!allAssigned.has(playerName)) {
      updatedPlayers.push({ ...player, currentPosition: FantaSoccerRole.Tribune })
    }
  })

  return updatedPlayers
}

/**
 * Applica la formazione ottimale a un team
 * Ritorna una copia aggiornata del team
 */
export const applyOptimalFormation = (team: LiveTeam, statPlayersMap?: Map<string, StatPlayer>): LiveTeam => {
  const updatedPlayers = calculateOptimalFormation(team, statPlayersMap)
  return {
    ...team,
    players: updatedPlayers,
  }
}

// ============================================
// EXPORT DEFAULT
// ============================================

const formationService = {
  calculatePlayerScore,
  calculateOptimalFormation,
  applyOptimalFormation,
}

export default formationService
