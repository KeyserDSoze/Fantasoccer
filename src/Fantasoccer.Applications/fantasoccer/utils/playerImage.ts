import { Image } from 'react-native'
import { getPlayerKey } from './keyUtils'

const PLAYER_IMAGE_BASE_URL = 'https://fasostorage.blob.core.windows.net/images/players'
const DEFAULT_PLAYER_IMAGE_KEY = 'default'

const buildImageUrl = (key: string): string => {
  const sanitizedKey = key.trim().toLowerCase() || DEFAULT_PLAYER_IMAGE_KEY
  return `${PLAYER_IMAGE_BASE_URL}/${sanitizedKey}.jpg`
}

export const buildPlayerImageKeyFromName = (name?: string | null): string => {
  if (!name) {
    return DEFAULT_PLAYER_IMAGE_KEY
  }

  const computed = getPlayerKey(name)
  return computed
}

export const getPlayerImageUrlFromName = (name?: string | null): { src: string; fallback: string } => {
  const key = buildPlayerImageKeyFromName(name)
  return {
    src: buildImageUrl(key),
    fallback: buildImageUrl(DEFAULT_PLAYER_IMAGE_KEY)
  }
}

export const preloadPlayerImage = async (name?: string | null): Promise<boolean> => {
  const { src } = getPlayerImageUrlFromName(name)

  try {
    await Image.prefetch(src)
    return true
  } catch {
    return false
  }
}
