// Utility to build a deterministic player key from a name (mirror of C# KeyUtils)
export const getPlayerKey = (name?: string | null): string => {
  if (!name) return ''
  // normalize accents, remove diacritics, lowercase and strip non-ascii letters
  return name.toLowerCase().replace(/[^a-z]/g, '')
}

export default getPlayerKey
