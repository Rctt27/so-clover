/**
 * Types pour le système de mouse tracking
 */

/** Position normalisée par rapport au centre du board */
export interface NormalizedPosition {
  nx: number; // -0.5 à 0.5 (gauche à droite)
  ny: number; // -0.5 à 0.5 (haut à bas)
  t: number; // timestamp absolu (Date.now())
}

/** Payload envoyé au serveur (compatible backend existant) */
export interface MousePayload {
  gameId: string;
  playerId: string;
  positions: NormalizedPosition[];
}

/** Données reçues du serveur */
export interface RemoteMouseData {
  playerId: string;
  playerName: string;
  cursorColorIndex?: number; // Optionnel pour rétrocompatibilité
  positions: NormalizedPosition[];
}

/** Configuration de l'émetteur */
export interface EmitterConfig {
  minDistance: number; // 0.004 (en coordonnées normalisées)
  minAngle: number; // 15 degrés (pour capturer l'intentionnalité)
  maxInterval: number; // 50ms (fréquence minimale garantie)
  flushInterval: number; // 50ms (batch flush)
  maxPointsPerBatch: number; // 20
  inactivityDelay: number; // 250ms (envoi fin de mouvement)
}

/** Configuration du récepteur */
export interface ReceiverConfig {
  bufferDelay: number; // 150ms
  maxBufferAge: number; // 3000ms (purge sécurité)
  interpolation: 'linear' | 'catmull-rom';
}

/** État d'un curseur distant */
export interface RemoteCursorState {
  playerId: string;
  playerName: string;
  colorIndex: number; // 1-10
  currentPos: { x: number; y: number } | null;
  isActive: boolean;
  lastUpdate: number;
}

/** Callback de souscription au store */
export type CursorStoreListener = (
  cursors: Map<string, RemoteCursorState>
) => void;

/** Vecteur 2D pour calculs d'angle */
export interface Vector2D {
  x: number;
  y: number;
}
