// Group League Admin Screen - Super Admin management of league settings and calculations
import { useCallback, useEffect, useMemo, useState } from 'react'
import { View, ScrollView } from 'react-native'
import { Button, Card, Input, Separator, Text, XStack, YStack, useMedia, Switch, Label } from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { useApp } from 'providers/AppProvider'
import { groupService } from 'services/groupService'
import { leagueManagerService } from 'services/leagueManagerService'
import type { League, LeagueSetting, VoteLeagueSetting, AnnualLeague, Role } from 'types/group'
import { LeagueType } from 'types/group'
import { Package, Calculator, Shuffle, RefreshCw, Save, Trash2 } from '@tamagui/lucide-icons'

export default function GroupLeagueAdminScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const { currentGroup, refreshGroups, selectedYear, setSelectedYear } = useApp()

  const [selectedLeagueId, setSelectedLeagueId] = useState<string | null>(null)
  const [newLeagueName, setNewLeagueName] = useState('')
  const [dayToRecalculate, setDayToRecalculate] = useState(38)
  
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Format year number to season string (e.g., 1 -> "2011/12", 14 -> "2024/25")
  const formatSeasonFromYear = (yearNumber: number): string => {
    const baseYear = 2011
    const startYear = baseYear + yearNumber - 1
    const endYear = startYear + 1
    return `${startYear}/${String(endYear).slice(-2)}`
  }

  // Available years for selector (year indices from 1 to current year index + 1)
  const availableYears = useMemo(() => {
    const baseYear = 2011
    const currentSolarYear = new Date().getFullYear()
    const maxYearIndex = currentSolarYear - baseYear + 2 // +2 to include next year
    const years: number[] = []
    for (let yearIndex = 1; yearIndex <= maxYearIndex; yearIndex++) {
      years.push(yearIndex)
    }
    return years
  }, [])

  // Selected year index (default to current year index)
  const currentSolarYear = new Date().getFullYear()
  const currentYearIndex = selectedYear ?? (currentSolarYear - 2011 + 1)

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
    return currentGroup.leagues.find(l => l.id === selectedLeagueId) || null
  }, [currentGroup, selectedLeagueId])

  // Selected yearly league
  const selectedYearlyLeague = useMemo(() => {
    if (!selectedLeague) return null
    return selectedLeague.years.find(y => y.year === currentYearIndex) || null
  }, [selectedLeague, currentYearIndex])

  // Settings for current year
  const settings = useMemo(() => {
    return selectedYearlyLeague?.settings || null
  }, [selectedYearlyLeague])

  // Initialize selected league
  useEffect(() => {
    if (currentGroup && currentGroup.leagues && currentGroup.leagues.length > 0 && !selectedLeagueId) {
      setSelectedLeagueId(currentGroup.leagues[0].id)
    }
  }, [currentGroup, selectedLeagueId])

  // Create new league
  const createLeague = useCallback(async () => {
    if (!currentGroup || !newLeagueName.trim()) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const newLeagueId = crypto.randomUUID()
      
      const newLeague: League = {
        id: newLeagueId,
        name: newLeagueName.trim(),
        isMain: false,
        type: LeagueType.League,
        years: [],
        basketsId: []
      }

      const updatedGroup = {
        ...currentGroup,
        leagues: [...(currentGroup.leagues || []), newLeague]
      }

      await groupService.updateGroup(currentGroup.id, updatedGroup)
      
      setSuccess(`Lega "${newLeagueName}" creata con successo`)
      setNewLeagueName('')
      setSelectedLeagueId(newLeagueId)
      
      await refreshGroups()
      
    } catch (err) {
      setError(`Errore nella creazione della lega: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, newLeagueName, refreshGroups])

  // Delete league
  const deleteLeague = useCallback(async (leagueId: string) => {
    if (!currentGroup) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const leagueToDelete = currentGroup.leagues.find(l => l.id === leagueId)
      
      const updatedGroup = {
        ...currentGroup,
        leagues: (currentGroup.leagues || []).filter(l => l.id !== leagueId)
      }

      await groupService.updateGroup(currentGroup.id, updatedGroup)
      
      setSuccess(`Lega "${leagueToDelete?.name}" eliminata con successo`)
      
      // Select first league if available
      if (updatedGroup.leagues && updatedGroup.leagues.length > 0) {
        setSelectedLeagueId(updatedGroup.leagues[0].id)
      } else {
        setSelectedLeagueId(null)
      }
      
      await refreshGroups()
      
    } catch (err) {
      setError(`Errore nell'eliminazione della lega: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, refreshGroups])

  // Save league settings
  const saveSettings = useCallback(async () => {
    if (!currentGroup || !selectedLeague || !settings) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const updatedGroup = {
        ...currentGroup,
        leagues: (currentGroup.leagues || []).map(l => 
          l.id === selectedLeague.id ? selectedLeague : l
        )
      }

      await groupService.updateGroup(currentGroup.id, updatedGroup)
      
      setSuccess('Settings salvati con successo')
      
      await refreshGroups()
      
    } catch (err) {
      setError(`Errore nel salvataggio: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedLeague, settings, refreshGroups])

  // Create calendar and rank
  const createCalendarAndRank = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const result = await leagueManagerService.createCalendarAndRank(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex
      )

      if (result) {
        setSuccess('Calendario e classifica creati con successo')
      } else {
        setError('Errore nella creazione del calendario')
      }
      
    } catch (err) {
      setError(`Errore nella creazione: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex])

  // Randomize players for auctions
  const randomizePlayers = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const result = await leagueManagerService.randomizePlayersForAuctions(
        currentGroup.id,
        selectedLeague.id,
        currentYearIndex
      )

      if (result) {
        setSuccess('Giocatori randomizzati con successo')
      } else {
        setError('Errore nella randomizzazione')
      }
      
    } catch (err) {
      setError(`Errore nella randomizzazione: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex])

  // Recalculate all
  const recalculateAll = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading(true)
    setError(null)
    setSuccess(null)

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
        setSuccess('Ricalcolo completato con successo')
      } else {
        setError('Errore nel ricalcolo')
      }
      
    } catch (err) {
      setError(`Errore nel ricalcolo: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex])

  // Recalculate only last day
  const recalculateLastDay = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading(true)
    setError(null)
    setSuccess(null)

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
        setSuccess('Ultima giornata ricalcolata con successo')
      } else {
        setError('Errore nel ricalcolo')
      }
      
    } catch (err) {
      setError(`Errore nel ricalcolo: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex])

  // Recalculate specific day
  const recalculateSpecificDay = useCallback(async () => {
    if (!currentGroup || !selectedLeague) return

    setLoading(true)
    setError(null)
    setSuccess(null)

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
        setSuccess(`Giornata ${dayToRecalculate} ricalcolata con successo`)
      } else {
        setError('Errore nel ricalcolo')
      }
      
    } catch (err) {
      setError(`Errore nel ricalcolo: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedLeague, currentYearIndex, dayToRecalculate])

  // Update setting value (generic)
  const updateSetting = useCallback(<K extends keyof LeagueSetting>(
    key: K,
    value: LeagueSetting[K]
  ) => {
    if (!selectedLeague || !selectedYearlyLeague || !settings) return

    const updatedSettings: LeagueSetting = {
      ...settings,
      [key]: value
    }

    const updatedYearlyLeague: AnnualLeague = {
      ...selectedYearlyLeague,
      settings: updatedSettings
    }

    const updatedLeague: League = {
      ...selectedLeague,
      years: selectedLeague.years.map(y =>
        y.year === currentYearIndex ? updatedYearlyLeague : y
      )
    }

    // If year doesn't exist, create it
    if (!selectedLeague.years.some(y => y.year === currentYearIndex)) {
      updatedLeague.years = [...selectedLeague.years, updatedYearlyLeague]
    }

    // Update in local state (will be saved on "Save" button)
    setSelectedLeagueId(updatedLeague.id)
  }, [selectedLeague, selectedYearlyLeague, settings, currentYearIndex])

  // Update vote setting for a role
  const updateVoteSetting = useCallback((
    role: Role,
    key: keyof VoteLeagueSetting,
    value: number
  ) => {
    if (!selectedLeague || !selectedYearlyLeague || !settings) return

    const currentVoteSetting = settings.votes[role] || {
      goal: 0,
      penalty: 0,
      sufferedGoal: 0,
      stoppedPenalty: 0,
      wrongedPenalty: 0,
      ownGoal: 0,
      assist: 0,
      yellowCard: 0,
      redCard: 0,
      injury: 0,
      manOfTheMatch: 0
    }

    const updatedVoteSetting: VoteLeagueSetting = {
      ...currentVoteSetting,
      [key]: value
    }

    const updatedSettings: LeagueSetting = {
      ...settings,
      votes: {
        ...settings.votes,
        [role]: updatedVoteSetting
      }
    }

    const updatedYearlyLeague: AnnualLeague = {
      ...selectedYearlyLeague,
      settings: updatedSettings
    }

    const updatedLeague: League = {
      ...selectedLeague,
      years: selectedLeague.years.map(y =>
        y.year === currentYearIndex ? updatedYearlyLeague : y
      )
    }

    if (!selectedLeague.years.some(y => y.year === currentYearIndex)) {
      updatedLeague.years = [...selectedLeague.years, updatedYearlyLeague]
    }

    setSelectedLeagueId(updatedLeague.id)
  }, [selectedLeague, selectedYearlyLeague, settings, currentYearIndex])

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
              
              {/* Messages */}
              {error && (
                <Card padding="$3" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
                  <Text color="$red11" fontSize="$3">{error}</Text>
                </Card>
              )}
              {success && (
                <Card padding="$3" backgroundColor="$green2" borderColor="$green8" borderWidth="$0.5">
                  <Text color="$green11" fontSize="$3">{success}</Text>
                </Card>
              )}

              {/* League Selectors */}
              <LeagueSelectorsWidget
                leagues={leagueOptions}
                selectedLeagueId={selectedLeagueId}
                onSelectLeague={setSelectedLeagueId}
                years={availableYears}
                selectedYear={currentYearIndex}
                onSelectYear={setSelectedYear}
                formatYearLabel={formatSeasonFromYear}
                title="Selettori Lega e Anno"
                leagueHint="Seleziona la lega da gestire"
                yearHint="Seleziona l'anno della lega (2011/12 - 2026/27)"
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
                    disabled={loading}
                  />
                  <Button
                    icon={Package}
                    onPress={createLeague}
                    disabled={loading || !newLeagueName.trim()}
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
                          disabled={loading}
                          theme="red"
                        >
                          Elimina Lega
                        </Button>
                      </XStack>

                      {/* Basic Settings */}
                      <YStack gap="$3">
                        <Text fontSize="$4" fontWeight="600" color="$color11">Impostazioni Generali</Text>
                        
                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Nome Lega</Label>
                          <Input
                            flex={2}
                            value={selectedLeague.name}
                            onChangeText={(val) => {
                              if (selectedLeague) {
                                const updated = { ...selectedLeague, name: val }
                                // Update state
                              }
                            }}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Is Main</Label>
                          <Switch
                            checked={selectedLeague.isMain}
                            onCheckedChange={(checked) => {
                              if (selectedLeague) {
                                const updated = { ...selectedLeague, isMain: checked }
                                // Update state
                              }
                            }}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Starting Money</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.startingMoney)}
                            onChangeText={(val) => updateSetting('startingMoney', Number(val) || 500)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Delayed Day</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.delayedDay)}
                            onChangeText={(val) => updateSetting('delayedDay', Number(val) || 0)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Cancelled Day</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.cancelledDay)}
                            onChangeText={(val) => updateSetting('cancelledDay', Number(val) || 0)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Point First Goal</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.pointForFirstGoal)}
                            onChangeText={(val) => updateSetting('pointForFirstGoal', Number(val) || 66)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Point Next Goal</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.pointForNextGoal)}
                            onChangeText={(val) => updateSetting('pointForNextGoal', Number(val) || 6)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Point Own Goal</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.pointForOwnGoal)}
                            onChangeText={(val) => updateSetting('pointForOwnGoal', Number(val) || 0)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Diff Point Own Goal</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.differencePointForOwnGoal)}
                            onChangeText={(val) => updateSetting('differencePointForOwnGoal', Number(val) || 0)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Point In Home</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.pointInHome)}
                            onChangeText={(val) => updateSetting('pointInHome', Number(val) || 0)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Point For Victory</Label>
                          <Input
                            flex={2}
                            keyboardType="numeric"
                            value={String(settings.pointForVictory)}
                            onChangeText={(val) => updateSetting('pointForVictory', Number(val) || 3)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Random Auction</Label>
                          <Switch
                            checked={settings.randomAuction}
                            onCheckedChange={(checked) => updateSetting('randomAuction', checked)}
                            disabled={loading}
                          />
                        </XStack>

                        <XStack gap="$3" alignItems="center">
                          <Label flex={1}>Rank With Value Points</Label>
                          <Switch
                            checked={settings.rankWithValuePoints}
                            onCheckedChange={(checked) => updateSetting('rankWithValuePoints', checked)}
                            disabled={loading}
                          />
                        </XStack>
                      </YStack>

                      {/* Save Button */}
                      <Button
                        icon={Save}
                        onPress={saveSettings}
                        disabled={loading}
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

                      <Button
                        icon={Calculator}
                        onPress={createCalendarAndRank}
                        disabled={loading}
                        theme="blue"
                      >
                        Crea Calendario e Classifica
                      </Button>

                      <Button
                        icon={Shuffle}
                        onPress={randomizePlayers}
                        disabled={loading}
                        theme="purple"
                      >
                        Randomizza Giocatori per Asta
                      </Button>

                      <Separator />

                      <Text fontSize="$4" fontWeight="600" color="$color11">Ricalcoli</Text>

                      <Button
                        icon={RefreshCw}
                        onPress={recalculateAll}
                        disabled={loading}
                        theme="orange"
                      >
                        Ricalcola Tutto
                      </Button>

                      <Button
                        icon={RefreshCw}
                        onPress={recalculateLastDay}
                        disabled={loading}
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
                          disabled={loading}
                        />
                        <Button
                          flex={1}
                          icon={RefreshCw}
                          onPress={recalculateSpecificDay}
                          disabled={loading || dayToRecalculate < 1 || dayToRecalculate > 38}
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
