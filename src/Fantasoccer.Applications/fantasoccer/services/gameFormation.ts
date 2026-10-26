import type { LiveTeam } from '../types/game'

export function formationRequest(team: LiveTeam, live = false, asAdmin = false) {
  if (!team.teamId || !Number.isInteger(team.version) || team.version < 0) throw new Error('Ricaricare la formazione dal server.')
  const active = team.players.filter(player => player.current.status === 0)
  const ids = active.map(player => player.current.playerId)
  if (ids.some(id => !id) || new Set(ids).size !== ids.length) throw new Error('Identità giocatori non valide.')
  if (team.version === 0 && team.sourceVersion == null) throw new Error('Rosa di origine non versionata. Ricaricare.')
  return {
    teamId: team.teamId, expectedVersion: team.version, sourceVersion: team.sourceVersion, live, asAdmin,
    positions: active.map(player => ({ playerId: player.current.playerId!, position: player.currentPosition })),
  }
}
