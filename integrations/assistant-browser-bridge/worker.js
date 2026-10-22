'use strict';
let socket, paired = false;
const address = 'ws://127.0.0.1:37892/yowidgets-assistant-v1';
const web = value => { try { const u = new URL(value); return ['http:', 'https:'].includes(u.protocol) && !u.username && !u.password; } catch { return false; } };
function send(value) { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(value)); }
async function publish() {
  if (!paired) return;
  const tabs = await chrome.tabs.query({});
  send({ type: 'tabs', tabs: tabs.filter(t => web(t.url)).slice(0, 100).map(t => ({ id: String(t.id), title: (t.title || t.url).slice(0, 500), url: t.url, browser: navigator.userAgent.includes('Edg/') ? 'Edge' : 'Chrome' })) });
}
// Runs only inside the explicitly attached tab, in the extension's isolated world.
function pageAction(action, payload) {
  const clean = node => (node.innerText || node.getAttribute('aria-label') || node.getAttribute('placeholder') || node.getAttribute('title') || node.tagName).trim().slice(0, 180);
  if (action === 'read') {
    const text = (document.body?.innerText || '').slice(0, 2000000);
    const offset = Math.max(0, Math.min(Number(payload.offset) || 0, text.length));
    const limit = Math.max(100, Math.min(Number(payload.limit) || 24000, 48000));
    const snapshot = crypto.randomUUID();
    const nodes = Array.from(document.querySelectorAll('a,button,input,textarea,select,[role="button"]')).filter(n => n.getBoundingClientRect().width && n.getBoundingClientRect().height).slice(0, 1000);
    globalThis.__yoWidgetsSnapshot = { id: snapshot, nodes };
    return { title: document.title, url: location.href, offset, totalLength: text.length, nextOffset: Math.min(text.length, offset + limit), text: text.slice(offset, offset + limit), snapshotId: snapshot, controls: nodes.map((n, i) => ({ id: i, role: n.tagName.toLowerCase(), label: clean(n), password: n.type === 'password', disabled: !!n.disabled })), images: Array.from(document.images).slice(0, 80).map(n => ({ alt: n.alt, url: n.currentSrc || n.src })) };
  }
  if (action === 'scroll') { window.scrollBy(0, Math.max(-1500, Math.min(1500, Number(payload.y) || 0))); return { scrolled: true, url: location.href }; }
  const state = globalThis.__yoWidgetsSnapshot;
  if (!state || state.id !== payload.snapshotId) throw Error('The page snapshot changed. Read the tab again.');
  const node = state.nodes[Number(payload.controlId)];
  if (!node?.isConnected || node.disabled || !node.getBoundingClientRect().width || node.type === 'password') throw Error('This control is unavailable or is a protected password field.');
  if (action === 'describe') return { label: clean(node), role: node.tagName, url: location.href };
  if (action === 'click') { node.click(); return { clicked: true, url: location.href }; }
  if (action === 'fill') {
    if (!['INPUT', 'TEXTAREA'].includes(node.tagName)) throw Error('Choose a text input.');
    if (['file', 'password', 'hidden'].includes(node.type) || node.readOnly) throw Error('This input is protected or read-only.');
    const value = String(payload.text || ''); if (value.length > 16000) throw Error('Text is too long.');
    const prototype = node.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(prototype, 'value').set.call(node, value); node.dispatchEvent(new Event('input', { bubbles: true })); node.dispatchEvent(new Event('change', { bubbles: true }));
    return { filled: true, url: location.href };
  }
  throw Error('Unsupported browser action.');
}
async function execute(message) {
  if (!/^\d+$/.test(message.tabId) || typeof message.requestId !== 'string') return;
  try {
    const tabId = Number(message.tabId), tab = await chrome.tabs.get(tabId);
    if (!web(tab.url) || tab.url !== message.expectedUrl) throw Error('The tab navigated. Attach it again.');
    if (!['read', 'describe', 'click', 'fill', 'scroll'].includes(message.action)) throw Error('Unsupported browser action.');
    const results = await chrome.scripting.executeScript({ target: { tabId }, func: pageAction, args: [message.action, message.payload || {}] });
    if (!results[0]?.result) throw Error('This page could not be inspected. Browser-internal pages and the extension store are protected.');
    send({ type: 'result', requestId: message.requestId, result: results[0].result }); await publish();
  } catch (e) { send({ type: 'result', requestId: message.requestId, error: String(e.message || e).slice(0, 500) }); }
}
async function connect() {
  if (socket && [WebSocket.OPEN, WebSocket.CONNECTING].includes(socket.readyState)) return;
  const { token } = await chrome.storage.local.get('token'); if (!token) return;
  const candidate = new WebSocket(address); socket = candidate; paired = false;
  candidate.onopen = () => send({ type: 'hello', token });
  candidate.onerror = () => {};
  candidate.onclose = () => { if (socket === candidate) { socket = null; paired = false; } };
  candidate.onmessage = event => {
    let message; try { message = JSON.parse(event.data); } catch { return; }
    if (message.type === 'ready') { paired = true; publish().catch(() => {}); }
    else if (paired && message.type === 'probe') publish().catch(() => {});
    else if (paired && message.type === 'request') execute(message);
  };
}
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (sender.id !== chrome.runtime.id || sender.tab) return false;
  if (message.type === 'pair' && /^[A-Za-z0-9+/=]{40,100}$/.test(message.token || '')) { chrome.storage.local.set({ token: message.token }).then(() => { socket?.close(); socket = null; return connect(); }).then(() => reply({ ok: true })); return true; }
  if (message.type === 'status') reply({ connected: paired }); return false;
});
for (const event of [chrome.tabs.onCreated, chrome.tabs.onRemoved, chrome.tabs.onUpdated, chrome.tabs.onActivated]) event.addListener(() => publish().catch(() => {}));
chrome.alarms.create('connect', { periodInMinutes: 0.5 }); chrome.alarms.onAlarm.addListener(() => connect().catch(() => {})); connect().catch(() => {});
