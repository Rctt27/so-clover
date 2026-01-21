/**
 * MouseSampler - Échantillonnage adaptatif des positions de souris
 *
 * Capture un point si au moins une condition est vraie :
 * 1. Distance >= minDistance (déplacement significatif)
 * 2. Angle >= minAngle (changement de direction, intentionnalité)
 * 3. Temps >= maxInterval (filet de sécurité, 20Hz minimum)
 */

import type { NormalizedPosition, EmitterConfig, Vector2D } from '../types';

export class MouseSampler {
  private lastSentPos: NormalizedPosition | null = null;
  private prevDirection: Vector2D | null = null;
  private lastSentTime = 0;
  private config: EmitterConfig;

  constructor(config: EmitterConfig) {
    this.config = config;
  }

  /**
   * Détermine si une position doit être capturée
   */
  shouldCapture(pos: NormalizedPosition): boolean {
    const now = pos.t;

    // Premier point : toujours capturer
    if (!this.lastSentPos) {
      this.lastSentPos = pos;
      this.lastSentTime = now;
      this.prevDirection = null;
      return true;
    }

    const dx = pos.nx - this.lastSentPos.nx;
    const dy = pos.ny - this.lastSentPos.ny;
    const dist = Math.sqrt(dx * dx + dy * dy);

    // Condition 1 : Distance minimale
    if (dist >= this.config.minDistance) {
      this.updateState(pos, now, { x: dx, y: dy });
      return true;
    }

    // Condition 2 : Angle minimal (intentionnalité)
    if (this.prevDirection && dist > 0.0001) {
      const angleChange = this.calculateAngleChange(
        this.prevDirection,
        { x: dx, y: dy }
      );

      if (angleChange >= this.config.minAngle) {
        this.updateState(pos, now, { x: dx, y: dy });
        return true;
      }
    }

    // Condition 3 : Temps maximum (filet de sécurité)
    if (now - this.lastSentTime >= this.config.maxInterval) {
      this.updateState(pos, now, { x: dx, y: dy });
      return true;
    }

    return false;
  }

  /**
   * Calcule le changement d'angle entre deux vecteurs directionnels
   */
  private calculateAngleChange(prevDir: Vector2D, currentDir: Vector2D): number {
    // Longueurs des vecteurs
    const prevLen = Math.sqrt(prevDir.x * prevDir.x + prevDir.y * prevDir.y);
    const currentLen = Math.sqrt(
      currentDir.x * currentDir.x + currentDir.y * currentDir.y
    );

    if (prevLen < 0.0001 || currentLen < 0.0001) {
      return 0;
    }

    // Normalisation
    const norm1 = {
      x: prevDir.x / prevLen,
      y: prevDir.y / prevLen,
    };

    const norm2 = {
      x: currentDir.x / currentLen,
      y: currentDir.y / currentLen,
    };

    // Produit scalaire pour obtenir le cosinus de l'angle
    const dot = norm1.x * norm2.x + norm1.y * norm2.y;

    // Clamp pour éviter les erreurs d'arrondi
    const clampedDot = Math.max(-1, Math.min(1, dot));

    // Conversion en degrés
    const angleRad = Math.acos(clampedDot);
    const angleDeg = angleRad * (180 / Math.PI);

    return angleDeg;
  }

  /**
   * Met à jour l'état interne
   */
  private updateState(
    pos: NormalizedPosition,
    time: number,
    direction: Vector2D
  ): void {
    this.lastSentPos = pos;
    this.lastSentTime = time;
    this.prevDirection = direction;
  }

  /**
   * Réinitialise l'état
   */
  reset(): void {
    this.lastSentPos = null;
    this.prevDirection = null;
    this.lastSentTime = 0;
  }
}
