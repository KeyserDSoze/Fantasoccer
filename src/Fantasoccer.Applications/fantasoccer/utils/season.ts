/**
 * Season utilities forFantaSoccer
 * 
 * Season year logic:
 * - Season 1 = 2012/13
 * - The app switches season on August 10, matching the backend
 * - Base year is 2012 (first season)
 */

const BASE_YEAR = 2012 // First season year
const SEASON_START_MONTH = 8 // August (1-indexed)
const SEASON_START_DAY = 10

/**
 * Get the current season year index based on current date
 * Examples:
 * - May 2026 -> Season 14 (2025/26)
 * - August 10, 2025 -> Season 14 (2025/26)
 * - July 2026 -> Season 14 (2025/26)
 * - August 2026 -> Season 15 (2026/27)
 */
export function getCurrentSeasonYear(now = new Date()): number {
  const currentYear = now.getUTCFullYear()
  const currentMonth = now.getUTCMonth() + 1 // getUTCMonth() is 0-indexed
  const currentDay = now.getUTCDate()

  const isNewSeason = currentMonth > SEASON_START_MONTH ||
    (currentMonth === SEASON_START_MONTH && currentDay >= SEASON_START_DAY)
  const seasonStartYear = isNewSeason ? currentYear : currentYear - 1
  
  // Calculate season index (1-based)
  return seasonStartYear - BASE_YEAR + 1
}

/**
 * Format a season year number to a season string
 * @param yearNumber - The season year index (1-based, e.g., 1, 2, 3...)
 * @returns Formatted season string (e.g., "2012/13", "2025/26")
 * 
 * Examples:
 * - formatSeasonFromYear(1) -> "2012/13"
 * - formatSeasonFromYear(14) -> "2025/26"
 */
export function formatSeasonFromYear(yearNumber: number): string {
  const startYear = BASE_YEAR + yearNumber - 1
  const endYear = startYear + 1
  return `${startYear}/${String(endYear).slice(-2)}`
}

/**
 * Get the base year constant
 */
export function getBaseYear(): number {
  return BASE_YEAR
}

/**
 * Get available season years from season 1 to current season + 1
 * This is useful for year selectors in admin screens
 * @returns Array of season year indices
 */
export function getAvailableSeasonYears(): number[] {
  const currentSeasonYear = getCurrentSeasonYear()
  const maxYearIndex = currentSeasonYear + 1 // Include next season for planning
  
  const years: number[] = []
  for (let yearIndex = 1; yearIndex <= maxYearIndex; yearIndex++) {
    years.push(yearIndex)
  }
  
  return years
}

/**
 * Parse a season string to get the year number
 * @param seasonString - Season string like "2025/26"
 * @returns Season year index (1-based) or null if invalid
 */
export function parseSeasonString(seasonString: string): number | null {
  const match = seasonString.match(/^(\d{4})\/\d{2}$/)
  if (!match) return null
  
  const startYear = parseInt(match[1], 10)
  return startYear - BASE_YEAR + 1
}

/**
 * Get the solar year range for a season
 * @param yearNumber - The season year index (1-based)
 * @returns Object with startYear and endYear
 */
export function getSeasonYearRange(yearNumber: number): { startYear: number; endYear: number } {
  const startYear = BASE_YEAR + yearNumber - 1
  const endYear = startYear + 1
  
  return { startYear, endYear }
}
