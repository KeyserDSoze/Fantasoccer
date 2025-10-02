import type { LivePlayer } from 'types/game'
import { FantaSoccerRole } from 'types/team'
import { Role } from 'types/group'

/**
 * Conta quanti giocatori ci sono in una specifica posizione (titolari)
 */
export function countPlayersInRole(players: LivePlayer[], role: Role): number {
  return players.filter(p => {
    const playerRole = (p.current as any)?.role ?? Role.Undefined
    return playerRole === role && 
           p.currentPosition >= FantaSoccerRole.GoalKeeper && 
           p.currentPosition <= FantaSoccerRole.Forward
  }).length
}

/**
 * Verifica se un giocatore può essere rimosso dalla formazione titolare
 * Regole:
 * - Portiere: MAI (deve esserci sempre 1 portiere)
 * - Difensore: solo se ce ne sono > 3
 * - Centrocampista: solo se ce ne sono > 3
 * - Attaccante: solo se ce ne sono > 1
 */
export function canRemovePlayer(player: LivePlayer, allPlayers: LivePlayer[]): boolean {
  const playerRole = (player.current as any)?.role ?? Role.Undefined
  
  // Può rimuovere solo se è titolare
  if (player.currentPosition < FantaSoccerRole.GoalKeeper || player.currentPosition > FantaSoccerRole.Forward) {
    return false
  }

  const currentCount = countPlayersInRole(allPlayers, playerRole)

  switch (playerRole) {
    case Role.GoalKeeper:
      return false // Mai rimuovere il portiere
    case Role.Defensor:
      return currentCount > 3
    case Role.Midfielder:
      return currentCount > 3
    case Role.Forward:
      return currentCount > 1
    default:
      return false
  }
}

/**
 * Verifica se un giocatore in tribuna può essere aggiunto alla formazione titolare
 * Regole:
 * - Portiere: solo se c'è 1 portiere (già garantito)
 * - Difensore: solo se ce ne sono < 5
 * - Centrocampista: solo se ce ne sono < 5
 * - Attaccante: solo se ce ne sono < 3
 */
export function canAddPlayerFromTribune(player: LivePlayer, allPlayers: LivePlayer[]): boolean {
  const playerRole = (player.current as any)?.role ?? Role.Undefined
  
  // Può aggiungere solo se è in tribuna
  if (player.currentPosition !== FantaSoccerRole.Tribune) {
    return false
  }

  const currentCount = countPlayersInRole(allPlayers, playerRole)

  switch (playerRole) {
    case Role.GoalKeeper:
      return currentCount < 1
    case Role.Defensor:
      return currentCount < 5
    case Role.Midfielder:
      return currentCount < 5
    case Role.Forward:
      return currentCount < 3
    default:
      return false
  }
}

/**
 * Verifica se uno scambio tra due giocatori è valido
 * Regole:
 * - Stesso ruolo (già garantito dalla UI)
 * - Se uno dei due è in tribuna, rispettare i limiti min/max
 */
export function canSwapPlayers(player1: LivePlayer, player2: LivePlayer, allPlayers: LivePlayer[]): boolean {
  const role1 = (player1.current as any)?.role ?? Role.Undefined
  const role2 = (player2.current as any)?.role ?? Role.Undefined

  // Devono essere dello stesso ruolo
  if (role1 !== role2) return false

  const pos1 = player1.currentPosition
  const pos2 = player2.currentPosition

  // Caso 1: Scambio tra titolare e tribuna
  if (
    (pos1 >= FantaSoccerRole.GoalKeeper && pos1 <= FantaSoccerRole.Forward && pos2 === FantaSoccerRole.Tribune) ||
    (pos2 >= FantaSoccerRole.GoalKeeper && pos2 <= FantaSoccerRole.Forward && pos1 === FantaSoccerRole.Tribune)
  ) {
    // Il giocatore in tribuna deve poter essere aggiunto
    const tribunePlayer = pos1 === FantaSoccerRole.Tribune ? player1 : player2
    return canAddPlayerFromTribune(tribunePlayer, allPlayers)
  }

  // Caso 2: Scambio tra titolare e panchina (sempre OK se stesso ruolo)
  // Caso 3: Scambio tra due titolari (sempre OK se stesso ruolo)
  // Caso 4: Scambio tra due panchina (sempre OK se stesso ruolo)
  return true
}

/**
 * Verifica che la formazione rispetti tutti i vincoli
 */
export function validateFormation(players: LivePlayer[]): { valid: boolean; errors: string[] } {
  const errors: string[] = []

  const gkCount = countPlayersInRole(players, Role.GoalKeeper)
  const defCount = countPlayersInRole(players, Role.Defensor)
  const midCount = countPlayersInRole(players, Role.Midfielder)
  const fwdCount = countPlayersInRole(players, Role.Forward)

  if (gkCount !== 1) {
    errors.push(`Portieri: deve essere esattamente 1 (attuale: ${gkCount})`)
  }

  if (defCount < 3 || defCount > 5) {
    errors.push(`Difensori: deve essere tra 3 e 5 (attuale: ${defCount})`)
  }

  if (midCount < 3 || midCount > 5) {
    errors.push(`Centrocampisti: deve essere tra 3 e 5 (attuale: ${midCount})`)
  }

  if (fwdCount < 1 || fwdCount > 3) {
    errors.push(`Attaccanti: deve essere tra 1 e 3 (attuale: ${fwdCount})`)
  }

  // Verifica che tutti abbiano una panchina obbligatoria
  const hasBenchGK = players.some(p => p.currentPosition === FantaSoccerRole.BackupGoalKeeper)
  const hasBenchDef = players.some(p => 
    p.currentPosition >= FantaSoccerRole.FirstBackupDefensor && 
    p.currentPosition <= FantaSoccerRole.SecondBackupDefensor
  )
  const hasBenchMid = players.some(p => 
    p.currentPosition >= FantaSoccerRole.FirstBackupMidfielder && 
    p.currentPosition <= FantaSoccerRole.SecondBackupMidfielder
  )
  const hasBenchFwd = players.some(p => 
    p.currentPosition >= FantaSoccerRole.FirstBackupForward && 
    p.currentPosition <= FantaSoccerRole.SecondBackupForward
  )

  if (!hasBenchGK) errors.push('Manca portiere di riserva')
  if (!hasBenchDef) errors.push('Manca almeno un difensore di riserva')
  if (!hasBenchMid) errors.push('Manca almeno un centrocampista di riserva')
  if (!hasBenchFwd) errors.push('Manca almeno un attaccante di riserva')

  return {
    valid: errors.length === 0,
    errors
  }
}
