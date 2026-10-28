import { useCallback, useEffect, useMemo, useState } from 'react'
import type { Group } from '../types/group'
import { listMyTeams } from '../utils/myTeams'
export { listMyTeams, type MyTeam } from '../utils/myTeams'

const storageKey = (groupId: string, year: number, scope: string) => `fz-selected-team:${scope}:${groupId}:${year}`

export function useMyTeams(group: Group | null | undefined, email: string | null | undefined, year: number | null | undefined, scope = 'default',
  options?: { includeDerived?: boolean; teamIds?: string[] | null }) {
  const teamIdsKey = options?.teamIds?.join(',') ?? ''
  const teams = useMemo(() => listMyTeams(group, email, year, options), [group, email, year, options?.includeDerived, teamIdsKey])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  useEffect(() => {
    if (!group || year == null) { setSelectedId(null); return }
    let stored: string | null = null
    try { stored = localStorage.getItem(storageKey(group.id, year, scope)) } catch { }
    setSelectedId(current => [current, stored].find(id => id && teams.some(team => team.teamId === id)) ?? teams[0]?.teamId ?? null)
  }, [group?.id, year, scope, teams])
  const select = useCallback((teamId: string) => {
    setSelectedId(teamId)
    if (group && year != null) { try { localStorage.setItem(storageKey(group.id, year, scope), teamId) } catch { } }
  }, [group?.id, year, scope])
  const selected = teams.find(team => team.teamId === selectedId) ?? null
  return { teams, selected, select }
}
