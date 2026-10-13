import { createContext, type ReactNode, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { useAuth } from 'providers/AuthProvider'
import { useApp } from 'providers/AppProvider'
import { marketService } from 'services/marketService'
import { MarketHelper, MarketStatus } from 'types/market'
import { MarketType } from 'types/group'
import { getActualFantacalcioYear } from 'utils/timezoneHelpers'

export interface MainLeagueMarketActivity {
  mainLeagueId: string | null
  year: number
  activeMarketCount: number
  pendingVoteCount: number
  refresh: () => Promise<void>
}

const MarketActivityContext = createContext<MainLeagueMarketActivity>({
  mainLeagueId: null,
  year: getActualFantacalcioYear(),
  activeMarketCount: 0,
  pendingVoteCount: 0,
  refresh: async () => {},
})

export function MarketActivityProvider({ children }: { children: ReactNode }) {
  const { currentGroup } = useApp()
  const { user, isAuthenticated, isLoading: authLoading } = useAuth()
  const year = getActualFantacalcioYear()
  const mainLeague = currentGroup?.leagues.find(league => league.isMain) ?? null
  const mainLeagueId = mainLeague?.id ?? null
  const userEmail = user?.email?.trim().toLowerCase() ?? null
  const leagueRequiresVote = mainLeague?.years.find(annual => annual.year === year)?.settings.market === MarketType.WithVote

  const userTeamOwner = useMemo(() => {
    if (!currentGroup || !mainLeague || !userEmail) return null
    const basketIds = new Set(mainLeague.basketsId)
    for (const basket of currentGroup.baskets) {
      if (!basketIds.has(basket.id)) continue
      const yearlyBasket = basket.years.find(item => item.year === year)
      for (const team of yearlyBasket?.teams ?? []) {
        if (
          team.owner.toLowerCase() === userEmail ||
          team.additionalOwners?.some(owner => owner.toLowerCase() === userEmail)
        ) {
          return team.owner
        }
      }
    }
    return null
  }, [currentGroup, mainLeague, userEmail, year])

  const [activeMarketCount, setActiveMarketCount] = useState(0)
  const [pendingVoteCount, setPendingVoteCount] = useState(0)
  const requestSequence = useRef(0)

  const refresh = useCallback(async () => {
    const requestId = ++requestSequence.current
    if (authLoading) return
    if (!isAuthenticated || !currentGroup?.id || !mainLeagueId || !userEmail) {
      if (requestId === requestSequence.current) {
        setActiveMarketCount(0)
        setPendingVoteCount(0)
      }
      return
    }

    try {
      const markets = await marketService.getMarketsByLeague(currentGroup.id, year, mainLeagueId)
      if (requestId !== requestSequence.current) return
      setActiveMarketCount(markets.filter(market => market.status === MarketStatus.Pending).length)
      setPendingVoteCount(
        leagueRequiresVote && userTeamOwner
          ? MarketHelper.getMarketsAwaitingUserVote(markets, [userTeamOwner]).length
          : 0
      )
    } catch (error) {
      if (requestId === requestSequence.current) {
        console.warn('Unable to refresh main league market activity', error)
      }
    }
  }, [authLoading, isAuthenticated, currentGroup, mainLeagueId, userEmail, year, leagueRequiresVote, userTeamOwner])

  useEffect(() => {
    const initialRefresh = setTimeout(() => void refresh(), 0)
    const interval = setInterval(() => void refresh(), 20000)
    return () => {
      clearTimeout(initialRefresh)
      clearInterval(interval)
    }
  }, [refresh])

  const value = useMemo<MainLeagueMarketActivity>(() => ({
    mainLeagueId,
    year,
    activeMarketCount,
    pendingVoteCount,
    refresh,
  }), [mainLeagueId, year, activeMarketCount, pendingVoteCount, refresh])

  return <MarketActivityContext.Provider value={value}>{children}</MarketActivityContext.Provider>
}

export function useMainLeagueMarketActivity() {
  return useContext(MarketActivityContext)
}
