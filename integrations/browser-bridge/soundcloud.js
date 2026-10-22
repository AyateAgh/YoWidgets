(function (host) {
  'use strict';
  const key = text => String(text ?? '').normalize('NFKC').replace(/[^\p{L}\p{N}]/gu, '').toLowerCase();
  const enabled = element => !!element && !element.disabled && element.getAttribute('aria-disabled') !== 'true';
  function read(document) {
    const root = document.querySelector('section[aria-label="miniplayer"]');
    if (!root) return null;
    const title = root.querySelector('a.playbackSoundBadge__titleLink')?.getAttribute('title')?.trim();
    const artistElement = root.querySelector('a.playbackSoundBadge__lightLink');
    const duration = Number(root.querySelector('.playbackTimeline__progressWrapper')?.getAttribute('aria-valuemax'));
    if (!title || !Number.isFinite(duration) || duration <= 0 || duration > 86400) return null;
    const play = root.querySelector('button.playControl');
    const shuffle = root.querySelector('button.shuffleControl'), repeat = root.querySelector('button.repeatControl');
    return { provider: 'soundcloud', title, artist: (artistElement?.getAttribute('title') ?? artistElement?.textContent ?? '').trim(),
      duration, playing: /^pause\b/i.test(play?.getAttribute('title') ?? play?.textContent ?? ''),
      canShuffle: enabled(shuffle), canRepeat: enabled(repeat),
      shuffle: enabled(shuffle) ? shuffle.classList.contains('m-shuffling') : null,
      repeat: enabled(repeat) ? repeat.classList.contains('m-one') ? 1 : repeat.classList.contains('m-all') ? 2 : 0 : null };
  }
  function matches(state, command) {
    return !!state && typeof command.expectedTitle === 'string' && key(state.title) === key(command.expectedTitle) &&
      typeof command.expectedPlaying === 'boolean' && state.playing === command.expectedPlaying &&
      typeof command.expectedDuration === 'number' && Number.isFinite(command.expectedDuration) && command.expectedDuration > 0 && Math.abs(state.duration - command.expectedDuration) <= 2;
  }
  function apply(document, command) {
    if (!command || !['shuffle', 'repeat'].includes(command.action) || !matches(read(document), command)) return false;
    const button = document.querySelector('section[aria-label="miniplayer"]')?.querySelector(`button.${command.action}Control`);
    if (!enabled(button)) return false;
    button.click(); // SoundCloud owns the queue, transition and confirmation.
    return true;
  }
  const api = { read, apply, matches };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else host.YoWidgetsSoundCloud = api;
})(globalThis);
