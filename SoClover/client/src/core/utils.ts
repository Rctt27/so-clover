export const LOGICAL_SLOTS = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'] as const;

/**
 * Convertit des degrés (0, 90, 180, 270) en index de rotation (0, 1, 2, 3)
 * 0 deg -> Index 0
 * 90 deg -> Index 1
 * 180 deg -> Index 2
 * 270 deg -> Index 3
 */
export function degreesToRotationIndex(degrees: number): number {
  const normalized = ((degrees % 360) + 360) % 360;
  return Math.floor(normalized / 90) % 4;
}
