import { apiRequest } from './httpClient'

export interface NewsArticle { ordinal: number; leagueId: string | null; leagueName: string; title: string; content: string; date: string; image: string | null; format: 'plain-text' | 'legacy-html'; sourceVersion: number | null }
export interface NewsEdition { groupId: string; year: number; day: number; version: number; articles: NewsArticle[] }
export interface TeamAdvice { groupId: string; teamId: string; year: number; day: number; message: string; version: number; isGenerated: boolean; sourceRosterVersion: number | null }
const newsPath = (group: string, year: number, day: number) => `/api/groups/${encodeURIComponent(group)}/news/${year}/${day}`
const advicePath = (group: string, team: string, year: number, day: number) => `/api/groups/${encodeURIComponent(group)}/teams/${encodeURIComponent(team)}/advice/${year}/${day}`
// Content is text data. A consumer must never render legacy/model-generated HTML
// directly into the DOM; generation is always an explicit action, never a GET.
export const editorialService = {
  async news(group: string, year: number, day: number) { return (await apiRequest<NewsEdition | undefined>(newsPath(group, year, day))) ?? null },
  generateNews(group: string, league: string, year: number, day: number, expectedVersion: number) { return apiRequest<NewsEdition>(`${newsPath(group, year, day)}/${encodeURIComponent(league)}/generate`, 'POST', { expectedVersion }) },
  async advice(group: string, team: string, year: number, day: number) { return (await apiRequest<TeamAdvice | undefined>(advicePath(group, team, year, day))) ?? null },
  generateAdvice(group: string, team: string, year: number, day: number, expectedVersion: number, expectedRosterVersion: number) { return apiRequest<TeamAdvice>(`${advicePath(group, team, year, day)}/generate`, 'POST', { expectedVersion, expectedRosterVersion }) },
}
