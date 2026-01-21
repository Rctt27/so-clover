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
  private colorAssignments = new Map<string, number>();
  private nextColorIndex = 1;

  /**
   * Obtient ou attribue une couleur pour un joueur
   */
  private getColorIndex(playerId: string): number {
    if (!this.colorAssignments.has(playerId)) {
      this.colorAssignments.set(playerId, this.nextColorIndex);
      this.nextColorIndex = (this.nextColorIndex % CURSOR_COLORS_COUNT) + 1;
    }
    return this.colorAssignments.get(playerId)!;
  }

  /**
   * Appelé lors de la réception de positions via SignalR
   */
  receivePositions(data: RemoteMouseData): void {
    const { playerId, playerName, positions } = data;

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
        colorIndex: this.getColorIndex(playerId),
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
   * Nettoie tous les curseurs
   */
  cleanup(): void {
    this.cursors.clear();
    this.buffers.forEach((buffer) => buffer.clear());
    this.buffers.clear();
    this.colorAssignments.clear();
    this.nextColorIndex = 1;
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
