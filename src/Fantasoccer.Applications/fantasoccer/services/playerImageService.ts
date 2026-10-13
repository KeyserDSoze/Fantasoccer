import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig';
import { tokenStorageService } from './tokenStorageService';
import { setPlayerImageRevision } from 'utils/playerImageRevision';

export interface ReloadTeamPlayerImagesRequest {
  groupId: string;
  year: number;
  basketId: string;
  owner: string;
}

export interface TeamPlayerImageRefreshFailure {
  playerKey: string;
  playerName: string;
  error: string | null;
}

export interface TeamPlayerImageRefreshSummary {
  total: number;
  succeededCount: number;
  failedCount: number;
  succeeded: { playerKey: string; playerName: string; revision: string }[];
  failed: TeamPlayerImageRefreshFailure[];
}

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

export async function reloadTeamPlayerImages(
  params: ReloadTeamPlayerImagesRequest
): Promise<TeamPlayerImageRefreshSummary> {
  const token = await tokenStorageService.getTokenString();
  if (!token) throw new Error('Accedi per aggiornare le immagini dei giocatori.');

  const response = await fetch(buildApiUrl('/PlayerImage/ReloadTeam'), {
    method: 'POST',
    headers: { ...getCommonHeaders(true, token), 'Content-Type': 'application/json' },
    body: JSON.stringify(params),
  });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.message ?? 'Impossibile aggiornare le immagini della squadra.');
  }
  if (!Array.isArray(body?.succeeded) || !Array.isArray(body?.failed)) {
    throw new Error('Il server non ha restituito il riepilogo dell’aggiornamento.');
  }

  const summary = body as TeamPlayerImageRefreshSummary;
  summary.succeeded.forEach((player) => setPlayerImageRevision(player.playerKey, player.revision));
  return summary;
}
