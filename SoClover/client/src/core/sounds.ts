import { Howl } from 'howler';
import cardPlaceUrl from '../assets/sounds/card-place.mp3';
import cardSwapUrl from '../assets/sounds/card-swap.mp3';
import cardRotateUrl from '../assets/sounds/card-rotate.mp3';
import boardRotateUrl from '../assets/sounds/board-rotate.mp3';
import correctUrl from '../assets/sounds/correct.mp3';
import incorrectUrl from '../assets/sounds/incorrect.mp3';
import timerWarningUrl from '../assets/sounds/timer-warning.mp3';
import lofiMusicUrl from '../assets/sounds/writing-clues-lofi.mp3';

// ─── Volumes ─────────────────────────────────────────────────────────────────
const VOLUME_CARD_PLACE             = 0.4;
const VOLUME_CARD_SWAP              = 0.4;
const VOLUME_CARD_ROTATE            = 0.4;
const VOLUME_BOARD_ROTATE           = 0.4;
const VOLUME_BOARD_VALIDATION_OK    = 0.5;
const VOLUME_BOARD_VALIDATION_FAIL  = 0.3;
const VOLUME_TIMER_WARNING          = 0.2;
export const VOLUME_WRITING_MUSIC   = 0.10; // exportée — utilisée dans useWritingCluesPhaseMusic
// ─────────────────────────────────────────────────────────────────────────────

const sounds = {
  cardPlace: new Howl({ src: [cardPlaceUrl], volume: VOLUME_CARD_PLACE }),
  cardSwap: new Howl({ src: [cardSwapUrl], volume: VOLUME_CARD_SWAP }),
  cardRotate: new Howl({ src: [cardRotateUrl], volume: VOLUME_CARD_ROTATE }),
  boardRotate: new Howl({ src: [boardRotateUrl], volume: VOLUME_BOARD_ROTATE }),
  boardValidationCorrect: new Howl({ src: [correctUrl], volume: VOLUME_BOARD_VALIDATION_OK }),
  boardValidationIncorrect: new Howl({ src: [incorrectUrl], volume: VOLUME_BOARD_VALIDATION_FAIL }),
  timerWarning: new Howl({ src: [timerWarningUrl], volume: VOLUME_TIMER_WARNING }),
};

export const writingCluesMusic = new Howl({
  src: [lofiMusicUrl],
  loop: true,
  volume: 0,
  // html5: false (default) — le Web Audio API est déjà déverrouillé par les autres sons,
  // ce qui permet de lancer la musique depuis un callback SignalR (hors geste utilisateur).
  // html5: true bloquerait silencieusement la lecture (politique autoplay navigateur).
});

export function playSound(name: keyof typeof sounds) {
  if (localStorage.getItem('so-clover-muted') === 'true') return;
  sounds[name].play();
}

export function toggleMute() {
  const current = localStorage.getItem('so-clover-muted') === 'true';
  const next = !current;
  localStorage.setItem('so-clover-muted', String(next));
  window.dispatchEvent(new CustomEvent('so-clover-mute-changed', { detail: { muted: next } }));
}

export function isMuted(): boolean {
  return localStorage.getItem('so-clover-muted') === 'true';
}
