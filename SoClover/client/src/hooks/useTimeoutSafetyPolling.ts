import { useEffect, useRef, useCallback } from 'react'
import { useGameStore } from '../core/store'
import { gameApi } from '../api/game-api'
import { useGameStateUpdate } from './useGameStateUpdate'

/**
 * Safety polling hook that detects when the phase timer expires and forces
 * a state refresh if SignalR hasn't already updated the state.
 *
 * This replicates the legacy-vanilla behavior where the client would poll
 * every few seconds to catch missed SignalR events, especially when the
 * timer reaches 00:00.
 *
 * The backend (GameProcessManager) handles all timeout transitions:
 * - Lobby timeout → game deleted
 * - WritingClues timeout → force transition to Guessing
 * - Guessing timeout → move to next board (or Scoring)
 * - Scoring timeout → game completed
 *
 * This hook ensures the client stays in sync even if SignalR events are missed.
 */
export const useTimeoutSafetyPolling = () => {
  const gameId = useGameStore(s => s.gameId)
  const phase = useGameStore(s => s.phase)
  const phaseEndsAtUtc = useGameStore(s => s.phaseEndsAtUtc)
  const resetAuth = useGameStore(s => s.resetAuth)
  const { updateStateFromResponse } = useGameStateUpdate()

  // Track when we last detected timeout to avoid multiple fetches
  const lastTimeoutRefreshRef = useRef<number>(0)
  // Track the phase to detect changes
  const lastPhaseRef = useRef<string | null>(null)
  // Track if we're currently waiting for a timeout refresh
  const pendingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const refreshGameState = useCallback(async () => {
    if (!gameId) return

    try {
      const state = await gameApi.getGameState(gameId)
      if (state) {
        updateStateFromResponse(state)
      }
    } catch (err: any) {
      console.error('[useTimeoutSafetyPolling] Failed to refresh game state', err)

      // If game not found (404), it was likely deleted due to timeout
      if (err?.status === 404 || err?.message === 'Game not found') {
        console.log('[useTimeoutSafetyPolling] Game not found (deleted), resetting auth')
        resetAuth()
      }
    }
  }, [gameId, updateStateFromResponse, resetAuth])

  useEffect(() => {
    // Clear any pending timeout when phase changes
    if (phase !== lastPhaseRef.current) {
      lastPhaseRef.current = phase
      if (pendingTimeoutRef.current) {
        clearTimeout(pendingTimeoutRef.current)
        pendingTimeoutRef.current = null
      }
    }
  }, [phase])

  useEffect(() => {
    if (!gameId || !phaseEndsAtUtc || phase === 'Initial') {
      return
    }

    const checkTimeout = () => {
      const now = Date.now()
      const deadline = new Date(phaseEndsAtUtc).getTime()
      const remaining = deadline - now

      // If timer has expired (remaining <= 0)
      if (remaining <= 0) {
        const timeSinceLastRefresh = now - lastTimeoutRefreshRef.current

        // Only trigger a refresh if we haven't done one in the last 3 seconds
        // This gives SignalR time to deliver the state update first
        if (timeSinceLastRefresh > 3000 && !pendingTimeoutRef.current) {
          console.log(`[useTimeoutSafetyPolling] Timer expired for phase ${phase}, scheduling refresh...`)

          // Schedule a refresh in 3 seconds to allow SignalR to update first
          pendingTimeoutRef.current = setTimeout(async () => {
            const currentPhase = useGameStore.getState().phase
            const currentDeadline = useGameStore.getState().phaseEndsAtUtc

            // Only refresh if we're still in the same phase with the same deadline
            // This avoids unnecessary refreshes if SignalR already updated the state
            if (currentPhase === phase && currentDeadline === phaseEndsAtUtc) {
              console.log(`[useTimeoutSafetyPolling] Forcing refresh for phase ${phase}`)
              lastTimeoutRefreshRef.current = Date.now()
              await refreshGameState()
            } else {
              console.log(`[useTimeoutSafetyPolling] Phase/deadline changed, skipping refresh`)
            }
            pendingTimeoutRef.current = null
          }, 3000)
        }
      }
    }

    // Check immediately
    checkTimeout()

    // Then check every 5 seconds (same as legacy-vanilla)
    const intervalId = setInterval(checkTimeout, 5000)

    return () => {
      clearInterval(intervalId)
      if (pendingTimeoutRef.current) {
        clearTimeout(pendingTimeoutRef.current)
        pendingTimeoutRef.current = null
      }
    }
  }, [gameId, phase, phaseEndsAtUtc, refreshGameState])

  // Also poll when timer is critically low (under 5 seconds) more frequently
  // to ensure we catch the exact moment of transition
  useEffect(() => {
    if (!gameId || !phaseEndsAtUtc || phase === 'Initial') {
      return
    }

    const checkCritical = () => {
      const now = Date.now()
      const deadline = new Date(phaseEndsAtUtc).getTime()
      const remaining = deadline - now

      // When under 5 seconds, check more frequently
      if (remaining <= 5000 && remaining > 0) {
        // We'll let the main effect handle the actual refresh after timeout
        // This effect just ensures we're ready
      }
    }

    // Check every second when timer is low
    const criticalIntervalId = setInterval(checkCritical, 1000)

    return () => clearInterval(criticalIntervalId)
  }, [gameId, phase, phaseEndsAtUtc])
}
