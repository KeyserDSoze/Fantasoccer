import type { FantasoccerLog } from '../types/log'

// "chances_<guid>" -> "chances"; "formations_<guid>" -> "formations"; legacy "ALog_-_Job_22" -> "Job".
export function jobNameOfLog(log: Pick<FantasoccerLog, 'secondaryKey'>): string {
  const rowKey = log.secondaryKey ?? ''
  if (rowKey.includes('_-_')) {
    const part = rowKey.split('_-_')[1] ?? rowKey
    return part.split('_').slice(0, -1).join('_') || part
  }
  if (rowKey.includes('_')) return rowKey.split('_').slice(0, -1).join('_') || rowKey.split('_')[0]
  return rowKey || 'Unknown'
}

const time = (value: string | null) => value ? new Date(value).toLocaleString('it-IT', { timeZone: 'Europe/Rome' }) : 'data non disponibile'
const stamp = (log: FantasoccerLog) => (log.timestamp ? Date.parse(log.timestamp) : 0)

export interface ErrorGroup {
  job: string
  source: string
  code: string
  detail: string
  count: number
  first: string | null
  last: string | null
  lastId: string
}

// The same failure repeated (a job retried every minute) is one group: job, error code and description, how many times, from when to when.
export function groupDistinctErrors(logs: FantasoccerLog[]): ErrorGroup[] {
  const groups = new Map<string, ErrorGroup>()
  for (const log of logs.filter(x => x.primaryKey === 'AError')) {
    const job = jobNameOfLog(log)
    const detail = (log.detail ?? '').trim()
    const key = [job, log.error ?? '', detail].join('\u0000')
    const group = groups.get(key)
    if (!group) {
      groups.set(key, { job, source: log.source, code: log.error ?? '', detail, count: 1, first: log.timestamp, last: log.timestamp, lastId: log.id })
      continue
    }
    group.count++
    if (stamp(log) < (group.first ? Date.parse(group.first) : Infinity)) group.first = log.timestamp
    if (stamp(log) >= (group.last ? Date.parse(group.last) : 0)) { group.last = log.timestamp; group.lastId = log.id }
  }
  return [...groups.values()].sort((a, b) => (b.last ? Date.parse(b.last) : 0) - (a.last ? Date.parse(a.last) : 0))
}

// Latest run of each job, whatever its outcome ("chances: Succeeded; elaborati=1; ...").
export function latestRunByJob(logs: FantasoccerLog[]): Map<string, { status: string; at: string | null; detail: string }> {
  const latest = new Map<string, FantasoccerLog>()
  for (const log of logs.filter(x => x.source === 'sql-job')) {
    const job = jobNameOfLog(log)
    const current = latest.get(job)
    if (!current || stamp(log) > stamp(current)) latest.set(job, log)
  }
  return new Map([...latest].map(([job, log]) => [job, { status: /:\s*([A-Za-z]+);/.exec(log.message)?.[1] ?? '?', at: log.timestamp, detail: (log.detail ?? '').trim() }]))
}

// A text to paste to an assistant: every distinct error with job, description and time, and how each job is doing now.
export function buildErrorReport(logs: FantasoccerLog[], now = new Date()): string {
  const groups = groupDistinctErrors(logs)
  const latest = latestRunByJob(logs)
  const lines = [
    `Analizza questi problemi dei job di Fantasoccer (log amministratore del ${now.toLocaleString('it-IT', { timeZone: 'Europe/Rome' })}, ora italiana) e proponi le correzioni.`,
    'Per ogni errore: capire la causa dal codice e dalla descrizione, dire se è già risolto (ultimo esito del job) e cosa cambiare.',
    '',
  ]
  if (groups.length === 0) lines.push('Nessun errore nei log caricati.')
  groups.forEach((group, index) => {
    const now_ = latest.get(group.job)
    lines.push(`${index + 1}. Job: ${group.job}${group.source && group.source !== 'sql-job' ? ` (${group.source})` : ''}`)
    lines.push(`   Errore: ${group.code || '(senza codice)'}`)
    if (group.detail) lines.push(`   Descrizione: ${group.detail}`)
    lines.push(`   Quando: ${group.count === 1 ? time(group.last) : `${group.count} volte, dal ${time(group.first)} al ${time(group.last)}`}`)
    lines.push(`   Ultima esecuzione con questo errore: ${group.job}_${group.lastId}`)
    if (now_ && group.source === 'sql-job') lines.push(`   Ultimo esito del job: ${now_.status} alle ${time(now_.at)}${now_.status === 'Succeeded' || now_.status === 'Partial' ? ' (dopo questo errore: probabilmente già risolto)' : ''}${now_.detail ? ` — ${now_.detail}` : ''}`)
    lines.push('')
  })
  return lines.join('\n').trimEnd() + '\n'
}
