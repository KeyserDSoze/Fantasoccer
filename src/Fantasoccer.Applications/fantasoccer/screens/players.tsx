import { useEffect, useMemo, useState, useCallback } from 'react'
import {
  AlertTriangle,
  BarChart3,
  RefreshCw,
  Search,
  ArrowUp,
  ArrowDown,
  ArrowUpDown,
} from '@tamagui/lucide-icons'
import {
  Button,
  Card,
  Input,
  Paragraph,
  ScrollView,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack,
  useMedia,
} from 'tamagui'

import { ScreenContent } from 'components/layout/ScreenContent'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { useApp } from 'providers/AppProvider'
import { useStatPlayers } from 'hooks/useStatPlayers'
import type { StatPlayer } from 'types/stat'
import { RoleExtensions } from 'types/group'

const formatSeasonFromYear = (yearNumber: number): string => {
  const baseYear = 2011
  const startYear = baseYear + yearNumber
  const endYear = startYear + 1
  return `${startYear}/${String(endYear).slice(-2)}`
}

type NumericStatKey =
  | 'summatory'
  | 'fantaSummatory'
  | 'withVote'
  | 'withoutVote'
  | 'withSpecial'
  | 'goals'
  | 'penalties'
  | 'assists'
  | 'stoppedPenalties'
  | 'sufferedGoals'
  | 'wrongedPenalties'
  | 'ownGoals'
  | 'yellowCards'
  | 'redCards'
  | 'enoughVotes'
  | 'manOfTheMatch'
  | 'injured'
  | 'average'
  | 'fantaAverage'

type ColumnKey = 'name' | 'role' | 'team' | NumericStatKey

type SortKey = ColumnKey

type SortDirection = 'asc' | 'desc'

type ColumnDefinition = {
  key: ColumnKey
  label: string
  minWidth?: number
  align?: 'left' | 'center' | 'right'
  isNumeric?: boolean
}

const getColumnValue = (player: StatPlayer, columnKey: ColumnKey): string => {
  switch (columnKey) {
    case 'name':
      return player.name
    case 'role':
      return player.roleLabel ?? RoleExtensions.asString(player.role)
    case 'team':
      return player.realTeam?.name ?? '—'
    case 'summatory':
      return player.summatory.toFixed(2)
    case 'fantaSummatory':
      return player.fantaSummatory.toFixed(2)
    case 'withVote':
      return player.withVote.toString()
    case 'withoutVote':
      return player.withoutVote.toString()
    case 'withSpecial':
      return player.withSpecial.toString()
    case 'goals':
      return player.goals.toString()
    case 'penalties':
      return player.penalties.toString()
    case 'assists':
      return player.assists.toString()
    case 'stoppedPenalties':
      return player.stoppedPenalties.toString()
    case 'sufferedGoals':
      return player.sufferedGoals.toString()
    case 'wrongedPenalties':
      return player.wrongedPenalties.toString()
    case 'ownGoals':
      return player.ownGoals.toString()
    case 'yellowCards':
      return player.yellowCards.toString()
    case 'redCards':
      return player.redCards.toString()
    case 'enoughVotes':
      return player.enoughVotes.toString()
    case 'manOfTheMatch':
      return player.manOfTheMatch.toString()
    case 'injured':
      return player.injured.toString()
    case 'average':
      return player.average.toFixed(2)
    case 'fantaAverage':
      return player.fantaAverage.toFixed(2)
    default:
      return '—'
  }
}

const numericSortKeys = new Set<NumericStatKey>([
  'summatory',
  'fantaSummatory',
  'withVote',
  'withoutVote',
  'withSpecial',
  'goals',
  'penalties',
  'assists',
  'stoppedPenalties',
  'sufferedGoals',
  'wrongedPenalties',
  'ownGoals',
  'yellowCards',
  'redCards',
  'enoughVotes',
  'manOfTheMatch',
  'injured',
  'average',
  'fantaAverage',
])

