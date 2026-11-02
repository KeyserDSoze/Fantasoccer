import { useEffect, useMemo, useState } from 'react'
import { Pressable } from 'react-native'
import { useNavigation } from '@react-navigation/native'
import { AlertTriangle } from '@tamagui/lucide-icons-2'
import { Button, Card, H3, ScrollView, Separator, Text, XStack, YStack, useMedia } from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { SplashScreen } from 'components/layout/SplashScreen'
import { SerieAMatchRow } from 'components/seriea/SerieAMatchRow'
import { SerieARankTable } from 'components/seriea/SerieARankTable'
import { useApp } from 'providers/AppProvider'
import { useSerieASeason } from 'hooks/useSerieASeason'
import { serieAService } from 'services/serieAService'
import type { RealDay } from 'types/serieA'
import { defaultDay } from 'utils/serieAMatch'
import { formatSeasonFromYear, getCurrentSeasonYear } from 'utils/season'

function Chip({ label, selected, onPress, accessibilityLabel }: { label: string; selected: boolean; onPress: () => void; accessibilityLabel: string }) {
  return (
    <Button
      size="$3" theme={selected ? 'blue' : undefined} variant={selected ? undefined : 'outlined'} onPress={onPress}
      accessibilityLabel={accessibilityLabel} accessibilityState={{ selected }}>
      {label}
    </Button>
  )
}

export default function SerieAScreen() {
  const { selectedYear } = useApp()
  const navigation = useNavigation<any>()
  const media = useMedia()
  const [years, setYears] = useState<number[] | null>(null)
  const [year, setYear] = useState<number | null>(null)
  const [day, setDay] = useState<number | null>(null)
  const [yearsError, setYearsError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    serieAService.getAvailableYears().then(value => {
      if (!active) return
      const list = [...value].sort((a, b) => b - a)
      setYears(list)
      const preferred = selectedYear ?? getCurrentSeasonYear()
      setYear(list.includes(preferred) ? preferred : (list[0] ?? null))
    }).catch(reason => { if (active) setYearsError(reason?.message ?? 'Stagioni non disponibili.') })
    return () => { active = false }
  }, [selectedYear])

  const { calendar, rank, isLoading, error } = useSerieASeason(year)
  useEffect(() => { setDay(null) }, [year])
  useEffect(() => { if (day === null && calendar) setDay(defaultDay(calendar)) }, [calendar, day])

  const days = useMemo(() => [...(calendar?.days ?? [])].sort((a, b) => a.serieADay - b.serieADay), [calendar])
  const current: RealDay | null = days.find(value => value.serieADay === day) ?? null
  const open = (game: RealDay['games'][number], index: number) => year !== null && current &&
    navigation.navigate('SerieAMatch', { year: String(year), day: String(current.serieADay), ordinal: String(game.ordinal ?? index) })

  const loading = years === null && !yearsError
  const failure = yearsError ?? (!isLoading ? error : null)

  return (
    <ScreenContent title="Serie A" path="serie-a">
      <YStack gap="$4" width="100%" maxWidth={1100} alignSelf="center">
        <H3 color="$color12">Serie A</H3>

        {loading || (isLoading && !calendar && !failure) ? (
          <SplashScreen compact message="Caricamento Serie A…" steps={[
            { id: 'seasons', label: 'Stagioni', status: years ? 'done' : 'pending' },
            { id: 'calendar', label: 'Calendario e classifica', status: 'pending' }]} />
        ) : failure && !calendar ? (
          <YStack alignItems="center" gap="$3" paddingVertical="$6">
            <AlertTriangle size={32} color="$red10" />
            <Text color="$color10" textAlign="center">{failure}</Text>
          </YStack>
        ) : (
          <>
            <Card padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$3">
              <YStack gap="$1.5">
                <Text fontSize="$2" fontWeight="700" color="$color10">Stagione</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  <XStack gap="$2">
                    {(years ?? []).map(value => (
                      <Chip key={value} label={formatSeasonFromYear(value)} selected={value === year} onPress={() => setYear(value)}
                        accessibilityLabel={`Stagione ${formatSeasonFromYear(value)}`} />
                    ))}
                  </XStack>
                </ScrollView>
              </YStack>
              <YStack gap="$1.5">
                <Text fontSize="$2" fontWeight="700" color="$color10">Giornata</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  <XStack gap="$2">
                    {days.map(value => (
                      <Chip key={value.serieADay} label={String(value.serieADay)} selected={value.serieADay === day} onPress={() => setDay(value.serieADay)}
                        accessibilityLabel={`Giornata ${value.serieADay}`} />
                    ))}
                  </XStack>
                </ScrollView>
              </YStack>
            </Card>

            <XStack gap="$4" flexWrap="wrap" alignItems="flex-start">
              <Card flex={1} minWidth={media.gtSm ? 420 : 280} padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$2">
                <Text color="$color12" fontSize="$5" fontWeight="700">{current ? `${current.serieADay}ª giornata` : 'Partite'}</Text>
                <Separator />
                {current?.games.length ? current.games.map((game, index) => (
                  <Card key={`${game.ordinal ?? index}`} paddingHorizontal="$2" backgroundColor="$color1" borderRadius="$3" borderWidth="$0.25" borderColor="$color3">
                    <Pressable accessibilityRole="button" accessibilityLabel={`Apri la partita ${game.home.name} - ${game.away.name}`} onPress={() => open(game, index)}>
                      <SerieAMatchRow game={game} />
                    </Pressable>
                  </Card>
                )) : <Text color="$color10">Nessuna partita per questa giornata.</Text>}
              </Card>

              <Card flex={1} minWidth={media.gtSm ? 360 : 280} padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$2">
                <Text color="$color12" fontSize="$5" fontWeight="700">Classifica</Text>
                <Separator />
                {rank?.teams.length ? <SerieARankTable teams={rank.teams} /> : <Text color="$color10">Classifica non disponibile.</Text>}
              </Card>
            </XStack>
          </>
        )}
      </YStack>
    </ScreenContent>
  )
}
