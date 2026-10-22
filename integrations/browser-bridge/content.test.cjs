const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
function scriptFixture(mode) {
  let handler, result, state = { title: 'Track', duration: 200, repeat: 0 }, timers = [], observers = [];
  const context = {
    document: { querySelector: () => ({}) },
    YoWidgetsSoundCloud: {
      read: () => ({ ...state }), matches: s => s?.title === 'Track',
      apply: () => { if (mode === 'sync') state.repeat = 1; return true; }
    },
    chrome: { runtime: { id: 'bridge', sendMessage: () => Promise.resolve(), onMessage: { addListener: fn => handler = fn } } },
    MutationObserver: class { constructor(fn) { this.fn = fn; this.live = false; observers.push(this); } observe() { this.live = true; } disconnect() { this.live = false; } },
    setTimeout: fn => { timers.push(fn); return timers.length; }, clearTimeout: () => {}
  };
  vm.runInNewContext(fs.readFileSync(require('node:path').join(__dirname, 'content.js'), 'utf8'), context);
  return { send() { handler({ type: 'command', action: 'repeat', expectedTitle: 'Track', expectedDuration: 200 }, { id: 'bridge' }, r => result = r); },
    result: () => result, timers: () => timers.length,
    sourceChanged(value = 1) { state.repeat = value; for (const observer of observers) if (observer.live) observer.fn(); },
    timeout() { for (const fn of timers) fn(); } };
}
test('confirmed source changes acknowledge without any background timer', () => {
  const f = scriptFixture('sync'); f.send();
  assert.equal(f.result()?.ok, true); assert.equal(f.result()?.state.repeat, 1); assert.equal(f.timers(), 0);
});
test('deferred source DOM confirmation does not wait for the timeout', () => {
  const f = scriptFixture('deferred'); f.send(); assert.equal(f.result(), undefined);
  f.sourceChanged(); assert.equal(f.result()?.ok, true); assert.equal(f.result()?.state.repeat, 1);
});
test('unchanged source is not acknowledged as a successful mode change', () => {
  const f = scriptFixture('refused'); f.send(); assert.equal(f.result(), undefined);
  f.timeout(); assert.equal(f.result()?.ok, false); assert.equal(f.result()?.state.repeat, 0);
});
test('a control disappearing during confirmation is not a successful toggle', () => {
  const f = scriptFixture('deferred'); f.send(); f.sourceChanged(null);
  assert.equal(f.result()?.ok, false);
});
