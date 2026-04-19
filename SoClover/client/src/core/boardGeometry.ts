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

const PETAL_PENETRATION_DEPTH = 253
const PETAL_CENTER_RATIO = 0.45
const CLUE_INPUT_WIDTH_PX = 270

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
