(() => {
  'use strict';
  const provider = globalThis.YoWidgetsSoundCloud;
  let last = '', observer;
  function publish(force = false) {
    const state = provider.read(document), serialized = JSON.stringify(state);
    if (!force && serialized === last) return;
    last = serialized;
    chrome.runtime.sendMessage({ type: 'source-state', state }).catch(() => {});
  }
  function observe() {
    const root = document.querySelector('section[aria-label="miniplayer"]');
    if (!root) return false;
    observer?.disconnect();
    observer = new MutationObserver(() => publish());
    observer.observe(root, { subtree: true, childList: true, attributes: true, attributeFilter: ['class', 'title', 'aria-valuemax', 'disabled', 'aria-disabled'] });
    publish(true);
    return true;
  }
  if (!observe()) {
    observer = new MutationObserver(() => observe());
    observer.observe(document.documentElement, { childList: true, subtree: true });
  }
  chrome.runtime.onMessage.addListener((message, sender, respond) => {
    if (sender.id !== chrome.runtime.id) return false;
    if (message.type === 'probe') { respond({ state: provider.read(document) }); return false; }
    if (message.type !== 'command') return false;
    const before = provider.read(document);
    if (!provider.matches(before, message)) { respond({ ok: false, state: before }); return false; }
    let complete = false, timeout;
    const property = message.action === 'shuffle' ? 'shuffle' : 'repeat';
    const finish = (ok, state) => {
      if (complete) return;
      complete = true; confirmation.disconnect(); clearTimeout(timeout);
      respond({ ok, state });
    };
    const confirm = () => {
      if (complete) return;
      const state = provider.read(document);
      if (!provider.matches(state, message)) finish(false, state);
      else if (state[property] == null) finish(false, state);
      else if (state[property] !== before[property]) finish(true, state);
    };
    const confirmation = new MutationObserver(confirm);
    confirmation.observe(document.querySelector('section[aria-label="miniplayer"]'), { subtree: true, childList: true, attributes: true, attributeFilter: ['class', 'title', 'aria-valuemax', 'disabled', 'aria-disabled'] });
    if (!provider.apply(document, message)) { finish(false, provider.read(document)); return false; }
    confirm();
    if (complete) return false;
    Promise.resolve().then(confirm);
    // This is a refusal deadline, never the clock for a successful acknowledgement.
    timeout = setTimeout(() => finish(false, provider.read(document)), 1200);
    return true;
  });
})();
