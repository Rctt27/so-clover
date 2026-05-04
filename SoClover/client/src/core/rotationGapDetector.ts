export interface RotationGapInput {
  source: 'BoardRotationUpdated' | 'GameStateUpdated'
  from: number | null
  to: number
}

export function detectRotationGap(input: RotationGapInput): void {
  if (input.from === null) return
  const delta = Math.abs(input.to - input.from)
  if (delta > 90) {
    console.warn(
      `[rotation gap] ${input.source}: from=${input.from} to=${input.to} delta=${delta}° (missed at least one event)`,
    )
  }
}
