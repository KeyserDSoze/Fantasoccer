import { teamService } from './teamService'
import { realVoteService } from './realVoteService'
import { chanceService } from './chanceService'
import { serieAService } from './serieAService'
import {
  Player,
  Team,
  EnhancedTeam,
} from 'types/team'
import { VotedRealPlayerWrapper } from 'types/serieA'
import { ChancedRealPlayerWrapper } from 'types/chance'
import { LeagueSetting, LeagueType } from 'types/group'
import { Point } from 'types/calendar'
import { VoteValue } from 'types/game'

// Local minimal types to match C# structures used by TeamCalculator
export interface EnrichedPlayerTs {
  current: Player
  vote?: any | null
  chance?: any | null
  realGame?: any | null
  finalValue?: VoteValue | null
  currentPosition?: number
}

export interface TeamCalculatorOptions {
  // Options mirror the C# BuildRequestAsync params
}

export interface ITeamCalculatorService {
  isLive(): boolean
  isEditable(): boolean
  serieADay(): number | null
  getLiveWrapper(): VotedRealPlayerWrapper | null
  getOfficialWrapper(): VotedRealPlayerWrapper | null
  getChanceWrapper(): ChancedRealPlayerWrapper | null
  getRealDay(): any | null
  getLeagueSetting(): LeagueSetting | null
  getLeagueType(): LeagueType | null
  buildRequest(year: number, day: number, leagueType: LeagueType, leagueSettings: LeagueSetting, withRealGame: boolean, withVote: boolean, withChance: boolean): Promise<void>
  setLeagueSettings(leagueType: LeagueType, leagueSettings: LeagueSetting): void
  calculatePointFromPlayers(players: Player[] | EnrichedPlayerTs[]): Point
  enrich(player: Player): EnrichedPlayerTs
  forceEnrich(player: Player, vote: any | null, chance: any | null, realGame: any | null): EnrichedPlayerTs
  enrichMany(players: Player[]): EnrichedPlayerTs[]
  finalValueFromPlayer(player: Player | EnrichedPlayerTs): VoteValue
  recalculateDay(serieADay: number): Promise<boolean>
}

// Minimal in-memory implementation that mirrors the high-level behavior of the C# TeamCalculator.
// Note: some complex league-specific logic (leagueCalculator factory, vote generation) is left as TODO markers
class TeamCalculatorService implements ITeamCalculatorService {
  private _isLive: boolean = false
  private _liveWrapper: VotedRealPlayerWrapper | null = null
  private _officialWrapper: VotedRealPlayerWrapper | null = null
  private _chanceWrapper: ChancedRealPlayerWrapper | null = null
  private _realDay: any | null = null
  private _leagueSetting: LeagueSetting | null = null
  private _isEditable: boolean = false
  private _serieADay: number = 0
  private _leagueType: LeagueType | null = null

  isLive() { return this._isLive }
  isEditable() { return this._isEditable }
  serieADay() { return this._serieADay }
  getLiveWrapper() { return this._liveWrapper }
  getOfficialWrapper() { return this._officialWrapper }
  getChanceWrapper() { return this._chanceWrapper }
  getRealDay() { return this._realDay }
  getLeagueSetting() { return this._leagueSetting }
  getLeagueType() { return this._leagueType ?? null }

  async buildRequest(year: number, day: number, leagueType: LeagueType, leagueSettings: LeagueSetting, withRealGame: boolean, withVote: boolean, withChance: boolean) {
    // Mirror C# logic: resolve year, fetch real calendar if needed, determine SerieADay and real day
    if (!year) year = new Date().getFullYear()
    this._leagueSetting = leagueSettings
    this._leagueType = leagueType

    if (withVote || withChance || withRealGame || day === 0) {
      const calendar = await serieAService.getEnhancedCurrentUserCalendar()
      if (!calendar) {
        this._realDay = null
        this._serieADay = day
        return
      }
      if (day === 0) {
        const next = calendar.nextDay ?? calendar.liveDay
        this._serieADay = next?.serieADay ?? 1
      } else {
        this._serieADay = day
      }
      this._realDay = calendar.days?.find((d: any) => d.serieADay === this._serieADay) ?? null
      this._isEditable = calendar.nextDay?.serieADay === this._serieADay
      this._isLive = calendar.liveSerieADay === this._serieADay

      if (withVote) {
        if (this._isLive) {
          this._liveWrapper = await realVoteService.getLiveWrapper({ year, day: this._serieADay })
        }
        this._officialWrapper = await realVoteService.getOfficialWrapper({ year, day: this._serieADay })
      } else {
        this._liveWrapper = null
        this._officialWrapper = null
      }

      if (withChance) {
        // chanceService.getEnhancedWrapper expects { year, day }
        this._chanceWrapper = await chanceService.getEnhancedWrapper({ year, day: this._serieADay })
      } else {
        this._chanceWrapper = null
      }
    } else {
      this._realDay = null
      this._serieADay = day
    }
  }

