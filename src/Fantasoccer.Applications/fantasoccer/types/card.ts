import { FantaSoccerRole } from './team'

// Enum per il tipo di carta
export enum CardType {
  Strategy = 0,
  Goalkeeper = 1,
  Defensor = 2,
  Midfielder = 3,
  Forward = 4,
  Special = 5
}

// Valori condizioni per singoli giocatori
export interface CardConditionForPlayersValues {
  vote?: number | null
  hasYellow?: boolean | null
  hasRed?: boolean | null
  goals?: number | null
  assists?: number | null
  ownGoals?: number | null
  penalties?: number | null
  stoppedPenalties?: number | null
  sufferedGoals?: number | null
  wrongedPenalties?: number | null
  isManOfTheMatch?: boolean | null
  isOut?: boolean | null
  isIn?: boolean | null
  isInjured?: boolean | null
}

// Raw type from API
export interface CardConditionForPlayersValuesRaw {
  v?: number | null
  yc?: boolean | null
  rc?: boolean | null
  g?: number | null
  a?: number | null
  o?: number | null
  p?: number | null
  s?: number | null
  d?: number | null
  w?: number | null
  mm?: boolean | null
  io?: boolean | null
  ii?: boolean | null
  ij?: boolean | null
}

// Condizioni per giocatori
export interface CardConditionForPlayers {
  minimumNumberOfOccurencies?: number | null
  minimum?: CardConditionForPlayersValues | null
  maximum?: CardConditionForPlayersValues | null
}

// Raw type from API
export interface CardConditionForPlayersRaw {
  m?: number | null
  mn?: CardConditionForPlayersValuesRaw | null
  mx?: CardConditionForPlayersValuesRaw | null
}

// Valori sommatori per condizioni di squadra
export interface CardConditionSummatoryPlayers {
  withVote?: number | null
  withoutVote?: number | null
  withSpecial?: number | null
  goal?: number | null
  penalty?: number | null
  assist?: number | null
  stoppedPenalty?: number | null
  sufferedGoal?: number | null
  wrongedPenalty?: number | null
  ownGoal?: number | null
  yellowCard?: number | null
  redCard?: number | null
  isEnough?: number | null
  manOfTheMatch?: number | null
  injured?: number | null
}

// Raw type from API
export interface CardConditionSummatoryPlayersRaw {
  v?: number | null
  q?: number | null
  s?: number | null
  g?: number | null
  p?: number | null
  u?: number | null
  j?: number | null
  m?: number | null
  w?: number | null
  o?: number | null
  y?: number | null
  d?: number | null
  e?: number | null
  c?: number | null
  ij?: number | null
}

// Condizioni sommatore
export interface CardConditionSummatory {
  maximum?: CardConditionSummatoryPlayers | null
  minimum?: CardConditionSummatoryPlayers | null
}

// Raw type from API
export interface CardConditionSummatoryRaw {
  mx?: CardConditionSummatoryPlayersRaw | null
  mn?: CardConditionSummatoryPlayersRaw | null
}

// Condizione carta
export interface CardCondition {
  rolesInvolved: FantaSoccerRole[]
  conditionForPlayer?: CardConditionForPlayers | null
  condition?: CardConditionSummatory | null
}

// Raw type from API
export interface CardConditionRaw {
  r: number[]
  cp?: CardConditionForPlayersRaw | null
  cs?: CardConditionSummatoryRaw | null
}

// Carta
export interface Card {
  id: string
  name: string
  description: string
  type: CardType
  points: number
  negativePoints: number
  enemyPoints: number
  myCondition?: CardCondition | null
  enemyCondition?: CardCondition | null
}

// Raw type from API
export interface CardRaw {
  id: string
  n: string
  d: string
  t: number
  p: number
  np: number
  ep: number
  mc?: CardConditionRaw | null
  ec?: CardConditionRaw | null
}

// Wrapper per lista di carte
export interface CardWrapper {
  cards: Card[]
}

// Raw type from API
export interface CardWrapperRaw {
  c: CardRaw[]
}

// Risultato singola carta
export interface CardResult {
  cardId: string
  points: number
  negativePoints: number
  enemyPoints: number
  reached: boolean
}

// Raw type from API
export interface CardResultRaw {
  i: string
  p: number
  np: number
  ep: number
  r: boolean
}

// Risultati carte
export interface CardResults {
  cards: CardResult[]
}

// Raw type from API
export interface CardResultsRaw {
  c: CardResultRaw[]
}

