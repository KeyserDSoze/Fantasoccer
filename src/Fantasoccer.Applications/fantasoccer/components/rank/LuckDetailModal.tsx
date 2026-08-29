import { type FC } from 'react'
import { AlertCircle, TrendingDown, TrendingUp } from '@tamagui/lucide-icons-2'
import { Button, Card, Dialog, ScrollView, Separator, Text, XStack, YStack } from 'tamagui'
import type { TeamLuck, LuckEvent } from 'types/rank'
import { LuckCalculator } from 'utils/luckCalculator'

export interface LuckDetailModalProps {
  teamLuck: TeamLuck | null
  open: boolean
  onClose: () => void
}

const LuckEventIcon: FC<{ points: number }> = ({ points }) => {
  if (points > 0) {
    return <TrendingUp size={16} color="$green10" />
  }
  if (points < 0) {
    return <TrendingDown size={16} color="$red10" />
  }
  return <AlertCircle size={16} color="$color9" />
}

const LuckEventCard: FC<{ event: LuckEvent }> = ({ event }) => {
  const isPositive = event.points > 0
  const isNegative = event.points < 0

  return (
    <Card
      padding="$3"
      backgroundColor={isPositive ? '$green2' : isNegative ? '$red2' : '$color2'}
      borderRadius="$3"
      borderWidth="$0.5"
      borderColor={isPositive ? '$green7' : isNegative ? '$red7' : '$borderColor'}
    >
      <YStack gap="$2">
        <XStack justifyContent="space-between" alignItems="flex-start" gap="$2">
          <YStack gap="$1" flex={1}>
            <XStack gap="$2" alignItems="center">
              <LuckEventIcon points={event.points} />
              <Text fontSize="$2" fontWeight="600" color="$color11">
                Giornata {event.gameDay}
              </Text>
            </XStack>
            <Text fontSize="$3" fontWeight="700" color="$color12">
              vs {event.opponent}
            </Text>
          </YStack>
          <YStack alignItems="flex-end" gap="$1">
            <Text
              fontSize="$4"
              fontWeight="700"
              color={LuckCalculator.getLuckColor(event.points)}
            >
              {LuckCalculator.formatLuckPoints(event.points)}
            </Text>
            <Text fontSize="$2" color="$color10">
              {event.result === 'win' ? 'Vittoria' : event.result === 'draw' ? 'Pareggio' : 'Sconfitta'}
            </Text>
          </YStack>
        </XStack>

        <Text fontSize="$2" color="$color11">
          {event.detail}
        </Text>

        <XStack gap="$2" justifyContent="center">
          <Text fontSize="$2" color="$color9">
            {event.myScore.toFixed(1)} - {event.opponentScore.toFixed(1)}
          </Text>
        </XStack>
      </YStack>
    </Card>
  )
}

export const LuckDetailModal: FC<LuckDetailModalProps> = ({ teamLuck, open, onClose }) => {
  if (!teamLuck) return null

  const sortedEvents = [...teamLuck.events].sort((a, b) => b.gameDay - a.gameDay)
  const positiveEvents = sortedEvents.filter(e => e.points > 0)
  const negativeEvents = sortedEvents.filter(e => e.points < 0)
  const neutralEvents = sortedEvents.filter(e => e.points === 0)

  return (
    <Dialog open={open} onOpenChange={(isOpen) => { if (!isOpen) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay
          key="overlay"
          transition="quick"
          opacity={0.5}
          enterStyle={{ opacity: 0 }}
          exitStyle={{ opacity: 0 }}
        />
        <Dialog.Content
          bordered
          elevation="$2"
          key="content"
          transition={[
            'quick',
            {
              opacity: {
                overshootClamping: true,
              },
            },
          ]}
          enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }}
          exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }}
          gap="$4"
          maxWidth={600}
          maxHeight="80vh"
        >
          <Dialog.Title fontSize="$6" fontWeight="700">
            Dettaglio Fortuna: {teamLuck.name}
          </Dialog.Title>

          <YStack gap="$3">
            {/* Summary */}
            <Card padding="$3" backgroundColor="$color2" borderRadius="$4">
              <YStack gap="$2">
                <XStack justifyContent="space-between" alignItems="center">
                  <Text fontSize="$3" color="$color11">
                    Fortuna totale
                  </Text>
                  <Text
                    fontSize="$6"
                    fontWeight="700"
                    color={LuckCalculator.getLuckColor(teamLuck.totalLuck)}
                  >
                    {LuckCalculator.formatLuckPoints(teamLuck.totalLuck)}
                  </Text>
                </XStack>
                <Separator />
                <XStack justifyContent="space-between" alignItems="center">
                  <Text fontSize="$3" color="$color11">
                    Fortuna media per partita
                  </Text>
                  <Text
                    fontSize="$5"
                    fontWeight="600"
                    color={LuckCalculator.getLuckColor(teamLuck.avgLuck)}
                  >
                    {LuckCalculator.formatLuckPoints(teamLuck.avgLuck)}
                  </Text>
                </XStack>
                <XStack justifyContent="space-between" alignItems="center">
                  <Text fontSize="$2" color="$color10">
                    Partite giocate
                  </Text>
                  <Text fontSize="$3" fontWeight="600" color="$color11">
                    {teamLuck.gamesPlayed}
                  </Text>
                </XStack>
              </YStack>
            </Card>

            {/* Statistics */}
            <XStack gap="$2" flexWrap="wrap">
              <Card flex={1} minWidth={120} padding="$2" backgroundColor="$green2">
                <YStack gap="$1" alignItems="center">
                  <Text fontSize="$1" color="$green11">
                    Eventi positivi
                  </Text>
                  <Text fontSize="$5" fontWeight="700" color="$green11">
                    {positiveEvents.length}
                  </Text>
                </YStack>
              </Card>
              <Card flex={1} minWidth={120} padding="$2" backgroundColor="$red2">
                <YStack gap="$1" alignItems="center">
                  <Text fontSize="$1" color="$red11">
                    Eventi negativi
                  </Text>
                  <Text fontSize="$5" fontWeight="700" color="$red11">
                    {negativeEvents.length}
                  </Text>
                </YStack>
              </Card>
            </XStack>

            <Separator />

            {/* Events list */}
            <YStack gap="$2">
              <Text fontSize="$4" fontWeight="600" color="$color12">
                Eventi di fortuna ({sortedEvents.length})
              </Text>
              <ScrollView maxHeight={400}>
                <YStack gap="$2" paddingRight="$2">
                  {sortedEvents.length === 0 ? (
                    <Card padding="$4" backgroundColor="$color2">
                      <Text textAlign="center" color="$color10">
                        Nessun evento di fortuna registrato
                      </Text>
                    </Card>
                  ) : (
                    sortedEvents.map((event, index) => (
                      <LuckEventCard key={`${event.gameId}-${index}`} event={event} />
                    ))
                  )}
                </YStack>
              </ScrollView>
            </YStack>
          </YStack>

          <XStack gap="$2" justifyContent="flex-end">
            <Dialog.Close asChild>
              <Button variant="outlined" onPress={onClose}>
                Chiudi
              </Button>
            </Dialog.Close>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
