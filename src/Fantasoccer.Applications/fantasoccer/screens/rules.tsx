import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, BookOpen, Users, Shield, Trophy, Target, Info } from '@tamagui/lucide-icons'
import {
  Card,
  H2,
  H3,
  H4,
  Paragraph,
  Spinner,
  Text,
  XStack,
  YStack,
  Separator,
  useMedia
} from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { useApp } from 'providers/AppProvider'
import { Role, RoleExtensions, GroupHelper, type LeagueSetting, type VoteLeagueSetting } from 'types/group'
import { PlayerCard } from 'components/lineup/PlayerCard'
import { GoodDefenseIcon, GoodPeopleIcon, OwnGoalIcon } from 'components/icons/BonusIcons'
import { SoccerBallSVG, AssistSVG, BehaviourCardSVG } from 'components/lineup/icons'
import { type LivePlayer } from 'types/game'
import { Behaviour } from 'types/serieA'
import { ChanceType as ChanceTypeEnum } from 'types/chance'
import { FantaSoccerRole, PlayerInTeamStatus } from 'types/team'

const formatSeasonFromYear = (yearNumber: number): string => {
  const baseYear = 2011
  const startYear = baseYear + yearNumber
  const endYear = startYear + 1
  return `${startYear}/${String(endYear).slice(-2)}`
}

// Regole fisse del fantacalcio
const FIXED_RULES = {
  totalPlayers: 25,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
  formations: ['3-4-3', '3-5-2', '4-5-1', '5-4-1', '5-3-2', '4-4-2', '4-3-3']
}

// Dati di esempio per PlayerCard con voto (post-partita)
const createExamplePlayerWithVote = (): LivePlayer => ({
  current: {
    name: 'Destro',
    team: { name: 'Roma', id: 'roma-id' } as any,
    role: Role.Forward,
    active: true,
    visible: true,
    position: 0,
    price: 10,
    status: PlayerInTeamStatus.Active,
    isActive: true
  },
  currentPosition: FantaSoccerRole.Forward,
  vote: {
    role: Role.Forward,
    value: 7.5,
    isFinal: true,
    hasVote: true,
    isOut: false,
    goals: 2,
    penalties: 0,
    assists: 1,
    stoppedPenalties: 0,
    sufferedGoals: 0,
    wrongedPenalties: 0,
    ownGoals: 0,
    status: Behaviour.YellowCard,
    manOfTheMatch: true,
    injury: false
  },
  finalValue: { value: 10.5, special: false },
  realGame: {
    home: { name: 'Roma', abbreviation: 'ROM' },
    away: { name: 'Milan', abbreviation: 'MIL' },
    date: new Date('2025-10-04T15:00:00'),
    homeGoals: 3,
    awayGoals: 1,
    delayed: false
  },
  chance: null
} as unknown as LivePlayer)

// Dati di esempio per PlayerCard con chance (pre-partita)
const createExamplePlayerWithChance = (): LivePlayer => ({
  current: {
    name: 'Destro',
    team: { name: 'Roma', id: 'roma-id' } as any,
    role: Role.Forward,
    active: true,
    visible: true,
    position: 0,
    price: 10,
    status: PlayerInTeamStatus.Active,
    isActive: true
  },
  currentPosition: FantaSoccerRole.Forward,
  vote: null,
  finalValue: null,
  realGame: {
    home: { name: 'Roma', abbreviation: 'ROM' },
    away: { name: 'Juventus', abbreviation: 'JUV' },
    date: new Date('2025-10-06T20:45:00'),
    homeGoals: null,
    awayGoals: null,
    delayed: false
  },
  chance: {
    fantagazzetta: true,
    gazzetta: false,
    mediaset: true,
    sky: false,
    status: ChanceTypeEnum.Normal,
    positiveness: 75,
    trend: 4, // Excellent
    description: null,
    lastGame: null
  }
} as unknown as LivePlayer)

