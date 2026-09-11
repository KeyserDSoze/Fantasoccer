import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig';
import { mapRawGameWrapperToGameWrapper, GameWrapperRaw } from 'types/game';
import { Team, mapTeamToRawTeam } from 'types/team';
import { tokenStorageService } from './tokenStorageService';

export interface OptimalFormationPlayer {
  playerKey: string;
  playerName: string;
  role: number;
  position: number;
  projection: number;
  confidence: number;
}

export interface OptimalFormationResult {
  players: OptimalFormationPlayer[];
  module: string;
  score: number;
  confidence: number;
  warnings: string[];
}

export const gameEndpoints = {
  getGame: (params: {
    groupId: string;
    leagueId: string;
    year: number;
    gameId: string;
    editMode?: GameEditMode;
  }) => buildApiUrl('/Game/Get', params as any),
  saveFormation: () => buildApiUrl('/Game/SaveFormation'),
  saveTeam: (params: GameContext & { asAdmin?: boolean }) => {
    const { asAdmin, ...context } = params;
    const query: any = context;
    if (asAdmin !== undefined) query.asAdmin = asAdmin;
    return buildApiUrl('/Game/SaveTeam', query);
  },
  optimalFormation: (params: {
    groupId: string;
    leagueId: string;
    year: number;
    day: number;
    owner: string;
  }) => buildApiUrl('/Game/OptimalFormation', params as any),
};

export type GameEditMode = 'formation' | 'live' | 'view';

export interface GameContext {
  groupId: string;
  leagueId: string;
  year: number;
  gameId: string;
  editMode: GameEditMode;
}

export const gameService = {
  async getGame(params: {
    groupId: string;
    leagueId: string;
    year: number;
    gameId: string;
    editMode?: GameEditMode;
  }) {
    const url = gameEndpoints.getGame(params);
    const accessToken = await tokenStorageService.getTokenString();
    if (!accessToken) {
      console.warn('No valid access token available');
      return null;
    }
    const res = await fetch(url, { headers: getCommonHeaders(true, accessToken) });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Failed to fetch game: ${res.status} ${text}`);
    }

    const raw = (await res.json()) as GameWrapperRaw;
    return mapRawGameWrapperToGameWrapper(raw);
  },

  async saveFormation(payload: any) {
    const url = gameEndpoints.saveFormation();
    const accessToken = await tokenStorageService.getTokenString();
    if (!accessToken) {
      console.warn('No valid access token available');
      return null;
    }
    const res = await fetch(url, {
      method: 'POST',
      headers: { ...getCommonHeaders(true, accessToken), 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Failed to save formation: ${res.status} ${text}`);
    }

    return await res.json();
  },

  async getOptimalFormation(params: {
    groupId: string;
    leagueId: string;
    year: number;
    day: number;
    owner: string;
  }): Promise<OptimalFormationResult | null> {
    const url = gameEndpoints.optimalFormation(params);
    const accessToken = await tokenStorageService.getTokenString();
    if (!accessToken) return null;
    const res = await fetch(url, { headers: getCommonHeaders(true, accessToken) });
    const body = await res.text();
    if (!res.ok) {
      let message = body.trim();
      try {
        const problem = message ? JSON.parse(message) : null;
        message = problem?.detail ?? problem?.title ?? message;
      } catch {
        // Keep the raw response when the server did not return JSON.
      }
      throw new Error(`Errore calcolo AI (${res.status}): ${message || res.statusText}`);
    }
    if (!body.trim()) return null;
    try {
      return JSON.parse(body) as OptimalFormationResult | null;
    } catch {
      throw new Error('Il server ha restituito una risposta non valida per il calcolo AI.');
    }
  },

  async saveTeam(context: GameContext, team: Team, asAdmin?: boolean) {
    const url = gameEndpoints.saveTeam({ ...context, asAdmin });
    const accessToken = await tokenStorageService.getTokenString();
    if (!accessToken) {
      console.warn('No valid access token available');
      return null;
    }
    const rawTeam = mapTeamToRawTeam(team);
    const res = await fetch(url, {
      method: 'POST',
      headers: { ...getCommonHeaders(true, accessToken), 'Content-Type': 'application/json' },
      body: JSON.stringify(rawTeam),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Failed to save team: ${res.status} ${text}`);
    }
    // TeamResponse -> { i: boolean, m: string }
    return (await res.json()) as { i: boolean; m?: string };
  },
};

export default gameService;
