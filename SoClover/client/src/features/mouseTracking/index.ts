/**
 * Mouse Tracking Feature - Point d'entrée public
 *
 * Exports :
 * - Composant RemoteCursorsLayer
 * - Hook useLocalCursorEmitter
 * - Feature flag global
 */

export { RemoteCursorsLayer } from './components/RemoteCursorsLayer';
export { useLocalCursorEmitter } from './emitter/useLocalCursorEmitter';
export { remoteCursorsStore } from './receiver/RemoteCursorsStore';

// Types publics
export type { RemoteMouseData, RemoteCursorState } from './types';

// Feature flag simple (peut être étendu avec un context/provider)
let mouseTrackingEnabled = true;

/**
 * Active ou désactive le mouse tracking globalement
 */
export function setMouseTrackingEnabled(enabled: boolean): void {
  mouseTrackingEnabled = enabled;
}

/**
 * Récupère l'état du feature flag
 */
export function useMouseTrackingEnabled(): boolean {
  // Pourrait être connecté à un store ou context pour réactivité
  return mouseTrackingEnabled;
}
