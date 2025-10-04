import React, { useMemo, useState } from 'react'
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
  ScrollView,
  useMedia,
  Button
} from 'tamagui'
import { useAllLogs } from 'hooks/useFantasoccerLog'
import { FantasoccerLog, formatLogTimestamp } from 'types/log'
import { 
  FileText, 
  AlertCircle, 
  Info, 
  Clock,
  RefreshCw,
  ChevronDown,
  ChevronUp
} from '@tamagui/lucide-icons'

export default function LogsScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const { logs, loading, error, refetch, statistics } = useAllLogs({
    refreshInterval: 60000 // Auto-refresh ogni minuto
  })

  const [expandedJobs, setExpandedJobs] = useState<Set<string>>(new Set())

  // Separa log per tipo (ALog vs AError) e raggruppa per job
  const { logsByJob, errorsByJob, jobNames } = useMemo(() => {
    const logsMap = new Map<string, FantasoccerLog[]>()
    const errorsMap = new Map<string, FantasoccerLog[]>()
    const jobs = new Set<string>()

    logs.forEach(log => {
      // Validazione dati
      if (!log.primaryKey || !log.secondaryKey) {
        console.warn('Log entry missing required keys:', log)
        return
      }

      const partitionKey = log.primaryKey
      const rowKey = log.secondaryKey
      
      // Estrai il nome del job dalla rowKey
      // Formato: {PartitionKey}_-_{JobName}_{timestamp} oppure {JobName}_{timestamp}
      // Es: "ALog_-_AllPlayersAndAllTeamsJob_22" oppure "LiveJob_22"
      let jobName = 'Unknown'
      
      if (rowKey.includes('_-_')) {
        // Formato completo: "ALog_-_AllPlayersAndAllTeamsJob_22"
        const parts = rowKey.split('_-_')
        if (parts.length > 1) {
          // Prendi la seconda parte e rimuovi il timestamp
          const jobPart = parts[1]
          const jobParts = jobPart.split('_')
          // Rimuovi l'ultimo elemento (timestamp) e ricostruisci il nome
          jobName = jobParts.slice(0, -1).join('_') || jobPart
        }
      } else if (rowKey.includes('_')) {
        // Formato semplice: "LiveJob_22"
        const parts = rowKey.split('_')
        // Rimuovi l'ultimo elemento (timestamp) e ricostruisci il nome
        jobName = parts.slice(0, -1).join('_') || parts[0]
      } else {
        jobName = rowKey
      }

      jobs.add(jobName)

      // Separa per tipo (ALog o AError)
      if (partitionKey === 'ALog') {
        if (!logsMap.has(jobName)) {
          logsMap.set(jobName, [])
        }
        logsMap.get(jobName)!.push(log)
      } else if (partitionKey === 'AError') {
        if (!errorsMap.has(jobName)) {
          errorsMap.set(jobName, [])
        }
        errorsMap.get(jobName)!.push(log)
      }
    })

    // Ordina i job alfabeticamente
    const sortedJobNames = Array.from(jobs).sort()

    return {
      logsByJob: logsMap,
      errorsByJob: errorsMap,
      jobNames: sortedJobNames
    }
  }, [logs])

  // Statistiche per job
  const jobStatistics = useMemo(() => {
    const stats = new Map<string, { logs: number; errors: number }>()
    
    jobNames.forEach(jobName => {
      stats.set(jobName, {
        logs: logsByJob.get(jobName)?.length || 0,
        errors: errorsByJob.get(jobName)?.length || 0
      })
    })
    
    return stats
  }, [jobNames, logsByJob, errorsByJob])

  const toggleJobExpanded = (jobName: string) => {
    setExpandedJobs(prev => {
      const next = new Set(prev)
      if (next.has(jobName)) {
        next.delete(jobName)
      } else {
        next.add(jobName)
      }
      return next
    })
  }

  const expandAll = () => {
    setExpandedJobs(new Set(jobNames))
  }

  const collapseAll = () => {
    setExpandedJobs(new Set())
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/logs.tsx" title="Platform Logs">
        <YStack
          width={containerWidth}
          maxWidth={1200}
          gap="$4"
          alignItems="stretch"
          alignSelf="center"
        >
          {/* Header con info */}
          <Card
            padding="$4"
            backgroundColor="$purple3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$purple7"
          >
            <XStack alignItems="center" gap="$3" marginBottom="$2">
              <FileText size="$2" color="$purple10" />
              <H3 color="$purple11">Log di Piattaforma</H3>
            </XStack>
            <Text fontSize="$3" color="$purple10">
              Log e errori delle ultime 24 ore raggruppati per job.
            </Text>
          </Card>

          {/* Statistiche generali */}
          <XStack gap="$3" flexWrap="wrap">
            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
              <XStack alignItems="center" gap="$2" marginBottom="$1">
                <FileText size="$1" color="$blue10" />
                <Text fontSize="$2" color="$color11">Totale Log</Text>
              </XStack>
              <Text fontSize="$7" fontWeight="bold" color="$blue10">
                {statistics?.info || 0}
              </Text>
            </Card>

            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
              <XStack alignItems="center" gap="$2" marginBottom="$1">
                <AlertCircle size="$1" color="$red10" />
                <Text fontSize="$2" color="$color11">Totale Errori</Text>
              </XStack>
              <Text fontSize="$7" fontWeight="bold" color="$red10">
                {statistics?.errors || 0}
              </Text>
            </Card>

            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
              <XStack alignItems="center" gap="$2" marginBottom="$1">
                <Clock size="$1" color="$green10" />
                <Text fontSize="$2" color="$color11">Jobs Attivi</Text>
              </XStack>
              <Text fontSize="$7" fontWeight="bold" color="$green10">
                {jobNames.length}
              </Text>
            </Card>
          </XStack>

          {/* Azioni rapide */}
          <XStack gap="$2" justifyContent="space-between" alignItems="center" flexWrap="wrap">
            <XStack gap="$2">
              <Button
                size="$3"
                variant="outlined"
                onPress={expandAll}
                icon={<ChevronDown size="$1" />}
              >
                Espandi Tutti
              </Button>
              <Button
                size="$3"
                variant="outlined"
                onPress={collapseAll}
                icon={<ChevronUp size="$1" />}
              >
                Chiudi Tutti
              </Button>
            </XStack>
            <Button
              size="$3"
              onPress={() => refetch()}
              icon={<RefreshCw size="$1" />}
              disabled={loading}
            >
              Aggiorna
            </Button>
          </XStack>

          {/* Lista logs */}
          {loading && logs.length === 0 ? (
            <Card padding="$6" alignItems="center" gap="$3">
              <Spinner size="large" color="$purple10" />
              <Text color="$color10">Caricamento log...</Text>
            </Card>
          ) : error ? (
            <Card padding="$4" backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5">
              <Text color="$red11" fontWeight="600">Errore: {error}</Text>
            </Card>
          ) : jobNames.length === 0 ? (
            <Card padding="$6" alignItems="center">
              <Text color="$color11" fontSize="$4">
                Nessun log disponibile nelle ultime 24 ore
              </Text>
            </Card>
          ) : (
            <YStack gap="$3">
              <Text fontSize="$3" color="$color11">
                Visualizzati {jobNames.length} job con {logs.length} log totali
              </Text>

              {jobNames.map(jobName => {
                const stats = jobStatistics.get(jobName)!
                const isExpanded = expandedJobs.has(jobName)
                const jobLogs = logsByJob.get(jobName) || []
                const jobErrors = errorsByJob.get(jobName) || []

                return (
                  <JobCard
                    key={jobName}
                    jobName={jobName}
                    logsCount={stats.logs}
                    errorsCount={stats.errors}
                    logs={jobLogs}
                    errors={jobErrors}
                    isExpanded={isExpanded}
                    onToggle={() => toggleJobExpanded(jobName)}
                  />
                )
              })}
            </YStack>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}

// ============================================
// JOB CARD COMPONENT
// ============================================

interface JobCardProps {
  jobName: string
  logsCount: number
  errorsCount: number
  logs: FantasoccerLog[]
  errors: FantasoccerLog[]
  isExpanded: boolean
  onToggle: () => void
}

function JobCard({ 
  jobName, 
  logsCount, 
  errorsCount, 
  logs, 
  errors, 
  isExpanded, 
  onToggle 
}: JobCardProps) {
  const hasErrors = errorsCount > 0

  return (
    <Card
      padding="$0"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor={hasErrors ? "$red7" : "$borderColor"}
    >
      {/* Header - Clickable */}
      <XStack
        padding="$4"
        gap="$3"
        alignItems="center"
        justifyContent="space-between"
        onPress={onToggle}
        cursor="pointer"
        hoverStyle={{ backgroundColor: '$color3' }}
        pressStyle={{ backgroundColor: '$color4' }}
      >
        <YStack gap="$2" flex={1}>
          <XStack alignItems="center" gap="$2">
            <Text fontSize="$5" fontWeight="bold" color="$color12">
              {jobName}
            </Text>
            {hasErrors && (
              <AlertCircle size="$1" color="$red10" />
            )}
          </XStack>

          <XStack gap="$3" flexWrap="wrap">
            {logsCount > 0 && (
              <XStack alignItems="center" gap="$1">
                <Info size="$0.75" color="$blue10" />
                <Text fontSize="$2" color="$blue10">
                  {logsCount} log
                </Text>
              </XStack>
            )}
            {errorsCount > 0 && (
              <XStack alignItems="center" gap="$1">
                <AlertCircle size="$0.75" color="$red10" />
                <Text fontSize="$2" color="$red10" fontWeight="600">
                  {errorsCount} errori
                </Text>
              </XStack>
            )}
          </XStack>
        </YStack>

        {isExpanded ? (
          <ChevronUp size="$1.5" color="$color11" />
        ) : (
          <ChevronDown size="$1.5" color="$color11" />
        )}
      </XStack>

      {/* Expanded Content */}
      {isExpanded && (
        <YStack padding="$4" paddingTop="$0" gap="$3">
          <Separator />

          {/* Errors Section */}
          {errorsCount > 0 && (
            <YStack gap="$2">
              <XStack alignItems="center" gap="$2">
                <AlertCircle size="$1" color="$red10" />
                <H4 fontSize="$4" color="$red11">
                  Errori ({errorsCount})
                </H4>
              </XStack>
              <YStack gap="$2">
                {errors.map((errorLog, idx) => (
                  <LogEntry key={`error-${idx}`} log={errorLog} type="error" />
                ))}
              </YStack>
            </YStack>
          )}

          {/* Separator between errors and logs */}
          {errorsCount > 0 && logsCount > 0 && (
            <Separator marginVertical="$2" />
          )}

          {/* Logs Section */}
          {logsCount > 0 && (
            <YStack gap="$2">
              <XStack alignItems="center" gap="$2">
                <Info size="$1" color="$blue10" />
                <H4 fontSize="$4" color="$blue11">
                  Log ({logsCount})
                </H4>
              </XStack>
              <YStack gap="$2">
                {logs.map((log, idx) => (
                  <LogEntry key={`log-${idx}`} log={log} type="info" />
                ))}
              </YStack>
            </YStack>
          )}
        </YStack>
      )}
    </Card>
  )
}

// ============================================
// LOG ENTRY COMPONENT
// ============================================

interface LogEntryProps {
  log: FantasoccerLog
  type: 'error' | 'info'
}

function LogEntry({ log, type }: LogEntryProps) {
  const bgColor = type === 'error' ? '$red2' : '$blue2'
  const borderColor = type === 'error' ? '$red5' : '$blue5'
  const textColor = type === 'error' ? '$red11' : '$blue11'

  return (
    <Card
      padding="$3"
      backgroundColor={bgColor}
      borderRadius="$3"
      borderWidth="$0.5"
      borderColor={borderColor}
    >
      <YStack gap="$2">
        {/* Timestamp */}
        <XStack alignItems="center" gap="$2" justifyContent="space-between">
          <XStack alignItems="center" gap="$1">
            <Clock size="$0.75" color="$color11" />
            <Text fontSize="$1" color="$color11">
              {formatLogTimestamp(log)}
            </Text>
          </XStack>
          {log.secondaryKey && (
            <Text fontSize="$1" color="$color10">
              {log.secondaryKey}
            </Text>
          )}
        </XStack>

        {/* Message */}
        <Text fontSize="$3" color={textColor} fontWeight="500">
          {log.message}
        </Text>

        {/* Error (if present) */}
        {log.error && log.error !== '' && (
          <Card
            padding="$2"
            backgroundColor="$color1"
            borderRadius="$2"
          >
            <Text fontSize="$2" color="$red11">
              {log.error}
            </Text>
          </Card>
        )}
      </YStack>
    </Card>
  )
}
