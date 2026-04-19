import { describe, it, expect } from 'vitest'
import { computeBoardGeometry } from './boardGeometry'

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
})
