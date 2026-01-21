/**
 * CursorRenderer - Rendu RAF des curseurs distants
 *
 * Responsabilités :
 * - Boucle requestAnimationFrame pour le rendu fluide
 * - Manipulation DOM impérative (pas de React re-renders)
 * - Conversion coordonnées normalisées → pixels
 * - Création/mise à jour/suppression des éléments DOM
 */

import { remoteCursorsStore } from './RemoteCursorsStore';
import { RECEIVER_CONFIG } from '../constants';

export class CursorRenderer {
  private container: HTMLElement | null = null;
  private boardRef: HTMLElement | null = null;
  private cursorElements = new Map<string, HTMLElement>();
  private rafId: number | null = null;
  private isRunning = false;

  /**
   * Démarre le renderer
   */
  start(container: HTMLElement, boardRef: HTMLElement): void {
    this.container = container;
    this.boardRef = boardRef;
    this.isRunning = true;
    this.tick();
  }

  /**
   * Arrête le renderer
   */
  stop(): void {
    this.isRunning = false;
    if (this.rafId) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
    this.cleanup();
  }

  /**
   * Boucle de rendu (RAF)
   */
  private tick = (): void => {
    if (!this.isRunning) return;

    const renderTime = Date.now() - RECEIVER_CONFIG.bufferDelay;
    const cursors = remoteCursorsStore.getCursors();

    cursors.forEach((cursor, playerId) => {
      if (!cursor.isActive) {
        this.hideCursor(playerId);
        return;
      }

      const pos = remoteCursorsStore.getInterpolatedPosition(
        playerId,
        renderTime
      );
      if (!pos) return;

      this.updateCursorDOM(
        playerId,
        cursor.playerName,
        cursor.colorIndex,
        pos.x,
        pos.y
      );
      remoteCursorsStore.updateRenderedPosition(playerId, pos.x, pos.y);
    });

    // Purger les curseurs inactifs
    remoteCursorsStore.purgeInactive(3000);

    this.rafId = requestAnimationFrame(this.tick);
  };

  /**
   * Met à jour ou crée un curseur dans le DOM
   */
  private updateCursorDOM(
    playerId: string,
    playerName: string,
    colorIndex: number,
    nx: number,
    ny: number
  ): void {
    if (!this.container || !this.boardRef) return;

    let el = this.cursorElements.get(playerId);

    if (!el) {
      el = this.createCursorElement(playerId, playerName, colorIndex);
      this.cursorElements.set(playerId, el);
      this.container.appendChild(el);
    }

    // Conversion coordonnées normalisées → pixels
    const boardRect = this.boardRef.getBoundingClientRect();
    const containerRect = this.container.getBoundingClientRect();

    const finalX =
      boardRect.left -
      containerRect.left +
      boardRect.width / 2 +
      nx * boardRect.width;
    const finalY =
      boardRect.top -
      containerRect.top +
      boardRect.height / 2 +
      ny * boardRect.height;

    el.style.transform = `translate3d(${finalX}px, ${finalY}px, 0)`;
    el.style.opacity = '1';
  }

  /**
   * Crée un élément DOM pour un curseur
   */
  private createCursorElement(
    playerId: string,
    playerName: string,
    colorIndex: number
  ): HTMLElement {
    const el = document.createElement('div');
    el.className = `remote-cursor cursor-color-${colorIndex}`;
    el.dataset.playerId = playerId;

    const icon = document.createElement('div');
    icon.className = 'remote-cursor-icon';

    const label = document.createElement('div');
    label.className = 'remote-cursor-label';
    label.textContent = playerName;

    el.appendChild(icon);
    el.appendChild(label);

    return el;
  }

  /**
   * Masque un curseur
   */
  private hideCursor(playerId: string): void {
    const el = this.cursorElements.get(playerId);
    if (el) {
      el.style.opacity = '0';
    }
  }

  /**
   * Nettoie tous les curseurs du DOM
   */
  private cleanup(): void {
    this.cursorElements.forEach((el) => el.remove());
    this.cursorElements.clear();
  }
}

// Singleton
export const cursorRenderer = new CursorRenderer();
