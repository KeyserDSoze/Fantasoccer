import { type FC, useEffect, useState } from 'react'
import { AlertTriangle, RefreshCw, Info } from '@tamagui/lucide-icons-2'
import { Button, Card, ScrollView, Separator, Spinner, Text, XStack, YStack } from 'tamagui'
import { useWindowDimensions, Platform } from 'react-native'
import type { EnhancedRankedTeam, TeamLuck } from 'types/rank'
import type { Calendar } from 'types/calendar'
import { TeamLogo } from '../team/TeamLogo'
import { LuckDetailModal } from './LuckDetailModal'
import { LuckCalculator } from 'utils/luckCalculator'

// Aggiungi stili CSS per scrollbar personalizzata (solo web)
if (Platform.OS === 'web') {
  const styleId = 'custom-scrollbar-styles'
  if (!document.getElementById(styleId)) {
    const style = document.createElement('style')
    style.id = styleId
    style.textContent = `
      .custom-horizontal-scrollbar {
        scrollbar-width: thin;
        scrollbar-color: rgba(100, 150, 200, 0.6) rgba(100, 150, 200, 0.1);
      }
      .custom-horizontal-scrollbar::-webkit-scrollbar {
        height: 10px;
      }
      .custom-horizontal-scrollbar::-webkit-scrollbar-track {
        background: rgba(100, 150, 200, 0.1);
        border-radius: 5px;
        margin: 0 4px;
      }
      .custom-horizontal-scrollbar::-webkit-scrollbar-thumb {
        background: rgba(100, 150, 200, 0.6);
        border-radius: 5px;
      }
      .custom-horizontal-scrollbar::-webkit-scrollbar-thumb:hover {
        background: rgba(100, 150, 200, 0.8);
      }
    `
    document.head.appendChild(style)
  }
}

export type LeagueRankingWidgetProps = {
  leagueName?: string | null
  seasonLabel?: string | null
  roundLabel?: string | null
  roundId?: string | null // ID del girone per calcolare la fortuna
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
  groupId?: string | null // Per il logo della squadra
  year?: number | null // Per il logo della squadra
  moneyForGoal?: number // Soldi per goal fatto
  moneyForSufferedGoal?: number // Soldi per goal subito
  calendar?: Calendar | null // Calendario per calcolare il parametro fortuna
}

const defaultSelectionMessage = 'Seleziona lega, stagione e girone per visualizzare la classifica.'
const defaultEmptyMessage = 'Non ci sono dati di classifica per i filtri selezionati.'
const defaultLoadingMessage = 'Caricamento classifica per il girone selezionato...'
const defaultRefreshLabel = 'Aggiorna classifica'
const defaultErrorActionLabel = 'Riprova'

type StatCellValue = {
  value: string
  color?: string
  fontWeight?: 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900
}

type StatColumnDefinition = {
  key: string
  label: string
  width: number
  render: (team: EnhancedRankedTeam) => StatCellValue
}

