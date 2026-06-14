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
  /** Largeur (% du plateau) des champs d'indice sous pointeur grossier (tactile).
   *  Plus large que `clueInputWidthPct` : sur device les champs sont dé-pivotés
   *  (horizontaux) et doivent afficher les mots longs sans troncature. */
  clueInputWidthPctCoarse: number
  cluePositions: Record<'top' | 'right' | 'bottom' | 'left', CluePosition>
}

export type CluePlacement = CluePosition & { widthPct: number }

const PETAL_PENETRATION_DEPTH = 253
const PETAL_CENTER_RATIO = 0.45
const CLUE_INPUT_WIDTH_PX = 270
// Champ tactile élargi : ~32 % du plateau. Centré via translate(-50 %), un champ
// latéral (centre de pétale à ~83 %) reste dans [0,100] sans déborder le plateau.
const CLUE_INPUT_WIDTH_PX_COARSE = 420

export function computeBoardGeometry({ referenceSize, cardSize }: BoardGeometryInput): BoardGeometry {
  const center = referenceSize / 2
  const coreSize = cardSize * 2
  const coreTop = center - coreSize / 2
  const coreLeft = coreTop
  const petalCenterOffset = coreTop - PETAL_PENETRATION_DEPTH * PETAL_CENTER_RATIO

  const clueInputWidthPct = (CLUE_INPUT_WIDTH_PX / referenceSize) * 100
  const clueInputWidthPctCoarse = (CLUE_INPUT_WIDTH_PX_COARSE / referenceSize) * 100
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
    clueInputWidthPctCoarse,
    cluePositions: {
      top:    { topPct: offsetPct, leftPct: 50,        rotation: 0   },
      right:  { topPct: 50,        leftPct: mirrorPct, rotation: 90  },
      bottom: { topPct: mirrorPct, leftPct: 50,        rotation: 180 },
      left:   { topPct: 50,        leftPct: offsetPct, rotation: -90 },
    },
  }
}

/**
 * Placement effectif d'un champ d'indice selon le type de pointeur.
 *
 * Desktop (`coarse = false`) : comportement historique — le champ est pivoté le long
 * de la direction de sa pétale (90/180/-90°) et garde la largeur de base.
 *
 * Tactile (`coarse = true`, mobile en paysage) : rotation neutralisée (texte horizontal,
 * lisible) et largeur élargie pour ne plus tronquer les mots longs. Les positions
 * (centres de pétales) restent inchangées ; seules rotation et largeur varient.
 */
export function getCluePlacement(
  geo: BoardGeometry,
  position: 'top' | 'right' | 'bottom' | 'left',
  coarse: boolean,
): CluePlacement {
  const { topPct, leftPct, rotation } = geo.cluePositions[position]
  return {
    topPct,
    leftPct,
    rotation: coarse ? 0 : rotation,
    widthPct: coarse ? geo.clueInputWidthPctCoarse : geo.clueInputWidthPct,
  }
}
