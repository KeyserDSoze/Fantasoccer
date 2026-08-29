// Group League Admin Screen - Super Admin management of league settings and calculations
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Platform, View, ScrollView } from 'react-native'
import { Button, Card, Input, Separator, Text, XStack, YStack, useMedia, Switch, Label, Select, Spinner } from 'tamagui'
import { useToastController } from '@tamagui/toast'
import { ScreenContent } from 'components/layout/ScreenContent'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { LeagueSettingsEditor } from 'components/LeagueSettingsEditor'
import { useApp } from 'providers/AppProvider'
import { groupService } from 'services/groupService'
import { leagueManagerService } from 'services/leagueManagerService'
import { calendarService } from 'services/calendarService'
import type { League, LeagueSetting, AnnualLeague } from 'types/group'
import type { Calendar } from 'types/calendar'
import { DefaultLeagueSetting, LeagueType } from 'types/group'
import { Package, Calculator, Shuffle, RefreshCw, Save, Trash2, ChevronDown, Copy } from '@tamagui/lucide-icons-2'
import { formatSeasonFromYear, getAvailableSeasonYears, getCurrentSeasonYear } from 'utils/season'
import { cloneLeagueSetting, copyAnnualLeagueToYear, getAnnualLeagueForYear, isLeagueSettingValid, upsertAnnualLeague } from 'utils/groupAdmin'

const getCalendarOwners = (calendar: Calendar): string[] => {
  const owners = new Set<string>()
  Object.values(calendar.rounds).flat().forEach(day => {
    day.games.forEach(game => {
      owners.add(game.homeOwner)
      owners.add(game.awayOwner)
    })
  })
  return [...owners].sort()
}

