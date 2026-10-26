import type { Card } from '../types/card'
import { ApiError } from '../configurations/apiConfig'
import { apiRequest, apiUpload } from './httpClient'

const payload = (card: Omit<Card, 'id'>) => ({ name: card.name, description: card.description, type: card.type, points: card.points,
  negativePoints: card.negativePoints, enemyPoints: card.enemyPoints, myCondition: card.myCondition ?? null, enemyCondition: card.enemyCondition ?? null })
export const cardService = {
  getAllCards(): Promise<Card[]> { return apiRequest('/api/cards') },
  async getCardById(id: string): Promise<Card | null> {
    try { return await apiRequest(`/api/cards/${encodeURIComponent(id)}`) }
    catch (error) { if (error instanceof ApiError && error.status === 404) return null; throw error }
  },
  createCard(card: Omit<Card, 'id'>): Promise<Card> { return apiRequest('/api/cards', 'POST', payload(card)) },
  updateCard(id: string, card: Card): Promise<Card> {
    if (card.version == null) throw new Error('Ricaricare la carta prima di modificarla.')
    return apiRequest(`/api/cards/${encodeURIComponent(id)}`, 'PUT', { ...payload(card), expectedVersion: card.version })
  },
  async deleteCard(id: string, expectedVersion?: number): Promise<void> {
    const version = expectedVersion ?? (await this.getCardById(id))?.version
    if (version == null) throw new Error('Carta non trovata o non versionata.')
    await apiRequest(`/api/cards/${encodeURIComponent(id)}?expectedVersion=${version}`, 'DELETE')
  },
  async uploadCardImage(id: string, file: File, expectedVersion?: number): Promise<Card> {
    const version = expectedVersion ?? (await this.getCardById(id))?.version
    if (version == null) throw new Error('Carta non trovata o non versionata.')
    const body = new FormData(); body.append('file', file)
    return apiUpload(`/api/cards/${encodeURIComponent(id)}/image?expectedVersion=${version}`, body)
  },
}
export default cardService
