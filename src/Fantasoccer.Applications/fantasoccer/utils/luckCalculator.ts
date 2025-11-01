import type { Calendar, CalendarGame, GameResult } from 'types/calendar'
import type { LuckEvent, LuckEventType, TeamLuck } from 'types/rank'

/**
 * Represents a game result with all teams' scores for that day
 */
interface GameContext {
  game: CalendarGame
  dayNumber: number
  allDayScores: { owner: string; name: string; score: number }[]
}

/**
 * Calculate luck for all teams based on calendar data
 */
export class LuckCalculator {
  /**
   * Main function to calculate luck for all teams in a calendar
   */
  static calculateTeamLuck(calendar: Calendar, roundId: string): Map<string, TeamLuck> {
    const teamLuckMap = new Map<string, TeamLuck>()
    
    // Get all days for the specified round
    const days = calendar.rounds[roundId]
    if (!days) {
      return teamLuckMap
    }

    // Process each day
    for (const day of days) {
      // Get all scores for this day (excluding cancelled games)
      const dayScores = this.getDayScores(day.games)
      
      // Process each game
      for (const game of day.games) {
        if (!game.result || game.result.isCancelled) {
          continue // Skip cancelled or unplayed games
        }

        // Skip unplayed games (0-0 with both scores at 0)
        if (game.result.home.value === 0 && game.result.away.value === 0) {
          continue
        }

        // Calculate luck events for both teams
        const homeEvents = this.calculateGameLuck(
          game,
          day.number,
          game.homeOwner,
          game.home,
          game.result.home.value,
          game.result.homeGoals,
          game.awayOwner,
          game.away,
          game.result.away.value,
          game.result.awayGoals,
          dayScores
        )

        const awayEvents = this.calculateGameLuck(
          game,
          day.number,
          game.awayOwner,
          game.away,
          game.result.away.value,
          game.result.awayGoals,
          game.homeOwner,
          game.home,
          game.result.home.value,
          game.result.homeGoals,
          dayScores
        )

        // Add events to team luck
        this.addEventsToTeam(teamLuckMap, game.homeOwner, game.home, homeEvents)
        this.addEventsToTeam(teamLuckMap, game.awayOwner, game.away, awayEvents)
      }
    }

    // Calculate averages
    for (const teamLuck of teamLuckMap.values()) {
      teamLuck.avgLuck = teamLuck.gamesPlayed > 0 
        ? teamLuck.totalLuck / teamLuck.gamesPlayed 
        : 0
    }

    return teamLuckMap
  }

  /**
   * Get all scores for a specific day
   */
  private static getDayScores(games: CalendarGame[]): { owner: string; name: string; score: number }[] {
    const scores: { owner: string; name: string; score: number }[] = []

    for (const game of games) {
      if (!game.result || game.result.isCancelled) {
        continue
      }

      scores.push({
        owner: game.homeOwner,
        name: game.home,
        score: game.result.home.value
      })

      scores.push({
        owner: game.awayOwner,
        name: game.away,
        score: game.result.away.value
      })
    }

    return scores.sort((a, b) => a.score - b.score) // Sort ascending
  }

