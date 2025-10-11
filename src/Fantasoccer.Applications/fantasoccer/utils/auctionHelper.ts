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
    1: 'Infortunato/Estero',
    2: 'Scartato',
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
 * Tipi di suoni per le notifiche dell'asta
 */
export enum AuctionSoundType {
  MyBid = 'my-bid',           // Quando faccio io un'offerta
  OtherBid = 'other-bid',     // Quando qualcun altro fa un'offerta
  MyWin = 'my-win',           // Quando vinco io l'asta
  OtherWin = 'other-win'      // Quando vince qualcun altro
}

/**
 * Riproduce un suono di notifica per le aste (solo web)
 * Utilizza Web Audio API per generare suoni diversi in base al tipo di evento
 * 
 * @param audioContext - Contesto audio Web Audio API
 * @param enabled - Se abilitato
 * @param type - Tipo di suono da riprodurre
 */
export const playAuctionSound = (
  audioContext: AudioContext | null, 
  enabled: boolean,
  type: AuctionSoundType
): void => {
  if (!enabled || !audioContext) return
  
  const now = audioContext.currentTime
  
  switch (type) {
    case AuctionSoundType.MyBid:
      // Mia offerta: suono ascendente breve (600 Hz → 900 Hz)
      {
        const oscillator = audioContext.createOscillator()
        const gainNode = audioContext.createGain()
        oscillator.connect(gainNode)
        gainNode.connect(audioContext.destination)
        oscillator.frequency.setValueAtTime(600, now)
        oscillator.frequency.exponentialRampToValueAtTime(900, now + 0.15)
        gainNode.gain.setValueAtTime(0.25, now)
        gainNode.gain.exponentialRampToValueAtTime(0.01, now + 0.15)
        oscillator.start(now)
        oscillator.stop(now + 0.15)
      }
      break
    
    case AuctionSoundType.OtherBid:
      // Offerta altri: suono discendente breve (900 Hz → 600 Hz)
      {
        const oscillator = audioContext.createOscillator()
        const gainNode = audioContext.createGain()
        oscillator.connect(gainNode)
        gainNode.connect(audioContext.destination)
        oscillator.frequency.setValueAtTime(900, now)
        oscillator.frequency.exponentialRampToValueAtTime(600, now + 0.15)
        gainNode.gain.setValueAtTime(0.25, now)
        gainNode.gain.exponentialRampToValueAtTime(0.01, now + 0.15)
        oscillator.start(now)
        oscillator.stop(now + 0.15)
      }
      break
    
    case AuctionSoundType.MyWin:
      // Mia vittoria: suono celebrativo a 3 toni ascendenti
      {
        const frequencies = [600, 800, 1000]
        frequencies.forEach((freq, index) => {
          const oscillator = audioContext.createOscillator()
          const gainNode = audioContext.createGain()
          oscillator.connect(gainNode)
          gainNode.connect(audioContext.destination)
          oscillator.frequency.value = freq
          const startTime = now + (index * 0.08)
          gainNode.gain.setValueAtTime(0.3, startTime)
          gainNode.gain.exponentialRampToValueAtTime(0.01, startTime + 0.12)
          oscillator.start(startTime)
          oscillator.stop(startTime + 0.12)
        })
      }
      break
    
    case AuctionSoundType.OtherWin:
      // Vittoria altri: suono neutro a 2 toni
      {
        // Primo tono (700 Hz)
        const oscillator1 = audioContext.createOscillator()
        const gainNode1 = audioContext.createGain()
        oscillator1.connect(gainNode1)
        gainNode1.connect(audioContext.destination)
        oscillator1.frequency.value = 700
        gainNode1.gain.setValueAtTime(0.2, now)
        gainNode1.gain.exponentialRampToValueAtTime(0.01, now + 0.1)
        oscillator1.start(now)
        oscillator1.stop(now + 0.1)
        
        // Secondo tono (500 Hz)
        const oscillator2 = audioContext.createOscillator()
        const gainNode2 = audioContext.createGain()
        oscillator2.connect(gainNode2)
        gainNode2.connect(audioContext.destination)
        oscillator2.frequency.value = 500
        gainNode2.gain.setValueAtTime(0.2, now + 0.1)
        gainNode2.gain.exponentialRampToValueAtTime(0.01, now + 0.2)
        oscillator2.start(now + 0.1)
        oscillator2.stop(now + 0.2)
      }
      break
  }
}