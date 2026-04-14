import { useEffect, useRef, useCallback } from 'react';
import { useGameStore } from '../core/store';
import { writingCluesMusic, isMuted, VOLUME_WRITING_MUSIC } from '../core/sounds';

const MUSIC_TARGET_VOLUME = VOLUME_WRITING_MUSIC;
const FADE_IN_MS = 5_000;
const FADE_OUT_DURATION_MS = 5_000;
const FADE_OUT_BEFORE_END_MS = 10_000;

export const useWritingCluesPhaseMusic = () => {
  const phase = useGameStore(s => s.phase);
  const phaseEndsAtUtc = useGameStore(s => s.phaseEndsAtUtc);
  const fadeOutTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const stopAfterFadeRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimeouts = useCallback(() => {
    if (fadeOutTimeoutRef.current) {
      clearTimeout(fadeOutTimeoutRef.current);
      fadeOutTimeoutRef.current = null;
    }
    if (stopAfterFadeRef.current) {
      clearTimeout(stopAfterFadeRef.current);
      stopAfterFadeRef.current = null;
    }
  }, []);

  const scheduleFadeOut = useCallback((phaseEndsAt: string) => {
    clearTimeouts();
    const msUntilFadeOut = new Date(phaseEndsAt).getTime() - Date.now() - FADE_OUT_BEFORE_END_MS;
    const delay = Math.max(0, msUntilFadeOut);
    fadeOutTimeoutRef.current = setTimeout(() => {
      writingCluesMusic.fade(writingCluesMusic.volume(), 0, FADE_OUT_DURATION_MS);
      stopAfterFadeRef.current = setTimeout(() => {
        writingCluesMusic.stop();
      }, FADE_OUT_DURATION_MS);
    }, delay);
  }, [clearTimeouts]);

  // ─── Lifecycle de phase ───────────────────────────────────────────────────
  useEffect(() => {
    if (phase !== 'WritingClues') {
      clearTimeouts();
      writingCluesMusic.stop();
      return;
    }
    if (isMuted()) return;

    writingCluesMusic.volume(0);
    writingCluesMusic.play();
    writingCluesMusic.fade(0, MUSIC_TARGET_VOLUME, FADE_IN_MS);
    if (phaseEndsAtUtc) {
      const msRemaining = new Date(phaseEndsAtUtc).getTime() - Date.now();
      if (msRemaining > FADE_OUT_BEFORE_END_MS) {
        scheduleFadeOut(phaseEndsAtUtc);
      }
    }

    return () => {
      clearTimeouts();
      writingCluesMusic.stop();
    };
    // phaseEndsAtUtc géré par l'effet suivant — intentionnellement absent ici
  }, [phase]);

  // ─── Recalcul du fade-out si phaseEndsAtUtc change en cours de phase ─────
  useEffect(() => {
    if (phase !== 'WritingClues' || isMuted() || !writingCluesMusic.playing() || !phaseEndsAtUtc) return;
    const msRemaining = new Date(phaseEndsAtUtc).getTime() - Date.now();
    if (msRemaining > FADE_OUT_BEFORE_END_MS) {
      scheduleFadeOut(phaseEndsAtUtc);
    }
  }, [phaseEndsAtUtc]);

  // ─── Réactivité mute/un-mute ──────────────────────────────────────────────
  useEffect(() => {
    const handleMuteChanged = (e: Event) => {
      const { muted } = (e as CustomEvent<{ muted: boolean }>).detail;
      // Lire l'état courant depuis le store pour éviter les problèmes de closure
      const currentPhase = useGameStore.getState().phase;
      const currentPhaseEndsAt = useGameStore.getState().phaseEndsAtUtc;
      if (currentPhase !== 'WritingClues') return;

      if (muted) {
        clearTimeouts();
        writingCluesMusic.fade(writingCluesMusic.volume(), 0, 1_000);
        stopAfterFadeRef.current = setTimeout(() => writingCluesMusic.stop(), 1_000);
      } else {
        writingCluesMusic.volume(0);
        writingCluesMusic.play();
        writingCluesMusic.fade(0, MUSIC_TARGET_VOLUME, FADE_IN_MS);
        if (currentPhaseEndsAt) {
          const msRemaining = new Date(currentPhaseEndsAt).getTime() - Date.now();
          if (msRemaining > FADE_OUT_BEFORE_END_MS) {
            scheduleFadeOut(currentPhaseEndsAt);
          }
        }
      }
    };

    window.addEventListener('so-clover-mute-changed', handleMuteChanged);
    return () => window.removeEventListener('so-clover-mute-changed', handleMuteChanged);
  }, []);
};
