import { buildApiUrl, getCommonHeaders } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'

export interface ChangeTeamNameResponse {
  messages: string[]
  success: boolean
}

export interface UploadLogoParams {
  groupId: string
  year: number
  owner: string
  basketId: string
  image: File | Blob
}

export interface ChangeTeamNameParams {
  groupId: string
  owner: string
  newName: string
  basketId: string
}

class SettingsService {
  /**
   * Carica il logo di una squadra
   */
  async uploadLogo(params: UploadLogoParams): Promise<boolean> {
    const formData = new FormData()
    formData.append('image', params.image)

    const url = buildApiUrl('/Settings/UploadLogo', {
      groupId: params.groupId,
      year: params.year.toString(),
      owner: params.owner,
      basketId: params.basketId,
    })

    const token = await tokenStorageService.getTokenString()
    const headers = getCommonHeaders(true, token ?? undefined)
    
    // Rimuovo Content-Type per FormData (il browser lo imposta automaticamente)
    const { 'Content-Type': _, ...headersWithoutContentType } = headers

    const response = await fetch(url, {
      method: 'POST',
      headers: headersWithoutContentType,
      body: formData,
    })

    if (!response.ok) {
      throw new Error(`Failed to upload logo: ${response.statusText}`)
    }

    return await response.json()
  }

  /**
   * Cambia il nome di una squadra
   */
  async changeTeamName(params: ChangeTeamNameParams): Promise<ChangeTeamNameResponse> {
    const url = buildApiUrl('/Settings/ChangeTeamName', {
      groupId: params.groupId,
      owner: params.owner,
      newName: params.newName,
      basketId: params.basketId,
    })

    try {
      const token = await tokenStorageService.getTokenString()
      const headers = getCommonHeaders(true, token ?? undefined)

      // L'API restituisce uno stream di messaggi (IAsyncEnumerable)
      const response = await fetch(url, {
        method: 'GET',
        headers,
      })

      if (!response.ok) {
        throw new Error(`Failed to change team name: ${response.statusText}`)
      }

      // Leggo la risposta come testo (stream di messaggi)
      const responseText = await response.text()
      const messages = responseText.split('\n').filter((msg: string) => msg.trim() !== '')
      
      const hasError = messages.some((msg: string) => 
        msg.includes('Nome già usato') || 
        msg.includes('Non sei owner') ||
        msg.includes('Nome non valido') ||
        msg.includes('Il nome è lo stesso')
      )

      return {
        messages,
        success: !hasError,
      }
    } catch (error) {
      console.error('Error changing team name:', error)
      return {
        messages: ['Errore durante il cambio del nome della squadra'],
        success: false,
      }
    }
  }

  /**
   * Stream the change team name operation, yielding messages as they arrive from the server.
   * The server returns newline separated text messages (IAsyncEnumerable on server side).
   */
  async *changeTeamNameStream(params: ChangeTeamNameParams): AsyncGenerator<string, void, unknown> {
    const url = buildApiUrl('/Settings/ChangeTeamName', {
      groupId: params.groupId,
      owner: params.owner,
      newName: params.newName,
      basketId: params.basketId,
    })

    const token = await tokenStorageService.getTokenString()
    const headers = getCommonHeaders(true, token ?? undefined)

    const response = await fetch(url, {
      method: 'GET',
      headers,
    })

    if (!response.ok) {
      // yield a single error line and return
      yield `error: ${response.status} ${response.statusText}`
      return
    }

    if (!response.body) {
      // no body to stream — fallback to reading whole text
      const text = await response.text()
      if (text) {
        const trimmed = text.trim()
        // If server returned a JSON array of messages, parse and yield each element
        if (trimmed.startsWith('[')) {
          try {
            const arr = JSON.parse(trimmed)
            if (Array.isArray(arr)) {
              for (const item of arr) {
                const s = String(item).trim()
                if (s) yield s
              }
            }
          } catch (e) {
            // fallback: split by newline
            for (const line of trimmed.split('\n').map((l) => l.trim()).filter(Boolean)) {
              yield line
            }
          }
        } else {
          for (const line of trimmed.split('\n').map((l) => l.trim()).filter(Boolean)) {
            yield line
          }
        }
      }
      return
    }

    const reader = response.body.getReader()
    const utf8Decoder = new TextDecoder('utf-8')
    let { value: chunk, done } = await reader.read()
    let buffer = ''

    while (!done) {
      buffer += utf8Decoder.decode(chunk, { stream: true })
      let lines = buffer.split('\n')
      // keep last partial line in buffer
      buffer = lines.pop() ?? ''

      for (const line of lines) {
        const trimmed = line.trim()
        if (trimmed.length > 0) {
          yield trimmed
        }
      }

      const readResult = await reader.read()
      chunk = readResult.value
      done = readResult.done ?? false
    }

    // flush remaining buffer
    const finalBuffer = buffer.trim()
    if (finalBuffer.length > 0) {
      // If buffer looks like a JSON array, parse it and yield elements separately
      if (finalBuffer.startsWith('[')) {
        try {
          const arr = JSON.parse(finalBuffer)
          if (Array.isArray(arr)) {
            for (const item of arr) {
              const s = String(item).trim()
              if (s) yield s
            }
          } else {
            yield finalBuffer
          }
        } catch (e) {
          // parsing failed, yield raw
          yield finalBuffer
        }
      } else {
        yield finalBuffer
      }
    }
  }

  /**
   * Aggiorna lo username dell'utente
   */
  async updateUsername(newUsername: string): Promise<boolean> {
    const url = buildApiUrl('/Settings/UpdateUsername', {
      newUsername,
    })

    try {
      const token = await tokenStorageService.getTokenString()
      const headers = getCommonHeaders(true, token ?? undefined)

      const response = await fetch(url, {
        method: 'GET',
        headers,
      })

      if (!response.ok) {
        throw new Error(`Failed to update username: ${response.statusText}`)
      }

      return await response.json()
    } catch (error) {
      console.error('Error updating username:', error)
      return false
    }
  }
}

export const settingsService = new SettingsService()

