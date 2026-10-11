const revisions = new Map<string, string>();
const listeners = new Set<() => void>();

export const getPlayerImageRevision = (key: string): string => {
  if (revisions.has(key)) return revisions.get(key)!;
  try {
    return localStorage.getItem(`player-image-revision:${key}`) ?? '';
  } catch {
    return '';
  }
};

export const setPlayerImageRevision = (key: string, revision: string): void => {
  revisions.set(key, revision);
  try {
    localStorage.setItem(`player-image-revision:${key}`, revision);
  } catch {
    // The in-memory revision also works on native or when browser storage is unavailable.
  }
  listeners.forEach((listener) => listener());
};

export const subscribePlayerImageRevisions = (listener: () => void): (() => void) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
