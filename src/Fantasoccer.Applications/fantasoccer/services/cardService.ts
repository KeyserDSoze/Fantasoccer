import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig'
import { 
  Card, 
  CardRaw, 
  mapRawCardToCard, 
  mapCardToRawCard 
} from 'types/card'
import { tokenStorageService } from './tokenStorageService'

export const cardEndpoints = {
  getAllCards: () => buildApiUrl('/Card/GetAllCards'),
  getCardById: (cardId: string) => buildApiUrl(`/Card/GetCardById/${cardId}`),
  createCard: () => buildApiUrl('/Card/CreateCard'),
  updateCard: (cardId: string) => buildApiUrl(`/Card/UpdateCard/${cardId}`),
  deleteCard: (cardId: string) => buildApiUrl(`/Card/DeleteCard/${cardId}`),
  uploadCardImage: (cardId: string) => buildApiUrl(`/Card/UploadCardImage/image/${cardId}`)
}

export const cardService = {
  /**
   * Get all cards - available to all authenticated users
   */
  async getAllCards(): Promise<Card[]> {
    const url = cardEndpoints.getAllCards()
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      console.warn('No valid access token available')
      return []
    }

    const res = await fetch(url, { 
      headers: getCommonHeaders(true, accessToken) 
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to fetch cards: ${res.status} ${text}`)
    }

    const rawCards = (await res.json()) as CardRaw[]
    return rawCards.map(mapRawCardToCard)
  },

  /**
   * Get a specific card by ID
   */
  async getCardById(cardId: string): Promise<Card | null> {
    const url = cardEndpoints.getCardById(cardId)
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      console.warn('No valid access token available')
      return null
    }

    const res = await fetch(url, { 
      headers: getCommonHeaders(true, accessToken) 
    })

    if (!res.ok) {
      if (res.status === 404) return null
      const text = await res.text()
      throw new Error(`Failed to fetch card: ${res.status} ${text}`)
    }

    const rawCard = (await res.json()) as CardRaw
    return mapRawCardToCard(rawCard)
  },

  /**
   * Create a new card - SuperAdmin only
   */
  async createCard(card: Omit<Card, 'id'>): Promise<Card> {
    const url = cardEndpoints.createCard()
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      throw new Error('No valid access token available')
    }

    // Create a temporary card with empty id for mapping
    const tempCard: Card = { ...card, id: '' }
    const rawCard = mapCardToRawCard(tempCard)
    
    const res = await fetch(url, {
      method: 'POST',
      headers: { 
        ...getCommonHeaders(true, accessToken), 
        'Content-Type': 'application/json' 
      },
      body: JSON.stringify(rawCard)
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to create card: ${res.status} ${text}`)
    }

    const createdRawCard = (await res.json()) as CardRaw
    return mapRawCardToCard(createdRawCard)
  },

  /**
   * Update a card - SuperAdmin only
   */
  async updateCard(cardId: string, card: Card): Promise<Card> {
    const url = cardEndpoints.updateCard(cardId)
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      throw new Error('No valid access token available')
    }

    const rawCard = mapCardToRawCard(card)

    const res = await fetch(url, {
      method: 'PUT',
      headers: { 
        ...getCommonHeaders(true, accessToken), 
        'Content-Type': 'application/json' 
      },
      body: JSON.stringify(rawCard)
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to update card: ${res.status} ${text}`)
    }

    const updatedRawCard = (await res.json()) as CardRaw
    return mapRawCardToCard(updatedRawCard)
  },

  /**
   * Delete a card - SuperAdmin only
   */
  async deleteCard(cardId: string): Promise<void> {
    const url = cardEndpoints.deleteCard(cardId)
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      throw new Error('No valid access token available')
    }

    const res = await fetch(url, {
      method: 'DELETE',
      headers: getCommonHeaders(true, accessToken)
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to delete card: ${res.status} ${text}`)
    }
  },

  /**
   * Upload card image - SuperAdmin only
   * @param cardId The card ID
   * @param file The image file (jpeg, png, gif, webp - max 5MB)
   */
  async uploadCardImage(cardId: string, file: File): Promise<void> {
    const url = cardEndpoints.uploadCardImage(cardId)
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      throw new Error('No valid access token available')
    }

    const formData = new FormData()
    formData.append('file', file)

    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${accessToken}`
        // Note: Don't set Content-Type header, browser will set it with boundary for FormData
      },
      body: formData
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to upload card image: ${res.status} ${text}`)
    }
  },

  /**
   * Get card image URL
   */
  getCardImageUrl(cardId: string): string {
    // Assuming images are served from a content repository
    return buildApiUrl(`/content/cards/${cardId}`)
  }
}

export default cardService
