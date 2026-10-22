const { test } = require('node:test');
const assert = require('node:assert/strict');
const provider = require('./soundcloud.js');

function fixture() {
  let shuffle = false, repeat = 0, clicks = 0;
  const root = { querySelector(selector) { return elements[selector] ?? null; } };
  const button = (action) => ({ disabled: false, getAttribute: () => null,
    classList: { contains(name) { return name === 'm-shuffling' ? shuffle : name === 'm-one' ? repeat === 1 : name === 'm-all' ? repeat === 2 : false; } },
    click() { clicks++; if (action === 'shuffle') shuffle = !shuffle; else repeat = (repeat + 1) % 3; }
  });
  const elements = {
    'a.playbackSoundBadge__titleLink': { getAttribute: () => 'SOPHIE - VYZEE' },
    'a.playbackSoundBadge__lightLink': { getAttribute: () => 'SOPHIE' },
    '.playbackTimeline__progressWrapper': { getAttribute: name => name === 'aria-valuemax' ? '197' : null },
    'button.playControl': { getAttribute: () => 'Pause current', textContent: 'Pause current' },
    'button.shuffleControl': button('shuffle'), 'button.repeatControl': button('repeat')
  };
  return { document: { querySelector: () => root }, elements, clicks: () => clicks };
}
test('snapshot reads source state and capabilities', () => {
  const f = fixture(), s = provider.read(f.document);
  assert.equal(s.title, 'SOPHIE - VYZEE'); assert.equal(s.playing, true);
  assert.equal(s.shuffle, false); assert.equal(s.repeat, 0);
  assert.equal(s.canShuffle, true); assert.equal(s.canRepeat, true);
});
test('commands click the source and read its resulting state', () => {
  const f = fixture(), command = { action: 'shuffle', expectedTitle: 'SOPHIE - VYZEE', expectedDuration: 197.834, expectedPlaying: true };
  assert.equal(provider.apply(f.document, command), true);
  assert.equal(provider.read(f.document).shuffle, true); assert.equal(f.clicks(), 1);
  for (const expected of [1, 2, 0]) { assert.equal(provider.apply(f.document, { ...command, action: 'repeat' }), true); assert.equal(provider.read(f.document).repeat, expected); }
});
test('wrong track, duration, action and disabled controls cannot be clicked', () => {
  const f = fixture(), c = { action: 'shuffle', expectedTitle: 'SOPHIE - VYZEE', expectedDuration: 197, expectedPlaying: true };
  for (const change of [{ expectedTitle: 'Other song' }, { expectedDuration: 250 }, { action: 'play' }, { expectedDuration: null }]) assert.equal(provider.apply(f.document, { ...c, ...change }), false);
  f.elements['button.shuffleControl'].disabled = true;
  assert.equal(provider.apply(f.document, c), false); assert.equal(f.clicks(), 0);
});
test('missing source buttons are unavailable, without placeholders', () => {
  const f = fixture(); delete f.elements['button.shuffleControl']; delete f.elements['button.repeatControl'];
  assert.equal(provider.read(f.document).canShuffle, false); assert.equal(provider.read(f.document).canRepeat, false);
  assert.equal(provider.apply(f.document, { action: 'repeat', expectedTitle: 'SOPHIE - VYZEE', expectedDuration: 197, expectedPlaying: true }), false);
  assert.equal(provider.read({ querySelector: () => null }), null);
});
test('a playback swap between matching tabs rejects the former target', () => {
  const f = fixture();
  f.elements['button.playControl'].getAttribute = () => 'Play current';
  assert.equal(provider.apply(f.document, { action: 'shuffle', expectedTitle: 'SOPHIE - VYZEE', expectedDuration: 197, expectedPlaying: true }), false);
  assert.equal(f.clicks(), 0);
  assert.equal(provider.apply(f.document, { action: 'shuffle', expectedTitle: 'SOPHIE - VYZEE', expectedDuration: 197, expectedPlaying: false }), true);
});
test('manifest identity matches the listener and restricts site access', () => {
  const fs = require('node:fs'), crypto = require('node:crypto');
  const manifest = require('./manifest.json');
  const id = crypto.createHash('sha256').update(Buffer.from(manifest.key, 'base64')).digest('hex').slice(0, 32).replace(/[0-9a-f]/g, c => String.fromCharCode(97 + parseInt(c, 16)));
  assert.equal(id, 'fllmnphklooimbfkdomfjlgjnphkkncm');
  assert.deepEqual(manifest.content_scripts[0].matches, ['https://soundcloud.com/*']);
  assert.deepEqual(manifest.permissions, ['alarms']);
  for (const file of ['worker.js', ...manifest.content_scripts[0].js]) assert.ok(fs.statSync(require('node:path').join(__dirname, file)).size > 0);
});
