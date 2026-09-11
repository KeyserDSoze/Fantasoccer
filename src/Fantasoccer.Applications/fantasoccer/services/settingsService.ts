import { buildApiUrl, getCommonHeaders } from '../configurations/apiConfig';
import { tokenStorageService } from './tokenStorageService';
import type { UserSettings } from 'types/settings';

export interface ChangeTeamNameResponse {
  messages: string[];
  success: boolean;
}

export interface UploadLogoParams {
  groupId: string;
  year: number;
  owner: string;
  basketId: string;
  image: File | Blob;
}

export interface CopyLogoParams {
  groupId: string;
  year: number;
  owner: string;
  basketId: string;
}

export interface ChangeTeamNameParams {
  groupId: string;
  owner: string;
  newName: string;
  basketId: string;
}

/**
 * JSON Stream Decoder for processing JSON objects as they arrive in chunks
 */
class JsonStreamDecoder {
  private level = 0;
  private partialItem = '';
  private static JTOKEN_START_OBJECT = '{';
  private static JTOKEN_END_OBJECT = '}';
  private static JTOKEN_START_ARRAY = '[';
  private static JTOKEN_END_ARRAY = ']';
  private decoder = new TextDecoder();

  public decodeChunk<T>(value: Uint8Array, decodedItemCallback: (item: T) => void): void {
    const chunk = this.decoder.decode(value, { stream: true });
    let itemStart = 0;

    for (let i = 0; i < chunk.length; i++) {
      const char = chunk[i];

      if (
        char === JsonStreamDecoder.JTOKEN_START_OBJECT ||
        char === JsonStreamDecoder.JTOKEN_START_ARRAY
      ) {
        if (this.level === 0) {
          itemStart = i;
        }
        this.level++;
      }

      if (
        char === JsonStreamDecoder.JTOKEN_END_OBJECT ||
        char === JsonStreamDecoder.JTOKEN_END_ARRAY
      ) {
        this.level--;
        if (this.level === 0) {
          let item = chunk.substring(itemStart, i + 1);
          if (this.partialItem) {
            item = this.partialItem + item;
            this.partialItem = '';
          }
          try {
            decodedItemCallback(JSON.parse(item));
          } catch (e) {
            // Skip invalid JSON
            console.warn('Invalid JSON item:', item, e);
          }
        }
      }
    }

    if (this.level !== 0) {
      this.partialItem += chunk.substring(itemStart);
    }
  }
}

class SettingsService {
  async getUserSettings(): Promise<UserSettings> {
    const token = await tokenStorageService.getTokenString();
    const response = await fetch(buildApiUrl('/Settings/Get'), {
      headers: getCommonHeaders(true, token ?? undefined),
    });
    if (!response.ok) throw new Error(`Failed to load settings: ${response.statusText}`);
    const raw = await response.json();
    return {
      aiPrompt: raw.aiPrompt ?? raw.AiPrompt ?? null,
      autoOptimalFormation: !!(raw.autoOptimalFormation ?? raw.AutoOptimalFormation),
    };
  }

