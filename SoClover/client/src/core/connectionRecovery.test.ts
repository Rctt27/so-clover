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

  it('appelle onUnauthorized quand le serveur rejette le rejoin (grâce expirée)', async () => {
    const invoke = vi.fn().mockRejectedValue(new Error('Unauthorized: player not in game'))
    const refreshGameState = vi.fn().mockResolvedValue(undefined)
    const onUnauthorized = vi.fn()

    const ok = await recoverConnection({
      getAuth: () => ({ gameId: 'GAME-1', playerId: 'PLAYER-1' }),
      invoke,
      refreshGameState,
      onUnauthorized,
    })

    expect(ok).toBe(false)
    expect(onUnauthorized).toHaveBeenCalledTimes(1)
    expect(refreshGameState).not.toHaveBeenCalled()
  })

  it('appelle onUnauthorized quand la partie a été supprimée pendant la grâce', async () => {
    // Le serveur mappe GameNotFound → HubException("Unauthorized: game no longer exists")
    // pour que le client purge l'identité périmée plutôt que de rester sur un écran mort.
    const invoke = vi.fn().mockRejectedValue(new Error('Unauthorized: game no longer exists'))
    const onUnauthorized = vi.fn()

    const ok = await recoverConnection({
      getAuth: () => ({ gameId: 'GAME-1', playerId: 'PLAYER-1' }),
      invoke,
      refreshGameState: vi.fn().mockResolvedValue(undefined),
      onUnauthorized,
    })

    expect(ok).toBe(false)
    expect(onUnauthorized).toHaveBeenCalledTimes(1)
  })

  it('n\'appelle pas onUnauthorized pour une erreur réseau ordinaire', async () => {
    const invoke = vi.fn().mockRejectedValue(new Error('boom'))
    const onUnauthorized = vi.fn()

    const ok = await recoverConnection({
      getAuth: () => ({ gameId: 'GAME-1', playerId: 'PLAYER-1' }),
      invoke,
      refreshGameState: vi.fn().mockResolvedValue(undefined),
      onUnauthorized,
    })

    expect(ok).toBe(false)
    expect(onUnauthorized).not.toHaveBeenCalled()
  })
})