export default function GroupLeagueAdminScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'
  const toast = useToastController()

  const { currentGroup, applyGroupUpdate, selectedYear, setSelectedYear } = useApp()

  const [selectedLeagueId, setSelectedLeagueId] = useState<string | null>(null)
  const [newLeagueName, setNewLeagueName] = useState('')
  const [dayToRecalculate, setDayToRecalculate] = useState(38)
  const [pendingLeagueChanges, setPendingLeagueChanges] = useState<Partial<League> | null>(null)
  const [calendarOwners, setCalendarOwners] = useState<string[] | null>(null)
  const [settingsValid, setSettingsValid] = useState(true)
  const [settingsRevision, setSettingsRevision] = useState(0)
  
  // Loading states for each operation
  const [loadingStates, setLoadingStates] = useState<Record<string, boolean>>({})
  
  const setLoading = (key: string, value: boolean) => {
    setLoadingStates(prev => ({ ...prev, [key]: value }))
  }
  
  const isLoading = (key: string) => loadingStates[key] || false
  const isAnyLoading = Object.values(loadingStates).some(v => v)

  // Available years for selector
  const availableYears = useMemo(() => getAvailableSeasonYears(), [])

  // Selected year index (default to current year index)
  const currentYearIndex = selectedYear ?? getCurrentSeasonYear()

  // League options for selector
  const leagueOptions = useMemo(() => {
    if (!currentGroup || !currentGroup.leagues) return []
    return currentGroup.leagues.map(league => ({
      id: league.id,
      name: league.name,
      isMain: league.isMain
    }))
  }, [currentGroup])

  // Selected league
  const selectedLeague = useMemo(() => {
    if (!currentGroup || !selectedLeagueId) return null
    const league = currentGroup.leagues.find(l => l.id === selectedLeagueId) || null
    // Merge with pending changes
    if (league && pendingLeagueChanges && pendingLeagueChanges.id === selectedLeagueId) {
      return { ...league, ...pendingLeagueChanges }
    }
    return league
  }, [currentGroup, selectedLeagueId, pendingLeagueChanges])

  const persistedSelectedLeague = useMemo(() => {
    return currentGroup?.leagues.find(league => league.id === selectedLeagueId) || null
  }, [currentGroup, selectedLeagueId])

  // Selected yearly league
  const selectedYearlyLeague = useMemo(() => {
    if (!selectedLeague) return null
    return getAnnualLeagueForYear(selectedLeague, currentYearIndex, DefaultLeagueSetting)
  }, [selectedLeague, currentYearIndex])

  const previousYearlyLeague = useMemo(() => {
    return persistedSelectedLeague?.years.find(y => y.year === currentYearIndex - 1) || null
  }, [persistedSelectedLeague, currentYearIndex])

  const isYearPersisted = useMemo(() => {
    const persistedLeague = currentGroup?.leagues.find(league => league.id === selectedLeagueId)
    return persistedLeague?.years.some(year => year.year === currentYearIndex) || false
  }, [currentGroup, selectedLeagueId, currentYearIndex])

  const isYearInDraft = selectedLeague?.years.some(year => year.year === currentYearIndex) || false
  const hasPendingChanges = pendingLeagueChanges?.id === selectedLeagueId
  const effectiveLeagueType = selectedYearlyLeague?.type || selectedLeague?.type || LeagueType.Null
  const associatedBaskets = currentGroup?.baskets.filter(basket => selectedLeague?.basketsId.includes(basket.id)) || []
  const currentYearTeams = associatedBaskets.flatMap(basket => (
    basket.years.find(year => year.year === currentYearIndex)?.teams || []
  ))
  const hasDuplicateOwners = new Set(currentYearTeams.map(team => team.owner)).size !== currentYearTeams.length
  const minimumCalendarTeams = effectiveLeagueType === LeagueType.Cup ? 16 : 2
  const canRunOperations = isYearPersisted && !hasPendingChanges && effectiveLeagueType !== LeagueType.Null
  const canCreateCalendar = canRunOperations && associatedBaskets.length > 0 &&
    currentYearTeams.length >= minimumCalendarTeams && !hasDuplicateOwners
  const currentOwners = [...new Set(currentYearTeams.map(team => team.owner))].sort()
  const hasMatchingCalendarRoster = calendarOwners !== null && (
    effectiveLeagueType === LeagueType.Cup
      ? calendarOwners.length === 16 && calendarOwners.every(owner => currentOwners.includes(owner))
      : calendarOwners.length === currentOwners.length &&
        calendarOwners.every((owner, index) => owner === currentOwners[index])
  )
  const canRecalculate = canRunOperations && !hasDuplicateOwners && hasMatchingCalendarRoster

  // Settings for current year
  const settings = useMemo(() => {
    return selectedYearlyLeague?.settings || null
  }, [selectedYearlyLeague])

  const confirmDiscardChanges = useCallback(async (): Promise<boolean> => {
    if (!hasPendingChanges) return true
    if (Platform.OS === 'web') {
      return window.confirm('Ci sono modifiche non salvate. Vuoi scartarle?')
    }

    return new Promise(resolve => {
      Alert.alert(
        'Modifiche non salvate',
        'Vuoi scartare le modifiche prima di cambiare selezione?',
        [
          { text: 'Resta qui', style: 'cancel', onPress: () => resolve(false) },
          { text: 'Scarta', style: 'destructive', onPress: () => resolve(true) }
        ],
        { cancelable: true, onDismiss: () => resolve(false) }
      )
    })
  }, [hasPendingChanges])

  const confirmSettingsReplacement = useCallback(async (message: string): Promise<boolean> => {
    if (Platform.OS === 'web') return window.confirm(message)

    return new Promise(resolve => {
      Alert.alert(
        'Sostituisci impostazioni',
        message,
        [
          { text: 'Annulla', style: 'cancel', onPress: () => resolve(false) },
          { text: 'Continua', style: 'destructive', onPress: () => resolve(true) }
        ],
        { cancelable: true, onDismiss: () => resolve(false) }
      )
    })
  }, [])

  const handleLeagueSelection = useCallback(async (leagueId: string | null) => {
    if (leagueId === selectedLeagueId || !await confirmDiscardChanges()) return
    setSelectedLeagueId(leagueId)
  }, [selectedLeagueId, confirmDiscardChanges])

  const handleYearSelection = useCallback(async (year: number | null) => {
    if (year === currentYearIndex || !await confirmDiscardChanges()) return
    await setSelectedYear(year)
  }, [currentYearIndex, confirmDiscardChanges, setSelectedYear])

  // Initialize selected league
  useEffect(() => {
    if (currentGroup && currentGroup.leagues && currentGroup.leagues.length > 0 && !selectedLeagueId) {
      setSelectedLeagueId(currentGroup.leagues[0].id)
    }
  }, [currentGroup, selectedLeagueId])

  // Draft changes belong to a single league and year.
  useEffect(() => {
    setPendingLeagueChanges(null)
    setSettingsValid(true)
    setSettingsRevision(revision => revision + 1)
  }, [selectedLeagueId, currentYearIndex])

  useEffect(() => {
    let active = true
    setCalendarOwners(null)

    if (!currentGroup || !selectedLeagueId || !isYearPersisted) return

    calendarService.getCalendarByComponents(currentGroup.id, selectedLeagueId, currentYearIndex)
      .then(calendar => {
        if (!active || !calendar) return

        setCalendarOwners(getCalendarOwners(calendar))
      })

    return () => {
      active = false
    }
  }, [currentGroup, selectedLeagueId, currentYearIndex, isYearPersisted])

  // Helper to update league properties
  const updateLeagueProperty = useCallback(<K extends keyof League>(
    key: K,
    value: League[K]
  ) => {
    if (!selectedLeague) return
    setPendingLeagueChanges(current => ({
      ...current,
      id: selectedLeague.id,
      [key]: value
    }))
  }, [selectedLeague])

  const updateAnnualLeague = useCallback((annualLeague: AnnualLeague) => {
    if (!selectedLeague) return

    setPendingLeagueChanges(current => ({
      ...current,
      id: selectedLeague.id,
      years: upsertAnnualLeague(selectedLeague.years, annualLeague)
    }))
  }, [selectedLeague])

  const initializeYearWithDefaults = useCallback(async () => {
    if (!selectedLeague) return
    if ((isYearPersisted || hasPendingChanges) && !await confirmSettingsReplacement(
      `Vuoi sostituire tutte le impostazioni del ${formatSeasonFromYear(currentYearIndex)} con i valori di default?`
    )) return

    const defaultSettings = cloneLeagueSetting(DefaultLeagueSetting)
    updateAnnualLeague({
      year: currentYearIndex,
      type: selectedLeague.type,
      settings: defaultSettings
    })
    setSettingsValid(isLeagueSettingValid(defaultSettings))
    setSettingsRevision(revision => revision + 1)
    toast.show(`Settings di default pronti per il ${formatSeasonFromYear(currentYearIndex)}. Premi Salva Impostazioni per confermare.`, { variant: 'success' })
  }, [selectedLeague, isYearPersisted, hasPendingChanges, currentYearIndex, confirmSettingsReplacement, updateAnnualLeague, toast])

  const copySettingsFromPreviousYear = useCallback(async () => {
    if (!previousYearlyLeague) return

    if (!selectedLeague) return
    if ((isYearPersisted || hasPendingChanges) && !await confirmSettingsReplacement(
      `Vuoi sostituire tutte le impostazioni del ${formatSeasonFromYear(currentYearIndex)} con quelle del ${formatSeasonFromYear(currentYearIndex - 1)}?`
    )) return

    const copiedYear = copyAnnualLeagueToYear(previousYearlyLeague, currentYearIndex, selectedLeague.type)
    updateAnnualLeague(copiedYear)
    setSettingsValid(isLeagueSettingValid(copiedYear.settings))
    setSettingsRevision(revision => revision + 1)
    toast.show(`Settings copiati dal ${formatSeasonFromYear(currentYearIndex - 1)}. Premi Salva Impostazioni per confermare.`, { variant: 'success' })
  }, [previousYearlyLeague, selectedLeague, isYearPersisted, hasPendingChanges, currentYearIndex, confirmSettingsReplacement, updateAnnualLeague, toast])

  // Create new league
  const createLeague = useCallback(async () => {
    if (!currentGroup || !newLeagueName.trim()) return

    setLoading('create', true)

    try {
      const newLeagueId = crypto.randomUUID()
      
      const newLeague: League = {
        id: newLeagueId,
        name: newLeagueName.trim(),
        isMain: false,
        type: LeagueType.League,
        years: [{
          year: currentYearIndex,
          type: LeagueType.League,
          settings: cloneLeagueSetting(DefaultLeagueSetting)
        }],
        basketsId: []
      }

      const updatedGroup = {
        ...currentGroup,
        leagues: [...(currentGroup.leagues || []), newLeague]
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      toast.show(`✅ Lega "${newLeagueName}" creata con successo`, { variant: 'success' })
      setNewLeagueName('')
      setSelectedLeagueId(newLeagueId)
      
    } catch (err) {
      toast.show(`❌ Errore nella creazione della lega: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('create', false)
    }
  }, [currentGroup, newLeagueName, currentYearIndex, applyGroupUpdate, toast])

  // Delete league
  const deleteLeague = useCallback(async (leagueId: string) => {
    if (!currentGroup) return

    setLoading('delete', true)

    try {
      const leagueToDelete = currentGroup.leagues.find(l => l.id === leagueId)
      
      const updatedGroup = {
        ...currentGroup,
        leagues: (currentGroup.leagues || []).filter(l => l.id !== leagueId)
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      toast.show(`✅ Lega "${leagueToDelete?.name}" eliminata con successo`, { variant: 'success' })
      
      // Select first league if available
      if (updatedGroup.leagues && updatedGroup.leagues.length > 0) {
        setSelectedLeagueId(updatedGroup.leagues[0].id)
      } else {
        setSelectedLeagueId(null)
      }
      
    } catch (err) {
      toast.show(`❌ Errore nell'eliminazione della lega: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('delete', false)
    }
  }, [currentGroup, applyGroupUpdate, toast])

  // Save league settings
  const saveSettings = useCallback(async () => {
    if (!currentGroup || !selectedLeague || !selectedYearlyLeague) return

    setLoading('save', true)

    try {
      const leagueToSave: League = {
        ...selectedLeague,
        years: upsertAnnualLeague(selectedLeague.years, selectedYearlyLeague)
      }

      const updatedGroup = {
        ...currentGroup,
        leagues: (currentGroup.leagues || []).map(l => 
          l.id === selectedLeague.id ? leagueToSave : l
        )
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      toast.show('✅ Settings salvati con successo', { variant: 'success' })
      
      // Clear pending changes after successful save
      setPendingLeagueChanges(null)
      
    } catch (err) {
      toast.show(`❌ Errore nel salvataggio: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('save', false)
    }
  }, [currentGroup, selectedLeague, selectedYearlyLeague, applyGroupUpdate, toast])

  // Create calendar and rank
  const createCalendarAndRank = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading('calendar', true)

    try {
      const result = await leagueManagerService.createCalendarAndRank(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex
      )

      if (result) {
        const calendar = await calendarService.getCalendarByComponents(
          currentGroup.id,
          selectedLeague.id,
          currentYearIndex
        )
        setCalendarOwners(calendar ? getCalendarOwners(calendar) : null)
        toast.show('✅ Calendario e classifica creati con successo', { variant: 'success' })
      } else {
        toast.show('❌ Errore nella creazione del calendario', { variant: 'error' })
      }
      
    } catch (err) {
      toast.show(`❌ Errore nella creazione: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('calendar', false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex, toast])

  // Randomize players for auctions
  const randomizePlayers = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading('randomize', true)

    try {
      const result = await leagueManagerService.randomizePlayersForAuctions(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex
      )

      if (result) {
        toast.show('✅ Giocatori randomizzati con successo', { variant: 'success' })
      } else {
        toast.show('❌ Errore nella randomizzazione', { variant: 'error' })
      }
      
    } catch (err) {
      toast.show(`❌ Errore nella randomizzazione: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('randomize', false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex, toast])

  // Recalculate all
  const recalculateAll = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading('recalcAll', true)

    try {
      const result = await leagueManagerService.recalculate(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex,
        38,
        false,
        false
      )

      if (result) {
        toast.show('✅ Ricalcolo completato con successo', { variant: 'success' })
      } else {
        toast.show('❌ Errore nel ricalcolo', { variant: 'error' })
      }
      
    } catch (err) {
      toast.show(`❌ Errore nel ricalcolo: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('recalcAll', false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex, toast])

  // Recalculate only last day
  const recalculateLastDay = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading('recalcLast', true)

    try {
      const result = await leagueManagerService.recalculate(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex,
        38,
        true,
        false
      )

      if (result) {
        toast.show('✅ Ultima giornata ricalcolata con successo', { variant: 'success' })
      } else {
        toast.show('❌ Errore nel ricalcolo', { variant: 'error' })
      }
      
    } catch (err) {
      toast.show(`❌ Errore nel ricalcolo: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('recalcLast', false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex, toast])

  // Recalculate specific day
  const recalculateSpecificDay = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading('recalcDay', true)

    try {
      const result = await leagueManagerService.recalculate(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex,
        dayToRecalculate,
        true,
        true
      )

      if (result) {
        toast.show(`✅ Giornata ${dayToRecalculate} ricalcolata con successo`, { variant: 'success' })
      } else {
        toast.show('❌ Errore nel ricalcolo', { variant: 'error' })
      }
      
    } catch (err) {
      toast.show(`❌ Errore nel ricalcolo: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('recalcDay', false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex, dayToRecalculate, toast])

  const updateSettings = useCallback((updatedSettings: LeagueSetting) => {
    if (!selectedLeague || !selectedYearlyLeague) return

    updateAnnualLeague({
      ...selectedYearlyLeague,
      settings: updatedSettings
    })
  }, [selectedLeague, selectedYearlyLeague, updateAnnualLeague])

  // Update yearly league type (not a setting, but a property of AnnualLeague)
  const updateYearlyLeagueType = useCallback((newType: LeagueType) => {
    if (!selectedLeague || !selectedYearlyLeague || !settings) return

    const updatedYearlyLeague: AnnualLeague = {
      ...selectedYearlyLeague,
      type: newType
    }

    updateAnnualLeague(updatedYearlyLeague)
  }, [selectedLeague, selectedYearlyLeague, settings, updateAnnualLeague])

  if (!currentGroup) {
    return (
      <View style={{ flex: 1 }}>
        <ScreenContent path="screens/group-league-admin.tsx" title="Gestione Lega">
          <YStack padding="$4" gap="$4" alignItems="center">
            <Text color="$color11" fontSize="$5">
              Nessun gruppo selezionato
            </Text>
          </YStack>
        </ScreenContent>
      </View>
    )
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/group-league-admin.tsx" title="Gestione Lega">
        <ScrollView>
          <YStack padding="$4" gap="$6" alignItems="center">
            <YStack width={containerWidth} gap="$4">
              
              {/* League Selectors */}
              <LeagueSelectorsWidget
                leagues={leagueOptions}
                selectedLeagueId={selectedLeagueId}
                onSelectLeague={handleLeagueSelection}
                years={availableYears}
                selectedYear={currentYearIndex}
                onSelectYear={handleYearSelection}
                formatYearLabel={formatSeasonFromYear}
                title="Selettori Lega e Anno"
                leagueHint="Seleziona la lega da gestire"
                yearHint="Seleziona l'anno della lega"
                showRoundSection={false}
              />

              {/* Create New League */}
              <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                <YStack gap="$3">
                  <Text fontSize="$5" fontWeight="700" color="$color11">
                    Crea Nuova Lega
                  </Text>
                  <Input
                    placeholder="Nome lega"
                    value={newLeagueName}
                    onChangeText={setNewLeagueName}
                    disabled={isAnyLoading}
                  />
                  <Button
                    icon={isLoading('create') ? Spinner : Package}
                    onPress={createLeague}
                    disabled={isAnyLoading || !newLeagueName.trim()}
                    theme="blue"
                  >
                    Crea Lega
                  </Button>
                </YStack>
              </Card>

              {/* League Settings */}
              {selectedLeague && settings && (
                <>
                  <Separator />
                  
                  <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                    <YStack gap="$4">
                      <XStack justifyContent="space-between" alignItems="center">
                        <Text fontSize="$5" fontWeight="700" color="$color11">
                          Impostazioni - {selectedLeague.name} ({formatSeasonFromYear(currentYearIndex)})
                        </Text>
                        <Button
                          icon={Trash2}
                          size="$3"
                          variant="outlined"
                          onPress={() => deleteLeague(selectedLeague.id)}
                          disabled={isAnyLoading}
                          theme="red"
                        >
                          Elimina Lega
                        </Button>
                      </XStack>

                      {!isYearPersisted && (
                        <Card padding="$3" backgroundColor="$orange3" borderColor="$orange7" borderWidth="$0.5">
                          <YStack gap="$3">
                            <Text color="$orange11" fontWeight="600">
                              {isYearInDraft
                                ? `I settings per il ${formatSeasonFromYear(currentYearIndex)} sono pronti ma non ancora salvati.`
                                : `Il ${formatSeasonFromYear(currentYearIndex)} non è ancora configurato per questa lega. Puoi modificare i default qui sotto oppure copiarli dall'anno precedente.`}
                            </Text>
                            {!previousYearlyLeague && (
                              <Text color="$orange10" fontSize="$2">
                                Nessun setting disponibile per il {formatSeasonFromYear(currentYearIndex - 1)}.
                              </Text>
                            )}
                          </YStack>
                        </Card>
                      )}

                      {/* Basic Settings */}
                      <YStack gap="$3">
                        <Text fontSize="$4" fontWeight="600" color="$color11">Impostazioni Generali</Text>
                        
                        <XStack gap="$3" alignItems="center">
                          <Label width={180}>Nome Lega</Label>
                          <Input
                            flex={1}
                            value={selectedLeague.name}
                            onChangeText={(val) => updateLeagueProperty('name', val)}
                            disabled={isAnyLoading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label width={180}>Is Main</Label>
                          <XStack flex={1}>
                            <Switch
                              size="$4"
                              backgroundColor={selectedLeague.isMain ? '$green9' : '$color6'}
                              borderColor={selectedLeague.isMain ? '$green10' : '$color8'}
                              checked={selectedLeague.isMain}
                              onCheckedChange={(checked) => updateLeagueProperty('isMain', checked)}
                              disabled={isAnyLoading}
                            >
                              <Switch.Thumb transition="quick" backgroundColor="$color12" />
                            </Switch>
                          </XStack>
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label width={180}>Tipo Lega (Default)</Label>
                          <YStack flex={1}>
                            <Select 
                              value={selectedLeague.type.toString()} 
                              onValueChange={(val) => updateLeagueProperty('type', Number(val) as LeagueType)}
                            >
                              <Select.Trigger iconAfter={ChevronDown} disabled={isAnyLoading}>
                                <Select.Value />
                              </Select.Trigger>
                              <Select.Content>
                                <Select.Viewport>
                                  <Select.Item index={0} value="0">
                                    <Select.ItemText>Nessuno</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={1} value="1">
                                    <Select.ItemText>Lega</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={2} value="2">
                                    <Select.ItemText>Coppa</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={3} value="3">
                                    <Select.ItemText>Nuova Coppa</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={4} value="4">
                                    <Select.ItemText>Super Lega</Select.ItemText>
                                  </Select.Item>
                                </Select.Viewport>
                              </Select.Content>
                            </Select>
                          </YStack>
                        </XStack>

                        <YStack gap="$2">
                          <Label>Basket Associati</Label>
                          <XStack gap="$2" flexWrap="wrap">
                            {currentGroup.baskets.map(basket => {
                              const isAssociated = selectedLeague.basketsId.includes(basket.id)
                              return (
                                <Button
                                  key={basket.id}
                                  size="$3"
                                  variant="outlined"
                                  borderColor={isAssociated ? '$blue8' : '$borderColor'}
                                  backgroundColor={isAssociated ? '$blue2' : 'transparent'}
                                  onPress={() => updateLeagueProperty(
                                    'basketsId',
                                    isAssociated
                                      ? selectedLeague.basketsId.filter(id => id !== basket.id)
                                      : [...selectedLeague.basketsId, basket.id]
                                  )}
                                  disabled={isAnyLoading}
                                >
                                  {basket.name}
                                </Button>
                              )
                            })}
                          </XStack>
                          {currentGroup.baskets.length === 0 && (
                            <Text color="$orange10" fontSize="$2">
                              Crea prima almeno un basket nella Gestione Basket del Gruppo.
                            </Text>
                          )}
                        </YStack>
                      </YStack>

                      <Separator />

                      {/* Year-Specific Settings */}
                      <YStack gap="$3">
                        <XStack gap="$3" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                          <Text fontSize="$4" fontWeight="600" color="$color11">
                            Impostazioni Anno {formatSeasonFromYear(currentYearIndex)}
                          </Text>
                          <XStack gap="$2" flexWrap="wrap">
                            <Button
                              size="$3"
                              variant="outlined"
                              onPress={initializeYearWithDefaults}
                              disabled={isAnyLoading}
                            >
                              Ripristina default
                            </Button>
                            <Button
                              size="$3"
                              icon={Copy}
                              onPress={copySettingsFromPreviousYear}
                              disabled={isAnyLoading || !previousYearlyLeague}
                              theme="blue"
                            >
                              Copia tutto dal {formatSeasonFromYear(currentYearIndex - 1)}
                            </Button>
                          </XStack>
                        </XStack>
                        <Text fontSize="$2" color="$color10">
                          La copia include tipo annuale, punteggi, bonus, crediti, modificatori voto e configurazioni avanzate.
                        </Text>
                        
                        <XStack gap="$3" alignItems="center">
                          <Label width={180}>Tipo Lega Anno</Label>
                          <YStack flex={1}>
                            <Select 
                              value={selectedYearlyLeague?.type?.toString() ?? selectedLeague.type.toString()} 
                              onValueChange={(val) => updateYearlyLeagueType(Number(val) as LeagueType)}
                            >
                              <Select.Trigger iconAfter={ChevronDown} disabled={isAnyLoading}>
                                <Select.Value placeholder={`Default: ${['Nessuno', 'Lega', 'Coppa', 'Nuova Coppa', 'Super Lega'][selectedLeague.type]}`} />
                              </Select.Trigger>
                              <Select.Content>
                                <Select.Viewport>
                                  <Select.Item index={0} value="0">
                                    <Select.ItemText>Nessuno</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={1} value="1">
                                    <Select.ItemText>Lega</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={2} value="2">
                                    <Select.ItemText>Coppa</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={3} value="3">
                                    <Select.ItemText>Nuova Coppa</Select.ItemText>
                                  </Select.Item>
                                  <Select.Item index={4} value="4">
                                    <Select.ItemText>Super Lega</Select.ItemText>
                                  </Select.Item>
                                </Select.Viewport>
                              </Select.Content>
                            </Select>
                          </YStack>
                        </XStack>

                        <LeagueSettingsEditor
                          key={`${selectedLeague.id}-${currentYearIndex}-${settingsRevision}`}
                          value={settings}
                          disabled={isAnyLoading}
                          resetKey={`${selectedLeague.id}-${currentYearIndex}-${settingsRevision}`}
                          onChange={updateSettings}
                          onValidityChange={setSettingsValid}
                        />
                      </YStack>

                      {/* Save Button */}
                      <Button
                        icon={isLoading('save') ? Spinner : Save}
                        onPress={saveSettings}
                        disabled={isAnyLoading || !settingsValid || !isLeagueSettingValid(settings)}
                        theme="green"
                      >
                        Salva Impostazioni
                      </Button>
                    </YStack>
                  </Card>

                  {/* Operations */}
                  <Separator />

                  <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                    <YStack gap="$4">
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        Operazioni Lega
                      </Text>

                      {!canCreateCalendar && (
                        <Text fontSize="$3" color="$orange10">
                          {!isYearPersisted
                            ? `Salva prima i settings del ${formatSeasonFromYear(currentYearIndex)}.`
                            : hasPendingChanges
                              ? 'Salva le modifiche prima di eseguire le operazioni.'
                              : effectiveLeagueType === LeagueType.Null
                                ? 'Seleziona un tipo lega valido e salva le impostazioni.'
                                : hasDuplicateOwners
                                  ? 'Lo stesso owner è presente in più basket associati. Seleziona basket senza owner duplicati.'
                                  : `Servono almeno ${minimumCalendarTeams} team nei basket associati per il ${formatSeasonFromYear(currentYearIndex)}.`}
                        </Text>
                      )}

                      <Button
                        icon={isLoading('calendar') ? Spinner : Calculator}
                        onPress={createCalendarAndRank}
                        disabled={isAnyLoading || !canCreateCalendar}
                        theme="blue"
                      >
                        Crea Calendario e Classifica
                      </Button>

                      <Button
                        icon={isLoading('randomize') ? Spinner : Shuffle}
                        onPress={randomizePlayers}
                        disabled={isAnyLoading || !canRunOperations}
                        theme="purple"
                      >
                        Randomizza Giocatori per Asta
                      </Button>

                      <Separator />

                      <Text fontSize="$4" fontWeight="600" color="$color11">Ricalcoli</Text>

                      <Button
                        icon={isLoading('recalcAll') ? Spinner : RefreshCw}
                        onPress={recalculateAll}
                        disabled={isAnyLoading || !canRecalculate}
                        theme="orange"
                      >
                        Ricalcola Tutto
                      </Button>

                      <Button
                        icon={isLoading('recalcLast') ? Spinner : RefreshCw}
                        onPress={recalculateLastDay}
                        disabled={isAnyLoading || !canRecalculate}
                        theme="orange"
                      >
                        Ricalcola Solo Ultima Giornata
                      </Button>

                      <XStack gap="$3" alignItems="center">
                        <Input
                          flex={1}
                          keyboardType="numeric"
                          placeholder="Giornata (1-38)"
                          value={String(dayToRecalculate)}
                          onChangeText={(val) => {
                            const num = Number(val)
                            if (num >= 1 && num <= 38) {
                              setDayToRecalculate(num)
                            }
                          }}
                          disabled={isAnyLoading || !canRecalculate}
                        />
                        <Button
                          flex={1}
                          icon={isLoading('recalcDay') ? Spinner : RefreshCw}
                          onPress={recalculateSpecificDay}
                          disabled={isAnyLoading || !canRecalculate || dayToRecalculate < 1 || dayToRecalculate > 38}
                          theme="orange"
                        >
                          Ricalcola Giornata
                        </Button>
                      </XStack>
                    </YStack>
                  </Card>
                </>
              )}
            </YStack>
          </YStack>
        </ScrollView>
      </ScreenContent>
    </View>
  )
}
