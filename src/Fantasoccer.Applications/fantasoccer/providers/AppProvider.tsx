import React, { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { useAuth } from './AuthProvider'
import { groupService } from '../services/groupService'
import { authService } from '../services/authService'
import { tokenStorageService } from '../services/tokenStorageService'
import { useGlobalLoading } from './LoadingProvider'
import { enhanceGroup, type EnhancedGroup, type Group, type League } from '../types/group'
import type { AppIdentityState } from '../types/identity'

export interface AppState {
  currentGroup: Group | null; enhancedGroup: EnhancedGroup | null; userGroups: Group[]; isGroupsLoading: boolean
  hasLoadedGroups: boolean; groupsError: string | null; selectedLeagueId: string | null; selectedYear: number | null; superAdminMode: boolean
}
export interface AppContextType extends AppState {
  refreshGroups: () => Promise<void>; applyGroupUpdate: (group: Group) => void; setCurrentGroup: (id: string | null) => Promise<void>
  setSelectedLeague: (id: string | null) => Promise<void>; setSelectedYear: (year: number | null) => Promise<void>
  getAvailableLeagues: () => League[]; getAvailableYears: (id?: string) => number[]; clearError: () => void; toggleSuperAdminMode: () => void
}
const AppContext = createContext<AppContextType | undefined>(undefined)
export function useApp() { const value = useContext(AppContext); if (!value) throw new Error('useApp requires AppProvider'); return value }

export function AppProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated, user, isLoading: authLoading, updateUser } = useAuth()
  const { showLoading, hideLoading } = useGlobalLoading()
  const [currentGroup, setGroup] = useState<Group | null>(null)
  const [userGroups, setGroups] = useState<Group[]>([])
  const [isGroupsLoading, setLoading] = useState(false)
  const [loadedGroupsFor, setLoadedGroupsFor] = useState<string | null>(null)
  const [groupsError, setError] = useState<string | null>(null)
  const [selectedLeagueId, setLeague] = useState<string | null>(null)
  const [selectedYear, setYear] = useState<number | null>(null)
  const [superAdminMode, setSuperAdmin] = useState(() => { try { return localStorage.getItem('superAdminMode') === 'true' } catch { return false } })
  const loadSequence = useRef(0)
  const selectionSequence = useRef(0)
  const writeQueue = useRef<Promise<void>>(Promise.resolve())
  const desiredSelection = useRef<AppIdentityState | null>(null)
  const desiredGroup = useRef<Group | null>(null)
  const selectionAbort = useRef(new AbortController())
  const activeEmail = useRef<string | null>(null)
  activeEmail.current = isAuthenticated ? user?.email ?? null : null
  useEffect(() => {
    selectionAbort.current.abort()
    selectionAbort.current = new AbortController()
    return () => selectionAbort.current.abort()
  }, [isAuthenticated, user?.email])

  function selectLocally(group: Group | null, leagueId?: string | null, year?: number | null, workingEmail = user?.state.meOrLeaderEmail ?? null) {
    const league = group?.leagues.find(value => value.id === leagueId) ?? group?.leagues.find(value => value.isMain) ?? group?.leagues[0]
    const season = year != null && league?.years.some(value => value.year === year) ? year : league?.years.length ? Math.max(...league.years.map(value => value.year)) : null
    setGroup(group); setLeague(league?.id ?? null); setYear(season)
    desiredGroup.current = group
    desiredSelection.current = { groupId: group?.id ?? null, leagueId: league?.id ?? null, year: season, meOrLeaderEmail: workingEmail }
  }
  async function refreshGroups() {
    const sequence = ++loadSequence.current
    if (!isAuthenticated || !user) return
    setLoadedGroupsFor(null)
    setLoading(true); setError(null); showLoading('Caricamento gruppi...')
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) throw new Error('Accesso richiesto.')
      const [groups, profile] = await Promise.all([groupService.getUserGroups(), authService.getUserInfo(token)])
      if (sequence !== loadSequence.current || profile.email !== user.email || activeEmail.current !== user.email) return
      updateUser(profile)
      setGroups(groups)
      setLoadedGroupsFor(profile.email.toLowerCase())
      const group = groups.find(value => value.id === profile.state.groupId) ?? groups[0] ?? null
      selectLocally(group, profile.state.leagueId, profile.state.year, profile.state.meOrLeaderEmail)
    } catch (error) {
      if (sequence === loadSequence.current) {
        setError(error instanceof Error ? error.message : 'Caricamento non riuscito.')
        setLoadedGroupsFor(user.email.toLowerCase())
      }
    }
    finally { if (sequence === loadSequence.current) { setLoading(false); hideLoading() } }
  }
  async function persist(selection: AppIdentityState, group: Group | null) {
    if (!user) return
    const sequence = ++selectionSequence.current
    desiredSelection.current = selection; desiredGroup.current = group
    setError(null)
    const updated = { ...user, state: selection }
    const controller = selectionAbort.current
    const operation = writeQueue.current.catch(() => undefined).then(async () => {
      if (controller.signal.aborted || activeEmail.current !== updated.email) return
      try {
        await authService.updateUserState(updated, controller.signal)
        if (controller.signal.aborted || activeEmail.current !== updated.email) return
        if (sequence !== selectionSequence.current) return
        updateUser(updated); setGroup(group); setLeague(selection.leagueId); setYear(selection.year)
      } catch (error) {
        if (controller.signal.aborted || activeEmail.current !== updated.email) return
        if (sequence === selectionSequence.current) {
          desiredSelection.current = user.state; desiredGroup.current = currentGroup
          setError(error instanceof Error ? error.message : 'Selezione non salvata.')
        }
        throw error
      }
    })
    writeQueue.current = operation
    await operation
  }
  async function setCurrentGroup(id: string | null) {
    const group = userGroups.find(value => value.id === id) ?? null
    const league = group?.leagues.find(value => value.isMain) ?? group?.leagues[0]
    const year = league?.years.length ? Math.max(...league.years.map(value => value.year)) : null
    await persist({ groupId: group?.id ?? null, leagueId: league?.id ?? null, year, meOrLeaderEmail: null }, group)
  }
  async function setSelectedLeague(id: string | null) {
    const group = desiredGroup.current ?? currentGroup
    const previousYear = desiredSelection.current?.year ?? selectedYear
    const league = group?.leagues.find(value => value.id === id)
    if (id && !league) throw new Error('La lega non appartiene al gruppo selezionato.')
    const year = previousYear && league?.years.some(value => value.year === previousYear) ? previousYear : league?.years.length ? Math.max(...league.years.map(value => value.year)) : null
    await persist({ groupId: group?.id ?? null, leagueId: id, year, meOrLeaderEmail: null }, group)
  }
  async function setSelectedYear(year: number | null) {
    const group = desiredGroup.current ?? currentGroup
    const previous = desiredSelection.current ?? user?.state
    await persist({ groupId: group?.id ?? null, leagueId: previous?.leagueId ?? selectedLeagueId, year, meOrLeaderEmail: previous?.meOrLeaderEmail ?? null }, group)
  }
  function applyGroupUpdate(group: Group) {
    loadSequence.current++; setGroups(values => values.map(value => value.id === group.id ? group : value))
    selectLocally(group, selectedLeagueId, selectedYear); setLoading(false); hideLoading()
  }
  useEffect(() => {
    if (isAuthenticated && user && !authLoading) { void refreshGroups(); return }
    if (!isAuthenticated) {
      loadSequence.current++; selectionSequence.current++; setGroups([]); setGroup(null); setLeague(null); setYear(null); setLoading(false); setError(null); setLoadedGroupsFor(null)
      desiredSelection.current = null; desiredGroup.current = null
    }
  }, [isAuthenticated, user?.email, authLoading])
  const hasLoadedGroups = !!user?.email && loadedGroupsFor === user.email.toLowerCase()
  return <AppContext.Provider value={{ currentGroup, enhancedGroup: currentGroup ? enhanceGroup(currentGroup) : null, userGroups,
    isGroupsLoading, hasLoadedGroups, groupsError, selectedLeagueId, selectedYear, superAdminMode, refreshGroups, applyGroupUpdate,
    setCurrentGroup, setSelectedLeague, setSelectedYear, getAvailableLeagues: () => currentGroup?.leagues ?? [],
    getAvailableYears: id => currentGroup?.leagues.find(value => value.id === (id ?? selectedLeagueId))?.years.map(value => value.year) ?? [],
    clearError: () => setError(null), toggleSuperAdminMode: () => setSuperAdmin(value => {
      try { localStorage.setItem('superAdminMode', String(!value)) } catch { }
      return !value
    }) }}>{children}</AppContext.Provider>
}
