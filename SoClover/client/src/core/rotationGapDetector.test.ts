import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { detectRotationGap } from './rotationGapDetector'

describe('detectRotationGap', () => {
  let warn: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
  })

  afterEach(() => {
    warn.mockRestore()
  })

  it('does not warn for a single 90° step', () => {
    detectRotationGap({ source: 'BoardRotationUpdated', from: 0, to: 90 })
    expect(warn).not.toHaveBeenCalled()
  })

  it('does not warn for a -90° step', () => {
    detectRotationGap({ source: 'BoardRotationUpdated', from: 90, to: 0 })
    expect(warn).not.toHaveBeenCalled()
  })

  it('warns when the absolute delta exceeds 90°', () => {
    detectRotationGap({ source: 'BoardRotationUpdated', from: 0, to: 180 })
    expect(warn).toHaveBeenCalledOnce()
    expect(warn.mock.calls[0][0]).toContain('rotation gap')
  })

  it('warns once per call, including the source channel and delta', () => {
    detectRotationGap({ source: 'GameStateUpdated', from: 0, to: 270 })
    expect(warn.mock.calls[0][0]).toContain('GameStateUpdated')
    expect(warn.mock.calls[0][0]).toContain('270')
  })

  it('treats null prev as no-warn (first observation)', () => {
    detectRotationGap({ source: 'BoardRotationUpdated', from: null, to: 360 })
    expect(warn).not.toHaveBeenCalled()
  })
})
