/**
 * Hook: useFormation
 * 
 * Custom hook per gestire la formazione ottimale di un team.
 * Fornisce funzioni per calcolare la formazione migliore e fare restore.
 * 
 * @example
 * ```tsx
 * const { applyOptimal, restore, hasBackup } = useFormation(team)
 * 
 * // Calcola formazione ottimale
 * const optimizedTeam = applyOptimal()
 * 
 * // Ripristina formazione precedente
 * const previousTeam = restore()
 * ```
 */

import { useState, useCallback } from 'react'
import type { LiveTeam } from 'types/game'
import formationService from '../services/formationService'

export interface UseFormationResult {
  /**
   * Applica la formazione ottimale al team
   * Salva automaticamente un backup per il restore
   */
  applyOptimal: () => LiveTeam | null
  
  /**
   * Ripristina la formazione precedente dal backup
   */
  restore: () => LiveTeam | null
  
  /**
   * Indica se c'è un backup disponibile
   */
  hasBackup: boolean
  
  /**
   * Pulisce il backup salvato
   */
  clearBackup: () => void
}

export const useFormation = (team?: LiveTeam | null): UseFormationResult => {
  const [backup, setBackup] = useState<LiveTeam | null>(null)

  const applyOptimal = useCallback((): LiveTeam | null => {
    if (!team) return null

    // Salva il backup prima di modificare
    setBackup(team)

    // Calcola e ritorna la formazione ottimale
    return formationService.applyOptimalFormation(team)
  }, [team])

  const restore = useCallback((): LiveTeam | null => {
    if (!backup) return null

    const restoredTeam = backup
    setBackup(null)
    return restoredTeam
  }, [backup])

  const clearBackup = useCallback(() => {
    setBackup(null)
  }, [])

  return {
    applyOptimal,
    restore,
    hasBackup: backup !== null,
    clearBackup,
  }
}

export default useFormation
