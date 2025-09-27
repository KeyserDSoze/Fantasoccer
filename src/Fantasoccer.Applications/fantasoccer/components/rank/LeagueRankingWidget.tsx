import { type FC } from 'react'
import { AlertTriangle, RefreshCw } from '@tamagui/lucide-icons'
import { Button, Card, ScrollView, Separator, Spinner, Text, XStack, YStack } from 'tamagui'
import { useWindowDimensions } from 'react-native'
import type { EnhancedRankedTeam } from 'types/rank'

export type LeagueRankingWidgetProps = {
  leagueName?: string | null
  seasonLabel?: string | null
  roundLabel?: string | null
  teamCount?: number | null
  rankedTeams: EnhancedRankedTeam[]
  loading?: boolean
  error?: string | null
  isSelectionIncomplete?: boolean
  onRefresh?: () => void
  selectionMessage?: string
  emptyMessage?: string
  loadingMessage?: string
  refreshLabel?: string
  errorActionLabel?: string
}

const defaultSelectionMessage = 'Seleziona lega, stagione e girone per visualizzare la classifica.'
const defaultEmptyMessage = 'Non ci sono dati di classifica per i filtri selezionati.'
const defaultLoadingMessage = 'Caricamento classifica per il girone selezionato...'
const defaultRefreshLabel = 'Aggiorna classifica'
const defaultErrorActionLabel = 'Riprova'

