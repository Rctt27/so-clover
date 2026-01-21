/**
 * useLocalCursorEmitter - Hook pour émettre les mouvements de souris locaux
 *
 * Responsabilités :
 * - Écouter les événements mousemove
 * - Convertir en coordonnées normalisées
 * - Filtrer via MouseSampler
 * - Batcher et envoyer via SignalR
 */

import { useEffect, useRef, useCallback } from 'react';
import { signalRClient } from '../../../api/signalr-client';
import { useGameStore, useGuessingStore } from '../../../core/store';
import { MouseSampler } from './MouseSampler';
import { MouseBatcher } from './MouseBatcher';
import { EMITTER_CONFIG } from '../constants';
import type { NormalizedPosition } from '../types';

/**
 * Hook d'émission du mouse tracking local
 *
 * @param boardRef - Référence au conteneur du board
 * @param enabled - Active/désactive le tracking
 */
export function useLocalCursorEmitter(
  boardRef: React.RefObject<HTMLElement>,
  enabled: boolean
): void {
  const gameId = useGameStore((s) => s.gameId);
  const playerId = useGameStore((s) => s.playerId);
  const currentBoardOwnerId = useGuessingStore((s) => s.currentBoardOwnerId);

  const samplerRef = useRef<MouseSampler | null>(null);
  const batcherRef = useRef<MouseBatcher | null>(null);

  // Callback d'envoi SignalR
  const sendPositions = useCallback(
    async (positions: NormalizedPosition[]) => {
      if (!gameId || !playerId || positions.length === 0) return;

      try {
        await signalRClient.invoke(
          'SendMousePositions',
          gameId,
          playerId,
          positions
        );
      } catch (err) {
        // Silencieux - best effort
        console.debug('[MouseTracking] Send failed:', err);
      }
    },
    [gameId, playerId]
  );

  useEffect(() => {
    if (!enabled || !boardRef.current) return;

    console.log('[MouseTracking] Activating emitter for board owner:', currentBoardOwnerId);

    // Initialisation du sampler et du batcher
    samplerRef.current = new MouseSampler(EMITTER_CONFIG);
    batcherRef.current = new MouseBatcher(EMITTER_CONFIG, sendPositions);

    const board = boardRef.current;

    const handleMouseMove = (e: MouseEvent) => {
      const rect = board.getBoundingClientRect();
      const centerX = rect.left + rect.width / 2;
      const centerY = rect.top + rect.height / 2;

      // Coordonnées normalisées par rapport au centre du board
      const nx = (e.clientX - centerX) / rect.width;
      const ny = (e.clientY - centerY) / rect.height;
      const t = Date.now();

      const pos: NormalizedPosition = { nx, ny, t };

      // Filtrer via le sampler
      if (samplerRef.current?.shouldCapture(pos)) {
        batcherRef.current?.add(pos);
      }
    };

    // Écouter sur le document pour ne pas perdre le curseur hors du board
    document.addEventListener('mousemove', handleMouseMove, { passive: true });

    return () => {
      console.log('[MouseTracking] Deactivating emitter');
      document.removeEventListener('mousemove', handleMouseMove);
      batcherRef.current?.destroy();
      samplerRef.current?.reset();
    };
  }, [enabled, boardRef, sendPositions, currentBoardOwnerId]);
}
