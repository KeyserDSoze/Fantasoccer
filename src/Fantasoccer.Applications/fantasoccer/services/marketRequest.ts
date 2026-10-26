import type { MarketDraft } from '../types/market'

export function marketRequest(draft: MarketDraft) {
  const buyerPlayerIds = draft.buyerPlayers.map(player => player.playerId)
  const sellerPlayerIds = draft.sellerPlayers.map(player => player.playerId)
  if (!draft.buyerTeamId || !draft.sellerTeamId || buyerPlayerIds.some(id => !id) || sellerPlayerIds.some(id => !id) ||
      !Number.isInteger(draft.expectedBuyerRosterVersion) || !Number.isInteger(draft.expectedSellerRosterVersion))
    throw new Error('Ricaricare le rose e selezionare squadre/giocatori con identità PostgreSQL.')
  return { buyerTeamId: draft.buyerTeamId, sellerTeamId: draft.sellerTeamId, buyerPlayerIds, sellerPlayerIds,
    moneyFromBuyer: draft.moneyFromBuyer, moneyFromSeller: draft.moneyFromSeller,
    expectedBuyerRosterVersion: draft.expectedBuyerRosterVersion, expectedSellerRosterVersion: draft.expectedSellerRosterVersion }
}
