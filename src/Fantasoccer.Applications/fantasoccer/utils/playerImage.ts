import { Image } from 'react-native';
import { getPlayerKey } from './keyUtils';
import { getPlayerImageRevision } from './playerImageRevision';

const BLOB_BASE_URL = process.env.EXPO_PUBLIC_BLOB_URL?.replace(/\/$/, '') || 'https://fasostorage.blob.core.windows.net';
const PLAYER_IMAGE_BASE_URL = `${BLOB_BASE_URL}/images/players`;
const DEFAULT_PLAYER_IMAGE_KEY = 'default';

const buildImageUrl = (key: string): string => {
  const sanitizedKey = key.trim().toLowerCase() || DEFAULT_PLAYER_IMAGE_KEY;
  return `${PLAYER_IMAGE_BASE_URL}/${sanitizedKey}.jpg`;
};

export const buildPlayerImageKeyFromName = (name?: string | null): string => {
  if (!name) {
    return DEFAULT_PLAYER_IMAGE_KEY;
  }

  const computed = getPlayerKey(name);
  return computed;
};

export const getPlayerImageUrlFromName = (
  name?: string | null,
  revision?: string,
  playerId?: string | null
): { src: string; fallback: string; placeholder: string } => {
  const alias = buildPlayerImageKeyFromName(name);
  const key = playerId || alias;
  const imageRevision = revision ?? getPlayerImageRevision(key);
  return {
    src: buildImageUrl(key) + (imageRevision ? `?v=${encodeURIComponent(imageRevision)}` : ''),
    // Old dumps/files used the name alias; new refreshes use the authoritative player ID.
    fallback: buildImageUrl(key === alias ? DEFAULT_PLAYER_IMAGE_KEY : alias),
    placeholder: buildImageUrl(DEFAULT_PLAYER_IMAGE_KEY),
  };
};

export const preloadPlayerImage = async (name?: string | null): Promise<boolean> => {
  const { src } = getPlayerImageUrlFromName(name);

  try {
    await Image.prefetch(src);
    return true;
  } catch {
    return false;
  }
};
