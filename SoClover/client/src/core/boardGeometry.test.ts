import { describe, it, expect } from 'vitest'
import { computeBoardGeometry, getCluePlacement } from './boardGeometry'

describe('computeBoardGeometry', () => {
  const geo = computeBoardGeometry({ referenceSize: 1300, cardSize: 320, cardGap: 4 })

  it('places the core square centered on the canvas', () => {
    expect(geo.coreLeft).toBe(330)
    expect(geo.coreTop).toBe(330)
    expect(geo.coreSize).toBe(640)
  })

  it('derives petal center offset from core geometry, not magic numbers', () => {
    // petalCenterOffset = coreTop - penetrationDepth * 0.45 = 330 - 113.85 = 216.15
    expect(geo.petalCenterOffset).toBeCloseTo(216.15, 1)
  })

  it('provides clue position as percentage for "top"', () => {
    expect(geo.cluePositions.top.topPct).toBeCloseTo((216.15 / 1300) * 100, 1)
    expect(geo.cluePositions.top.leftPct).toBe(50)
    expect(geo.cluePositions.top.rotation).toBe(0)
  })

  it('provides clue position as percentage for "right"', () => {
    expect(geo.cluePositions.right.topPct).toBe(50)
    expect(geo.cluePositions.right.leftPct).toBeCloseTo(((1300 - 216.15) / 1300) * 100, 1)
    expect(geo.cluePositions.right.rotation).toBe(90)
  })

  it('provides clue position as percentage for "bottom"', () => {
    expect(geo.cluePositions.bottom.topPct).toBeCloseTo(((1300 - 216.15) / 1300) * 100, 1)
    expect(geo.cluePositions.bottom.leftPct).toBe(50)
    expect(geo.cluePositions.bottom.rotation).toBe(180)
  })

  it('provides clue position as percentage for "left"', () => {
    expect(geo.cluePositions.left.topPct).toBe(50)
    expect(geo.cluePositions.left.leftPct).toBeCloseTo((216.15 / 1300) * 100, 1)
    expect(geo.cluePositions.left.rotation).toBe(-90)
  })

  it('provides clue input width as percentage', () => {
    // 270px input width / 1300 reference = 20.77%
    expect(geo.clueInputWidthPct).toBeCloseTo((270 / 1300) * 100, 1)
  })

  it('exposes a wider clue input width for coarse pointers (mobile)', () => {
    // Sur device tactile, les champs non pivotés doivent être plus larges pour
    // afficher les mots longs (« Bibliothèque ») sans troncature.
    expect(geo.clueInputWidthPctCoarse).toBeGreaterThan(geo.clueInputWidthPct)
  })
})

describe('getCluePlacement', () => {
  const geo = computeBoardGeometry({ referenceSize: 1300, cardSize: 320, cardGap: 4 })

  it('keeps petal rotation and base width on a fine pointer (desktop)', () => {
    for (const position of ['top', 'right', 'bottom', 'left'] as const) {
      const placement = getCluePlacement(geo, position, false)
      expect(placement.rotation).toBe(geo.cluePositions[position].rotation)
      expect(placement.widthPct).toBeCloseTo(geo.clueInputWidthPct, 5)
      expect(placement.topPct).toBeCloseTo(geo.cluePositions[position].topPct, 5)
      expect(placement.leftPct).toBeCloseTo(geo.cluePositions[position].leftPct, 5)
    }
  })

  it('neutralises rotation and widens every field under a coarse pointer (mobile)', () => {
    for (const position of ['top', 'right', 'bottom', 'left'] as const) {
      const placement = getCluePlacement(geo, position, true)
      expect(placement.rotation).toBe(0)
      expect(placement.widthPct).toBeCloseTo(geo.clueInputWidthPctCoarse, 5)
      // Positions (centres de pétales) inchangées : seuls rotation et largeur varient.
      expect(placement.topPct).toBeCloseTo(geo.cluePositions[position].topPct, 5)
      expect(placement.leftPct).toBeCloseTo(geo.cluePositions[position].leftPct, 5)
    }
  })

  it('keeps the widened coarse fields within the board bounds (no overflow)', () => {
    // Centrés via translate(-50%), les champs latéraux ne doivent pas déborder [0,100].
    for (const position of ['top', 'right', 'bottom', 'left'] as const) {
      const { leftPct, widthPct } = getCluePlacement(geo, position, true)
      expect(leftPct - widthPct / 2).toBeGreaterThanOrEqual(0)
      expect(leftPct + widthPct / 2).toBeLessThanOrEqual(100)
    }
  })
})
