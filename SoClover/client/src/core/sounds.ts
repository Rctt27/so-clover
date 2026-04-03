import { Howl } from 'howler';
import cardPlaceUrl from '../public/sounds/card-place.mp3';
import cardSwapUrl from '../public/sounds/card-swap.mp3';
import cardRotateUrl from '../public/sounds/card-rotate.mp3';
import boardRotateUrl from '../public/sounds/board-rotate.mp3';
import correctUrl from '../public/sounds/correct.mp3';
import incorrectUrl from '../public/sounds/incorrect.mp3';
import timerWarningUrl from '../public/sounds/timer-warning.mp3';

const sounds = {
  cardPlace: new Howl({ src: [cardPlaceUrl], volume: 0.5 }),
  cardSwap: new Howl({ src: [cardSwapUrl], volume: 0.5 }),
  cardRotate: new Howl({ src: [cardRotateUrl], volume: 0.5 }),
  boardRotate: new Howl({ src: [boardRotateUrl], volume: 0.5 }),
  boardValidationCorrect: new Howl({ src: [correctUrl], volume: 0.6 }),
  boardValidationIncorrect: new Howl({ src: [incorrectUrl], volume: 0.4 }),
  timerWarning: new Howl({ src: [timerWarningUrl], volume: 0.3 }),
};

export function playSound(name: keyof typeof sounds) {
  if (localStorage.getItem('so-clover-muted') === 'true') return;
  sounds[name].play();
}

export function toggleMute() {
  const current = localStorage.getItem('so-clover-muted') === 'true';
  localStorage.setItem('so-clover-muted', String(!current));
}

export function isMuted(): boolean {
  return localStorage.getItem('so-clover-muted') === 'true';
}
