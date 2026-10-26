import { apiRequest } from './httpClient'
import { enhanceChance, type Chance, type ChancedRealPlayerWrapper } from '../types/chance'

// Readable wire contracts. Availability flags are presentation enrichment, not a
// second abbreviated representation of the server document.
export async function getFootball<T>(year: number, resource: string): Promise<T | null> {
  return (await apiRequest<T | undefined>(`/api/football/${year}/${resource}`)) ?? null
}
export async function getFootballChances(year: number, day: number): Promise<ChancedRealPlayerWrapper | null> {
  type Wire = { players: (Omit<ChancedRealPlayerWrapper['players'][number], 'chance'> & { chance: Chance })[] }
  const result = await getFootball<Wire>(year, `days/${day}/chances`)
  return result ? { players: result.players.map(player => ({ ...player, chance: enhanceChance(player.chance) })) } : null
}