export const LeagueRankingWidget: FC<LeagueRankingWidgetProps> = ({
  leagueName = 'Lega non disponibile',
  seasonLabel,
  roundLabel,
  teamCount,
  rankedTeams,
  loading = false,
  error = null,
  isSelectionIncomplete = false,
  onRefresh,
  selectionMessage = defaultSelectionMessage,
  emptyMessage = defaultEmptyMessage,
  loadingMessage = defaultLoadingMessage,
  refreshLabel = defaultRefreshLabel,
  errorActionLabel = defaultErrorActionLabel
}) => {
  const { width } = useWindowDimensions()
  const isCompact = width < 768

  const showLoading = loading && rankedTeams.length === 0
  const showEmpty = !loading && rankedTeams.length === 0 && !error && !isSelectionIncomplete

  return (
    <Card
      padding="$4"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$borderColor"
      gap="$3"
    >
      <YStack gap="$1">
        <Text fontSize="$5" fontWeight="700" color="$color12">
          {leagueName ?? 'Lega non disponibile'}
        </Text>
        <Text fontSize="$3" color="$color10">
          {seasonLabel ? `Stagione ${seasonLabel}` : 'Seleziona una stagione disponibile'}
        </Text>
        <Text fontSize="$2" color="$color9">
          {roundLabel ?? 'Seleziona un girone disponibile'}
        </Text>
        <Text fontSize="$2" color="$color9">
          {teamCount && teamCount > 0
            ? `${teamCount} squadre in classifica`
            : 'Nessuna squadra trovata per questa combinazione'}
        </Text>
      </YStack>

      <Separator marginVertical="$2" />

      {isSelectionIncomplete ? (
        <YStack gap="$2" alignItems="center" paddingVertical="$4">
          <Text color="$color10" fontSize="$3" textAlign="center">
            {selectionMessage}
          </Text>
        </YStack>
      ) : error ? (
        <YStack gap="$3">
          <XStack alignItems="center" gap="$2">
            <AlertTriangle size="$1.5" color="$red10" />
            <Text color="$red11" fontWeight="600">
              {error}
            </Text>
          </XStack>
          {onRefresh && (
            <Button icon={RefreshCw} onPress={() => { onRefresh() }} variant="outlined">
              {errorActionLabel}
            </Button>
          )}
        </YStack>
      ) : showLoading ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <Spinner size="small" color="$blue10" />
          <Text color="$color10">{loadingMessage}</Text>
        </YStack>
      ) : showEmpty ? (
        <YStack gap="$2" alignItems="center" paddingVertical="$4">
          <Text color="$color10" fontSize="$3" textAlign="center">
            {emptyMessage}
          </Text>
          {onRefresh && (
            <Button icon={RefreshCw} onPress={() => { onRefresh() }} variant="outlined">
              {refreshLabel}
            </Button>
          )}
        </YStack>
      ) : (
        <YStack gap="$2">
          <XStack
            justifyContent="flex-start"
            alignItems="center"
            paddingHorizontal="$2"
            paddingBottom="$1"
            gap="$2"
          >
            <Text fontSize="$3" fontWeight="700" color="$color10" width={40} textAlign="center">
              Pos
            </Text>
            <Text
              fontSize="$3"
              fontWeight="700"
              color="$color10"
              flexShrink={0}
              flex={1}
              minWidth={isCompact ? 140 : 220}
              textAlign="left"
            >
              Squadra
            </Text>
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={isCompact}
              style={{ flexGrow: 1, flexShrink: 1, minWidth: 0 }}
              contentContainerStyle={{ alignItems: 'center' }}
            >
              <XStack gap="$3" alignItems="center" paddingRight="$2">
                <Text fontSize="$3" fontWeight="700" color="$color10" width={44} textAlign="center">
                  Pt
                </Text>
                <Text fontSize="$3" fontWeight="700" color="$color10" width={36} textAlign="center">
                  V
                </Text>
                <Text fontSize="$3" fontWeight="700" color="$color10" width={36} textAlign="center">
                  N
                </Text>
                <Text fontSize="$3" fontWeight="700" color="$color10" width={36} textAlign="center">
                  P
                </Text>
                <Text fontSize="$3" fontWeight="700" color="$color10" width={48} textAlign="center">
                  Diff
                </Text>
                <Text fontSize="$3" fontWeight="700" color="$color10" width={56} textAlign="center">
                  Pt/G
                </Text>
              </XStack>
            </ScrollView>
          </XStack>

          <Separator />

          <YStack gap="$1">
            {rankedTeams.map((team, index) => {
              const goalDiff = team.goalDifference ?? team.goal - team.sufferedGoal
              const position = team.position ?? '-'
              const pointsPerGame = Number.isFinite(team.pointsPerGame)
                ? team.pointsPerGame.toFixed(2)
                : '-'
              return (
                <XStack
                  key={`${team.owner}-${team.name}-${index}`}
                  justifyContent="flex-start"
                  alignItems="center"
                  paddingVertical="$1.5"
                  paddingHorizontal="$2"
                  backgroundColor={index % 2 === 0 ? '$color1' : 'transparent'}
                  borderRadius="$2"
                  gap="$2"
                >
                  <Text fontSize="$3" fontWeight="700" color="$color9" width={40} textAlign="center">
                    {position}
                  </Text>
                  <YStack
                    flexShrink={0}
                    flex={1}
                    marginLeft="$2"
                    gap="$1"
                    minWidth={isCompact ? 160 : 240}
                  >
                    <Text fontSize="$3" fontWeight="700" color="$color12" numberOfLines={1}>
                      {team.name}
                    </Text>
                    <Text fontSize="$2" color="$color10" numberOfLines={1}>
                      Allenatore: {team.owner}
                    </Text>
                  </YStack>
                  <ScrollView
                    horizontal
                    showsHorizontalScrollIndicator={isCompact}
                    style={{ flexGrow: 1, flexShrink: 1, minWidth: 0 }}
                    contentContainerStyle={{ alignItems: 'center' }}
                  >
                    <XStack gap="$3" alignItems="center" paddingRight="$2">
                      <Text fontSize="$3" fontWeight="700" color="$color12" width={44} textAlign="center">
                        {team.point}
                      </Text>
                      <Text fontSize="$3" color="$color11" width={36} textAlign="center">
                        {team.victories}
                      </Text>
                      <Text fontSize="$3" color="$color11" width={36} textAlign="center">
                        {team.draws}
                      </Text>
                      <Text fontSize="$3" color="$color11" width={36} textAlign="center">
                        {team.defeats}
                      </Text>
                      <Text
                        fontSize="$3"
                        color={goalDiff >= 0 ? '$color10' : '$red10'}
                        width={48}
                        textAlign="center"
                      >
                        {goalDiff >= 0 ? `+${goalDiff}` : goalDiff}
                      </Text>
                      <Text fontSize="$3" color="$color11" width={56} textAlign="center">
                        {pointsPerGame}
                      </Text>
                    </XStack>
                  </ScrollView>
                </XStack>
              )
            })}
          </YStack>

          {onRefresh && (
            <>
              <Separator marginTop="$2" />
              <XStack justifyContent="flex-end">
                <Button icon={RefreshCw} size="$2" variant="outlined" onPress={() => { onRefresh() }}>
                  {refreshLabel}
                </Button>
              </XStack>
            </>
          )}
        </YStack>
      )}
    </Card>
  )
}