const columnDefinitions: ColumnDefinition[] = [
  { key: 'name', label: 'Giocatore', minWidth: 220 },
  { key: 'team', label: 'Squadra', minWidth: 160 },
  { key: 'role', label: 'Ruolo', minWidth: 140 },
  { key: 'average', label: 'Media', minWidth: 120, align: 'right', isNumeric: true },
  { key: 'fantaAverage', label: 'FantaMedia', minWidth: 120, align: 'right', isNumeric: true },
  { key: 'summatory', label: 'Somma Voti', minWidth: 120, align: 'right', isNumeric: true },
  { key: 'fantaSummatory', label: 'Somma Fanta', minWidth: 140, align: 'right', isNumeric: true },
  { key: 'withVote', label: 'Presenze', minWidth: 120, align: 'right', isNumeric: true },
  { key: 'withoutVote', label: 'Senza voto', minWidth: 130, align: 'right', isNumeric: true },
  { key: 'withSpecial', label: 'Con bonus', minWidth: 130, align: 'right', isNumeric: true },
  { key: 'goals', label: 'Goal', minWidth: 100, align: 'right', isNumeric: true },
  { key: 'penalties', label: 'Rigori segnati', minWidth: 140, align: 'right', isNumeric: true },
  { key: 'assists', label: 'Assist', minWidth: 110, align: 'right', isNumeric: true },
  { key: 'stoppedPenalties', label: 'Rigori parati', minWidth: 140, align: 'right', isNumeric: true },
  { key: 'sufferedGoals', label: 'Goal subiti', minWidth: 130, align: 'right', isNumeric: true },
  { key: 'wrongedPenalties', label: 'Rigori sbagliati', minWidth: 150, align: 'right', isNumeric: true },
  { key: 'ownGoals', label: 'Autogol', minWidth: 110, align: 'right', isNumeric: true },
  { key: 'yellowCards', label: 'Ammonizioni', minWidth: 140, align: 'right', isNumeric: true },
  { key: 'redCards', label: 'Espulsioni', minWidth: 120, align: 'right', isNumeric: true },
  { key: 'enoughVotes', label: 'Voti utili', minWidth: 130, align: 'right', isNumeric: true },
  { key: 'manOfTheMatch', label: 'MVP', minWidth: 100, align: 'right', isNumeric: true },
  { key: 'injured', label: 'Infortuni', minWidth: 120, align: 'right', isNumeric: true },
]

const getSortValue = (player: StatPlayer, sortKey: SortKey): number | string => {
  if (numericSortKeys.has(sortKey as NumericStatKey)) {
    const value = (player as unknown as Record<string, number | null | undefined>)[sortKey]
    return value ?? 0
  }

  switch (sortKey) {
    case 'name':
      return player.name.toLowerCase()
    case 'team':
      return player.realTeam?.name?.toLowerCase() ?? ''
    case 'role':
      return (player.roleLabel ?? RoleExtensions.asString(player.role)).toLowerCase()
    default:
      return ''
  }
}