  async updateUserSettings(settings: UserSettings): Promise<boolean> {
    const token = await tokenStorageService.getTokenString();
    const response = await fetch(buildApiUrl('/Settings/Update'), {
      method: 'PUT',
      headers: {
        ...getCommonHeaders(true, token ?? undefined),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ autoOptimalFormation: settings.autoOptimalFormation }),
    });
    if (!response.ok) throw new Error(`Failed to update settings: ${response.statusText}`);
    return await response.json();
  }

  /**
   * Carica il logo di una squadra
   */
  async uploadLogo(params: UploadLogoParams): Promise<boolean> {
    const formData = new FormData();
    // Il backend si aspetta 'image' come nome del campo (parametro IFormFile image)
    formData.append('image', params.image, 'logo.png');

    const url = buildApiUrl('/Settings/UploadLogo', {
      groupId: params.groupId,
      year: params.year.toString(),
      owner: params.owner,
      basketId: params.basketId,
    });

    const token = await tokenStorageService.getTokenString();
    const headers = getCommonHeaders(true, token ?? undefined);

    // Rimuovo Content-Type per FormData (il browser lo imposta automaticamente con boundary)
    const { 'Content-Type': _, ...headersWithoutContentType } = headers;

    const response = await fetch(url, {
      method: 'POST',
      headers: headersWithoutContentType,
      body: formData,
    });

    if (!response.ok) {
      throw new Error(`Failed to upload logo: ${response.statusText}`);
    }

    return await response.json();
  }

  /**
   * Copia il logo dall'anno precedente
   */
  async copyLogoFromPreviousYear(params: CopyLogoParams): Promise<boolean> {
    const url = buildApiUrl('/Settings/CopyLogoFromPreviousYear', {
      groupId: params.groupId,
      year: params.year.toString(),
      owner: params.owner,
      basketId: params.basketId,
    });

    const token = await tokenStorageService.getTokenString();
    const headers = getCommonHeaders(true, token ?? undefined);

    const response = await fetch(url, {
      method: 'POST',
      headers,
    });

    if (!response.ok) {
      throw new Error(`Failed to copy logo: ${response.statusText}`);
    }

    return await response.json();
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
    });

    try {
      const token = await tokenStorageService.getTokenString();
      const headers = getCommonHeaders(true, token ?? undefined);

      // L'API restituisce uno stream di messaggi (IAsyncEnumerable)
      const response = await fetch(url, {
        method: 'GET',
        headers,
      });

      if (!response.ok) {
        throw new Error(`Failed to change team name: ${response.statusText}`);
      }

      // Leggo la risposta come testo (stream di messaggi)
      const responseText = await response.text();
      const messages = responseText.split('\n').filter((msg: string) => msg.trim() !== '');

      const hasError = messages.some(
        (msg: string) =>
          msg.includes('Nome già usato') ||
          msg.includes('Non sei owner') ||
          msg.includes('Nome non valido') ||
          msg.includes('Il nome è lo stesso')
      );

      return {
        messages,
        success: !hasError,
      };
    } catch (error) {
      console.error('Error changing team name:', error);
      return {
        messages: ['Errore durante il cambio del nome della squadra'],
        success: false,
      };
    }
  }

  /**
   * Stream the change team name operation, yielding messages as they arrive from the server.
   * The server returns JSON objects or newline-separated text messages.
   */
  async *changeTeamNameStream(params: ChangeTeamNameParams): AsyncGenerator<string, void, unknown> {
    const url = buildApiUrl('/Settings/ChangeTeamName', {
      groupId: params.groupId,
      owner: params.owner,
      newName: params.newName,
      basketId: params.basketId,
    });

    const token = await tokenStorageService.getTokenString();
    const headers = getCommonHeaders(true, token ?? undefined);

    const response = await fetch(url, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      yield `error: ${response.status} ${response.statusText}`;
      return;
    }

    if (!response.body) {
      // Fallback: read entire response as text
      const text = await response.text();
      if (text) {
        const trimmed = text.trim();
        for (const line of trimmed
          .split('\n')
          .map((l) => l.trim())
          .filter(Boolean)) {
          yield line;
        }
      }
      return;
    }

    const contentType = response.headers.get('content-type') || '';

    // Check if response is JSON stream (application/json or text/json)
    if (contentType.includes('application/json') || contentType.includes('text/json')) {
      // Use JSON stream decoder for structured JSON objects
      const reader = response.body.getReader();
      const decoder = new JsonStreamDecoder();
      const messages: string[] = [];

      try {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          if (!value) continue;

          decoder.decodeChunk<string>(value, (message) => {
            messages.push(message);
            // Yield immediately when decoded
          });
        }
      } finally {
        reader.releaseLock();
      }

      // Yield all decoded messages
      for (const msg of messages) {
        yield msg;
      }
      return;
    }

    // Default: treat as text/plain stream (newline-separated)
    const reader = response.body.getReader();
    const utf8Decoder = new TextDecoder('utf-8');
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (!value) continue;

        buffer += utf8Decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');

        // Keep last partial line in buffer
        buffer = lines.pop() ?? '';

        for (const line of lines) {
          const trimmed = line.trim();
          if (trimmed.length > 0) {
            yield trimmed;
          }
        }
      }

      // Flush remaining buffer
      const finalBuffer = buffer.trim();
      if (finalBuffer.length > 0) {
        yield finalBuffer;
      }
    } finally {
      reader.releaseLock();
    }
  }

  /**
   * Aggiorna lo username dell'utente
   */
  async updateUsername(newUsername: string): Promise<boolean> {
    const url = buildApiUrl('/Settings/UpdateUsername', {
      newUsername,
    });

    try {
      const token = await tokenStorageService.getTokenString();
      const headers = getCommonHeaders(true, token ?? undefined);

      const response = await fetch(url, {
        method: 'GET',
        headers,
      });

      if (!response.ok) {
        throw new Error(`Failed to update username: ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error('Error updating username:', error);
      return false;
    }
  }
}

export const settingsService = new SettingsService();