export default function Rules() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const {
    currentGroup,
    userGroups,
    selectedLeagueId,
    selectedYear,
    getAvailableLeagues,
    getAvailableYears,
    isGroupsLoading,
    groupsError
  } = useApp()

  const [localLeagueId, setLocalLeagueId] = useState<string | null>(selectedLeagueId)
  const [localYear, setLocalYear] = useState<number | null>(selectedYear ?? null)

  useEffect(() => {
    setLocalLeagueId(selectedLeagueId ?? null)
  }, [selectedLeagueId])

  useEffect(() => {
    setLocalYear(selectedYear ?? null)
  }, [selectedYear])

  const availableLeagues = getAvailableLeagues()
  const selectedLeague = availableLeagues.find((league) => league.id === localLeagueId) ?? null
  const availableYears = localLeagueId ? getAvailableYears(localLeagueId) : []
  const sortedYears = useMemo(() => [...availableYears].sort((a, b) => b - a), [availableYears])
  const leagueOptions = useMemo(
    () => availableLeagues.map((league) => ({ id: league.id, name: league.name, isMain: league.isMain })),
    [availableLeagues]
  )

  useEffect(() => {
    if (!localLeagueId) {
      if (localYear !== null) {
        setLocalYear(null)
      }
      return
    }

    if (availableYears.length === 0) {
      if (localYear !== null) {
        setLocalYear(null)
      }
      return
    }

    if (localYear == null || !availableYears.includes(localYear)) {
      const latestYear = Math.max(...availableYears)
      setLocalYear(latestYear)
    }
  }, [localLeagueId, availableYears, localYear])

  const leagueName = selectedLeague?.name ?? (availableLeagues.length === 1 ? availableLeagues[0]?.name : null)
  const seasonLabel = localYear != null ? formatSeasonFromYear(localYear) : null

  const hasMultipleGroups = userGroups.length > 1
  const hasMultipleLeagues = availableLeagues.length > 1
  const hasMultipleYears = sortedYears.length > 1
  const shouldRenderSelectors = hasMultipleGroups || hasMultipleLeagues || hasMultipleYears

  // Get current league settings
  const currentLeagueSettings = useMemo(() => {
    if (!currentGroup || !localLeagueId || localYear == null) {
      return null
    }
    const annualLeague = GroupHelper.getAnnualLeague(currentGroup, localLeagueId, localYear)
    return annualLeague?.settings ?? null
  }, [currentGroup, localLeagueId, localYear])

  // Helper to render vote settings for a role
  const renderVoteSettings = (role: Role, settings: VoteLeagueSetting) => {
    const roleName = role !== Role.GoalKeeper && role !== Role.Defensor && role !== Role.Midfielder && role !== Role.Forward ? 'Tutti i ruoli' : RoleExtensions.asString(role)
    
    return (
      <YStack key={role} gap="$2" backgroundColor="$color2" padding="$3" borderRadius="$3">
        <Text fontSize="$4" fontWeight="700" color="$blue10">
          {roleName}
        </Text>
        <YStack gap="$1">
          {settings.goal !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">⚽ Gol</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.goal > 0 ? '+' : ''}{settings.goal}</Text>
            </XStack>
          )}
          {settings.assist !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🎯 Assist</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.assist > 0 ? '+' : ''}{settings.assist}</Text>
            </XStack>
          )}
          {settings.penalty !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">⚡ Rigore segnato</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.penalty > 0 ? '+' : ''}{settings.penalty}</Text>
            </XStack>
          )}
          {settings.stoppedPenalty !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🧤 Rigore parato</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.stoppedPenalty > 0 ? '+' : ''}{settings.stoppedPenalty}</Text>
            </XStack>
          )}
          {settings.wrongedPenalty !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">❌ Rigore sbagliato</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.wrongedPenalty > 0 ? '+' : ''}{settings.wrongedPenalty}</Text>
            </XStack>
          )}
          {settings.ownGoal !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🔴 Autogol</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.ownGoal > 0 ? '+' : ''}{settings.ownGoal}</Text>
            </XStack>
          )}
          {settings.sufferedGoal !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🥅 Gol subito</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.sufferedGoal > 0 ? '+' : ''}{settings.sufferedGoal}</Text>
            </XStack>
          )}
          {settings.yellowCard !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🟨 Ammonizione</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.yellowCard > 0 ? '+' : ''}{settings.yellowCard}</Text>
            </XStack>
          )}
          {settings.redCard !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🟥 Espulsione</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.redCard > 0 ? '+' : ''}{settings.redCard}</Text>
            </XStack>
          )}
          {settings.injury !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">🏥 Infortunio</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.injury > 0 ? '+' : ''}{settings.injury}</Text>
            </XStack>
          )}
          {settings.manOfTheMatch !== 0 && (
            <XStack justifyContent="space-between">
              <Text fontSize="$3" color="$color11">⭐ Migliore in campo</Text>
              <Text fontSize="$3" fontWeight="600" color="$color12">{settings.manOfTheMatch > 0 ? '+' : ''}{settings.manOfTheMatch}</Text>
            </XStack>
          )}
        </YStack>
      </YStack>
    )
  }

  const renderLeagueSettings = (settings: LeagueSetting) => {
    return (
      <YStack gap="$4">
        {/* Modificatori Voto */}
        <YStack gap="$3">
          <XStack alignItems="center" gap="$2">
            <Target size="$1" color="$blue10" />
            <H3 color="$color12">Modificatori Voto</H3>
          </XStack>
          <Separator />
          <YStack gap="$2">
            {Object.entries(settings.votes).map(([roleKey, voteSettings]) => {
              const role = parseInt(roleKey) as Role
              return renderVoteSettings(role, voteSettings)
            })}
          </YStack>
        </YStack>

        {/* Punteggi Partita */}
        <YStack gap="$3">
          <XStack alignItems="center" gap="$2">
            <Trophy size="$1" color="$green10" />
            <H3 color="$color12">Punteggi Partita</H3>
          </XStack>
          <Separator />
          <Card backgroundColor="$color2" padding="$3" borderRadius="$3">
            <YStack gap="$1.5">
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">🏆 Vittoria</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForVictory} punti</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">🤝 Pareggio</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForDraw} punti</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">❌ Sconfitta</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForDefeat} punti</Text>
              </XStack>
            <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">🏠 Bonus partita in casa</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointInHome > 0 ? '+' : ''}{settings.pointInHome} punti</Text>
            </XStack>
            </YStack>
          </Card>
        </YStack>

        {/* Bonus Gol */}
        <YStack gap="$3">
          <XStack alignItems="center" gap="$2">
            <Target size="$1" color="$orange10" />
            <H3 color="$color12">Punteggio</H3>
          </XStack>
          <Separator />
          <Card backgroundColor="$color2" padding="$3" borderRadius="$3">
            <YStack gap="$1.5">
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">⚽ Primo gol della partita</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForFirstGoal} punti</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">⚽ Gol successivi ogni</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForNextGoal} punti</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">🔴 Autogol sotto a:</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForFirstGoal - settings.pointForOwnGoal - 0.5} punti</Text>
              </XStack>
              {settings.differencePointForOwnGoal !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">📉 Differenza minima (maggiore o uguale) per autogol:</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.differencePointForOwnGoal} punti</Text>
                </XStack>
              )}
            </YStack>
          </Card>
        </YStack>

        {/* Bonus Difesa */}
        <YStack gap="$3">
          <XStack alignItems="center" gap="$2">
            <Shield size="$1" color="$purple10" />
            <H3 color="$color12">Bonus</H3>
          </XStack>
          <Separator />
          <Card backgroundColor="$color2" padding="$3" borderRadius="$3">
            <YStack gap="$1.5">
              {settings.pointForCleanSheet !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">🛡️ Porta inviolata</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForCleanSheet > 0 ? '+' : ''}{settings.pointForCleanSheet} punti</Text>
                </XStack>
              )}
              {settings.pointForStrongDefense !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">🔒 Difesa forte (≤ 3 difensori)</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForStrongDefense > 0 ? '+' : ''}{settings.pointForStrongDefense} punti</Text>
                </XStack>
              )}
              {settings.pointForStrongDefense4 !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">🔒 Difesa forte (4 difensori)</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForStrongDefense4 > 0 ? '+' : ''}{settings.pointForStrongDefense4} punti</Text>
                </XStack>
              )}
              {settings.pointForStrongDefense5 !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">🔒 Difesa forte (5 difensori)</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForStrongDefense5 > 0 ? '+' : ''}{settings.pointForStrongDefense5} punti</Text>
                </XStack>
              )}
              {settings.pointForGoodPeople !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">😇 Fair play</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.pointForGoodPeople > 0 ? '+' : ''}{settings.pointForGoodPeople} punti</Text>
                </XStack>
              )}
            </YStack>
          </Card>
        </YStack>

        {/* Impostazioni Generali */}
        <YStack gap="$3">
          <XStack alignItems="center" gap="$2">
            <Users size="$1" color="$red10" />
            <H3 color="$color12">Impostazioni Generali</H3>
          </XStack>
          <Separator />
          <Card backgroundColor="$color2" padding="$3" borderRadius="$3">
            <YStack gap="$1.5">
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">💰 Budget iniziale</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.startingMoney} crediti</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">⏸️ Giornata di serie A di partenza</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.delayedDay + 1}</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">🚫 Giornate cancellate</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">prime {settings.cancelledDay}</Text>
              </XStack>
              {settings.moneyForGoal !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">💵 Denaro per gol fatto</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.moneyForGoal} crediti</Text>
                </XStack>
              )}
              {settings.moneyForSufferedGoal !== 0 && (
                <XStack justifyContent="space-between">
                  <Text fontSize="$3" color="$color11">💸 Denaro per gol subito</Text>
                  <Text fontSize="$3" fontWeight="600" color="$color12">{settings.moneyForSufferedGoal} crediti</Text>
                </XStack>
              )}
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">🎲 Asta random</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.randomAuction ? 'Sì' : 'No'}</Text>
              </XStack>
              <XStack justifyContent="space-between">
                <Text fontSize="$3" color="$color11">📊 Classifica con punti valore</Text>
                <Text fontSize="$3" fontWeight="600" color="$color12">{settings.rankWithValuePoints ? 'Sì' : 'No'}</Text>
              </XStack>
            </YStack>
          </Card>
        </YStack>
      </YStack>
    )
  }

  return (
    <ScreenContent path="screens/rules.tsx" title="Regolamento">
      <YStack
        width={containerWidth}
        maxWidth={960}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
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
              <Text color="$color10">Caricamento dati della lega...</Text>
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
        ) : !currentGroup ? (
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
              Per visualizzare il regolamento scegli un gruppo dalla barra laterale.
            </Paragraph>
          </Card>
        ) : (
          <YStack gap="$4">
            {/* Selettori Lega e Anno */}
            {shouldRenderSelectors && (
              <LeagueSelectorsWidget
                leagues={leagueOptions}
                selectedLeagueId={localLeagueId}
                onSelectLeague={(leagueId) => {
                  setLocalLeagueId(leagueId)
                }}
                years={sortedYears}
                selectedYear={localYear}
                onSelectYear={(year) => {
                  setLocalYear(year)
                }}
                formatYearLabel={formatSeasonFromYear}
                emptyLeagueLabel={leagueName ?? '—'}
              />
            )}

            {/* Header Card */}
            <Card
              padding="$4"
              backgroundColor="$blue3"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$blue7"
            >
              <XStack alignItems="center" gap="$3" marginBottom="$2">
                <BookOpen size="$2" color="$blue10" />
                <H2 color="$blue11">Regolamento FantaZone</H2>
              </XStack>
              {leagueName && (
                <Text fontSize="$4" color="$blue10" fontWeight="600">
                  {leagueName}
                  {seasonLabel && ` • Stagione ${seasonLabel}`}
                </Text>
              )}
            </Card>

            {/* Regole Fisse */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
              gap="$3"
            >
              <XStack alignItems="center" gap="$2">
                <Users size="$1" color="$color12" />
                <H3 color="$color12">Regole Base</H3>
              </XStack>
              <Separator />
              
              {/* Rosa */}
              <YStack gap="$2">
                <H4 color="$color11">🎯 Composizione Rosa</H4>
                <Text fontSize="$3" color="$color10">
                  Ogni squadra è composta da <Text fontWeight="700" color="$color12">{FIXED_RULES.totalPlayers} giocatori</Text>:
                </Text>
                <YStack gap="$1" paddingLeft="$3">
                  <Text fontSize="$3" color="$color10">• {FIXED_RULES.goalkeepers} Portieri</Text>
                  <Text fontSize="$3" color="$color10">• {FIXED_RULES.defenders} Difensori</Text>
                  <Text fontSize="$3" color="$color10">• {FIXED_RULES.midfielders} Centrocampisti</Text>
                  <Text fontSize="$3" color="$color10">• {FIXED_RULES.forwards} Attaccanti</Text>
                </YStack>
              </YStack>

              <Separator />

              {/* Moduli */}
              <YStack gap="$2">
                <H4 color="$color11">⚽ Moduli Consentiti</H4>
                <Text fontSize="$3" color="$color10">
                  Puoi schierare la tua formazione con uno dei seguenti moduli:
                </Text>
                <XStack flexWrap="wrap" gap="$2" paddingLeft="$3">
                  {FIXED_RULES.formations.map((formation) => (
                    <Card
                      key={formation}
                      backgroundColor="$color3"
                      paddingHorizontal="$3"
                      paddingVertical="$2"
                      borderRadius="$3"
                    >
                      <Text fontSize="$3" fontWeight="600" color="$color12">
                        {formation}
                      </Text>
                    </Card>
                  ))}
                </XStack>
              </YStack>
            </Card>

            {/* League Settings */}
            {currentLeagueSettings ? (
              renderLeagueSettings(currentLeagueSettings)
            ) : (
              <Card
                padding="$4"
                backgroundColor="$color2"
                borderRadius="$4"
                borderWidth="$0.5"
                borderColor="$borderColor"
              >
                <Text color="$color12" fontWeight="600" marginBottom="$2">
                  Seleziona una lega e una stagione
                </Text>
                <Paragraph color="$color10">
                  Per visualizzare le regole specifiche, seleziona una lega e una stagione dai selettori qui sopra.
                </Paragraph>
              </Card>
            )}

            {/* Sezione Spiegazione Icone */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
              gap="$4"
              marginTop="$4"
            >
              <XStack alignItems="center" gap="$2">
                <Info size="$1" color="$blue10" />
                <H3 color="$color12">Guida alle Icone</H3>
              </XStack>
              <Separator />

              <Paragraph fontSize="$3" color="$color11">
                Le card dei giocatori mostrano diverse informazioni a seconda del momento della partita. 
                Ecco una guida completa alle icone e ai colori utilizzati.
              </Paragraph>

              {/* Card di esempio con voto (post-partita) */}
              <YStack gap="$3">
                <H4 color="$color11">📊 Dopo la Partita - Con Voto</H4>
                <Text fontSize="$3" color="$color10">
                  Quando la partita è terminata, la card mostra il voto finale e i bonus/malus ottenuti:
                </Text>
                
                <XStack justifyContent="center" padding="$3" backgroundColor="$color3" borderRadius="$3">
                  <PlayerCard player={createExamplePlayerWithVote()} />
                </XStack>

                <YStack gap="$2" paddingLeft="$2">
                  <H4 fontSize="$3" color="$color11">🎨 Colori Badge Voto (in alto a sinistra):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#16a34a" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Verde: voto finale confermato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#f59e0b" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Arancione: voto live (partita in corso, lampeggiante)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#dc2626" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Rosso: giocatore uscito dal campo</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#6b7280" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Grigio: non ha giocato abbastanza (voto non valido)</Text>
                    </XStack>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">⚡ Icone Eventi (lato destro):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#000000" size={16} />
                      <Text fontSize="$2" color="$color10">Gol segnato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <SoccerBallSVG primaryColor="#000000" secondaryColor="#ffffff" size={16} />
                      <Text fontSize="$2" color="$color10">Rigore segnato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <AssistSVG color="#000" size={16} />
                      <Text fontSize="$2" color="$color10">Assist fornito</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <SoccerBallSVG primaryColor="#3b82f6" secondaryColor="#ffffff" size={16} />
                      <Text fontSize="$2" color="$color10">Rigore parato (portieri)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" size={16} />
                      <Text fontSize="$2" color="$color10">Gol subito (portieri/chi sta in porta)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <SoccerBallSVG primaryColor="#c113d1ff" secondaryColor="#ffffff" size={16} />
                      <Text fontSize="$2" color="$color10">Rigore sbagliato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <SoccerBallSVG primaryColor="#fbbf24" secondaryColor="#ffffff" size={16} />
                      <Text fontSize="$2" color="$color10">Autogol</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <BehaviourCardSVG color="#fbbf24" size={16} />
                      <Text fontSize="$2" color="$color10">Cartellino giallo</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <BehaviourCardSVG color="#ef4444" size={16} />
                      <Text fontSize="$2" color="$color10">Cartellino rosso (espulsione)</Text>
                    </XStack>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">🌟 Effetti Speciali:</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <Text fontSize="$2" color="$color10">
                      🏆 <Text fontWeight="600">Bordo arcobaleno animato</Text>: Migliore in campo (Man of the Match)
                    </Text>
                    <Text fontSize="$2" color="$color10">
                      ⚡ <Text fontWeight="600">Voto lampeggiante</Text>: Partita in corso (aggiornamento live ogni 10 secondi)
                    </Text>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">🎨 Colore Bordo Inferiore (nome giocatore):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#16a34a" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Verde: giocatore titolare (posizioni 0-3)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#f59e0b" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Arancione: prima panchina (posizioni 4, prime riserve)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#3b82f6" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Blu: seconda panchina (seconde riserve)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#dc2626" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Rosso: tribuna (non schierato)</Text>
                    </XStack>
                  </YStack>
                </YStack>
              </YStack>

              <Separator />

              {/* Card di esempio con chance (pre-partita) */}
              <YStack gap="$3">
                <H4 color="$color11">🔮 Prima della Partita - Con Probabilità</H4>
                <Text fontSize="$3" color="$color10">
                  Prima che inizi la partita, la card mostra le probabilità di schieramento e la forma del giocatore:
                </Text>
                
                <XStack justifyContent="center" padding="$3" backgroundColor="$color3" borderRadius="$3">
                  <PlayerCard player={createExamplePlayerWithChance()} />
                </XStack>

                <YStack gap="$2" paddingLeft="$2">
                  <H4 fontSize="$3" color="$color11">📈 Badge Forma (in alto a sinistra, sotto il voto se presente):</H4>
                  <Text fontSize="$2" color="$color10" paddingLeft="$3">
                    Mostra la forma del giocatore con un numero percentuale (0-100) e una freccia direzionale:
                  </Text>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#dc2626" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↓</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Rosso con freccia giù: forma molto negativa</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#f97316" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↘</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Arancione con freccia obliqua giù: forma negativa</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#facc15" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#000">→</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Giallo con freccia orizzontale: forma neutra</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#4ade80" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↗</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Verde chiaro con freccia obliqua su: forma positiva</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#16a34a" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↑</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Verde scuro con freccia su: forma eccellente</Text>
                    </XStack>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">📰 Badge Probabilità Titolarità (lato destro):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$green10">F</Text>
                      <Text fontSize="$2" color="$color10">Verde: probabile titolare secondo Fantacalcio.it</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$red10">F</Text>
                      <Text fontSize="$2" color="$color10">Rosso: probabile panchinaro secondo Fantacalcio.it</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$green10">G</Text>
                      <Text fontSize="$2" color="$color10">Verde: probabile titolare secondo Gazzetta dello Sport</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$red10">G</Text>
                      <Text fontSize="$2" color="$color10">Rosso: probabile panchinaro secondo Gazzetta dello Sport</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <AlertTriangle size={14} color="#ef4444" />
                      <Text fontSize="$2" color="$color10">Triangolo rosso: giocatore infortunato</Text>
                    </XStack>
                  </YStack>
                </YStack>
              </YStack>

              <Separator />

              {/* Bonus squadra nel risultato partita */}
              <YStack gap="$3">
                <H4 color="$color11">🏆 Bonus Squadra nel Risultato Partita</H4>
                <Text fontSize="$3" color="$color10">
                  Nella schermata del risultato della partita, accanto al punteggio di ogni squadra, possono apparire le seguenti icone bonus:
                </Text>

                <YStack gap="$2" paddingLeft="$3">
                  <XStack alignItems="center" gap="$2">
                    <GoodDefenseIcon size={20} />
                    <Text fontSize="$2" color="$color10">
                      <Text fontWeight="600">Difesa forte</Text>: bonus per difesa che non ha subito molti gol (muro arancione)
                    </Text>
                  </XStack>
                  <XStack alignItems="center" gap="$2">
                    <GoodPeopleIcon size={20} />
                    <Text fontSize="$2" color="$color10">
                      <Text fontWeight="600">Brava gente</Text>: bonus per squadra con pochi cartellini (stella gialla)
                    </Text>
                  </XStack>
                  <XStack alignItems="center" gap="$2">
                    <OwnGoalIcon size={20} />
                    <Text fontSize="$2" color="$color10">
                      <Text fontWeight="600">Autogol</Text>: malus per squadra che ha segnato un autogol decisivo (pallone rosso e bianco)
                    </Text>
                  </XStack>
                </YStack>

                <Text fontSize="$2" color="$color11" fontStyle="italic" paddingLeft="$3">
                  Nota: l'autogol viene mostrato solo se entrambe le squadre hanno meno di 66 punti e la differenza è di almeno 6 punti.
                </Text>
              </YStack>
            </Card>
          </YStack>
        )}
      </YStack>
    </ScreenContent>
  )
}
