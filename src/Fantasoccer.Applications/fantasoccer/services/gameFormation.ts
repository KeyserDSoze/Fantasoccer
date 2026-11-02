import type { LivePlayer, LiveTeam } from '../types/game'

export function formationRequest(team: LiveTeam, live = false, asAdmin = false, applyToLeagues?: string[]) {
  if (!team.teamId || !Number.isInteger(team.version) || team.version < 0) throw new Error('Ricaricare la formazione dal server.')
  const active = team.players.filter(player => player.current.status === 0)
  const ids = active.map(player => player.current.playerId)
  if (ids.some(id => !id) || new Set(ids).size !== ids.length) throw new Error('Identità giocatori non valide.')
  if (team.version === 0 && team.sourceVersion == null) throw new Error('Rosa di origine non versionata. Ricaricare.')
  return {
    teamId: team.teamId, expectedVersion: team.version, sourceVersion: team.sourceVersion, live, asAdmin,
    ...(live && applyToLeagues?.length ? { applyToLeagues } : {}),
    positions: active.map(player => ({ playerId: player.current.playerId!, position: player.currentPosition })),
  }
}

// A displayed copy knows the saved player it stands for, so every action works on the saved lineup.
const savedOf = new WeakMap<object, LivePlayer>()

// Players as the pitch shows them: the automatic substitutions applied. Never use these to build a save or a live change.
// While the owner has unsaved edits the saved positions are shown, because the edits are expressed on them.
export function displayedPlayers(players: LivePlayer[], hasUnsavedEdits = false): LivePlayer[] {
  if (hasUnsavedEdits) return players
  return players.map(player => {
    const shown = player.projectedPosition
    if (shown == null || shown === player.currentPosition) return player
    const copy = { ...player, currentPosition: shown }
    savedOf.set(copy, player)
    return copy
  })
}

export const savedPlayer = (player: LivePlayer): LivePlayer => savedOf.get(player) ?? player

// Competitions of the same team whose lineup is, right now, the same as the one being changed.
export const sameLineupLeagues = <T extends { sameLineup?: boolean }>(sources: T[]): T[] => sources.filter(source => source.sameLineup === true)
