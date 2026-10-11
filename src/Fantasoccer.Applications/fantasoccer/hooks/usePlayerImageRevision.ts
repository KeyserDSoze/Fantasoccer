import { useSyncExternalStore } from 'react';
import { getPlayerImageRevision, subscribePlayerImageRevisions } from 'utils/playerImageRevision';

export function usePlayerImageRevision(key: string): string {
  return useSyncExternalStore(
    subscribePlayerImageRevisions,
    () => getPlayerImageRevision(key),
    () => ''
  );
}
