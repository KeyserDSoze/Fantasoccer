import React, { useMemo, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { SplashScreen } from 'components/layout/SplashScreen'
import { 
  YStack, 
  XStack, 
  Text, 
  H3, 
  H4,
  Card, 
  Separator,
  ScrollView,
  useMedia,
  Button
} from 'tamagui'
import { useAllLogs } from 'hooks/useFantasoccerLog'
import { FantasoccerLog, formatLogTimestamp } from 'types/log'
import { buildErrorReport, groupDistinctErrors, jobNameOfLog, latestRunByJob, type ErrorGroup } from '../utils/logReport'
import { 
  FileText, 
  AlertCircle, 
  Info, 
  Clock,
  ChevronDown,
  ChevronUp,
  Copy
} from '@tamagui/lucide-icons-2'

export default function LogsScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const { logs, loading, error, statistics } = useAllLogs({
    refreshInterval: 60000 // Auto-refresh ogni minuto
  })

  const [expandedJobs, setExpandedJobs] = useState<Set<string>>(new Set())
  const [copied, setCopied] = useState<'ok' | 'error' | null>(null)

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
      
      const jobName = jobNameOfLog(log)

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

  const latestRuns = useMemo(() => latestRunByJob(logs), [logs])
  const errorGroupsByJob = useMemo(() => {
    const map = new Map<string, ErrorGroup[]>()
    groupDistinctErrors(logs).forEach(group => map.set(group.job, [...(map.get(group.job) ?? []), group]))
    return map
  }, [logs])

  // Every distinct error with job, description and time, ready to be pasted as a prompt.
  const copyErrors = async () => {
    try {
      if (typeof navigator === 'undefined' || !navigator.clipboard?.writeText) throw new Error('Clipboard unavailable')
      await navigator.clipboard.writeText(buildErrorReport(logs))
      setCopied('ok')
    } catch {
      setCopied('error')
    }
    setTimeout(() => setCopied(null), 3000)
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
              theme="purple"
              onPress={copyErrors}
              icon={<Copy size="$1" />}
            >
              {copied === 'ok' ? 'Copiato negli appunti' : copied === 'error' ? 'Copia non riuscita' : 'Copia errori (prompt)'}
            </Button>
          </XStack>

          {/* Lista logs */}
          {loading && logs.length === 0 ? (
            <Card padding="$6" alignItems="center" gap="$3">
              <SplashScreen compact message="Caricamento log…" steps={[{ id: 'logs', label: 'Log e risultati dei job', status: 'pending' }]} />
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
                    errorGroups={errorGroupsByJob.get(jobName) ?? []}
                    latest={latestRuns.get(jobName) ?? null}
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
  errorGroups: ErrorGroup[]
  latest: { status: string; at: string | null; detail: string } | null
  isExpanded: boolean
  onToggle: () => void
}

function JobCard({ 
  jobName, 
  logsCount, 
  errorsCount, 
  logs, 
  errors, 
  errorGroups,
  latest,
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

          {latest && (
            <Text fontSize="$2" color={latest.status === 'Failed' || latest.status === 'LeaseLost' ? '$red10' : '$color10'}>
              Ultimo esito: {latest.status} · {latest.at ? new Date(latest.at).toLocaleString('it-IT') : 'data non disponibile'}
              {latest.detail ? ` · ${latest.detail}` : ''}
            </Text>
          )}

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
                {/* The same error repeated is one line: how many times, from when to when. */}
                {errorGroups.map((group, idx) => (
                  <Card key={`error-${idx}`} padding="$3" backgroundColor="$red2" borderRadius="$3" borderWidth="$0.5" borderColor="$red5">
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color11">
                        {group.count > 1
                          ? `${group.count} volte · dal ${group.first ? new Date(group.first).toLocaleString('it-IT') : '?'} al ${group.last ? new Date(group.last).toLocaleString('it-IT') : '?'}`
                          : group.last ? new Date(group.last).toLocaleString('it-IT') : 'data non disponibile'}
                      </Text>
                      <Text fontSize="$3" color="$red11" fontWeight="600">{group.code || '(senza codice)'}</Text>
                      {group.detail ? <Text fontSize="$2" color="$color12" lineHeight="$3">{group.detail}</Text> : null}
                      <Text fontSize="$1" color="$color10">ultima: {group.job}_{group.lastId}</Text>
                    </YStack>
                  </Card>
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

        {/* Detail: which matchday, which matches or clubs, why */}
        {log.detail && (
          <Text fontSize="$2" color="$color12" lineHeight="$3">
            {log.detail}
          </Text>
        )}
      </YStack>
    </Card>
  )
}