  /**
   * Calculate luck events for a single team in a single game
   */
  private static calculateGameLuck(
    game: CalendarGame,
    dayNumber: number,
    myOwner: string,
    myName: string,
    myScore: number,
    myGoals: number,
    oppOwner: string,
    oppName: string,
    oppScore: number,
    oppGoals: number,
    dayScores: { owner: string; name: string; score: number }[]
  ): LuckEvent[] {
    const events: LuckEvent[] = []
    const result = this.getGameResult(myGoals, oppGoals)
    const goalDiff = Math.abs(myGoals - oppGoals)
    const scoreDiff = Math.abs(myScore - oppScore)

    // Find my position and opponent's position in day rankings
    // dayScores is sorted ascending, so rank 1 = worst score, last rank = best score
    const myRank = dayScores.findIndex(s => s.owner === myOwner) + 1
    const oppRank = dayScores.findIndex(s => s.owner === oppOwner) + 1
    const totalTeams = dayScores.length
    const bestRank = totalTeams // Last position = best score
    const worstRank = 1 // First position = worst score

    // EVENT 1: Victory with less than 66 points and 1 goal difference (+5)
    if (result === 'win' && goalDiff === 1 && myScore < 66) {
      events.push({
        type: 'win-vs-worst',
        points: 5,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Vittoria fortunatissima con meno di 66 punti (${myScore.toFixed(1)}) e 1 solo goal di scarto`
      })
    }
    // EVENT 2: Victory with 1 goal difference against opponent with low score (not if already triggered event 1)
    else if (result === 'win' && goalDiff === 1 && oppRank <= 3 && myScore >= 66) {
      // Worst = +3, 2nd worst = +2, 3rd worst = +1
      const luckPoints = oppRank === 1 ? 3 : oppRank === 2 ? 2 : 1
      const rankLabel = oppRank === 1 ? 'peggiore' : oppRank === 2 ? 'secondo peggiore' : 'terzo peggiore'
      events.push({
        type: 'win-vs-second-worst',
        points: luckPoints,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Vittoria di 1 goal contro il ${rankLabel} punteggio del turno (${oppScore.toFixed(1)} punti)`
      })
    }

    // EVENT 3: Narrow victory (1 goal, <6 points difference)
    // The closer the scores, the luckier the win: 6 - scoreDiff
    if (result === 'win' && goalDiff === 1 && scoreDiff < 6 && myScore >= 66 && oppRank > 3) {
      let luckPoints = 6 - scoreDiff // Max 5 at diff=1, min ~1 at diff=5
      
      // Additional luck if opponent was close to next goal
      const oppNextGoalThreshold = (oppGoals + 1) * 66
      const oppDistanceToNextGoal = oppNextGoalThreshold - oppScore
      
      if (oppDistanceToNextGoal <= 3) {
        luckPoints += 2 // Extra luck: opponent almost tied the game
      }
      
      events.push({
        type: 'win-narrow',
        points: luckPoints,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Vittoria di misura con solo ${scoreDiff.toFixed(1)} punti di scarto${oppDistanceToNextGoal <= 3 ? ` (avversario a ${oppDistanceToNextGoal.toFixed(1)} punti dal pareggio!)` : ''}`
      })
    }

    // EVENT 4: Draw with wide gap (lucky/unlucky draw)
    if (result === 'draw' && scoreDiff > 0) {
      // If I scored less than opponent, I'm lucky to have drawn
      // If I scored more than opponent, I'm unlucky to have drawn
      const maxDrawGap = 5.5 // Maximum possible gap for a draw (e.g., 72 vs 77.5)
      let baseLuckPoints = myScore < oppScore 
        ? (scoreDiff / maxDrawGap) * 3  // Lucky: lower score but drew
        : -(scoreDiff / maxDrawGap) * 3 // Unlucky: higher score but only drew
      
      // Additional luck/unluck based on proximity to next goal
      // Goals are at 66, 132 (66*2), 198 (66*3), etc.
      const myNextGoalThreshold = (myGoals + 1) * 66
      const oppNextGoalThreshold = (oppGoals + 1) * 66
      const myDistanceToNextGoal = myNextGoalThreshold - myScore
      const oppDistanceToNextGoal = oppNextGoalThreshold - oppScore
      
      // If opponent was very close to next goal (within 3 points), add luck
      if (oppDistanceToNextGoal <= 3) {
        baseLuckPoints += 2 // Extra luck for opponent almost scoring another goal
      }
      // If I was very close to next goal (within 3 points), add unluck
      if (myDistanceToNextGoal <= 3) {
        baseLuckPoints -= 2 // Extra unluck for almost scoring another goal myself
      }
      
      events.push({
        type: 'draw-wide-gap',
        points: baseLuckPoints,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: myScore < oppScore
          ? `Pareggio fortunato con ${scoreDiff.toFixed(1)} punti in meno dell'avversario${oppDistanceToNextGoal <= 3 ? ` (avversario a ${oppDistanceToNextGoal.toFixed(1)} punti dal goal successivo!)` : ''}`
          : `Pareggio sfortunato con ${scoreDiff.toFixed(1)} punti in più dell'avversario${myDistanceToNextGoal <= 3 ? ` (eri a ${myDistanceToNextGoal.toFixed(1)} punti dal goal successivo!)` : ''}`
      })
    }

    // EVENT 5: Narrow loss (unlucky)
    if (result === 'loss' && goalDiff === 1 && scoreDiff < 6) {
      let unluckPoints = -(6 - scoreDiff) // Same formula as narrow win, but negative
      
      // Additional unluck if I was close to next goal (would have tied)
      const myNextGoalThreshold = (myGoals + 1) * 66
      const myDistanceToNextGoal = myNextGoalThreshold - myScore
      
      if (myDistanceToNextGoal <= 3) {
        unluckPoints -= 2 // Extra unluck: I almost tied the game
      }
      
      events.push({
        type: 'loss-narrow',
        points: unluckPoints,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Sconfitta di misura per soli ${scoreDiff.toFixed(1)} punti${myDistanceToNextGoal <= 3 ? ` (eri a ${myDistanceToNextGoal.toFixed(1)} punti dal pareggio!)` : ''}`
      })
    }

    // EVENT 6: Loss despite being best score of the day (-8)
    if (result === 'loss' && myRank === bestRank) {
      events.push({
        type: 'loss-as-best',
        points: -8,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Sconfitta pur avendo il miglior punteggio del turno (${myScore.toFixed(1)} punti)`
      })
    }

    // EVENT 7: Loss despite being in top 3 (-4)
    if (result === 'loss' && myRank >= totalTeams - 2 && myRank !== bestRank) {
      events.push({
        type: 'loss-as-top3',
        points: -4,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Sconfitta pur essendo tra i migliori 3 punteggi (${myScore.toFixed(1)} punti)`
      })
    }

    // EVENT 8: Victory despite having worst or bottom 3 score (+3 max)
    if (result === 'win' && myRank <= 3) {
      events.push({
        type: 'win-as-worst',
        points: 3,
        gameId: game.id,
        gameDay: dayNumber,
        opponent: oppName,
        myScore,
        opponentScore: oppScore,
        result,
        detail: `Vittoria pur avendo uno dei peggiori punteggi del turno (${myScore.toFixed(1)} punti)`
      })
    }

    // EVENT 9: Playing against top 2 scores (unlucky modifier)
    // Second best rank is totalTeams - 1, best rank is totalTeams
    const secondBestRank = totalTeams - 1
    if (oppRank === secondBestRank || oppRank === bestRank) {
      let unluckPoints = 0
      let detail = ''
      const rankLabel = oppRank === bestRank ? 'miglior' : 'secondo miglior'
      
      if (result === 'win') {
        unluckPoints = -1
        detail = `Vittoria sfortunata contro il ${rankLabel} punteggio del turno (${oppScore.toFixed(1)} punti)`
      } else if (result === 'draw') {
        unluckPoints = -2
        detail = `Pareggio sfortunato contro il ${rankLabel} punteggio del turno (${oppScore.toFixed(1)} punti)`
      } else if (result === 'loss' && goalDiff === 1) {
        // Only count as unlucky if lost by 1 goal
        unluckPoints = -3
        detail = `Sconfitta sfortunata di 1 goal contro il ${rankLabel} punteggio del turno (${oppScore.toFixed(1)} punti)`
      }
      
      if (unluckPoints !== 0) {
        events.push({
          type: 'loss-as-top3', // Reusing type for unlucky scenarios
          points: unluckPoints,
          gameId: game.id,
          gameDay: dayNumber,
          opponent: oppName,
          myScore,
          opponentScore: oppScore,
          result,
          detail
        })
      }
    }

    return events
  }

  /**
   * Determine game result from goals
   */
  private static getGameResult(myGoals: number, oppGoals: number): 'win' | 'draw' | 'loss' {
    if (myGoals > oppGoals) return 'win'
    if (myGoals < oppGoals) return 'loss'
    return 'draw'
  }

  /**
   * Add events to team luck map
   */
  private static addEventsToTeam(
    map: Map<string, TeamLuck>,
    owner: string,
    name: string,
    events: LuckEvent[]
  ): void {
    let teamLuck = map.get(owner)
    
    if (!teamLuck) {
      teamLuck = {
        owner,
        name,
        totalLuck: 0,
        gamesPlayed: 0,
        avgLuck: 0,
        events: []
      }
      map.set(owner, teamLuck)
    }

    teamLuck.gamesPlayed++
    teamLuck.events.push(...events)
    teamLuck.totalLuck += events.reduce((sum, event) => sum + event.points, 0)
  }

  /**
   * Format luck value for display as percentage (for ranking table)
   * +1.5 becomes "+150%"
   */
  static formatLuck(luck: number): string {
    const percentage = Math.round(luck * 100)
    const sign = percentage > 0 ? '+' : ''
    return `${sign}${percentage}%`
  }

  /**
   * Format luck value for display as points (for detail modal)
   * +1.5 becomes "+1.5"
   */
  static formatLuckPoints(luck: number): string {
    const sign = luck > 0 ? '+' : ''
    return `${sign}${luck.toFixed(1)}`
  }

  /**
   * Get luck color based on value
   */
  static getLuckColor(luck: number): string {
    if (luck > 0) return '$green10'
    if (luck < 0) return '$red10'
    return '$color11'
  }
}
