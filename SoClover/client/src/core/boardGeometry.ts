export interface BoardGeometryInput {
  referenceSize: number
  cardSize: number
  cardGap: number
}

export interface CluePosition {
  topPct: number
  leftPct: number
  rotation: number
}

export interface BoardGeometry {
  referenceSize: number
  center: number
  coreSize: number
  coreTop: number
  coreLeft: number
  /** Centre géométrique des cercles de pétales : coreTop − penetrationDepth × 0.45 */
  petalCenterOffset: number
  clueInputWidthPct: number
  cluePositions: Record<'top' | 'right' | 'bottom' | 'left', CluePosition>
}

export type CluePlacement = CluePosition & { widthPct: number }

const PETAL_PENETRATION_DEPTH = 253
const PETAL_CENTER_RATIO = 0.45
const CLUE_INPUT_WIDTH_PX = 400

export function computeBoardGeometry({ referenceSize, cardSize }: BoardGeometryInput): BoardGeometry {
  const center = referenceSize / 2
  const coreSize = cardSize * 2
  const coreTop = center - coreSize / 2
  const coreLeft = coreTop
  const petalCenterOffset = coreTop - PETAL_PENETRATION_DEPTH * PETAL_CENTER_RATIO

  const clueInputWidthPct = (CLUE_INPUT_WIDTH_PX / referenceSize) * 100
  const offsetPct = (petalCenterOffset / referenceSize) * 100
  const mirrorPct = ((referenceSize - petalCenterOffset) / referenceSize) * 100

  return {
    referenceSize,
    center,
    coreSize,
    coreTop,
    coreLeft,
    petalCenterOffset,
    clueInputWidthPct,
    cluePositions: {
      top:    { topPct: offsetPct, leftPct: 50,        rotation: 0   },
      right:  { topPct: 50,        leftPct: mirrorPct, rotation: 90  },
      bottom: { topPct: mirrorPct, leftPct: 50,        rotation: 180 },
      left:   { topPct: 50,        leftPct: offsetPct, rotation: -90 },
    },
  }
}

/**
 * Placement effectif d'un champ d'indice. Le champ est pivoté le long de la direction
 * de sa pétale (90/180/-90°) et garde la largeur de base — identique sur desktop ET
 * mobile : l'orientation des indices reste corrélée au sens du plateau (décision
 * produit 2026-06-14, supersède le dé-pivotage tactile précédent).
 */
export function getCluePlacement(
  geo: BoardGeometry,
  position: 'top' | 'right' | 'bottom' | 'left',
): CluePlacement {
  const { topPct, leftPct, rotation } = geo.cluePositions[position]
  return {
    topPct,
    leftPct,
    rotation,
    widthPct: geo.clueInputWidthPct,
  }
}