const createStatColumns = (
  isCompact: boolean, 
  moneyForGoal: number = 0, 
  moneyForSufferedGoal: number = 0,
  teamLuckMap: Map<string, TeamLuck> | null = null
) => {
  const columns: StatColumnDefinition[] = [
    {
      key: 'points',
      label: 'Pt',
      width: isCompact ? 48 : 56,
      render: (team) => ({
        value: `${team.point ?? 0}`,
        color: '$color12',
        fontWeight: 700,
      }),
    },
    {
      key: 'victories',
      label: 'V',
      width: 36,
      render: (team) => ({
        value: `${team.victories ?? 0}`,
      }),
    },
    {
      key: 'draws',
      label: 'N',
      width: 36,
      render: (team) => ({
        value: `${team.draws ?? 0}`,
      }),
    },
    {
      key: 'defeats',
      label: 'P',
      width: 36,
      render: (team) => ({
        value: `${team.defeats ?? 0}`,
      }),
    },
    {
      key: 'goalsFor',
      label: 'GF',
      width: 40,
      render: (team) => ({
        value: `${team.goal ?? 0}`,
        color: '$green10',
      }),
    },
    {
      key: 'goalsAgainst',
      label: 'GS',
      width: 40,
      render: (team) => ({
        value: `${team.sufferedGoal ?? 0}`,
        color: '$red10',
      }),
    },
    {
      key: 'goalDifference',
      label: 'Diff',
      width: isCompact ? 52 : 56,
      render: (team) => {
        const goalDiff =
          team.goalDifference ?? (team.goal ?? 0) - (team.sufferedGoal ?? 0)
        const formatted = goalDiff > 0 ? `+${goalDiff}` : `${goalDiff}`

        return {
          value: formatted,
          color: goalDiff >= 0 ? '$color10' : '$red10',
        }
      },
    },
    {
      key: 'valuePoints',
      label: 'PF',
      width: isCompact ? 48 : 56,
      render: (team) => ({
        value: `${team.valuePoint ?? 0}`,
        color: '$blue10',
      }),
    },
    {
      key: 'sufferedValuePoints',
      label: 'PS',
      width: isCompact ? 48 : 56,
      render: (team) => ({
        value: `${team.sufferedValuePoint ?? 0}`,
        color: '$orange10',
      }),
    },
    {
      key: 'avgValuePointsPerGame',
      label: 'PF/G',
      width: isCompact ? 56 : 64,
      render: (team) => {
        const totalGames = (team.victories ?? 0) + (team.draws ?? 0) + (team.defeats ?? 0)
        const avgPoints = totalGames > 0 ? (team.valuePoint ?? 0) / totalGames : 0
        
        return {
          value: avgPoints > 0 ? avgPoints.toFixed(1) : '-',
          color: '$blue10',
        }
      },
    },
    {
      key: 'avgSufferedValuePointsPerGame',
      label: 'PS/G',
      width: isCompact ? 56 : 64,
      render: (team) => {
        const totalGames = (team.victories ?? 0) + (team.draws ?? 0) + (team.defeats ?? 0)
        const avgSuffered = totalGames > 0 ? (team.sufferedValuePoint ?? 0) / totalGames : 0
        
        return {
          value: avgSuffered > 0 ? avgSuffered.toFixed(1) : '-',
          color: '$orange10',
        }
      },
    },
    {
      key: 'goalMoney',
      label: '+$',
      width: isCompact ? 64 : 72,
      render: (team) => {
        const goalsMade = team.goal ?? 0
        const goalsSuffered = team.sufferedGoal ?? 0
        const totalMoney = (goalsMade * moneyForGoal) + (goalsSuffered * moneyForSufferedGoal)
        
        return {
          value: `$${totalMoney}`,
          color: totalMoney >= 0 ? '$green10' : '$red10',
        }
      },
    },
    {
      key: 'pointsPerGame',
      label: 'Pt/G',
      width: isCompact ? 64 : 72,
      render: (team) => {
        const value =
          typeof team.pointsPerGame === 'number' && Number.isFinite(team.pointsPerGame)
            ? team.pointsPerGame.toFixed(2)
            : '-'

        return {
          value,
        }
      },
    },
  ]

  // Add luck column if data is available
  if (teamLuckMap) {
    columns.push({
      key: 'luck',
      label: '🍀',
      width: isCompact ? 56 : 64,
      render: (team) => {
        const teamLuck = teamLuckMap.get(team.owner)
        if (!teamLuck) {
          return { value: '-', color: '$color9' }
        }

        return {
          value: LuckCalculator.formatLuck(teamLuck.avgLuck),
          color: LuckCalculator.getLuckColor(teamLuck.avgLuck),
          fontWeight: 600,
        }
      },
    })
  }

  const gapBetweenColumns = 12
  const minWidth = columns.reduce(
    (acc, column, index) =>
      acc + column.width + (index === columns.length - 1 ? 0 : gapBetweenColumns),
    0,
  )

  return { columns, minWidth }
}

