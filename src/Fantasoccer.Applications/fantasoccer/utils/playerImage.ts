import { Image } from 'react-native'
import { RealPlayerHelper } from 'types/serieA'

const PLAYER_IMAGE_BASE_URL = 'https://fasostorage.blob.core.windows.net/images/players'
const DEFAULT_PLAYER_IMAGE_KEY = 'default'

const buildImageUrl = (key: string): string => {
  const sanitizedKey = key.trim().toLowerCase() || DEFAULT_PLAYER_IMAGE_KEY
  return `${PLAYER_IMAGE_BASE_URL}/${sanitizedKey}.jpg`
}

export interface PlayerImageOptions {
  key?: string | null
  name?: string | null
}

export const buildPlayerImageKey = (input?: string | null): string => {
  if (!input) {
    return DEFAULT_PLAYER_IMAGE_KEY
  }

  const normalized = input.trim().toLowerCase()
  return normalized || DEFAULT_PLAYER_IMAGE_KEY
}

export const buildPlayerImageKeyFromName = (name?: string | null): string => {
  if (!name) {
    return DEFAULT_PLAYER_IMAGE_KEY
  }

  const computed = RealPlayerHelper.buildKey(name)
  return buildPlayerImageKey(computed)
}

export const resolvePlayerImageKey = (options: PlayerImageOptions): string => {
  const { key, name } = options
  if (key) {
    return buildPlayerImageKey(key)
  }

  if (name) {
    return buildPlayerImageKeyFromName(name)
  }

  return DEFAULT_PLAYER_IMAGE_KEY
}

export const getPlayerImageUrl = (key?: string | null): string => {
  const resolvedKey = buildPlayerImageKey(key)
  return buildImageUrl(resolvedKey)
}

export const getPlayerImageUrlFromName = (name?: string | null): string => {
  const key = buildPlayerImageKeyFromName(name)
  return buildImageUrl(key)
}

export const getPlayerImageUrlFromOptions = (options: PlayerImageOptions): string => {
  const key = resolvePlayerImageKey(options)
  return buildImageUrl(key)
}

export const getPlayerImageSources = (options: PlayerImageOptions = {}): { src: string; fallback: string } => {
  const key = resolvePlayerImageKey(options)
  return {
    src: buildImageUrl(key),
    fallback: buildImageUrl(DEFAULT_PLAYER_IMAGE_KEY)
  }
}

export const preloadPlayerImage = async (options: PlayerImageOptions): Promise<boolean> => {
  const { src } = getPlayerImageSources(options)

  try {
    await Image.prefetch(src)
    return true
  } catch {
    return false
  }
}
