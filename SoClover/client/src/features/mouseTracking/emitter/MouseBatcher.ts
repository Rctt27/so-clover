/**
 * MouseBatcher - Gestion du batching et de l'envoi des positions
 *
 * Stratégie :
 * - Accumule les positions dans un buffer
 * - Flush périodique (50ms) OU
 * - Flush immédiat si maxPointsPerBatch atteint OU
 * - Flush après inactivité (250ms)
 */

import type { NormalizedPosition, EmitterConfig } from '../types';

export class MouseBatcher {
  private buffer: NormalizedPosition[] = [];
  private flushTimer: number | null = null;
  private inactivityTimer: number | null = null;
  private config: EmitterConfig;
  private onFlush: (positions: NormalizedPosition[]) => void;

  constructor(
    config: EmitterConfig,
    onFlush: (positions: NormalizedPosition[]) => void
  ) {
    this.config = config;
    this.onFlush = onFlush;
  }

  /**
   * Ajoute une position au buffer
   */
  add(pos: NormalizedPosition): void {
    this.buffer.push(pos);

    // Réinitialiser le timer d'inactivité
    this.resetInactivityTimer();

    // Flush immédiat si le buffer est plein
    if (this.buffer.length >= this.config.maxPointsPerBatch) {
      this.flush();
      return;
    }

    // Démarrer le timer de flush périodique si pas déjà actif
    if (!this.flushTimer) {
      this.flushTimer = window.setTimeout(() => {
        this.flush();
      }, this.config.flushInterval);
    }
  }

  /**
   * Réinitialise le timer d'inactivité
   */
  private resetInactivityTimer(): void {
    if (this.inactivityTimer) {
      clearTimeout(this.inactivityTimer);
    }

    this.inactivityTimer = window.setTimeout(() => {
      this.flush();
    }, this.config.inactivityDelay);
  }

  /**
   * Vide le buffer et envoie les positions
   */
  flush(): void {
    // Nettoyer les timers
    if (this.flushTimer) {
      clearTimeout(this.flushTimer);
      this.flushTimer = null;
    }

    if (this.inactivityTimer) {
      clearTimeout(this.inactivityTimer);
      this.inactivityTimer = null;
    }

    // Envoyer si le buffer n'est pas vide
    if (this.buffer.length > 0) {
      this.onFlush([...this.buffer]);
      this.buffer = [];
    }
  }

  /**
   * Nettoie et détruit le batcher
   */
  destroy(): void {
    this.flush();
    if (this.flushTimer) clearTimeout(this.flushTimer);
    if (this.inactivityTimer) clearTimeout(this.inactivityTimer);
    this.buffer = [];
  }
}