export const LeagueRankingWidget: FC<LeagueRankingWidgetProps> = ({
  leagueName,
  seasonLabel,
  roundLabel,
  roundId,
  teamCount,
  rankedTeams,
  loading,
  error,
  isSelectionIncomplete,
  onRefresh,
  selectionMessage = defaultSelectionMessage,
  emptyMessage = defaultEmptyMessage,
  loadingMessage = defaultLoadingMessage,
  refreshLabel = defaultRefreshLabel,
  errorActionLabel = defaultErrorActionLabel,
  groupId,
  year,
  moneyForGoal = 0,
  moneyForSufferedGoal = 0,
  calendar = null,
}) => {
  const { width } = useWindowDimensions()
  const isCompact = width < 600
  const [selectedTeamLuck, setSelectedTeamLuck] = useState<TeamLuck | null>(null)
  const [luckModalOpen, setLuckModalOpen] = useState(false)

  // Calculate team luck from calendar if available
  const teamLuckMap = calendar && roundId 
    ? LuckCalculator.calculateTeamLuck(calendar, roundId)
    : null

  const handleLuckClick = (team: EnhancedRankedTeam) => {
    if (!teamLuckMap) return
    
    const teamLuck = teamLuckMap.get(team.owner)
    if (teamLuck) {
      setSelectedTeamLuck(teamLuck)
      setLuckModalOpen(true)
    }
  }

  const showSelectionIncomplete = Boolean(isSelectionIncomplete)
  const showError = Boolean(error)
  const showLoading = Boolean(loading)
  const showEmpty =
    !showSelectionIncomplete && !showError && !showLoading && rankedTeams.length === 0

  const { columns: statColumns, minWidth: statsMinWidth} = createStatColumns(
    isCompact, 
    moneyForGoal, 
    moneyForSufferedGoal,
    teamLuckMap
  )

  // Dimensioni aggiornate per fare spazio al logo
  const logoSize = isCompact ? 32 : 36
  const positionColumnWidth = isCompact ? 0 : 40 // Nascosto su mobile
  const teamColumnWidth = isCompact ? 140 : 200 // Ridotto per fare spazio
  const leftColumnWidth = positionColumnWidth + logoSize + teamColumnWidth + (isCompact ? 8 : 16) + 8 // +8 per gap tra logo e testo
  const rowMinHeight = isCompact ? 68 : 72

  return (
    <Card padding="$3" elevation="$2" backgroundColor="$color1" gap="$3">
      <YStack gap="$1">
        {leagueName && (
          <Text fontSize="$5" fontWeight="700" color="$color12">
            {leagueName}
          </Text>
        )}
        <XStack gap="$2" flexWrap="wrap">
          {seasonLabel && (
            <Text fontSize="$2" color="$color10">
              Stagione: {seasonLabel}
            </Text>
          )}
          {roundLabel && (
            <Text fontSize="$2" color="$color10">
              Girone: {roundLabel}
            </Text>
          )}
          {typeof teamCount === 'number' && Number.isFinite(teamCount) && (
            <Text fontSize="$2" color="$color10">
              Squadre: {teamCount}
            </Text>
          )}
        </XStack>
      </YStack>

      {showSelectionIncomplete ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <AlertTriangle size={32} color="$yellow10" />
          <Text color="$color10" textAlign="center">
            {selectionMessage}
          </Text>
        </YStack>
      ) : showError ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <AlertTriangle size={32} color="$red10" />
          <Text color="$color10" textAlign="center">
            {error}
          </Text>
          {onRefresh && (
            <Button onPress={() => { onRefresh() }} icon={RefreshCw} variant="outlined">
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
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
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
          <XStack alignItems="stretch" gap="$2">
            <YStack flexShrink={0} width={leftColumnWidth}>
              <XStack
                alignItems="center"
                gap="$2"
                paddingHorizontal="$2"
                paddingBottom="$1"
              >
                {!isCompact && (
                  <Text
                    fontSize="$3"
                    fontWeight="700"
                    color="$color10"
                    width={positionColumnWidth}
                    textAlign="center"
                  >
                    Pos
                  </Text>
                )}
                <Text
                  fontSize="$3"
                  fontWeight="700"
                  color="$color10"
                  width={logoSize}
                  textAlign="center"
                >
                  {/* Spazio per logo */}
                </Text>
                <Text
                  fontSize="$3"
                  fontWeight="700"
                  color="$color10"
                  width={teamColumnWidth}
                >
                  Squadra
                </Text>
              </XStack>

              <Separator />

              <YStack gap="$1">
                {rankedTeams.map((team, index) => {
                  const position = team.position ?? index + 1

                  return (
                    <XStack
                      key={`left-${team.owner}-${team.name}-${index}`}
                      alignItems="center"
                      gap="$2"
                      paddingHorizontal="$2"
                      paddingVertical="$1.5"
                      backgroundColor={index % 2 === 0 ? '$color1' : 'transparent'}
                      borderRadius="$2"
                      minHeight={rowMinHeight}
                    >
                      {!isCompact && (
                        <Text
                          fontSize="$3"
                          fontWeight="700"
                          color="$color9"
                          width={positionColumnWidth}
                          textAlign="center"
                        >
                          {position}
                        </Text>
                      )}
                      {groupId && year && (
                        <TeamLogo
                          groupId={groupId}
                          year={year}
                          ownerEmail={team.owner}
                          size={logoSize}
                        />
                      )}
                      <YStack width={teamColumnWidth} gap="$1">
                        <Text fontSize="$3" fontWeight="700" color="$color12" numberOfLines={1}>
                          {team.name}
                        </Text>
                      </YStack>
                    </XStack>
                  )
                })}
              </YStack>
            </YStack>

            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={true}
              contentContainerStyle={{ paddingRight: 16 }}
              // @ts-ignore - className is web-only
              className={Platform.OS === 'web' ? 'custom-horizontal-scrollbar' : undefined}
            >
              <YStack minWidth={statsMinWidth} flexShrink={0}>
                <XStack
                  alignItems="center"
                  gap="$3"
                  paddingHorizontal="$2"
                  paddingBottom="$1"
                >
                  {statColumns.map((column) => (
                    <Text
                      key={`header-${column.key}`}
                      fontSize="$3"
                      fontWeight="700"
                      color="$color10"
                      width={column.width}
                      textAlign="center"
                    >
                      {column.label}
                    </Text>
                  ))}
                </XStack>

                <Separator />

                <YStack gap="$1">
                  {rankedTeams.map((team, index) => (
                    <XStack
                      key={`right-${team.owner}-${team.name}-${index}`}
                      alignItems="center"
                      gap="$3"
                      paddingHorizontal="$2"
                      paddingVertical="$1.5"
                      backgroundColor={index % 2 === 0 ? '$color1' : 'transparent'}
                      borderRadius="$2"
                      minHeight={rowMinHeight}
                    >
                      {statColumns.map((column) => {
                        const { value, color, fontWeight } = column.render(team)
                        const isLuckColumn = column.key === 'luck'
                        const hasLuckData = isLuckColumn && teamLuckMap?.has(team.owner)
                        
                        if (isLuckColumn && hasLuckData) {
                          return (
                            <Button
                              key={`${column.key}-${team.owner}-${team.name}-${index}`}
                              size="$2"
                              chromeless
                              padding="$0"
                              width={column.width}
                              onPress={() => { handleLuckClick(team) }}
                              cursor="pointer"
                            >
                              <XStack gap="$1" alignItems="center" justifyContent="center">
                                <Text
                                  fontSize="$3"
                                  color={color ?? '$color11'}
                                  fontWeight={fontWeight}
                                  textAlign="center"
                                >
                                  {value}
                                </Text>
                                <Info size={12} color={color ?? '$color11'} />
                              </XStack>
                            </Button>
                          )
                        }
                        
                        return (
                          <Text
                            key={`${column.key}-${team.owner}-${team.name}-${index}`}
                            fontSize="$3"
                            color={color ?? '$color11'}
                            fontWeight={fontWeight}
                            width={column.width}
                            textAlign="center"
                          >
                            {value}
                          </Text>
                        )
                      })}
                    </XStack>
                  ))}
                </YStack>
              </YStack>
            </ScrollView>
          </XStack>

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
      
      {/* Luck Detail Modal */}
      <LuckDetailModal
        teamLuck={selectedTeamLuck}
        open={luckModalOpen}
        onClose={() => { setLuckModalOpen(false) }}
      />
    </Card>
  )
}
