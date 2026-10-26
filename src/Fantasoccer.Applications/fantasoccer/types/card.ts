import type { FantaSoccerRole } from './team'

export enum CardType { Strategy = 0, Goalkeeper = 1, Defensor = 2, Midfielder = 3, Forward = 4, Special = 5 }
export interface CardConditionForPlayersValues {
  vote?: number | null; hasYellow?: boolean | null; hasRed?: boolean | null; goals?: number | null; assists?: number | null
  ownGoals?: number | null; penalties?: number | null; stoppedPenalties?: number | null; sufferedGoals?: number | null; wrongedPenalties?: number | null
  isManOfTheMatch?: boolean | null; isOut?: boolean | null; isIn?: boolean | null; isInjured?: boolean | null
}
export interface CardConditionForPlayers { minimumNumberOfOccurrences?: number | null; minimum?: CardConditionForPlayersValues | null; maximum?: CardConditionForPlayersValues | null }
export interface CardConditionSummatoryPlayers {
  withVote?: number | null; withoutVote?: number | null; withSpecial?: number | null; goals?: number | null; penalties?: number | null; assists?: number | null
  stoppedPenalties?: number | null; sufferedGoals?: number | null; wrongedPenalties?: number | null; ownGoals?: number | null; yellowCards?: number | null
  redCards?: number | null; enoughVotes?: number | null; manOfTheMatch?: number | null; injured?: number | null
}
export interface CardConditionSummatory { maximum?: CardConditionSummatoryPlayers | null; minimum?: CardConditionSummatoryPlayers | null }
export interface CardCondition { rolesInvolved: FantaSoccerRole[]; conditionForPlayer?: CardConditionForPlayers | null; condition?: CardConditionSummatory | null }
export interface Card { id: string; name: string; description: string; type: CardType; points: number; negativePoints: number; enemyPoints: number; myCondition?: CardCondition | null; enemyCondition?: CardCondition | null; imageUrl?: string | null; version?: number }
export interface CardWrapper { cards: Card[] }
export interface CardResult { cardId: string; points: number; negativePoints: number; enemyPoints: number; reached: boolean }
export interface CardResults { cards: CardResult[] }
export const getCardTypeLabel = (type: CardType) => ({ 0: 'Strategia', 1: 'Portiere', 2: 'Difensore', 3: 'Centrocampista', 4: 'Attaccante', 5: 'Speciale' })[type] ?? 'Sconosciuto'
export const getCardTypeColor = (type: CardType) => ({ 0: '$blue10', 1: '$yellow10', 2: '$green10', 3: '$orange10', 4: '$red10', 5: '$purple10' })[type] ?? '$gray10'
export const getCardTypeBgColor = (type: CardType) => ({ 0: '$blue3', 1: '$yellow3', 2: '$green3', 3: '$orange3', 4: '$red3', 5: '$purple3' })[type] ?? '$gray3'