  setLeagueSettings(leagueType: LeagueType, leagueSettings: LeagueSetting) {
    this._leagueType = leagueType
    this._leagueSetting = leagueSettings
  }

  calculatePointFromPlayers(players: Player[] | EnrichedPlayerTs[]) {
    // If players are plain Player, enrich them first
    const enriched = (players as any[]).length && (players as any[])[0].current ? (players as EnrichedPlayerTs[]) : this.enrichMany(players as Player[])
  if (!enriched || enriched.length === 0 || !this._leagueSetting) return { value: 0, defensiveBonus: false, goodPeople: false, ownGoal: false } as Point

    // Basic calculation mirroring the C# algorithm:
    let point = 0
    let defensiveBonus = true
    let defensorCount = 0
    let goodPeople = true

    // TODO: Use a proper leagueCalculator.GetRightFormation(enriched)
    const rightFormation = enriched // placeholder

    // Role comparisons use Role enum values (imported from types/group)
    const { Role } = require('types/group') as any
    for (const p of rightFormation.filter((x: any) => (x.currentPosition ?? 0) <= 5 /* Forward threshold coarse */)) {
      if (p.vote && p.vote.hasVote) {
        const fv = this.finalValueFromPlayer(p)
        point += fv.value
        if (p.current.role === Role.Defensor) defensorCount++
      }
      if (p.current.role === Role.Defensor && (!p.vote || !p.vote.hasVote || p.vote.value < 6)) defensiveBonus = false
      if (!p.vote || !p.vote.hasVote || p.vote.status !== 'Nothing') goodPeople = false
    }

    if (goodPeople && this._leagueSetting) point += this._leagueSetting.pointForGoodPeople
    if (defensiveBonus && this._leagueSetting) {
      if (defensorCount === 3) point += this._leagueSetting.pointForStrongDefense
      else if (defensorCount === 4) point += this._leagueSetting.pointForStrongDefense4
      else if (defensorCount === 5) point += this._leagueSetting.pointForStrongDefense5
    }

  return { value: point, defensiveBonus, goodPeople, ownGoal: (this._leagueSetting?.pointForOwnGoal ?? 0) > 0 && point < ((this._leagueSetting?.pointForFirstGoal ?? 0) - (this._leagueSetting?.pointForOwnGoal ?? 0)) } as Point
  }

  enrich(player: Player): EnrichedPlayerTs {
    // map player to EnrichedPlayerTs structure
  // NOTE: TS Player doesn't have 'key' or 'realTeam'. We'll match votes/chances by player name and team abbreviation (best-effort).
  const matchByNameAndTeam = (p: any) => p.name === player.name && p.team?.abbreviation === (player.team?.abbreviation ?? player.team?.name)
  const vote = this._officialWrapper?.players?.find((x: any) => matchByNameAndTeam(x))?.vote ?? this._liveWrapper?.players?.find((x: any) => matchByNameAndTeam(x))?.vote ?? null
  const chance = this._chanceWrapper?.players?.find((x: any) => matchByNameAndTeam(x))?.chance ?? null
  const realGame = this._realDay?.games?.find((g: any) => g.home?.abbreviation === player.team?.abbreviation || g.away?.abbreviation === player.team?.abbreviation) ?? null

    const enriched: any = {
      current: player,
      vote,
      chance,
      realGame,
      finalValue: null,
      currentPosition: player.position
    }
    if (enriched.vote) enriched.finalValue = this.finalValueFromPlayer(enriched)
    return enriched as EnrichedPlayerTs
  }

  forceEnrich(player: Player, vote: any | null, chance: any | null, realGame: any | null): EnrichedPlayerTs {
    const enriched: any = {
      current: player,
      vote,
      chance,
      realGame,
      finalValue: null,
      currentPosition: player.position
    }
    if (enriched.vote) enriched.finalValue = this.finalValueFromPlayer(enriched)
    return enriched as EnrichedPlayerTs
  }

  enrichMany(players: Player[]): EnrichedPlayerTs[] {
    return players.map(p => this.enrich(p))
  }

  finalValueFromPlayer(player: any): VoteValue {
    // TODO: Implement detailed vote calculation mirroring C# FinalValue(EnrichedPlayer)
    // For now, a naive mapping if vote exists
    if (!player) return { value: 0, special: false } as VoteValue
    if (player.finalValue) return player.finalValue as VoteValue
    const vote = player.vote
    if (!vote) return { value: 0, special: false } as VoteValue
    let value = vote.value ?? 0
    if (vote.goal) value += (vote.goal * 3)
    if (vote.assist) value += vote.assist
    return { value, special: false } as VoteValue
  }

  async recalculateDay(serieADay: number): Promise<boolean> {
    // TODO: call vote generator infrastructure if available
    // Placeholder returns false
    return false
  }
}

export const teamCalculatorService = new TeamCalculatorService()
