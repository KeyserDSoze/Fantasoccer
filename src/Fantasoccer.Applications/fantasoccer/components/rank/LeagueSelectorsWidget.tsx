import { type FC, type ReactNode, useMemo } from 'react'
import { Button, Card, Text, XStack, YStack } from 'tamagui'

type LeagueOption = {
  id: string
  name: string
  isMain?: boolean
}

type RoundOption = {
  id: string
  label: string
  isDefault?: boolean
}

type FormatYearLabel = (year: number) => string

type LeagueSelectorsWidgetProps = {
  leagues: LeagueOption[]
  selectedLeagueId: string | null
  onSelectLeague?: (leagueId: string) => void
  years?: number[]
  selectedYear?: number | null
  onSelectYear?: (year: number) => void
  formatYearLabel?: FormatYearLabel
  title?: string
  leagueHint?: string
  yearHint?: string
  rounds?: RoundOption[]
  selectedRoundId?: string | null
  onSelectRound?: (roundId: string) => void
  roundHint?: string
  emptyLeagueLabel?: string
  showLeagueSection?: boolean
  showYearSection?: boolean
  showRoundSection?: boolean
  footer?: ReactNode
}

const defaultFormatYear: FormatYearLabel = (year) => String(year)

export const LeagueSelectorsWidget: FC<LeagueSelectorsWidgetProps> = ({
  leagues,
  selectedLeagueId,
  onSelectLeague,
  years = [],
  selectedYear = null,
  onSelectYear,
  formatYearLabel = defaultFormatYear,
  title = 'Selettori lega',
  leagueHint = 'Scegli la lega da visualizzare',
  yearHint = 'Seleziona la stagione',
  rounds = [],
  selectedRoundId = null,
  onSelectRound,
  roundHint = 'Scegli il girone o la classifica da visualizzare',
  emptyLeagueLabel = 'Nessuna lega disponibile',
  showLeagueSection = true,
  showYearSection = true,
  showRoundSection = true,
  footer
}) => {
  const selectedLeague = useMemo(
    () => leagues.find((league) => league.id === selectedLeagueId) ?? null,
    [leagues, selectedLeagueId]
  )

  const canSelectLeague = showLeagueSection && Boolean(onSelectLeague) && leagues.length > 1
  const canSelectYear = showYearSection && Boolean(onSelectYear) && years.length > 1
  const canSelectRound = showRoundSection && Boolean(onSelectRound) && rounds.length > 1

  return (
    <Card
      padding="$4"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$borderColor"
      gap="$3"
    >
      <Text fontSize="$4" fontWeight="700" color="$color11">
        {title}
      </Text>

      {showLeagueSection && (
        canSelectLeague ? (
          <YStack gap="$2">
            <Text fontSize="$3" color="$color10">
              {leagueHint}
            </Text>
            <XStack gap="$2" flexWrap="wrap">
              {leagues.map((league) => {
                const isSelected = league.id === selectedLeagueId
                return (
                  <Button
                    key={league.id}
                    size="$2"
                    variant="outlined"
                    borderColor={isSelected ? '$blue8' : '$color6'}
                    backgroundColor={isSelected ? '$blue2' : 'transparent'}
                    onPress={() => {
                      if (!isSelected) {
                        onSelectLeague?.(league.id)
                      }
                    }}
                  >
                    <Text color={isSelected ? '$blue11' : '$color11'} fontSize="$3" fontWeight="600">
                      {league.name}
                      {league.isMain ? ' • Principale' : ''}
                    </Text>
                  </Button>
                )
              })}
            </XStack>
          </YStack>
        ) : (
          <Text color="$color10" fontSize="$3">
            Lega corrente: {selectedLeague?.name ?? emptyLeagueLabel}
          </Text>
        )
      )}

      {showYearSection && (
        canSelectYear ? (
          <YStack gap="$2">
            <Text fontSize="$3" color="$color10">
              {yearHint}
            </Text>
            <XStack gap="$2" flexWrap="wrap">
              {years.map((year) => {
                const isSelected = year === selectedYear
                return (
                  <Button
                    key={year}
                    size="$2"
                    variant="outlined"
                    borderColor={isSelected ? '$green8' : '$color6'}
                    backgroundColor={isSelected ? '$green2' : 'transparent'}
                    onPress={() => {
                      if (!isSelected) {
                        onSelectYear?.(year)
                      }
                    }}
                  >
                    <Text color={isSelected ? '$green11' : '$color11'} fontSize="$3" fontWeight="600">
                      {formatYearLabel(year)}
                    </Text>
                  </Button>
                )
              })}
            </XStack>
          </YStack>
        ) : (
          <Text color="$color10" fontSize="$3">
            {selectedYear != null ? `Stagione ${formatYearLabel(selectedYear)}` : 'Nessuna stagione disponibile'}
          </Text>
        )
      )}

      {showRoundSection && (
        canSelectRound ? (
          <YStack gap="$2">
            <Text fontSize="$3" color="$color10">
              {roundHint}
            </Text>
            <XStack gap="$2" flexWrap="wrap">
              {rounds.map((round) => {
                const isSelected = round.id === selectedRoundId
                return (
                  <Button
                    key={round.id}
                    size="$2"
                    variant="outlined"
                    borderColor={isSelected ? '$purple8' : '$color6'}
                    backgroundColor={isSelected ? '$purple2' : 'transparent'}
                    onPress={() => {
                      if (!isSelected) {
                        onSelectRound?.(round.id)
                      }
                    }}
                  >
                    <Text color={isSelected ? '$purple11' : '$color11'} fontSize="$3" fontWeight="600">
                      {round.label}
                      {round.isDefault ? ' • Default' : ''}
                    </Text>
                  </Button>
                )
              })}
            </XStack>
          </YStack>
        ) : rounds.length > 0 ? (
          <Text color="$color10" fontSize="$3">
            {selectedRoundId != null
              ? `Girone selezionato: ${rounds.find((round) => round.id === selectedRoundId)?.label ?? selectedRoundId}`
              : 'Nessun girone selezionato'}
          </Text>
        ) : null
      )}

      {footer}
    </Card>
  )
}

export type { LeagueSelectorsWidgetProps, LeagueOption, RoundOption }
export type { FormatYearLabel }