export default function PlayersScreen() {
  const {
    currentGroup,
    userGroups,
    selectedYear,
    isGroupsLoading,
    groupsError,
  } = useApp()
  const media = useMedia()
  const isWideLayout = Boolean(media.gtMd)

  const [localYear, setLocalYear] = useState<number | null>(selectedYear ?? null)
  const [searchTerm, setSearchTerm] = useState('')
  const [sortKey, setSortKey] = useState<SortKey>('fantaAverage')
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc')

  useEffect(() => {
    setLocalYear(selectedYear ?? null)
  }, [selectedYear])

  const availableYears = useMemo(() => {
    const yearsSet = new Set<number>()

    if (currentGroup) {
      currentGroup.leagues.forEach((league) => {
        league.years.forEach((year) => yearsSet.add(year.year))
      })
    }

    if (selectedYear != null) {
      yearsSet.add(selectedYear)
    }

    if (localYear != null) {
      yearsSet.add(localYear)
    }

    return Array.from(yearsSet).sort((a, b) => b - a)
  }, [currentGroup, selectedYear, localYear])

  useEffect(() => {
    if ((localYear == null || !availableYears.includes(localYear)) && availableYears.length > 0) {
      setLocalYear(availableYears[0])
    }
  }, [availableYears, localYear])

  const { players, isLoading, error, lastUpdated, refetch } = useStatPlayers({
    year: localYear ?? undefined,
    autoRefresh: true,
    enabled: localYear != null,
  })

  const handleYearChange = useCallback((year: number) => {
    setLocalYear(year)
  }, [])

  const handleSortChange = useCallback((key: SortKey) => {
    setSortKey((currentKey) => {
      if (currentKey === key) {
        setSortDirection((direction) => (direction === 'asc' ? 'desc' : 'asc'))
        return currentKey
      }
      setSortDirection('desc')
      return key
    })
  }, [])

  const filteredPlayers = useMemo(() => {
    if (!searchTerm) {
      return players
    }

    const normalizedTerm = searchTerm.trim().toLowerCase()
    if (!normalizedTerm) {
      return players
    }

    return players.filter((player) => {
      const matchesName = player.name.toLowerCase().includes(normalizedTerm)
      const matchesTeam = player.realTeam?.name?.toLowerCase().includes(normalizedTerm)
      const matchesRole = (player.roleLabel ?? RoleExtensions.asString(player.role)).toLowerCase().includes(normalizedTerm)
      return matchesName || matchesTeam || matchesRole
    })
  }, [players, searchTerm])

  const sortedPlayers = useMemo(() => {
    const sortable = [...filteredPlayers]

    sortable.sort((a, b) => {
      const aValue = getSortValue(a, sortKey)
      const bValue = getSortValue(b, sortKey)

      if (typeof aValue === 'number' && typeof bValue === 'number') {
        return sortDirection === 'asc' ? aValue - bValue : bValue - aValue
      }

      const aStr = String(aValue)
      const bStr = String(bValue)
      return sortDirection === 'asc'
        ? aStr.localeCompare(bStr, 'it-IT', { sensitivity: 'base' })
        : bStr.localeCompare(aStr, 'it-IT', { sensitivity: 'base' })
    })

    return sortable
  }, [filteredPlayers, sortKey, sortDirection])

  const showSelectionCard = availableYears.length === 0
  const hasPlayers = sortedPlayers.length > 0

  return (
    <ScreenContent path="screens/players.tsx" title="Giocatori">
      <YStack
        width="100%"
        maxWidth={isWideLayout ? '100%' : 960}
        gap="$4"
        alignSelf={isWideLayout ? 'stretch' : 'center'}
      >
        {isGroupsLoading ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor"
          >
            <XStack alignItems="center" gap="$3" justifyContent="center">
              <Spinner size="small" color="$blue10" />
              <Text color="$color10">Caricamento dati del gruppo...</Text>
            </XStack>
          </Card>
        ) : groupsError ? (
          <Card
            padding="$4"
            backgroundColor="$red3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$red7"
          >
            <XStack alignItems="center" gap="$2">
              <AlertTriangle size="$1.5" color="$red10" />
              <Text color="$red11" fontWeight="600">
                {groupsError}
              </Text>
            </XStack>
            <Paragraph marginTop="$2" color="$red11">
              Prova ad aggiornare la pagina o selezionare nuovamente il gruppo dalla barra di navigazione.
            </Paragraph>
          </Card>
        ) : !currentGroup || userGroups.length === 0 ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor"
          >
            <Text color="$color12" fontWeight="600" marginBottom="$2">
              Nessun gruppo selezionato
            </Text>
            <Paragraph color="$color10">
              Per visualizzare le statistiche dei giocatori seleziona un gruppo dalla barra laterale.
            </Paragraph>
          </Card>
        ) : (
          <YStack gap="$4">
            <LeagueSelectorsWidget
              leagues={[]}
              selectedLeagueId={null}
              years={availableYears}
              selectedYear={localYear}
              onSelectYear={handleYearChange}
              formatYearLabel={formatSeasonFromYear}
              title="Filtro stagione"
              yearHint="Seleziona la stagione da analizzare"
              showLeagueSection={false}
              showRoundSection={false}
              emptyLeagueLabel="N/A"
            />

            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
              gap="$4"
            >
              <YStack gap="$3">
                <XStack alignItems="center" justifyContent="space-between" flexWrap="wrap" gap="$3">
                  <XStack gap="$2" alignItems="center">
                    <BarChart3 size="$1.5" color="$purple10" />
                    <Text fontSize="$5" fontWeight="700" color="$color12">
                      Statistiche giocatori
                    </Text>
                  </XStack>

                  <XStack gap="$2" alignItems="center" width="100%" maxWidth={320}>
                    <Search size="$1" color="$color10" />
                    <Input
                      flex={1}
                      value={searchTerm}
                      onChangeText={setSearchTerm}
                      placeholder="Cerca per nome, squadra o ruolo"
                      borderColor="$color5"
                    />
                  </XStack>
                </XStack>

                <XStack gap="$2" alignItems="center" flexWrap="wrap">
                  <Text color="$color10" fontSize="$2">
                    {hasPlayers ? `${sortedPlayers.length} giocatori` : 'Nessun giocatore disponibile'}
                  </Text>
                  {lastUpdated && (
                    <Text color="$color9" fontSize="$2">
                      Aggiornato: {lastUpdated.toLocaleString('it-IT')}
                    </Text>
                  )}
                  <Button
                    size="$2"
                    icon={RefreshCw}
                    variant="outlined"
                    onPress={() => {
                      void refetch()
                    }}
                  >
                    Aggiorna
                  </Button>
                </XStack>

                {isLoading && !hasPlayers ? (
                  <YStack gap="$3" alignItems="center" paddingVertical="$4">
                    <Spinner size="small" color="$blue10" />
                    <Text color="$color10">Carico le statistiche dei giocatori...</Text>
                  </YStack>
                ) : error ? (
                  <YStack gap="$3" alignItems="center" paddingVertical="$4">
                    <AlertTriangle size={32} color="$red10" />
                    <Text color="$color10" textAlign="center">
                      {error}
                    </Text>
                    <Button icon={RefreshCw} variant="outlined" onPress={() => void refetch()}>
                      Riprova
                    </Button>
                  </YStack>
                ) : showSelectionCard ? (
                  <YStack gap="$3" alignItems="center" paddingVertical="$4">
                    <AlertTriangle size={32} color="$yellow10" />
                    <Text color="$color10" textAlign="center">
                      Nessuna stagione disponibile per questo gruppo. Seleziona una stagione diversa dalla barra
                      laterale.
                    </Text>
                  </YStack>
                ) : (
                  <ScrollView horizontal showsHorizontalScrollIndicator>
                    <YStack minWidth="100%">
                      <XStack gap="$2" paddingBottom="$2">
                        {columnDefinitions.map((column) => {
                          const isActive = sortKey === column.key
                          const Icon = isActive ? (sortDirection === 'asc' ? ArrowUp : ArrowDown) : ArrowUpDown
                          return (
                            <Button
                              key={column.key}
                              size="$2"
                              variant="outlined"
                              onPress={() => handleSortChange(column.key)}
                              alignItems="center"
                              justifyContent="space-between"
                              gap="$2"
                              width={column.minWidth ?? 120}
                              borderColor={isActive ? '$purple8' : '$color6'}
                              backgroundColor={isActive ? '$purple2' : 'transparent'}
                            >
                              <Text
                                flex={1}
                                textAlign={column.align ?? 'left'}
                                fontWeight="600"
                                color={isActive ? '$purple11' : '$color11'}
                              >
                                {column.label}
                              </Text>
                              <Icon size="$1" color={isActive ? '$purple11' : '$color10'} />
                            </Button>
                          )
                        })}
                      </XStack>

                      <Separator marginBottom="$2" />

                      {hasPlayers ? (
                        <YStack gap="$2">
                          {sortedPlayers.map((player) => (
                            <Card
                              key={player.key}
                              padding="$3"
                              backgroundColor="$color1"
                              borderRadius="$3"
                              borderWidth="$0.5"
                              borderColor="$color4"
                            >
                              <XStack gap="$2">
                                {columnDefinitions.map((column) => (
                                  <YStack
                                    key={`${player.key}-${column.key}`}
                                    minWidth={column.minWidth ?? 120}
                                    alignItems={column.align === 'right' ? 'flex-end' : column.align === 'center' ? 'center' : 'flex-start'}
                                  >
                                    <Text color="$color10" fontSize="$2">
                                      {column.label}
                                    </Text>
                                    <Text color="$color12" fontWeight="600" fontSize="$3">
                                      {getColumnValue(player, column.key)}
                                    </Text>
                                  </YStack>
                                ))}
                              </XStack>
                            </Card>
                          ))}
                        </YStack>
                      ) : (
                        <YStack gap="$3" alignItems="center" paddingVertical="$4">
                          <Text color="$color10" textAlign="center">
                            Nessun giocatore trovato per i filtri selezionati.
                          </Text>
                        </YStack>
                      )}
                    </YStack>
                  </ScrollView>
                )}
              </YStack>
            </Card>
          </YStack>
        )}
      </YStack>
    </ScreenContent>
  )
}
