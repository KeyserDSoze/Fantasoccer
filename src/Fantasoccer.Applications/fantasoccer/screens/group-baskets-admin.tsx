import React, { useState, useCallback, useMemo, useEffect } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { 
  YStack, 
  XStack, 
  Text, 
  H3,
  H4,
  Card, 
  Spinner, 
  Separator,
  Button,
  Input,
  useMedia
} from 'tamagui'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { useApp } from 'providers/AppProvider'
import { groupService } from 'services/groupService'
import { appIdentityService } from 'services/appIdentityService'
import { Basket, AnnualTeam } from 'types/group'
import { IdentityRole } from 'types/identity'
import { 
  Package,
  Plus,
  Trash2,
  Copy,
  Users,
  UserPlus,
  AlertCircle,
  CheckCircle
} from '@tamagui/lucide-icons-2'
import { formatSeasonFromYear, getAvailableSeasonYears } from 'utils/season'
import { copyMissingTeams } from 'utils/groupAdmin'

const EMAIL_PATTERN = /^[\w-\.]+@([\w-]+\.)+[\w-]{2,6}$/

export default function GroupBasketsAdminScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const { currentGroup, applyGroupUpdate, selectedYear, setSelectedYear } = useApp()

  const [selectedBasketId, setSelectedBasketId] = useState<string | null>(null)
  const [newBasketName, setNewBasketName] = useState('')
  const [newTeamName, setNewTeamName] = useState('')
  const [newTeamOwner, setNewTeamOwner] = useState<string | null>(null)
  const [coOwnerEmailByTeam, setCoOwnerEmailByTeam] = useState<Record<string, string>>({})
  
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Available years for selector
  const availableYears = useMemo(() => getAvailableSeasonYears(), [])

  // Selected year index (default to current year index)
  const currentYearIndex = selectedYear ?? availableYears[availableYears.length - 2] ?? 1

  // Selected basket
  const selectedBasket = useMemo(() => {
    if (!currentGroup || !selectedBasketId) return null
    return currentGroup.baskets.find(b => b.id === selectedBasketId) || null
  }, [currentGroup, selectedBasketId])

  // Current year basket data
  const yearlyBasket = useMemo(() => {
    if (!selectedBasket) return null
    return selectedBasket.years.find(y => y.year === currentYearIndex) || null
  }, [selectedBasket, currentYearIndex])

  // Teams for current year
  const teams = useMemo(() => {
    return yearlyBasket?.teams || []
  }, [yearlyBasket])

  // Initialize selected basket
  useEffect(() => {
    if (currentGroup && currentGroup.baskets && currentGroup.baskets.length > 0 && !selectedBasketId) {
      setSelectedBasketId(currentGroup.baskets[0].id)
    }
  }, [currentGroup, selectedBasketId])

  // Initialize new team owner
  useEffect(() => {
    if (currentGroup && currentGroup.users && currentGroup.users.length > 0 && !newTeamOwner) {
      setNewTeamOwner(currentGroup.users[0].email)
    }
  }, [currentGroup, newTeamOwner])

  // Create new basket
  const createBasket = useCallback(async () => {
    if (!currentGroup || !newBasketName.trim()) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const newBasketId = crypto.randomUUID()
      
      const newBasket: Basket = {
        id: newBasketId,
        name: newBasketName.trim(),
        years: [
          {
            year: currentYearIndex,
            teams: []
          }
        ]
      }

      const updatedGroup = {
        ...currentGroup,
        baskets: [...(currentGroup.baskets || []), newBasket]
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      setSuccess(`Basket "${newBasketName}" creato con successo`)
      setNewBasketName('')
      setSelectedBasketId(newBasketId)
      
    } catch (err) {
      setError(`Errore nella creazione del basket: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, newBasketName, currentYearIndex, applyGroupUpdate])

  // Delete basket
  const deleteBasket = useCallback(async (basketId: string) => {
    if (!currentGroup) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const basketToDelete = currentGroup.baskets.find(b => b.id === basketId)
      
      const updatedGroup = {
        ...currentGroup,
        baskets: (currentGroup.baskets || []).filter(b => b.id !== basketId)
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      setSuccess(`Basket "${basketToDelete?.name}" eliminato con successo`)
      
      // Select first basket if available
      if (updatedGroup.baskets && updatedGroup.baskets.length > 0) {
        setSelectedBasketId(updatedGroup.baskets[0].id)
      } else {
        setSelectedBasketId(null)
      }
      
    } catch (err) {
      setError(`Errore nell'eliminazione del basket: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, applyGroupUpdate])

  // Add team to current year
  const addTeam = useCallback(async () => {
    if (!currentGroup || !selectedBasket || !newTeamOwner) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      // Check if owner already has a team this year
      if (teams.some(t => t.owner === newTeamOwner)) {
        setError('Questo utente ha già un team in questo anno')
        return
      }

      const teamName = newTeamName.trim() || `${newTeamOwner} team`
      
      const newTeam: AnnualTeam = {
        name: teamName,
        owner: newTeamOwner,
        additionalOwners: []
      }

      // Update basket
      const updatedBasket: Basket = {
        ...selectedBasket,
        years: selectedBasket.years.map(y => {
          if (y.year === currentYearIndex) {
            return {
              ...y,
              teams: [...y.teams, newTeam]
            }
          }
          return y
        })
      }

      // If year doesn't exist, create it
      if (!selectedBasket.years.some(y => y.year === currentYearIndex)) {
        updatedBasket.years.push({
          year: currentYearIndex,
          teams: [newTeam]
        })
      }

      // Update group
      const updatedGroup = {
        ...currentGroup,
        baskets: (currentGroup.baskets || []).map(b => 
          b.id === selectedBasket.id ? updatedBasket : b
        )
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      setSuccess(`Team "${teamName}" aggiunto con successo`)
      setNewTeamName('')
      
    } catch (err) {
      setError(`Errore nell'aggiunta del team: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedBasket, newTeamOwner, newTeamName, teams, currentYearIndex, applyGroupUpdate])

  // Delete team
  const deleteTeam = useCallback(async (teamOwner: string) => {
    if (!currentGroup || !selectedBasket) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const updatedBasket: Basket = {
        ...selectedBasket,
        years: selectedBasket.years.map(y => {
          if (y.year === currentYearIndex) {
            return {
              ...y,
              teams: y.teams.filter(t => t.owner !== teamOwner)
            }
          }
          return y
        })
      }

      const updatedGroup = {
        ...currentGroup,
        baskets: (currentGroup.baskets || []).map(b => 
          b.id === selectedBasket.id ? updatedBasket : b
        )
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      setSuccess('Team eliminato con successo')
      
    } catch (err) {
      setError(`Errore nell'eliminazione del team: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedBasket, currentYearIndex, applyGroupUpdate])

  // Change team owner
  const changeTeamOwner = useCallback(async (currentOwner: string, newOwner: string) => {
    if (!currentGroup || !selectedBasket || newOwner === currentOwner) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      // Check if new owner already has a team
      if (teams.some(t => t.owner === newOwner)) {
        setError('Il nuovo owner ha già un team in questo anno')
        return
      }

      const updatedBasket: Basket = {
        ...selectedBasket,
        years: selectedBasket.years.map(y => {
          if (y.year === currentYearIndex) {
            return {
              ...y,
              teams: y.teams.map(t => 
                t.owner === currentOwner 
                  ? { ...t, owner: newOwner }
                  : t
              )
            }
          }
          return y
        })
      }

      const updatedGroup = {
        ...currentGroup,
        baskets: (currentGroup.baskets || []).map(b => 
          b.id === selectedBasket.id ? updatedBasket : b
        )
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      setSuccess('Owner aggiornato con successo')
      
    } catch (err) {
      setError(`Errore nell'aggiornamento dell'owner: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedBasket, teams, currentYearIndex, applyGroupUpdate])

  // Toggle co-owner
  const toggleCoOwner = useCallback(async (teamOwner: string, coOwner: string) => {
    if (!currentGroup || !selectedBasket) return

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const updatedBasket: Basket = {
        ...selectedBasket,
        years: selectedBasket.years.map(y => {
          if (y.year === currentYearIndex) {
            return {
              ...y,
              teams: y.teams.map(t => {
                if (t.owner === teamOwner) {
                  const additionalOwners = t.additionalOwners || []
                  const hasCoOwner = additionalOwners.includes(coOwner)
                  return {
                    ...t,
                    additionalOwners: hasCoOwner
                      ? additionalOwners.filter(o => o !== coOwner)
                      : [...additionalOwners, coOwner]
                  }
                }
                return t
              })
            }
          }
          return y
        })
      }

      const updatedGroup = {
        ...currentGroup,
        baskets: (currentGroup.baskets || []).map(b => 
          b.id === selectedBasket.id ? updatedBasket : b
        )
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      
      setSuccess('Co-owner aggiornato con successo')
      
    } catch (err) {
      setError(`Errore nell'aggiornamento dei co-owner: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedBasket, currentYearIndex, applyGroupUpdate])

  const addCoOwnerByEmail = useCallback(async (teamOwner: string) => {
    if (!currentGroup || !selectedBasket) return

    const coOwner = coOwnerEmailByTeam[teamOwner]?.trim().toLowerCase()
    if (!coOwner || !EMAIL_PATTERN.test(coOwner)) {
      setError('Inserisci una email valida per il co-owner')
      return
    }
    if (coOwner === teamOwner.toLowerCase()) {
      setError('Il co-owner non può coincidere con l\'owner principale')
      return
    }

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      let userIdentity = await appIdentityService.getAppIdentity(coOwner)
      if (!userIdentity) {
        userIdentity = {
          email: coOwner,
          username: coOwner,
          state: { raw: {} },
          groups: [{
            id: currentGroup.id,
            name: currentGroup.name,
            role: IdentityRole.Reader,
            raw: { i: currentGroup.id, n: currentGroup.name, r: IdentityRole.Reader }
          }],
          raw: {
            e: coOwner,
            u: coOwner,
            s: {},
            g: [{ i: currentGroup.id, n: currentGroup.name, r: IdentityRole.Reader }]
          }
        }
        await appIdentityService.insertAppIdentity(userIdentity)
      } else {
        const groupIndex = userIdentity.groups.findIndex(g => g.id === currentGroup.id)
        if (groupIndex === -1) {
          userIdentity.groups.push({
            id: currentGroup.id,
            name: currentGroup.name,
            role: IdentityRole.Reader,
            raw: { i: currentGroup.id, n: currentGroup.name, r: IdentityRole.Reader }
          })
        } else if (userIdentity.groups[groupIndex].role === IdentityRole.None) {
          userIdentity.groups[groupIndex].role = IdentityRole.Reader
          userIdentity.groups[groupIndex].raw.r = IdentityRole.Reader
        }
        await appIdentityService.updateAppIdentity(userIdentity.email, userIdentity)
      }

      const users = currentGroup.users?.some(user => user.email.toLowerCase() === coOwner)
        ? currentGroup.users.map(user => user.email.toLowerCase() === coOwner && user.role === IdentityRole.None
          ? { ...user, role: IdentityRole.Reader }
          : user)
        : [...(currentGroup.users || []), { email: coOwner, username: userIdentity.username || coOwner, role: IdentityRole.Reader }]

      const updatedBasket: Basket = {
        ...selectedBasket,
        years: selectedBasket.years.map(y => y.year === currentYearIndex
          ? {
              ...y,
              teams: y.teams.map(t => {
                if (t.owner !== teamOwner) return t
                const additionalOwners = t.additionalOwners || []
                return additionalOwners.some(owner => owner.toLowerCase() === coOwner)
                  ? t
                  : { ...t, additionalOwners: [...additionalOwners, coOwner] }
              })
            }
          : y)
      }

      const updatedGroup = {
        ...currentGroup,
        users,
        baskets: (currentGroup.baskets || []).map(b => b.id === selectedBasket.id ? updatedBasket : b)
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)
      setCoOwnerEmailByTeam(prev => ({ ...prev, [teamOwner]: '' }))
      setSuccess(`Co-owner ${coOwner} aggiunto con successo`)
    } catch (err) {
      setError(`Errore nell'aggiunta del co-owner: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedBasket, coOwnerEmailByTeam, currentYearIndex, applyGroupUpdate])

  // Copy teams from previous year
  const copyFromPreviousYear = useCallback(async () => {
    if (!currentGroup || !selectedBasket) return

    const previousYear = currentYearIndex - 1
    const previousYearBasket = selectedBasket.years.find(y => y.year === previousYear)

    if (!previousYearBasket || previousYearBasket.teams.length === 0) {
      setError(`Nessun team trovato per il ${formatSeasonFromYear(previousYear)}`)
      return
    }

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      // Get existing teams for current year
      const existingTeams = yearlyBasket?.teams || []
      
      // Copy teams that don't already exist (by owner)
      const teamsToCopy = copyMissingTeams(previousYearBasket.teams, existingTeams)

      if (teamsToCopy.length === 0) {
        setError('Tutti i team dell\'anno precedente sono già presenti')
        return
      }

      const updatedBasket: Basket = {
        ...selectedBasket,
        years: selectedBasket.years.map(y => {
          if (y.year === currentYearIndex) {
            return {
              ...y,
              teams: [...y.teams, ...teamsToCopy]
            }
          }
          return y
        })
      }

      // If current year doesn't exist, create it
      if (!selectedBasket.years.some(y => y.year === currentYearIndex)) {
        updatedBasket.years.push({
          year: currentYearIndex,
          teams: teamsToCopy
        })
      }

      const updatedGroup = {
        ...currentGroup,
        baskets: (currentGroup.baskets || []).map(b => 
          b.id === selectedBasket.id ? updatedBasket : b
        )
      }

      const savedGroup = await groupService.updateGroup(currentGroup.id, updatedGroup)
      applyGroupUpdate(savedGroup)

      setSuccess(`${teamsToCopy.length} team copiati dal ${formatSeasonFromYear(previousYear)}`)
      
    } catch (err) {
      setError(`Errore nella copia dei team: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [currentGroup, selectedBasket, currentYearIndex, yearlyBasket, applyGroupUpdate])

  if (!currentGroup) {
    return (
      <View style={{ flex: 1 }}>
        <ScreenContent path="screens/group-baskets-admin.tsx" title="Gestione Basket">
          <YStack padding="$4" alignItems="center" gap="$3">
            <AlertCircle size="$4" color="$orange10" />
            <Text fontSize="$5" color="$color11">
              Nessun gruppo selezionato
            </Text>
            <Text fontSize="$3" color="$color10" textAlign="center">
              Seleziona un gruppo per gestire i basket
            </Text>
          </YStack>
        </ScreenContent>
      </View>
    )
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/group-baskets-admin.tsx" title="Gestione Basket">
        <YStack
          width={containerWidth}
          maxWidth={1200}
          gap="$4"
          alignItems="stretch"
          alignSelf="center"
        >
          {/* Header */}
          <Card
            padding="$4"
            backgroundColor="$purple3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$purple7"
          >
            <XStack alignItems="center" gap="$3" marginBottom="$2">
              <Package size="$2" color="$purple10" />
              <H3 color="$purple11">Gestione Basket del Gruppo</H3>
            </XStack>
            <Text fontSize="$3" color="$purple10">
              Gestisci i contenitori di squadre per il gruppo <Text fontWeight="bold">{currentGroup.name}</Text>.
            </Text>
          </Card>

          {/* Year Selector */}
          <LeagueSelectorsWidget
            leagues={[]}
            selectedLeagueId={null}
            years={availableYears}
            selectedYear={currentYearIndex}
            onSelectYear={setSelectedYear}
            formatYearLabel={formatSeasonFromYear}
            yearHint={`Seleziona l'anno del basket (${formatSeasonFromYear(availableYears[0])} - ${formatSeasonFromYear(availableYears[availableYears.length - 1])})`}
            showLeagueSection={false}
            showRoundSection={false}
            showYearSection={true}
            title="Selettore Anno Basket"
          />

          {/* Success Message */}
          {success && (
            <Card padding="$3" backgroundColor="$green3" borderColor="$green7" borderWidth="$0.5">
              <XStack alignItems="center" gap="$2">
                <CheckCircle size="$1" color="$green11" />
                <Text color="$green11" fontWeight="600">{success}</Text>
              </XStack>
            </Card>
          )}

          {/* Error Message */}
          {error && (
            <Card padding="$3" backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5">
              <XStack alignItems="center" gap="$2">
                <AlertCircle size="$1" color="$red11" />
                <Text color="$red11" fontWeight="600">{error}</Text>
              </XStack>
            </Card>
          )}

          {/* Basket Selection */}
          <Card padding="$4" backgroundColor="$color2" borderRadius="$4">
            <H4 marginBottom="$3" color="$color12">
              <Package size="$1" style={{ marginRight: 8 }} />
              Seleziona Basket
            </H4>

            {currentGroup.baskets && currentGroup.baskets.length > 0 ? (
              <XStack gap="$2" flexWrap="wrap" marginBottom="$3">
                {currentGroup.baskets.map((basket) => (
                  <Button
                    key={basket.id}
                    size="$3"
                    variant="outlined"
                    borderColor={basket.id === selectedBasketId ? '$purple8' : '$borderColor'}
                    backgroundColor={basket.id === selectedBasketId ? '$purple2' : 'transparent'}
                    onPress={() => setSelectedBasketId(basket.id)}
                  >
                    <XStack alignItems="center" gap="$2">
                      <Text color={basket.id === selectedBasketId ? '$purple11' : '$color11'} fontWeight="600">
                        {basket.name}
                      </Text>
                      {basket.id === selectedBasketId && (
                        <Button
                          size="$1"
                          chromeless
                          icon={<Trash2 size="$0.75" color="$red10" />}
                          onPress={(e) => {
                            e.stopPropagation()
                            deleteBasket(basket.id)
                          }}
                          disabled={loading}
                        />
                      )}
                    </XStack>
                  </Button>
                ))}
              </XStack>
            ) : (
              <Text fontSize="$3" color="$color10" marginBottom="$3">
                Nessun basket disponibile
              </Text>
            )}

            <Separator marginVertical="$3" />

            <YStack gap="$2">
              <Text fontSize="$3" color="$color11" fontWeight="600">
                Crea Nuovo Basket
              </Text>
              <Input
                placeholder="Nome basket (es. Basket Principale)"
                value={newBasketName}
                onChangeText={setNewBasketName}
                size="$3"
              />
              <Button
                size="$3"
                backgroundColor="$purple9"
                onPress={createBasket}
                disabled={!newBasketName.trim() || loading}
                icon={loading ? <Spinner size="small" /> : <Plus size="$1" />}
              >
                {loading ? 'Creazione...' : 'Crea Basket'}
              </Button>
            </YStack>
          </Card>

          {/* Teams Management */}
          {selectedBasket && (
            <Card padding="$4" backgroundColor="$color2" borderRadius="$4">
              <XStack alignItems="center" justifyContent="space-between" marginBottom="$3">
                <H4 color="$color12">
                  <Users size="$1" style={{ marginRight: 8 }} />
                  Teams - {selectedBasket.name} ({formatSeasonFromYear(currentYearIndex)})
                </H4>
                
                <Button
                  size="$2"
                  variant="outlined"
                  borderColor="$blue8"
                  onPress={copyFromPreviousYear}
                  disabled={loading}
                  icon={<Copy size="$0.75" />}
                >
                  Copia dall&apos;anno scorso
                </Button>
              </XStack>

              {/* Teams List */}
              {teams.length > 0 && (
                <YStack gap="$2" marginBottom="$4">
                  {teams.map((team) => (
                    <Card
                      key={team.owner}
                      padding="$3"
                      backgroundColor="$color1"
                      borderRadius="$3"
                      borderWidth="$0.5"
                      borderColor="$borderColor"
                    >
                      <YStack gap="$3">
                        {/* Team Info */}
                        <XStack alignItems="center" gap="$3" justifyContent="space-between">
                          <YStack flex={1} gap="$1">
                            <Text fontSize="$4" fontWeight="600" color="$color12">
                              {team.name}
                            </Text>
                            <Text fontSize="$2" color="$color11">
                              Owner: {team.owner}
                            </Text>
                            {team.additionalOwners && team.additionalOwners.length > 0 && (
                              <Text fontSize="$2" color="$color10">
                                Co-owners: {team.additionalOwners.join(', ')}
                              </Text>
                            )}
                          </YStack>

                          <Button
                            size="$2"
                            variant="outlined"
                            borderColor="$red8"
                            color="$red11"
                            onPress={() => deleteTeam(team.owner)}
                            disabled={loading}
                            icon={<Trash2 size="$0.75" />}
                          >
                            Elimina
                          </Button>
                        </XStack>

                        <Separator />

                        {/* Change Owner */}
                        <YStack gap="$2">
                          <Text fontSize="$2" color="$color11" fontWeight="600">
                            Cambia Owner:
                          </Text>
                          <XStack gap="$2" flexWrap="wrap">
                            {(currentGroup.users || [])
                              .filter(u => u.email !== team.owner)
                              .map((user) => (
                                <Button
                                  key={user.email}
                                  size="$2"
                                  variant="outlined"
                                  onPress={() => changeTeamOwner(team.owner, user.email)}
                                  disabled={loading || teams.some(t => t.owner === user.email)}
                                >
                                  {user.username}
                                </Button>
                              ))}
                          </XStack>
                        </YStack>

                        {/* Co-Owners */}
                        <YStack gap="$2">
                          <Text fontSize="$2" color="$color11" fontWeight="600">
                            Co-Owners (click per aggiungere/rimuovere):
                          </Text>
                          <XStack gap="$2" flexWrap="wrap">
                            {(currentGroup.users || [])
                              .filter(u => u.email !== team.owner)
                              .map((user) => {
                                const isCoOwner = team.additionalOwners?.includes(user.email) ?? false
                                return (
                                  <Button
                                    key={user.email}
                                    size="$2"
                                    variant="outlined"
                                    borderColor={isCoOwner ? '$green8' : '$borderColor'}
                                    backgroundColor={isCoOwner ? '$green2' : 'transparent'}
                                    color={isCoOwner ? '$green11' : '$color11'}
                                    onPress={() => toggleCoOwner(team.owner, user.email)}
                                    disabled={loading}
                                  >
                                    {user.username} {isCoOwner ? '✓' : ''}
                                  </Button>
                                )
                              })}
                          </XStack>
                          <XStack gap="$2" flexWrap="wrap" alignItems="center">
                            <Input
                              flex={1}
                              minWidth={240}
                              placeholder="Email nuovo co-owner"
                              value={coOwnerEmailByTeam[team.owner] || ''}
                              onChangeText={(value) => setCoOwnerEmailByTeam(prev => ({ ...prev, [team.owner]: value }))}
                              keyboardType="email-address"
                              autoCapitalize="none"
                              autoCorrect={false}
                              borderColor={coOwnerEmailByTeam[team.owner] && !EMAIL_PATTERN.test(coOwnerEmailByTeam[team.owner].trim()) ? '$red8' : '$borderColor'}
                            />
                            <Button
                              size="$2"
                              backgroundColor="$green9"
                              onPress={() => addCoOwnerByEmail(team.owner)}
                              disabled={loading || !EMAIL_PATTERN.test((coOwnerEmailByTeam[team.owner] || '').trim())}
                              icon={loading ? <Spinner size="small" /> : <UserPlus size="$0.75" />}
                            >
                              Aggiungi co-owner
                            </Button>
                          </XStack>
                        </YStack>
                      </YStack>
                    </Card>
                  ))}
                </YStack>
              )}

              {/* Add Team */}
              <Separator marginVertical="$3" />
              
              <YStack gap="$3">
                <H4 color="$color12">
                  <UserPlus size="$1" style={{ marginRight: 8 }} />
                  Aggiungi Nuovo Team
                </H4>

                <Input
                  placeholder="Nome team (opzionale, verrà generato se vuoto)"
                  value={newTeamName}
                  onChangeText={setNewTeamName}
                  size="$3"
                />

                <YStack gap="$2">
                  <Text fontSize="$3" color="$color11">
                    Seleziona Owner:
                  </Text>
                  <XStack gap="$2" flexWrap="wrap">
                    {(currentGroup.users || []).map((user) => {
                      const isSelected = user.email === newTeamOwner
                      const hasTeam = teams.some(t => t.owner === user.email)
                      return (
                        <Button
                          key={user.email}
                          size="$2"
                          variant="outlined"
                          borderColor={isSelected ? '$blue8' : '$borderColor'}
                          backgroundColor={isSelected ? '$blue2' : 'transparent'}
                          color={isSelected ? '$blue11' : hasTeam ? '$color9' : '$color11'}
                          onPress={() => setNewTeamOwner(user.email)}
                          disabled={hasTeam}
                        >
                          {user.username} {hasTeam ? '(già nel basket)' : ''}
                        </Button>
                      )
                    })}
                  </XStack>
                </YStack>

                <Button
                  size="$4"
                  backgroundColor="$blue9"
                  onPress={addTeam}
                  disabled={!newTeamOwner || loading}
                  icon={loading ? <Spinner size="small" /> : <UserPlus size="$1" />}
                >
                  {loading ? 'Aggiunta...' : 'Aggiungi Team'}
                </Button>
              </YStack>
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}
