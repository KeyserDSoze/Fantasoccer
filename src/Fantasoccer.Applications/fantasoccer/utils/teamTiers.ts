/**
 * Team Tiers Helper
 * 
 * Sistema di classificazione delle squadre di Serie A in base alla forza/prestigio.
 * Tier 1 = Top clubs (Napoli, Juventus, Inter, Milan)
 * Tier 2 = Strong clubs (Atalanta, Lazio, Roma, Bologna, Fiorentina)
 * Tier 3 = Mid-table clubs (Udinese, Como, Sassuolo, Torino)
 * Tier 4 = Other clubs (tutte le altre squadre)
 */

export enum TeamTier {
  T1 = 1,
  T2 = 2,
  T3 = 3,
  T4 = 4
}

// Squadre Tier 1 - Top clubs
const TIER_1_TEAMS = ['napoli', 'juventus', 'inter', 'milan']

// Squadre Tier 2 - Strong clubs
const TIER_2_TEAMS = ['atalanta', 'lazio', 'roma', 'bologna', 'fiorentina']

// Squadre Tier 3 - Mid-table clubs
const TIER_3_TEAMS = ['udinese', 'como', 'sassuolo', 'torino']

/**
 * Normalizza il nome della squadra per il confronto
 * Rimuove spazi, caratteri speciali e converte in lowercase
 */
const normalizeTeamName = (teamName: string): string => {
  return teamName
    .toLowerCase()
    .trim()
    .replace(/\s+/g, '')
    .replace(/[^a-z0-9]/g, '')
}

/**
 * Ottiene il tier di una squadra in base al nome
 * 
 * @param teamName - Nome della squadra (es: "Napoli", "Inter", "Bologna")
 * @returns TeamTier enum (1-4)
 */
export const getTeamTier = (teamName: string | null | undefined): TeamTier => {
  if (!teamName) return TeamTier.T4
  
  const normalized = normalizeTeamName(teamName)
  
  if (TIER_1_TEAMS.includes(normalized)) return TeamTier.T1
  if (TIER_2_TEAMS.includes(normalized)) return TeamTier.T2
  if (TIER_3_TEAMS.includes(normalized)) return TeamTier.T3
  
  return TeamTier.T4
}

/**
 * Ottiene l'etichetta testuale del tier
 * 
 * @param tier - TeamTier enum
 * @returns Label string (es: "T1", "T2", etc.)
 */
export const getTeamTierLabel = (tier: TeamTier): string => {
  return `T${tier}`
}

/**
 * Ottiene il colore associato al tier (per Tamagui)
 * 
 * T1 = Gold/Yellow (top clubs)
 * T2 = Blue (strong clubs)
 * T3 = Green (mid-table clubs)
 * T4 = Gray (other clubs)
 * 
 * @param tier - TeamTier enum
 * @returns Tamagui color token
 */
export const getTeamTierColor = (tier: TeamTier): string => {
  const colors: Record<TeamTier, string> = {
    [TeamTier.T1]: '$yellow10',  // Gold/Yellow for top clubs
    [TeamTier.T2]: '$blue10',    // Blue for strong clubs
    [TeamTier.T3]: '$green10',   // Green for mid-table
    [TeamTier.T4]: '$gray10'     // Gray for others
  }
  return colors[tier]
}

/**
 * Ottiene il colore di sfondo per il badge del tier
 * 
 * @param tier - TeamTier enum
 * @returns Tamagui background color token
 */
export const getTeamTierBackgroundColor = (tier: TeamTier): string => {
  const colors: Record<TeamTier, string> = {
    [TeamTier.T1]: '$yellow3',   // Light gold background
    [TeamTier.T2]: '$blue3',     // Light blue background
    [TeamTier.T3]: '$green3',    // Light green background
    [TeamTier.T4]: '$gray3'      // Light gray background
  }
  return colors[tier]
}

/**
 * Helper per ottenere tier direttamente dal nome della squadra con tutti i dati
 * 
 * @param teamName - Nome della squadra
 * @returns Oggetto con tier, label, color e backgroundColor
 */
export const getTeamTierInfo = (teamName: string | null | undefined) => {
  const tier = getTeamTier(teamName)
  return {
    tier,
    label: getTeamTierLabel(tier),
    color: getTeamTierColor(tier),
    backgroundColor: getTeamTierBackgroundColor(tier)
  }
}
