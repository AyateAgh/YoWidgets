const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
function fixture(token) {
  const sockets = [], sent = [], events = [], calls = [];
  let listener, nodes = [], tab = { id: 7, title: 'Fixture', url: 'https://example.test/page' };
  class Input { set value(v) { this.saved = v; } }
  class Socket { static OPEN = 1; static CONNECTING = 0; constructor(url) { this.url = url; this.readyState = 0; sockets.push(this); } send(s) { sent.push(JSON.parse(s)); } close() { this.readyState = 3; this.onclose?.(); } }
  const context = {
    URL, WebSocket: Socket, navigator: { userAgent: 'Chrome fixture' }, crypto: { randomUUID: () => 'snapshot-1' },
    document: { title: 'Fixture', body: { innerText: 'Public fixture text' }, images: [], querySelectorAll: () => nodes }, location: { href: tab.url }, window: { scrollBy: (_x, y) => calls.push(y) },
    HTMLInputElement: Input, HTMLTextAreaElement: Input, Event: class { constructor(name) { this.type = name; } },
    chrome: { storage: { local: { get: async () => ({ token }), set: async () => {} } },
      runtime: { id: 'trusted-extension', onMessage: { addListener: fn => listener = fn } },
      tabs: { query: async () => [tab, { id: 8, url: 'chrome://settings/' }], get: async () => tab, ...Object.fromEntries(['onCreated','onRemoved','onUpdated','onActivated'].map(k => [k, { addListener: f => events.push(f) }])) },
      scripting: { executeScript: async args => { calls.push(args); return [{ result: { title: 'Fixture', text: 'Read content' } }]; } }, alarms: { create: () => {}, onAlarm: { addListener: () => {} } }
    }
  };
  vm.createContext(context); vm.runInContext(fs.readFileSync(path.join(__dirname, 'worker.js'), 'utf8'), context);
  return { context, sockets, sent, calls, listener: (...a) => listener(...a), nodes: value => nodes = value, navigate: url => tab = { ...tab, url },
    control(type = 'text') { return { tagName: 'INPUT', type, value: 'private-value', getAttribute: name => name === 'placeholder' ? 'Search fixture' : null, getBoundingClientRect: () => ({ width: 100, height: 30 }), isConnected: true, click: () => calls.push('click'), dispatchEvent: event => calls.push(event.type) }; } };
}
const tick = () => new Promise(resolve => setImmediate(resolve));
test('No connection or tab metadata is published before the user pairs', async () => {
  const f = fixture(); await tick(); assert.equal(f.sockets.length, 0); assert.equal(f.sent.length, 0);
});
test('Pairing handshake publishes only web tabs after ready; page senders cannot re-pair', async () => {
  const f = fixture('a'.repeat(44)); await tick(); const socket = f.sockets[0]; socket.readyState = 1; socket.onopen(); assert.equal(f.sent[0].type, 'hello'); assert.equal(f.sent.length, 1);
  socket.onmessage({ data: JSON.stringify({ type: 'ready' }) }); await tick(); assert.equal(f.sent[1].tabs.length, 1); assert.equal(f.sent[1].tabs[0].id, '7');
  assert.equal(f.listener({ type: 'pair', token: 'b'.repeat(44) }, { id: 'trusted-extension', tab: { id: 7 } }, () => assert.fail('page sender replied')), false);
});
test('Read produces real controls and bounded text without serializing field values', () => {
  const f = fixture(), control = f.control(), password = f.control('password'); f.nodes([control,password]);
  const result = f.context.pageAction('read', { offset: 0, limit: 100 }); assert.equal(result.controls.length, 2); assert.equal(result.controls[1].password, true); assert.ok(!JSON.stringify(result).includes('private-value')); assert.ok(result.snapshotId);
});
test('Stale snapshots, password/file/read-only fields cannot be filled', () => {
  const f = fixture(), node = f.control('password'); f.nodes([node]); const read = f.context.pageAction('read', {});
  assert.throws(() => f.context.pageAction('fill', { snapshotId: 'old', controlId: 0, text: 'x' }), /snapshot changed/);
  assert.throws(() => f.context.pageAction('fill', { snapshotId: read.snapshotId, controlId: 0, text: 'x' }), /password/);
  node.type = 'file'; assert.throws(() => f.context.pageAction('fill', { snapshotId: read.snapshotId, controlId: 0, text: 'x' }), /protected/);
  node.type = 'text'; node.readOnly = true; assert.throws(() => f.context.pageAction('fill', { snapshotId: read.snapshotId, controlId: 0, text: 'x' }), /read-only/);
});
test('Native field setter dispatches input/change and scroll stays bounded', () => {
  const f = fixture(), node = f.control(); f.nodes([node]); const read = f.context.pageAction('read', {});
  f.context.pageAction('fill', { snapshotId: read.snapshotId, controlId: 0, text: 'Fixture input' }); assert.equal(node.saved, 'Fixture input'); assert.deepEqual(f.calls, ['input','change']);
  f.context.pageAction('scroll', { y: 90000 }); assert.equal(f.calls.at(-1), 1500);
});
test('Navigation revokes execution and arbitrary browser scripts are rejected', async () => {
  const f = fixture('a'.repeat(44)); await tick(); f.sockets[0].readyState = 1;
  await f.context.execute({ requestId: 'test', tabId: '7', expectedUrl: 'https://example.test/old', action: 'read' }); assert.match(f.sent.at(-1).error, /navigated/); assert.equal(f.calls.length, 0);
  await f.context.execute({ requestId: 'test2', tabId: '7', expectedUrl: 'https://example.test/page', action: 'javascript' }); assert.match(f.sent.at(-1).error, /Unsupported/); assert.equal(f.calls.length, 0);
});
