import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'

export const recalculationEndpoints = {
  recalculateDay: (params: { year: number; serieADay: number }) =>
    buildApiUrl('/Recalculation/RecalculateDay', params as any),
  recalculateStats: (params: { year: number }) =>
    buildApiUrl('/Recalculation/RecalculateStats', params as any)
}

export const recalculationService = {
  /**
   * Ricalcola tutti i voti della giornata di Serie A specificata
   * @param year - Anno della stagione
   * @param serieADay - Numero della giornata di Serie A
   * @returns true se il ricalcolo è andato a buon fine
   */
  async recalculateDay(year: number, serieADay: number): Promise<boolean> {
    const url = recalculationEndpoints.recalculateDay({ year, serieADay })
    const accessToken = await tokenStorageService.getTokenString()
    
    if (!accessToken) {
      console.warn('No valid access token available')
      throw new Error('Token di autenticazione non disponibile')
    }

    const res = await fetch(url, { 
      headers: getCommonHeaders(true, accessToken) 
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Errore nel ricalcolo della giornata: ${res.status} ${text}`)
    }

    return await res.json()
  },

  /**
   * Ricalcola tutte le statistiche dell'anno specificato
   * Controlla i voti di tutto l'anno e rigenera le statistiche
   * @param year - Anno della stagione
   * @returns true se il ricalcolo è andato a buon fine
   */
  async recalculateStats(year: number): Promise<boolean> {
    const url = recalculationEndpoints.recalculateStats({ year })
    const accessToken = await tokenStorageService.getTokenString()
    
    if (!accessToken) {
      console.warn('No valid access token available')
      throw new Error('Token di autenticazione non disponibile')
    }

    const res = await fetch(url, { 
      headers: getCommonHeaders(true, accessToken) 
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Errore nel ricalcolo delle statistiche: ${res.status} ${text}`)
    }

    return await res.json()
  }
}

export default recalculationService
