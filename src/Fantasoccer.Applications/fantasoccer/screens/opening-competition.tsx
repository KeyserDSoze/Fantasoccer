import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, Gift, RefreshCw, Save } from '@tamagui/lucide-icons-2'
import { Button, Card, H2, Spinner, Text, XStack, YStack } from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { useApp } from 'providers/AppProvider'
import { openingCompetitionService } from 'services/openingCompetitionService'
import { OpeningCompetitionStandings } from 'types/openingCompetition'
import { FantaSoccerRole } from 'types/team'

export default function OpeningCompetition() {
  const { currentGroup, selectedLeagueId, selectedYear } = useApp()
  const [data, setData] = useState<OpeningCompetitionStandings | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [team, setTeam] = useState<import('types/team').Team | null>(null)
  const [savingTeam, setSavingTeam] = useState(false)
  const [selectedDay, setSelectedDay] = useState(1)
  const load = useCallback(async () => {
    if (!currentGroup || !selectedLeagueId || selectedYear == null) return
    setLoading(true); setError(null)
    try { setData(await openingCompetitionService.get(currentGroup.id, selectedLeagueId, selectedYear)) }
    catch (e) { setData(null); setError(e instanceof Error ? e.message : 'Impossibile caricare la competizione.') }
    finally { setLoading(false) }
  }, [currentGroup, selectedLeagueId, selectedYear])
  useEffect(() => { void load() }, [load])
  useEffect(() => { setTeam(null); setSelectedDay(1) }, [currentGroup?.id, selectedLeagueId, selectedYear])
  const prepare = async () => {
    if (!currentGroup || !selectedLeagueId || selectedYear == null) return
    setLoading(true); setError(null)
    try { setData(await openingCompetitionService.prepare(currentGroup.id, selectedLeagueId, selectedYear)) }
    catch (e) { setError(e instanceof Error ? e.message : 'Preparazione non riuscita.') }
    finally { setLoading(false) }
  }
  const loadTeam = async () => {
    if (!currentGroup || !selectedLeagueId || selectedYear == null) return
    try { setTeam(await openingCompetitionService.team(currentGroup.id, selectedLeagueId, selectedYear, selectedDay)); setError(null) }
    catch (e) { setError(e instanceof Error ? e.message : 'Impossibile caricare la formazione.') }
  }
  const saveTeam = async () => {
    if (!team || !currentGroup || !selectedLeagueId || selectedYear == null) return
    setSavingTeam(true)
    try {
      await openingCompetitionService.saveTeam(currentGroup.id, selectedLeagueId, selectedYear, team, selectedDay)
      setTeam(await openingCompetitionService.team(currentGroup.id, selectedLeagueId, selectedYear, selectedDay, team.teamId))
      await prepare()
    }
    catch (e) { setError(e instanceof Error ? e.message : 'Impossibile salvare la formazione.') }
    finally { setSavingTeam(false) }
  }
  const rotatePosition = (index: number) => {
    if (!team) return
    const role = team.players[index].role
    const positions = role === 0 ? [0, 4, 11] : role === 1 ? [1, 5, 6, 11] : role === 2 ? [2, 7, 8, 11] : [3, 9, 10, 11]
    const current = team.players[index].position
    const next = positions[(positions.indexOf(current) + 1) % positions.length]
    setTeam({ ...team, players: team.players.map((player, playerIndex) => playerIndex === index ? { ...player, position: next as FantaSoccerRole } : player) })
  }

  return <ScreenContent path="screens/opening-competition.tsx" title="Competizione d'apertura">
    <YStack width="100%" maxWidth={720} alignSelf="center" gap="$4">
      <XStack justifyContent="space-between" alignItems="center"><H2>Classifica apertura</H2><XStack gap="$2"><Button onPress={() => void loadTeam()}>La mia squadra</Button><Button icon={<RefreshCw size="$1" />} onPress={() => void load()} disabled={loading}>Aggiorna</Button></XStack></XStack>
      {team && <Card padding="$3"><YStack gap="$2"><XStack justifyContent="space-between" alignItems="center"><Text fontWeight="bold">{team.name || 'Squadra di apertura'} - giornata {selectedDay}</Text><XStack gap="$2"><Button size="$2" onPress={() => { const day = Math.max(1, selectedDay - 1); setSelectedDay(day); if (currentGroup && selectedLeagueId && selectedYear != null) void openingCompetitionService.team(currentGroup.id, selectedLeagueId, selectedYear, day).then(setTeam) }} disabled={selectedDay <= 1}>-</Button><Button size="$2" onPress={() => { const day = Math.min(data?.serieADays ?? 1, selectedDay + 1); setSelectedDay(day); if (currentGroup && selectedLeagueId && selectedYear != null) void openingCompetitionService.team(currentGroup.id, selectedLeagueId, selectedYear, day).then(setTeam) }} disabled={selectedDay >= (data?.serieADays ?? 1)}>+</Button><Button icon={<Save size="$1" />} onPress={() => void saveTeam()} disabled={savingTeam}>{savingTeam ? 'Salvataggio...' : 'Salva'}</Button></XStack></XStack>{team.players.map((player, index) => <XStack key={`${player.name}-${index}`} justifyContent="space-between" alignItems="center"><Text>{player.name}</Text><Button size="$2" variant="outlined" onPress={() => rotatePosition(index)}>{FantaSoccerRole[player.position] ?? 'Tribuna'}</Button></XStack>)}</YStack></Card>}
      {loading && <XStack justifyContent="center" gap="$2"><Spinner /><Text>Caricamento classifica...</Text></XStack>}
      <Button onPress={() => void prepare()} disabled={loading}>Prepara / ricalcola apertura</Button>
      {!loading && !error && !data && <Text>La classifica non è ancora stata preparata.</Text>}
      {error && <Card padding="$4" backgroundColor="$red3"><XStack gap="$2"><AlertTriangle color="$red10" /><Text color="$red11">{error}</Text></XStack></Card>}
      {!loading && !error && data && <Card padding="$0" overflow="hidden"><YStack>
        <XStack padding="$3" backgroundColor="$color3" justifyContent="space-between"><Text fontWeight="bold">Squadra</Text><Text fontWeight="bold">Punti</Text><Text fontWeight="bold">Premio</Text></XStack>
         {data.teams.map((team, index) => <XStack key={team.owner} padding="$3" borderBottomWidth={index === data.teams.length - 1 ? 0 : 1} borderColor="$borderColor" alignItems="center" gap="$2"><Text width={28} fontWeight="bold">{team.position || index + 1}</Text><Text flex={1}>{team.name || team.owner}</Text><Text width={70} textAlign="right">{team.score.toFixed(2)}</Text><XStack width={70} justifyContent="flex-end" gap="$1">{team.prize > 0 && <Gift size="$1" color="$orange10" />}<Text>{team.prize > 0 ? `${team.prize} cr` : '-'}</Text></XStack></XStack>)}
      </YStack></Card>}
      {!loading && !error && data && data.teams.length === 0 && <Text>Nessuna squadra disponibile.</Text>}
    </YStack>
  </ScreenContent>
}
