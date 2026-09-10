import { useCallback, useEffect, useMemo, useState } from 'react'
import { Platform, View } from 'react-native'
import { RouteProp, useNavigation, useRoute } from '@react-navigation/native'
import { Card, H2, ScrollView, Spinner, Text, XStack, YStack } from 'tamagui'
import { AlertCircle, Clock, Coins, Gavel, Monitor, ShieldCheck, Users } from '@tamagui/lucide-icons-2'
import { RootStackParamList } from 'navigation'
import { useAuth } from 'providers/AuthProvider'
import { auctionService } from 'services/auctionService'
import { AuctionState, AuctionStatus, ActualAuction, BidPlaced } from 'types/auction'
import { Role } from 'types/group'
import { TeamHelper } from 'types/team'
import { getPlayerKey } from 'utils/keyUtils'
import { getAuctionStatusLabel, getRoleLabel } from 'utils/auctionHelper'
import { getTeamTier, TeamTier } from 'utils/teamTiers'

type DisplayRoute = RouteProp<RootStackParamList, 'AuctionDisplay'>

const ROLE_LIMITS: Record<Role, number> = {
  [Role.Undefined]: 0,
  [Role.GoalKeeper]: 3,
  [Role.Defensor]: 8,
  [Role.Midfielder]: 8,
  [Role.Forward]: 6,
}

const ROLES = [Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward]

