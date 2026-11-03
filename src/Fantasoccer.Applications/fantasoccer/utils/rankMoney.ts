// Credits earned with the goals of a ranking row. The server already applies the rules of the league and
// of the shown season (plusMoney); the rules passed in are only a fallback for rows that do not carry it.
export function rankPlusMoney(
  team: { plusMoney?: number | null; goal?: number | null; sufferedGoal?: number | null },
  moneyForGoal = 0,
  moneyForSufferedGoal = 0
): number {
  if (typeof team.plusMoney === 'number' && Number.isFinite(team.plusMoney)) return team.plusMoney
  return (team.goal ?? 0) * moneyForGoal + (team.sufferedGoal ?? 0) * moneyForSufferedGoal
}

// Credits for goals scored and conceded: by default on for the main league and off for the others, unless the league says otherwise.
export function goalCreditsEnabled(isMain: boolean | null | undefined, settings?: { goalCreditsEnabled?: boolean | null } | null): boolean {
  return settings?.goalCreditsEnabled ?? Boolean(isMain)
}
