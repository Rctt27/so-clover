import { describe, it, expect, vi } from 'vitest'
import { recoverConnection } from './connectionRecovery'

describe('recoverConnection', () => {
  it('ne fait rien et renvoie false sans gameId/playerId', async () => {
    const invoke = vi.fn().mockResolvedValue(undefined)
    const refreshGameState = vi.fn().mockResolvedValue(undefined)

    const ok = await recoverConnection({
      getAuth: () => ({ gameId: null, playerId: null }),
      invoke,
      refreshGameState,
    })

    expect(ok).toBe(false)
    expect(invoke).not.toHaveBeenCalled()
    expect(refreshGameState).not.toHaveBeenCalled()
  })

  it('re-invoque JoinGame puis refreshGameState avec l\'identité persistée', async () => {
    const invoke = vi.fn().mockResolvedValue(undefined)
    const refreshGameState = vi.fn().mockResolvedValue(undefined)

    const ok = await recoverConnection({
      getAuth: () => ({ gameId: 'GAME-1', playerId: 'PLAYER-1' }),
      invoke,
      refreshGameState,
    })

    expect(ok).toBe(true)
    expect(invoke).toHaveBeenCalledWith('JoinGame', 'GAME-1', 'PLAYER-1')
    expect(refreshGameState).toHaveBeenCalledTimes(1)
  })

  it('renvoie false si l\'invoke JoinGame échoue (pas de throw)', async () => {
    const invoke = vi.fn().mockRejectedValue(new Error('boom'))
    const refreshGameState = vi.fn().mockResolvedValue(undefined)

    const ok = await recoverConnection({
      getAuth: () => ({ gameId: 'GAME-1', playerId: 'PLAYER-1' }),
      invoke,
      refreshGameState,
    })

    expect(ok).toBe(false)
    expect(refreshGameState).not.toHaveBeenCalled()
  })
})
