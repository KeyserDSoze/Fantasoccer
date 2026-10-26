import { apiRequest } from './httpClient'

export const recalculationService = {
  async recalculateDay(year: number, serieADay: number): Promise<boolean> {
    await apiRequest('/api/football/refresh', 'POST', { operation: 'official', year, day: serieADay })
    return true
  },
  async recalculateStats(year: number): Promise<boolean> {
    await apiRequest('/api/football/refresh', 'POST', { operation: 'statistics', year })
    return true
  },
}
export default recalculationService
