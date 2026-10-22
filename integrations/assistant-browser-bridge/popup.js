'use strict';
chrome.runtime.sendMessage({ type: 'status' }).then(s => document.querySelector('#state').textContent = s?.connected ? 'Connected' : 'Open YoWidgets, then connect.');
document.querySelector('#pair').onclick = async () => {
  const token = document.querySelector('#token').value.trim();
  if (!/^[A-Za-z0-9+/=]{40,100}$/.test(token)) { document.querySelector('#state').textContent = 'Paste the complete pairing code.'; return; }
  await chrome.runtime.sendMessage({ type: 'pair', token }); document.querySelector('#token').value = ''; document.querySelector('#state').textContent = 'Connecting…';
  setTimeout(async () => { const s = await chrome.runtime.sendMessage({ type: 'status' }); document.querySelector('#state').textContent = s?.connected ? 'Connected' : 'Not connected. Check that YoWidgets is open and the pairing code matches.'; }, 1800);
};
