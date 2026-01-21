/**
 * RemoteCursorsLayer - Composant React pour le rendu des curseurs distants
 *
 * Responsabilités :
 * - Écouter les événements SignalR
 * - Démarrer/arrêter le renderer
 * - Nettoyer au démontage
 */

import { useEffect, useRef } from 'react';
import { cursorRenderer } from '../receiver/CursorRenderer';
import { remoteCursorsStore } from '../receiver/RemoteCursorsStore';
import { signalRClient } from '../../../api/signalr-client';
import { useGuessingStore } from '../../../core/store';
import type { RemoteMouseData } from '../types';
import '../styles/remoteCursors.css';

interface Props {
  boardRef: React.RefObject<HTMLElement>;
  enabled: boolean;
}

export function RemoteCursorsLayer({ boardRef, enabled }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const currentBoardOwnerId = useGuessingStore((s) => s.currentBoardOwnerId);

  // Écoute SignalR
  useEffect(() => {
    if (!enabled) return;

    const handleMouseMoved = (data: RemoteMouseData) => {
      remoteCursorsStore.receivePositions(data);
    };

    signalRClient.on('GuessingMouseMoved', handleMouseMoved);

    return () => {
      signalRClient.off('GuessingMouseMoved', handleMouseMoved);
    };
  }, [enabled]);

  // Démarrage du renderer
  useEffect(() => {
    if (!enabled || !containerRef.current || !boardRef.current) return;

    console.log('[MouseTracking] Starting renderer for board owner:', currentBoardOwnerId);
    cursorRenderer.start(containerRef.current, boardRef.current);

    return () => {
      console.log('[MouseTracking] Stopping renderer and cleaning up cursors');
      cursorRenderer.stop();
      remoteCursorsStore.cleanup();
    };
  }, [enabled, boardRef, currentBoardOwnerId]);

  if (!enabled) return null;

  return (
    <div
      ref={containerRef}
      className="remote-cursors-container"
      style={{
        position: 'fixed',
        inset: 0,
        pointerEvents: 'none',
        zIndex: 9999,
        overflow: 'hidden',
      }}
    />
  );
}
