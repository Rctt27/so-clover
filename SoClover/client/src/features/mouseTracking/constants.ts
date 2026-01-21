/**
 * Configuration du système de mouse tracking
 */

import type { EmitterConfig, ReceiverConfig } from './types';

/**
 * Configuration de l'émetteur (capture locale)
 */
export const EMITTER_CONFIG: EmitterConfig = {
  // Distance minimale pour capturer un point (~4px sur board de 1000px)
  minDistance: 0.004,

  // Angle minimal pour capturer un changement de direction (intentionnalité)
  // 15° permet de capturer ~24 points pour un cercle complet (360/15)
  minAngle: 15,

  // Intervalle maximum entre deux points (20Hz minimum garanti)
  maxInterval: 50,

  // Intervalle de flush du batch (tous les 50ms)
  flushInterval: 50,

  // Nombre maximum de points par batch
  maxPointsPerBatch: 20,

  // Délai d'inactivité avant envoi (détection fin de mouvement)
  inactivityDelay: 250,
};

/**
 * Configuration du récepteur (rendu distant)
 */
export const RECEIVER_CONFIG: ReceiverConfig = {
  // Délai de buffer pour lisser les variations réseau (150ms)
  bufferDelay: 150,

  // Âge maximum des points dans le buffer (purge sécurité)
  maxBufferAge: 3000,

  // Type d'interpolation (linéaire en phase 1)
  interpolation: 'linear',
};

/**
 * Timeout d'inactivité pour masquer un curseur (3 secondes)
 */
export const CURSOR_INACTIVITY_TIMEOUT = 3000;

/**
 * Nombre de couleurs disponibles pour les curseurs
 */
export const CURSOR_COLORS_COUNT = 10;
