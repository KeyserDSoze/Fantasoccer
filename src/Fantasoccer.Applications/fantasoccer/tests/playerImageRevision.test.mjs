import assert from 'node:assert/strict';
import test from 'node:test';
import { createRequire } from 'node:module';
import {
  getPlayerImageRevision,
  setPlayerImageRevision,
  subscribePlayerImageRevisions,
} from '../utils/playerImageRevision.ts';

const require = createRequire(import.meta.url);
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs');

test('authoritative image IDs do not collide for equal names and legacy aliases remain a fallback', () => {
  const { getPlayerImageUrlFromName } = loadTypeScript('utils/playerImage.ts', {
    'react-native': { Image: {} },
    './playerImageRevision': { getPlayerImageRevision },
  }, { process: { env: { EXPO_PUBLIC_BLOB_URL: 'https://files.example.test' } } });
  setPlayerImageRevision('imagefirst', 'revision-a');
  setPlayerImageRevision('imagesecond', 'revision-b');
  const first = getPlayerImageUrlFromName('Same Player', undefined, 'imagefirst');
  const second = getPlayerImageUrlFromName('Same Player', undefined, 'imagesecond');
  assert.equal(first.src, 'https://files.example.test/images/players/imagefirst.jpg?v=revision-a');
  assert.equal(second.src, 'https://files.example.test/images/players/imagesecond.jpg?v=revision-b');
  assert.equal(first.fallback, 'https://files.example.test/images/players/sameplayer.jpg');
  assert.equal(first.placeholder, 'https://files.example.test/images/players/default.jpg');
  const legacy = getPlayerImageUrlFromName('Same Player');
  assert.equal(legacy.src, first.fallback);
  assert.equal(legacy.fallback, first.placeholder);
});

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
