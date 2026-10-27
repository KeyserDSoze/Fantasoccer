import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('minimal vote snapshots retain all named votes even with no club and inactive metadata', () => {
  const { enhanceVotedRealPlayersWrapper } = loadTypeScript('types/serieA.ts')
  const players = Array.from({ length: 317 }, (_, index) => {
    const suffix = String.fromCharCode(97 + Math.floor(index / 676), 97 + Math.floor(index / 26) % 26, 97 + index % 26)
    const key = `fixturekeeper${suffix}`
    return {
      playerId: key, playerKey: key, name: `Fixture keeper ${suffix}`, key,
      role: 0, roleLabel: 'P', isActive: false, isVisible: true, realTeam: null,
      vote: { role: 0, value: 6, hasVote: true, isFinal: false, goals: 0, penalties: 0, assists: 0,
        stoppedPenalties: 0, sufferedGoals: 0, wrongedPenalties: 0, ownGoals: 0, status: 0,
        manOfTheMatch: false, isOut: false, isIn: false, injured: false,
        hasPositiveBonus: false, hasNegativeBonus: false, hasDoneSomething: true },
    }
  })
  const result = enhanceVotedRealPlayersWrapper({ players })
  assert.equal(result.totalPlayers, 317)
  assert.equal(result.votedPlayersCount, 317)
  assert.equal(result.playersWithVote.length, 317)
  assert.equal(result.playersWithoutVote.length, 0)
  assert.equal(result.playersByRole[0].length, 317)
  for (const player of result.playersWithVote) {
    assert.equal(player.realTeam, null)
    assert.equal(player.isActive, false)
    assert.equal(player.vote.hasVote, true)
    assert.equal(player.vote.value, 6)
  }
})
