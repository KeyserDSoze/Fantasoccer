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

/**
 * Riproduce un suono di notifica per le offerte (solo web)
 * Utilizza Web Audio API per generare un suono a due toni
 */
export const playBidSound = (audioContext: AudioContext | null, enabled: boolean): void => {
  if (!enabled || !audioContext) return
  
  const now = audioContext.currentTime
  
  // Primo tono (800 Hz)
  const oscillator1 = audioContext.createOscillator()
  const gainNode1 = audioContext.createGain()
  oscillator1.connect(gainNode1)
  gainNode1.connect(audioContext.destination)
  oscillator1.frequency.value = 800
  gainNode1.gain.setValueAtTime(0.3, now)
  gainNode1.gain.exponentialRampToValueAtTime(0.01, now + 0.1)
  oscillator1.start(now)
  oscillator1.stop(now + 0.1)
  
  // Secondo tono (1000 Hz)
  const oscillator2 = audioContext.createOscillator()
  const gainNode2 = audioContext.createGain()
  oscillator2.connect(gainNode2)
  gainNode2.connect(audioContext.destination)
  oscillator2.frequency.value = 1000
  gainNode2.gain.setValueAtTime(0.3, now + 0.1)
  gainNode2.gain.exponentialRampToValueAtTime(0.01, now + 0.2)
  oscillator2.start(now + 0.1)
  oscillator2.stop(now + 0.2)
}
