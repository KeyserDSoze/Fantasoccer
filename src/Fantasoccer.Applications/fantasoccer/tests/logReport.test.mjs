import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

const log = (job, status, at, extra = {}) => ({ id: `${job}-${at}`, primaryKey: status === 'Failed' ? 'AError' : 'ALog', secondaryKey: `${job}_${job}-${at}`,
  timestamp: `2026-11-03T${at}:00Z`, message: `${job}: ${status}; elaborati=0; saltati=0`, error: '', source: 'sql-job', detail: null, ...extra })

test('repeated errors are one distinct group with job, description, count and time range; the report tells whether the job recovered', () => {
  const { groupDistinctErrors, buildErrorReport, jobNameOfLog, latestRunByJob } = loadTypeScript('utils/logReport.ts')
  const failed = at => log('chances', 'Failed', at, { error: 'football_provider_incomplete', detail: 'Giornata 11: sorgenti senza giocatori: Gazzetta.' })
  const logs = [failed('06:00'), failed('06:02'), failed('08:49'),
    log('chances', 'Succeeded', '09:00', { detail: 'Probabilità della giornata 11: Fantagazzetta 400.' }),
    log('formations', 'Failed', '07:00', { error: 'job_execution_failed', detail: 'Eccezione NpgsqlException ← TimeoutException' }),
    log('formations', 'Succeeded', '07:05')]
  assert.equal(jobNameOfLog({ secondaryKey: 'live-projections_abc' }), 'live-projections')
  assert.equal(jobNameOfLog({ secondaryKey: 'ALog_-_OldJob_22' }), 'OldJob')
  const groups = groupDistinctErrors(logs)
  assert.equal(groups.length, 2)
  const chances = groups.find(x => x.job === 'chances')
  assert.equal(chances.count, 3); assert.equal(chances.first, '2026-11-03T06:00:00Z'); assert.equal(chances.last, '2026-11-03T08:49:00Z')
  assert.equal(latestRunByJob(logs).get('chances').status, 'Succeeded')
  const report = buildErrorReport(logs, new Date('2026-11-05T09:00:00Z'))
  assert.match(report, /Job: chances/); assert.match(report, /Errore: football_provider_incomplete/)
  assert.match(report, /Descrizione: Giornata 11: sorgenti senza giocatori: Gazzetta\./); assert.match(report, /3 volte, dal/)
  assert.match(report, /Ultimo esito del job: Succeeded .*probabilmente già risolto/)
  assert.match(report, /Job: formations/)
  assert.equal(buildErrorReport([], new Date()).includes('Nessun errore'), true)
})
