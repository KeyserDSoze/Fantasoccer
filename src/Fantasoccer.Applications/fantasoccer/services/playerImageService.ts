import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig';
import { tokenStorageService } from './tokenStorageService';
import { setPlayerImageRevision } from 'utils/playerImageRevision';

export async function reloadPlayerImage(groupId: string, year: number, playerKey: string) {
  const token = await tokenStorageService.getTokenString();
  if (!token) throw new Error('Accedi per aggiornare la foto del giocatore.');
  const response = await fetch(buildApiUrl('/PlayerImage/Reload'), {
    method: 'POST',
    headers: { ...getCommonHeaders(true, token), 'Content-Type': 'application/json' },
    body: JSON.stringify({ groupId, year, playerKey }),
  });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.message ?? 'Impossibile aggiornare la foto. Riprova più tardi.');
  }
  if (!body?.playerKey || !body?.revision) {
    throw new Error('Il server non ha confermato l’aggiornamento della foto.');
  }
  setPlayerImageRevision(body.playerKey, body.revision);
}
