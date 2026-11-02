import type { Vote } from '../types/serieA'

export type MatchEventKind =
  | 'goal' | 'penalty' | 'assist' | 'stoppedPenalty' | 'redCard' | 'wrongedPenalty' | 'ownGoal' | 'sufferedGoal' | 'yellowCard' | 'out' | 'in'

export interface MatchEvent { kind: MatchEventKind; count: number; label: string }
export interface EventPlayer { key: string; name: string; vote: Vote }

const YELLOW_CARD = 1
const RED_CARD = 2

// Most important first: this is also the order of the badges inside a row and of the rows in a list.
const DEFINITIONS: { kind: MatchEventKind; one: string; many: string; count: (vote: Vote) => number }[] = [
  { kind: 'goal', one: 'Gol', many: 'Gol', count: vote => vote.goals },
  { kind: 'penalty', one: 'Rigore', many: 'Rigori', count: vote => vote.penalties },
  { kind: 'assist', one: 'Assist', many: 'Assist', count: vote => vote.assists },
  { kind: 'stoppedPenalty', one: 'Rigore parato', many: 'Rigori parati', count: vote => vote.stoppedPenalties },
  { kind: 'redCard', one: 'Espulso', many: 'Espulso', count: vote => (vote.status === RED_CARD ? 1 : 0) },
  { kind: 'wrongedPenalty', one: 'Rigore sbagliato', many: 'Rigori sbagliati', count: vote => vote.wrongedPenalties },
  { kind: 'ownGoal', one: 'Autogol', many: 'Autogol', count: vote => vote.ownGoals },
  { kind: 'sufferedGoal', one: 'Gol subito', many: 'Gol subiti', count: vote => vote.sufferedGoals },
  { kind: 'yellowCard', one: 'Ammonito', many: 'Ammonito', count: vote => (vote.status === YELLOW_CARD ? 1 : 0) },
]
const SUBSTITUTIONS: typeof DEFINITIONS = [
  { kind: 'out', one: 'Uscito', many: 'Uscito', count: vote => (vote.isOut ? 1 : 0) },
  { kind: 'in', one: 'Entrato', many: 'Entrato', count: vote => (vote.isIn ? 1 : 0) },
]

// Events of one player, most important first. Substitutions only when asked: they carry no minute and are not "something done".
export function playerEvents(vote: Vote | null | undefined, includeSubstitutions = false): MatchEvent[] {
  if (!vote) return []
  return [...DEFINITIONS, ...(includeSubstitutions ? SUBSTITUTIONS : [])].flatMap(definition => {
    const count = definition.count(vote)
    return count > 0 ? [{ kind: definition.kind, count, label: count === 1 ? definition.one : definition.many }] : []
  })
}

// Players ordered by their most important event, then by how many of it, then by the next one...
export function sortByImportance<T extends { vote: Vote | null | undefined; name?: string }>(players: T[], includeSubstitutions = false): T[] {
  const order = [...DEFINITIONS, ...(includeSubstitutions ? SUBSTITUTIONS : [])]
  const counts = (player: T) => order.map(definition => (player.vote ? definition.count(player.vote) : 0))
  return players.filter(player => counts(player).some(count => count > 0)).sort((a, b) => {
    const left = counts(a); const right = counts(b)
    for (let index = 0; index < left.length; index++) if (left[index] !== right[index]) return right[index] - left[index]
    return (a.name ?? '').localeCompare(b.name ?? '')
  })
}

export interface VoteBand { key: 'none' | 'poor' | 'low' | 'fair' | 'good' | 'great'; background: string; label: string }

// The vote is coloured by how good it is, not by how provisional it is.
export function voteBand(vote: Pick<Vote, 'value' | 'hasVote'> | null | undefined): VoteBand {
  if (!vote?.hasVote) return { key: 'none', background: '#6b7280', label: 's.v.' }
  const value = vote.value
  if (value >= 7) return { key: 'great', background: '#15803d', label: 'ottimo' }
  if (value >= 6.5) return { key: 'good', background: '#0e7490', label: 'buono' }
  if (value >= 6) return { key: 'fair', background: '#1d4ed8', label: 'sufficiente' }
  if (value >= 5.5) return { key: 'low', background: '#b45309', label: 'insufficiente' }
  return { key: 'poor', background: '#b91c1c', label: 'scarso' }
}

export const formatVote = (vote: Pick<Vote, 'value' | 'hasVote'> | null | undefined) =>
  vote?.hasVote ? (Number.isInteger(vote.value) ? vote.value.toFixed(0) : vote.value.toFixed(1)) : 's.v.'
