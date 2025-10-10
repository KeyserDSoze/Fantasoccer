import { AuctionStatus } from '../types/auction'
import { Role } from '../types/group'

/**
 * Auction Helper Utilities
 * 
 * Funzioni helper per la gestione delle aste:
 * - Etichette per ruoli, stati, stati giocatori
 * - Colori per stati
 */

/**
 * Ottiene l'etichetta testuale per un ruolo
 */
export const getRoleLabel = (role: Role): string => {
  const labels: Record<Role, string> = {
    [Role.Undefined]: 'Indefinito',
    [Role.GoalKeeper]: 'Portiere',
    [Role.Defensor]: 'Difensore',
    [Role.Midfielder]: 'Centrocampista',
    [Role.Forward]: 'Attaccante'
  }
  return labels[role] || 'Sconosciuto'
}

/**
 * Ottiene l'etichetta testuale per uno stato asta
 */
export const getAuctionStatusLabel = (status: AuctionStatus): string => {
  const labels: Record<AuctionStatus, string> = {
    [AuctionStatus.NotStarted]: 'Non Iniziata',
    [AuctionStatus.Paused]: 'In Pausa',
    [AuctionStatus.InProgress]: 'In Corso',
    [AuctionStatus.Finished]: 'Terminata'
  }
  return labels[status] || 'Sconosciuto'
}

/**
 * Ottiene il colore associato ad uno stato asta
 */
export const getAuctionStatusColor = (status: AuctionStatus): string => {
  const colors: Record<AuctionStatus, string> = {
    [AuctionStatus.NotStarted]: '$gray10',
    [AuctionStatus.Paused]: '$orange10',
    [AuctionStatus.InProgress]: '$green10',
    [AuctionStatus.Finished]: '$blue10'
  }
  return colors[status] || '$gray10'
}

/**
 * Ottiene l'etichetta testuale per uno stato giocatore nel team
 * 
 * PlayerInTeamStatus enum:
 * - 0: Active (Attivo)
 * - 1: SoldForOneHalf (Venduto per 1 girone)
 * - 2: SoldWithNoReturnedPrice (Venduto senza rimborso)
 * - 3: Sold (Venduto)
 * - 4: Removed (Rimosso)
 */
export const getPlayerStatusLabel = (status: number): string => {
  const labels: Record<number, string> = {
    0: 'Attivo',
    1: 'Venduto 1 girone',
    2: 'Venduto senza rimborso',
    3: 'Venduto',
    4: 'Rimosso'
  }
  return labels[status] || 'Sconosciuto'
}

/**
 * Ottiene il colore associato ad uno stato giocatore nel team
 */
export const getPlayerStatusColor = (status: number): string => {
  const colors: Record<number, string> = {
    0: '$green11',  // Active
    1: '$orange11', // SoldForOneHalf
    2: '$red11',    // SoldWithNoReturnedPrice
    3: '$blue11',   // Sold
    4: '$gray11'    // Removed
  }
  return colors[status] || '$gray11'
}
