/**
 * CursorBuffer - Buffer de positions avec interpolation
 *
 * Responsabilités :
 * - Stocker les positions reçues avec leur playTime
 * - Interpoler entre les points pour un rendu fluide
 * - Purger les points trop anciens
 */

import type { NormalizedPosition, ReceiverConfig } from '../types';

interface BufferedPoint {
  pos: NormalizedPosition;
  playTime: number; // Temps de lecture calculé
}

export class CursorBuffer {
  private buffer: BufferedPoint[] = [];
  private config: ReceiverConfig;

  constructor(config: ReceiverConfig) {
    this.config = config;
  }

  /**
   * Ajoute des positions au buffer
   */
  addPositions(positions: NormalizedPosition[]): void {
    const now = Date.now();

    positions.forEach((pos) => {
      // playTime = arrivée + bufferDelay pour lisser les variations réseau
      const playTime = now + this.config.bufferDelay;
      this.buffer.push({ pos, playTime });
    });

    // Purger les vieux points
    this.purgeOld(now);
  }

  /**
   * Obtient la position interpolée pour un temps de rendu donné
   */
  getPositionAt(renderTime: number): { x: number; y: number } | null {
    if (this.buffer.length === 0) return null;

    // Trouver les deux points encadrant renderTime
    let prev: BufferedPoint | null = null;
    let next: BufferedPoint | null = null;

    for (let i = 0; i < this.buffer.length; i++) {
      if (this.buffer[i].pos.t <= renderTime) {
        prev = this.buffer[i];
      } else {
        next = this.buffer[i];
        break;
      }
    }

    // Cas simples
    if (!prev && !next) return null;
    if (!prev) return { x: next!.pos.nx, y: next!.pos.ny };
    if (!next) return { x: prev.pos.nx, y: prev.pos.ny };

    // Interpolation linéaire
    const t1 = prev.pos.t;
    const t2 = next.pos.t;
    const alpha = t2 === t1 ? 0 : (renderTime - t1) / (t2 - t1);

    return {
      x: prev.pos.nx + (next.pos.nx - prev.pos.nx) * alpha,
      y: prev.pos.ny + (next.pos.ny - prev.pos.ny) * alpha,
    };
  }

  /**
   * Purge les points trop anciens
   */
  private purgeOld(now: number): void {
    const cutoff = now - this.config.maxBufferAge;
    this.buffer = this.buffer.filter((p) => p.pos.t > cutoff);
  }

  /**
   * Vide le buffer
   */
  clear(): void {
    this.buffer = [];
  }
}