// Mappers - CardConditionForPlayersValues
export function mapRawCardConditionForPlayersValuesToCardConditionForPlayersValues(
  raw: CardConditionForPlayersValuesRaw
): CardConditionForPlayersValues {
  return {
    vote: raw.v ?? null,
    hasYellow: raw.yc ?? null,
    hasRed: raw.rc ?? null,
    goals: raw.g ?? null,
    assists: raw.a ?? null,
    ownGoals: raw.o ?? null,
    penalties: raw.p ?? null,
    stoppedPenalties: raw.s ?? null,
    sufferedGoals: raw.d ?? null,
    wrongedPenalties: raw.w ?? null,
    isManOfTheMatch: raw.mm ?? null,
    isOut: raw.io ?? null,
    isIn: raw.ii ?? null,
    isInjured: raw.ij ?? null
  }
}

export function mapCardConditionForPlayersValuesToRawCardConditionForPlayersValues(
  values: CardConditionForPlayersValues
): CardConditionForPlayersValuesRaw {
  return {
    v: values.vote ?? undefined,
    yc: values.hasYellow ?? undefined,
    rc: values.hasRed ?? undefined,
    g: values.goals ?? undefined,
    a: values.assists ?? undefined,
    o: values.ownGoals ?? undefined,
    p: values.penalties ?? undefined,
    s: values.stoppedPenalties ?? undefined,
    d: values.sufferedGoals ?? undefined,
    w: values.wrongedPenalties ?? undefined,
    mm: values.isManOfTheMatch ?? undefined,
    io: values.isOut ?? undefined,
    ii: values.isIn ?? undefined,
    ij: values.isInjured ?? undefined
  }
}

// Mappers - CardConditionForPlayers
export function mapRawCardConditionForPlayersToCardConditionForPlayers(
  raw: CardConditionForPlayersRaw
): CardConditionForPlayers {
  return {
    minimumNumberOfOccurencies: raw.m ?? null,
    minimum: raw.mn ? mapRawCardConditionForPlayersValuesToCardConditionForPlayersValues(raw.mn) : null,
    maximum: raw.mx ? mapRawCardConditionForPlayersValuesToCardConditionForPlayersValues(raw.mx) : null
  }
}

export function mapCardConditionForPlayersToRawCardConditionForPlayers(
  condition: CardConditionForPlayers
): CardConditionForPlayersRaw {
  return {
    m: condition.minimumNumberOfOccurencies ?? undefined,
    mn: condition.minimum ? mapCardConditionForPlayersValuesToRawCardConditionForPlayersValues(condition.minimum) : undefined,
    mx: condition.maximum ? mapCardConditionForPlayersValuesToRawCardConditionForPlayersValues(condition.maximum) : undefined
  }
}

// Mappers - CardConditionSummatoryPlayers
export function mapRawCardConditionSummatoryPlayersToCardConditionSummatoryPlayers(
  raw: CardConditionSummatoryPlayersRaw
): CardConditionSummatoryPlayers {
  return {
    withVote: raw.v ?? null,
    withoutVote: raw.q ?? null,
    withSpecial: raw.s ?? null,
    goal: raw.g ?? null,
    penalty: raw.p ?? null,
    assist: raw.u ?? null,
    stoppedPenalty: raw.j ?? null,
    sufferedGoal: raw.m ?? null,
    wrongedPenalty: raw.w ?? null,
    ownGoal: raw.o ?? null,
    yellowCard: raw.y ?? null,
    redCard: raw.d ?? null,
    isEnough: raw.e ?? null,
    manOfTheMatch: raw.c ?? null,
    injured: raw.ij ?? null
  }
}

export function mapCardConditionSummatoryPlayersToRawCardConditionSummatoryPlayers(
  players: CardConditionSummatoryPlayers
): CardConditionSummatoryPlayersRaw {
  return {
    v: players.withVote ?? undefined,
    q: players.withoutVote ?? undefined,
    s: players.withSpecial ?? undefined,
    g: players.goal ?? undefined,
    p: players.penalty ?? undefined,
    u: players.assist ?? undefined,
    j: players.stoppedPenalty ?? undefined,
    m: players.sufferedGoal ?? undefined,
    w: players.wrongedPenalty ?? undefined,
    o: players.ownGoal ?? undefined,
    y: players.yellowCard ?? undefined,
    d: players.redCard ?? undefined,
    e: players.isEnough ?? undefined,
    c: players.manOfTheMatch ?? undefined,
    ij: players.injured ?? undefined
  }
}

// Mappers - CardConditionSummatory
export function mapRawCardConditionSummatoryToCardConditionSummatory(
  raw: CardConditionSummatoryRaw
): CardConditionSummatory {
  return {
    maximum: raw.mx ? mapRawCardConditionSummatoryPlayersToCardConditionSummatoryPlayers(raw.mx) : null,
    minimum: raw.mn ? mapRawCardConditionSummatoryPlayersToCardConditionSummatoryPlayers(raw.mn) : null
  }
}

