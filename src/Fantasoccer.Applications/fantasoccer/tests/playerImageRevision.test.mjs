import assert from 'node:assert/strict';
import test from 'node:test';
import {
  getPlayerImageRevision,
  setPlayerImageRevision,
  subscribePlayerImageRevisions,
} from '../utils/playerImageRevision.ts';

test('refreshes every copy of the player without changing other image revisions', () => {
  let updates = 0;
  const unsubscribe = subscribePlayerImageRevisions(() => updates++);
  assert.equal(getPlayerImageRevision('test-player'), '');
  setPlayerImageRevision('test-player', 'first-refresh');
  assert.equal(getPlayerImageRevision('test-player'), 'first-refresh');
  assert.equal(getPlayerImageRevision('other-player'), '');
  setPlayerImageRevision('test-player', 'second-refresh');
  assert.equal(getPlayerImageRevision('test-player'), 'second-refresh');
  assert.equal(updates, 2);
  unsubscribe();
  setPlayerImageRevision('test-player', 'third-refresh');
  assert.equal(updates, 2);
});

test('reads saved revisions after reopening the app', () => {
  const previousStorage = globalThis.localStorage;
  const saved = new Map([['player-image-revision:reopened-player', 'saved-revision']]);
  globalThis.localStorage = {
    getItem: (key) => saved.get(key) ?? null,
    setItem: (key, value) => saved.set(key, value),
  };
  try {
    assert.equal(getPlayerImageRevision('reopened-player'), 'saved-revision');
    setPlayerImageRevision('persisted-player', 'new-revision');
    assert.equal(saved.get('player-image-revision:persisted-player'), 'new-revision');
  } finally {
    if (previousStorage === undefined) delete globalThis.localStorage;
    else globalThis.localStorage = previousStorage;
  }
});
