import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig'
import { mapRawGameWrapperToGameWrapper, GameWrapperRaw } from 'types/game'
import { Team, mapTeamToRawTeam } from 'types/team'
import { tokenStorageService } from './tokenStorageService';

export const gameEndpoints = {
  getGame: (params: { groupId: string; leagueId: string; year: number; gameId: string }) =>
    buildApiUrl('/Game/Get', params as any),
  saveFormation: () => buildApiUrl('/Game/SaveFormation'),
  saveTeam: (groupId: string, asAdmin?: boolean) => {
    const params: any = { groupId }
    if (asAdmin !== undefined) {
      params.asAdmin = asAdmin
    }
    return buildApiUrl('/Game/SaveTeam', params)
  }
}

export const gameService = {
  async getGame(params: { groupId: string; leagueId: string; year: number; gameId: string }) {
    const url = gameEndpoints.getGame(params)
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
        console.warn('No valid access token available')
        return null
    }
    const res = await fetch(url, { headers: getCommonHeaders(true, accessToken) })
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to fetch game: ${res.status} ${text}`)
    }

    const raw = (await res.json()) as GameWrapperRaw
    return mapRawGameWrapperToGameWrapper(raw)
  },

  async saveFormation(payload: any) {
    const url = gameEndpoints.saveFormation()
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
        console.warn('No valid access token available')
        return null
    }
    const res = await fetch(url, {
      method: 'POST',
      headers: { ...getCommonHeaders(true, accessToken), 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to save formation: ${res.status} ${text}`)
    }

    return await res.json()
  },

  async saveTeam(groupId: string, team: Team, asAdmin?: boolean) {
    const url = gameEndpoints.saveTeam(groupId, asAdmin)
    const accessToken = await tokenStorageService.getTokenString()
    if (!accessToken) {
      console.warn('No valid access token available')
      return null
    }
    const rawTeam = mapTeamToRawTeam(team)
    const res = await fetch(url, {
      method: 'POST',
      headers: { ...getCommonHeaders(true, accessToken), 'Content-Type': 'application/json' },
      body: JSON.stringify(rawTeam)
    })
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`Failed to save team: ${res.status} ${text}`)
    }
    // TeamResponse -> { i: boolean, m: string }
    return await res.json() as { i: boolean; m?: string }
  }
}

export default gameService