export default function AuctionDisplayScreen() {
  const { auctionId } = useRoute<DisplayRoute>().params
  const navigation = useNavigation()
  const { user } = useAuth()
  const [state, setState] = useState<AuctionState | null>(null)
  const [actual, setActual] = useState<ActualAuction | null>(null)
  const [timer, setTimer] = useState(0)
  const [bid, setBid] = useState<BidPlaced | null>(null)
  const [starterKeys, setStarterKeys] = useState<Set<string>>(new Set())
  const [starterDataAvailable, setStarterDataAvailable] = useState(false)
  const [discardedKeys, setDiscardedKeys] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const timeout = setTimeout(async () => {
      if (user?.email) return
      const { returnUrlService } = await import('../services/returnUrlService')
      await returnUrlService.saveReturnUrl(`/auction/${auctionId}/display`)
      navigation.navigate('Login' as never)
    }, 3000)
    return () => clearTimeout(timeout)
  }, [auctionId, navigation, user?.email])

  const refresh = useCallback(async () => {
    const next = await auctionService.getAuction(auctionId)
    setState(next)
    setActual(next.wrapper.actual)
    setTimer(next.wrapper.secondsPerAuction)
    return next
  }, [auctionId])

  const loadReferenceData = useCallback(async () => {
    try {
      const [{ getActualFantacalcioYear }, { serieAService }, { chanceService }] = await Promise.all([
        import('../utils/timezoneHelpers'),
        import('../services/serieAService'),
        import('../services/chanceService'),
      ])
      const year = getActualFantacalcioYear()
      const calendar = await serieAService.getEnhancedCalendarByYear(year)
      const day = calendar?.liveSerieADay || calendar?.nextDay?.serieADay || 0
      if (day > 0) {
        let chances = await chanceService.getEnhancedWrapper({ year, day })
        if ((!chances?.players.length) && day > 1) chances = await chanceService.getEnhancedWrapper({ year, day: day - 1 })
        setStarterKeys(new Set(chances?.players.filter(player => player.chance.fantagazzetta).map(player => player.key) ?? []))
        setStarterDataAvailable(Boolean(chances?.players.length))
      }
      const discarded = await Promise.all(ROLES.map(role => auctionService.getDiscardedPlayers(auctionId, role)))
      setDiscardedKeys(new Set(discarded.flat().map(player => player.key)))
    } catch (loadError) {
      console.warn('Auction display reference data unavailable', loadError)
    }
  }, [auctionId])

  useEffect(() => {
    if (!user?.email) return
    let active = true
    const reload = async () => {
      try { if (active) await refresh() } catch (cause) { if (active) setError(cause instanceof Error ? cause.message : 'Asta non disponibile.') }
    }
    auctionService.setEventHandlers({
      onPlayerShown: next => { setActual(next); setBid(null) },
      onBidPlaced: setBid,
      onTimerUpdate: setTimer,
      onTeamUpdated: () => void reload(),
      onPlayerAssigned: () => { void reload(); void loadReferenceData() },
      onCurrentRoleChanged: () => void reload(),
      onAuctionPaused: () => void reload(),
      onAuctionResumed: () => void reload(),
      onAuctionClosed: () => void reload(),
      onCurrentAuctionReset: () => { setActual(null); setBid(null); void reload() },
      onCurrentAuctionClosed: () => { setActual(null); setBid(null); void reload() },
      onReconnected: () => void reload(),
      onError: setError,
    })
    void (async () => {
      try {
        await reload()
        await loadReferenceData()
        await auctionService.connect(auctionId, user.email)
        await auctionService.joinAuction(auctionId)
      } catch (cause) {
        if (active) setError(cause instanceof Error ? cause.message : 'Impossibile collegarsi all’asta.')
      }
    })()
    return () => { active = false; void auctionService.leaveAuction(auctionId); void auctionService.disconnect() }
  }, [auctionId, loadReferenceData, refresh, user?.email])

  const assignedKeys = useMemo(() => new Set(state?.teams.flatMap(team => team.value.players.map(player => getPlayerKey(player.name))) ?? []), [state?.teams])
  const remaining = useMemo(() => state?.realPlayers.filter(player => !assignedKeys.has(player.key) && !discardedKeys.has(player.key)) ?? [], [assignedKeys, discardedKeys, state?.realPlayers])
  const currentRole = state?.wrapper.currentRole ?? actual?.player.role ?? Role.Undefined
  const roleRemaining = remaining.filter(player => player.role === currentRole)
  const startersRemaining = roleRemaining.filter(player => starterKeys.has(player.key)).length
  const tierCounts = [TeamTier.T1, TeamTier.T2, TeamTier.T3, TeamTier.T4].map(tier => {
    const players = roleRemaining.filter(player => getTeamTier(player.realTeam?.name) === tier)
    return { tier, count: players.length, starters: players.filter(player => starterKeys.has(player.key)).length }
  })
  const startingMoney = state?.league?.settings?.startingMoney ?? 1000

  if (error) return <View style={{ flex: 1, backgroundColor: '#080b12', alignItems: 'center', justifyContent: 'center', padding: 32 }}><AlertCircle color="$red10" size={48} /><Text color="white" fontSize="$6" textAlign="center">{error}</Text></View>
  if (!state) return <View style={{ flex: 1, backgroundColor: '#080b12', alignItems: 'center', justifyContent: 'center' }}><Spinner size="large" color="$blue9" /><Text color="white" marginTop="$3">Collegamento alla regia asta...</Text></View>

  return <View style={{ flex: 1, backgroundColor: '#080b12' }}>
    <ScrollView contentContainerStyle={{ padding: Platform.OS === 'web' ? 28 : 16 }}>
      <YStack gap="$4" maxWidth={1800} width="100%" alignSelf="center">
        <XStack justifyContent="space-between" alignItems="center" flexWrap="wrap" gap="$3">
          <XStack gap="$3" alignItems="center"><Monitor color="$blue9" size={34} /><YStack><H2 color="white">{state.leagueName ?? 'Asta live'}</H2><Text color="$gray9">Modalità TV · aggiornamento in tempo reale</Text></YStack></XStack>
          <Card padding="$3" backgroundColor={state.wrapper.status === AuctionStatus.InProgress ? '$green4' : '$orange4'}><Text fontSize="$5" fontWeight="800">{getAuctionStatusLabel(state.wrapper.status)}</Text></Card>
        </XStack>

        <XStack gap="$4" flexWrap="wrap">
          <Card flex={2} minWidth={420} padding="$5" backgroundColor="$blue3" borderColor="$blue8" borderWidth={1}>
            <YStack gap="$3" alignItems="center">
              <Text color="$blue11" fontWeight="800" fontSize="$5">{getRoleLabel(currentRole).toUpperCase()}</Text>
              {actual ? <><Text color="$color12" fontWeight="900" fontSize="$9" textAlign="center">{actual.player.name}</Text><Text color="$color10" fontSize="$5">{actual.player.realTeam?.name} · FM {actual.player.fantaAverage.toFixed(2)}</Text>{starterDataAvailable && starterKeys.has(actual.player.key) && <Text color="$green10" fontSize="$4" fontWeight="800">TITOLARE FANTACALCIO.IT</Text>}<XStack gap="$5"><XStack gap="$2" alignItems="center"><Gavel size={28} color="$orange10" /><Text fontSize="$7" fontWeight="900">{bid?.amount ?? actual.price}</Text></XStack><Text fontSize="$5" color="$color10">{bid?.bidderName ?? actual.owner ?? 'Nessuna offerta'}</Text></XStack></> : <Text color="$color10" fontSize="$7" fontWeight="700">In attesa del prossimo giocatore</Text>}
            </YStack>
          </Card>
          <Card minWidth={220} padding="$5" backgroundColor="$color2"><YStack alignItems="center" gap="$2"><Clock size={38} color="$red10" /><Text fontSize="$10" fontWeight="900" color={timer <= 5 ? '$red10' : '$color12'}>{timer}</Text><Text color="$color10">secondi</Text></YStack></Card>
          <Card minWidth={260} padding="$5" backgroundColor="$green3"><YStack alignItems="center" gap="$2"><ShieldCheck size={38} color="$green10" /><Text fontSize="$9" fontWeight="900">{starterDataAvailable ? startersRemaining : 'N/D'}</Text><Text fontSize="$4" fontWeight="700" textAlign="center">Titolari Fantacalcio.it rimasti</Text><Text color="$green10">{starterDataAvailable ? 'nel ruolo corrente' : 'formazioni non disponibili'}</Text></YStack></Card>
        </XStack>

        <XStack gap="$3" flexWrap="wrap">
          <Metric label="Totale rimasti" value={remaining.filter(player => starterKeys.has(player.key)).length} secondary={remaining.length} />
          {ROLES.map(role => {
            const players = remaining.filter(player => player.role === role)
            return <Metric key={role} label={getRoleLabel(role)} value={players.filter(player => starterKeys.has(player.key)).length} secondary={players.length} active={role === currentRole} />
          })}
          {tierCounts.map(item => <Metric key={item.tier} label={`T${item.tier} ${getRoleLabel(currentRole)}`} value={item.starters} secondary={item.count} active={item.tier === TeamTier.T1} />)}
        </XStack>

        <XStack gap="$2" alignItems="center"><Users color="$blue9" /><Text color="white" fontSize="$6" fontWeight="800">Situazione squadre</Text></XStack>
        <XStack gap="$3" flexWrap="wrap">
          {state.teams.map(team => {
            const activePlayers = TeamHelper.getActivePlayers(team.value)
            const roleCount = activePlayers.filter(player => player.role === currentRole).length
            const roleMissing = Math.max(0, (ROLE_LIMITS[currentRole] ?? 0) - roleCount)
            const budget = startingMoney - TeamHelper.getCost(team.value)
            return <Card key={team.key.email} flexBasis={300} flexGrow={1} maxWidth={430} padding="$4" backgroundColor="$color2" borderColor={roleMissing > 0 ? '$orange7' : '$green7'} borderWidth={1}>
              <YStack gap="$3"><Text fontSize="$5" fontWeight="800">{team.value.name}</Text><XStack justifyContent="space-between"><XStack gap="$1" alignItems="center"><Coins size={18} color="$yellow10" /><Text fontSize="$5" fontWeight="800">{budget}</Text></XStack><Text color="$color10">{activePlayers.length}/25 giocatori</Text></XStack><XStack justifyContent="space-between"><Text>{getRoleLabel(currentRole)}</Text><Text fontWeight="800" color={roleMissing > 0 ? '$orange10' : '$green10'}>{roleCount}/{ROLE_LIMITS[currentRole]} · ne mancano {roleMissing}</Text></XStack></YStack>
            </Card>
          })}
        </XStack>
      </YStack>
    </ScrollView>
  </View>
}

function Metric({ label, value, secondary, active = false }: { label: string; value: number; secondary?: number; active?: boolean }) {
  return <Card minWidth={135} flexGrow={1} padding="$3" backgroundColor={active ? '$blue4' : '$color2'} borderColor={active ? '$blue8' : '$borderColor'} borderWidth={1}><Text color="$color10" textAlign="center">{label}</Text><Text fontSize="$7" fontWeight="900" textAlign="center">{value}{secondary !== undefined && <Text fontSize="$4" color="$color10"> ({secondary})</Text>}</Text>{secondary !== undefined && <Text fontSize="$1" color="$color10" textAlign="center">titolari (totali)</Text>}</Card>
}