export function mapCardConditionSummatoryToRawCardConditionSummatory(
  summatory: CardConditionSummatory
): CardConditionSummatoryRaw {
  return {
    mx: summatory.maximum ? mapCardConditionSummatoryPlayersToRawCardConditionSummatoryPlayers(summatory.maximum) : undefined,
    mn: summatory.minimum ? mapCardConditionSummatoryPlayersToRawCardConditionSummatoryPlayers(summatory.minimum) : undefined
  }
}

// Mappers - CardCondition
export function mapRawCardConditionToCardCondition(raw: CardConditionRaw): CardCondition {
  return {
    rolesInvolved: raw.r as FantaSoccerRole[],
    conditionForPlayer: raw.cp ? mapRawCardConditionForPlayersToCardConditionForPlayers(raw.cp) : null,
    condition: raw.cs ? mapRawCardConditionSummatoryToCardConditionSummatory(raw.cs) : null
  }
}

export function mapCardConditionToRawCardCondition(condition: CardCondition): CardConditionRaw {
  return {
    r: condition.rolesInvolved as number[],
    cp: condition.conditionForPlayer ? mapCardConditionForPlayersToRawCardConditionForPlayers(condition.conditionForPlayer) : undefined,
    cs: condition.condition ? mapCardConditionSummatoryToRawCardConditionSummatory(condition.condition) : undefined
  }
}

// Mappers - Card
export function mapRawCardToCard(raw: CardRaw): Card {
  return {
    id: raw.id,
    name: raw.n,
    description: raw.d,
    type: raw.t as CardType,
    points: raw.p,
    negativePoints: raw.np,
    enemyPoints: raw.ep,
    myCondition: raw.mc ? mapRawCardConditionToCardCondition(raw.mc) : null,
    enemyCondition: raw.ec ? mapRawCardConditionToCardCondition(raw.ec) : null
  }
}

export function mapCardToRawCard(card: Card): CardRaw {
  return {
    id: card.id,
    n: card.name,
    d: card.description,
    t: card.type as number,
    p: card.points,
    np: card.negativePoints,
    ep: card.enemyPoints,
    mc: card.myCondition ? mapCardConditionToRawCardCondition(card.myCondition) : undefined,
    ec: card.enemyCondition ? mapCardConditionToRawCardCondition(card.enemyCondition) : undefined
  }
}

// Mappers - CardResult
export function mapRawCardResultToCardResult(raw: CardResultRaw): CardResult {
  return {
    cardId: raw.i,
    points: raw.p,
    negativePoints: raw.np,
    enemyPoints: raw.ep,
    reached: raw.r
  }
}

export function mapCardResultToRawCardResult(result: CardResult): CardResultRaw {
  return {
    i: result.cardId,
    p: result.points,
    np: result.negativePoints,
    ep: result.enemyPoints,
    r: result.reached
  }
}

// Mappers - CardResults
export function mapRawCardResultsToCardResults(raw: CardResultsRaw): CardResults {
  return {
    cards: raw.c.map(mapRawCardResultToCardResult)
  }
}

export function mapCardResultsToRawCardResults(results: CardResults): CardResultsRaw {
  return {
    c: results.cards.map(mapCardResultToRawCardResult)
  }
}

// Helper per ottenere il label del CardType
export function getCardTypeLabel(type: CardType): string {
  switch (type) {
    case CardType.Strategy:
      return 'Strategia'
    case CardType.Goalkeeper:
      return 'Portiere'
    case CardType.Defensor:
      return 'Difensore'
    case CardType.Midfielder:
      return 'Centrocampista'
    case CardType.Forward:
      return 'Attaccante'
    case CardType.Special:
      return 'Speciale'
    default:
      return 'Sconosciuto'
  }
}

// Helper per ottenere il colore del CardType
export function getCardTypeColor(type: CardType): string {
  switch (type) {
    case CardType.Strategy:
      return '$blue10'
    case CardType.Goalkeeper:
      return '$yellow10'
    case CardType.Defensor:
      return '$green10'
    case CardType.Midfielder:
      return '$orange10'
    case CardType.Forward:
      return '$red10'
    case CardType.Special:
      return '$purple10'
    default:
      return '$gray10'
  }
}

// Helper per ottenere il background color del CardType
export function getCardTypeBgColor(type: CardType): string {
  switch (type) {
    case CardType.Strategy:
      return '$blue3'
    case CardType.Goalkeeper:
      return '$yellow3'
    case CardType.Defensor:
      return '$green3'
    case CardType.Midfielder:
      return '$orange3'
    case CardType.Forward:
      return '$red3'
    case CardType.Special:
      return '$purple3'
    default:
      return '$gray3'
  }
}
