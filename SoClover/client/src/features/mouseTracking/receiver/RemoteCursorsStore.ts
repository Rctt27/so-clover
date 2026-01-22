/**
 * RemoteCursorsStore - Store singleton pour l'état des curseurs distants
 *
 * Responsabilités :
 * - Gérer l'état de tous les curseurs distants
 * - Attribuer des couleurs persistantes par joueur
 * - Gérer les buffers de positions
 * - Notifier les listeners (pattern observable)
 */

import type {
  RemoteCursorState,
  CursorStoreListener,
  RemoteMouseData,
} from '../types';
import { CursorBuffer } from './CursorBuffer';
import { RECEIVER_CONFIG, CURSOR_COLORS_COUNT } from '../constants';

class RemoteCursorsStore {
  private cursors = new Map<string, RemoteCursorState>();
  private buffers = new Map<string, CursorBuffer>();
  private listeners = new Set<CursorStoreListener>();
  private knownPlayerColors = new Map<string, number>();

  /**
   * Initialise les couleurs depuis le game state (appelé au mount)
   */
  initializePlayerColors(players: Array<{ playerId: string; cursorColorIndex: number }>): void {
    players.forEach((p) => {
      if (p.cursorColorIndex > 0) {
        this.knownPlayerColors.set(p.playerId, p.cursorColorIndex);
      }
    });
  }

  /**
   * Obtient la couleur d'un joueur (serveur ou fallback)
   */
  private getColorIndex(playerId: string, serverColor?: number): number {
    // Priorité 1: couleur fournie par le serveur dans l'event
    if (serverColor && serverColor > 0) {
      this.knownPlayerColors.set(playerId, serverColor);
      return serverColor;
    }

    // Priorité 2: couleur connue du cache
    if (this.knownPlayerColors.has(playerId)) {
      return this.knownPlayerColors.get(playerId)!;
    }

    // Priorité 3: fallback déterministe basé sur le hash du playerId
    return this.hashToColorIndex(playerId);
  }

  /**
   * Fallback déterministe : hash du playerId vers colorIndex
   */
  private hashToColorIndex(playerId: string): number {
    let hash = 0;
    for (let i = 0; i < playerId.length; i++) {
      hash = ((hash << 5) - hash) + playerId.charCodeAt(i);
      hash |= 0;
    }
    return (Math.abs(hash) % CURSOR_COLORS_COUNT) + 1;
  }

  /**
   * Appelé lors de la réception de positions via SignalR
   */
  receivePositions(data: RemoteMouseData): void {
    const { playerId, playerName, cursorColorIndex, positions } = data;

    // Créer le buffer si nécessaire
    if (!this.buffers.has(playerId)) {
      this.buffers.set(playerId, new CursorBuffer(RECEIVER_CONFIG));
    }

    // Ajouter les positions au buffer
    this.buffers.get(playerId)!.addPositions(positions);

    // Mettre à jour ou créer l'état du curseur
    if (!this.cursors.has(playerId)) {
      this.cursors.set(playerId, {
        playerId,
        playerName,
        colorIndex: this.getColorIndex(playerId, cursorColorIndex),
        currentPos: null,
        isActive: true,
        lastUpdate: Date.now(),
      });
    } else {
      const cursor = this.cursors.get(playerId)!;
      cursor.lastUpdate = Date.now();
      cursor.isActive = true;
    }

    this.notify();
  }

  /**
   * Appelé par le renderer à chaque frame
   */
  getInterpolatedPosition(
    playerId: string,
    renderTime: number
  ): { x: number; y: number } | null {
    const buffer = this.buffers.get(playerId);
    if (!buffer) return null;

    return buffer.getPositionAt(renderTime);
  }

  /**
   * Mise à jour directe de la position rendue (pour le store)
   */
  updateRenderedPosition(playerId: string, x: number, y: number): void {
    const cursor = this.cursors.get(playerId);
    if (cursor) {
      cursor.currentPos = { x, y };
    }
  }

  /**
   * Souscription aux changements
   */
  subscribe(listener: CursorStoreListener): () => void {
    this.listeners.add(listener);
    listener(this.cursors);
    return () => this.listeners.delete(listener);
  }

  /**
   * Notifie tous les listeners
   */
  private notify(): void {
    this.listeners.forEach((l) => l(this.cursors));
  }

  /**
   * Récupère tous les curseurs
   */
  getCursors(): Map<string, RemoteCursorState> {
    return this.cursors;
  }

  /**
   * Nettoie tous les curseurs (mais préserve les couleurs connues)
   */
  cleanup(): void {
    this.cursors.clear();
    this.buffers.forEach((buffer) => buffer.clear());
    this.buffers.clear();
    // NE PAS effacer knownPlayerColors - elles persistent pendant la partie!
    this.notify();
  }

  /**
   * Nettoie les curseurs inactifs (timeout)
   */
  purgeInactive(maxAge: number = 3000): void {
    const now = Date.now();
    let changed = false;

    this.cursors.forEach((cursor) => {
      if (now - cursor.lastUpdate > maxAge) {
        cursor.isActive = false;
        changed = true;
      }
    });

    if (changed) this.notify();
  }
}

// Singleton
export const remoteCursorsStore = new RemoteCursorsStore();
