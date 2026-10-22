'use strict';
let socket;
const states = new Map();
const address = 'ws://127.0.0.1:37891/yowidgets-media-v1';
const soundcloud = url => typeof url === 'string' && url.startsWith('https://soundcloud.com/');
function send(payload) { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(payload)); }
function publish() { send({ type: 'state', states: Array.from(states.values()).slice(0, 16) }); }
function remember(tabId, state) {
  if (state?.provider === 'soundcloud') states.set(tabId, { ...state, id: String(tabId) });
  else states.delete(tabId);
}
async function probe() {
  const tabs = await chrome.tabs.query({ url: 'https://soundcloud.com/*' });
  const existing = new Set(tabs.map(tab => tab.id));
  for (const id of states.keys()) if (!existing.has(id)) states.delete(id);
  await Promise.allSettled(tabs.slice(0, 16).map(async tab => {
    try { remember(tab.id, (await chrome.tabs.sendMessage(tab.id, { type: 'probe' }))?.state); }
    catch { states.delete(tab.id); }
  }));
  publish();
}
function connect() {
  if (socket && [WebSocket.OPEN, WebSocket.CONNECTING].includes(socket.readyState)) return;
  const candidate = new WebSocket(address);
  socket = candidate;
  candidate.onopen = () => probe().catch(() => {});
  candidate.onerror = () => {};
  candidate.onclose = () => { if (socket === candidate) socket = null; };
  candidate.onmessage = async event => {
    let message;
    try { message = JSON.parse(event.data); } catch { return; }
    if (message.type === 'probe') { await probe().catch(() => {}); return; }
    if (message.type !== 'command' || !['shuffle', 'repeat'].includes(message.action) || !/^\d+$/.test(message.targetId) || typeof message.requestId !== 'string') return;
    let ok = false;
    const tabId = Number(message.targetId);
    try {
      const tab = await chrome.tabs.get(tabId);
      if (soundcloud(tab.url) && states.has(tabId)) {
        const result = await chrome.tabs.sendMessage(tabId, { type: 'command', action: message.action, expectedTitle: message.expectedTitle, expectedDuration: message.expectedDuration, expectedPlaying: message.expectedPlaying });
        remember(tabId, result?.state);
        ok = result?.ok === true;
      }
    } catch { states.delete(tabId); }
    publish(); // Publish the source's real state before acknowledging the click.
    send({ type: 'ack', requestId: message.requestId, ok });
  };
}
chrome.runtime.onMessage.addListener((message, sender) => {
  if (sender.id !== chrome.runtime.id || !soundcloud(sender.tab?.url) || message.type !== 'source-state') return false;
  remember(sender.tab.id, message.state); connect(); publish(); return false;
});
chrome.tabs.onRemoved.addListener(id => { if (states.delete(id)) publish(); });
chrome.tabs.onUpdated.addListener((id, change) => { if (change.url && !soundcloud(change.url) && states.delete(id)) publish(); });
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === 'yowidgets-connect') connect(); });
chrome.alarms.create('yowidgets-connect', { periodInMinutes: 0.5 });
connect();
