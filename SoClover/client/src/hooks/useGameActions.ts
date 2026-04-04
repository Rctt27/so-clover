import { useState, useCallback } from 'react'
import { useGameStore, useBoardStore, useGuessingStore } from '../core/store'
import { gameApi } from '../api/game-api'
import { useNotifications } from './useNotifications'
import { useGameStateUpdate } from './useGameStateUpdate'

export const useGameActions = () => {
  const { gameId, playerId } = useGameStore()
  const { updateMyClue, setMyBoardSubmitted } = useBoardStore()
  const { setIsValidationPending, setValidationResults } = useGuessingStore()
  const { notifySuccess, notifyError, notifyInfo } = useNotifications()
  const { updateStateFromResponse } = useGameStateUpdate()
  const [loading, setLoading] = useState(false)

  const fetchGameState = useCallback(async (showLoading = true) => {
    if (!gameId || !playerId) return null

    if (showLoading) setLoading(true)
    try {
      const gameState = await gameApi.getGameState(gameId)
      updateStateFromResponse(gameState)
      return gameState
    } catch (error) {
      console.error('[useGameActions] Failed to fetch game state:', error)
      notifyError('Impossible de charger l\'état de la partie.')
    } finally {
      if (showLoading) setLoading(false)
    }
    return null
  }, [gameId, playerId, updateStateFromResponse, notifyError])

  const submitClue = async (position: 'top' | 'right' | 'bottom' | 'left', text: string) => {
    if (!gameId || !playerId) return

    try {
      const direction = position.charAt(0).toUpperCase() + position.slice(1)
      await gameApi.submitClue(gameId, playerId, direction, text)
      updateMyClue(position, text)
    } catch (error) {
      console.error(`[useGameActions] Failed to submit ${position} clue:`, error)
      throw error // Let the component handle it (e.g., ClueInput status)
    }
  }

  const submitBoard = async () => {
    if (!gameId || !playerId) return

    try {
      await gameApi.submitBoard(gameId, playerId)
      setMyBoardSubmitted(true)
      notifySuccess('Plateau soumis avec succès !')
    } catch (error) {
      console.error('[useGameActions] Failed to submit board:', error)
      notifyError('Échec de la soumission du plateau.')
      throw error
    }
  }

  const placeGuessingCard = async (outsideCardIndex: number, position: string) => {
    if (!gameId || !playerId) return
    try {
      await gameApi.placeGuessingCard(gameId, playerId, outsideCardIndex, position)
    } catch (error) {
      console.error('[useGameActions] Failed to place guessing card:', error)
      notifyError('Impossible de placer la carte.')
    }
  }

  const swapGuessingCards = async (position1: string, position2: string) => {
    if (!gameId || !playerId) return
    try {
      await gameApi.swapGuessingCards(gameId, playerId, position1, position2)
    } catch (error) {
      console.error('[useGameActions] Failed to swap guessing cards:', error)
      notifyError('Impossible d\'échanger les cartes.')
    }
  }

  const swapOutsidePoolCards = async (index1: number, index2: number) => {
    if (!gameId || !playerId) return
    try {
      await gameApi.swapOutsidePoolCards(gameId, playerId, index1, index2)
    } catch (error) {
      console.error('[useGameActions] Failed to swap pool cards:', error)
      notifyError('Impossible d\'échanger les cartes dans le pool.')
    }
  }

  const returnGuessingCard = async (position: string) => {
    if (!gameId || !playerId) return
    try {
      await gameApi.returnGuessingCard(gameId, playerId, position)
    } catch (error) {
      console.error('[useGameActions] Failed to return card to pool:', error)
      notifyError('Impossible de retirer la carte du plateau.')
    }
  }

  const validateBoard = async () => {
    if (!gameId || !playerId) return
    
    setIsValidationPending(true)
    try {
      const results = await gameApi.validateGuessingBoard(gameId, playerId)
      setValidationResults(results)
      
      if (results.isComplete) {
        notifySuccess('Parfait ! Toutes les cartes sont bien placées.')
      } else {
        notifyInfo(`${results.correctPositions.length} cartes correctes. ${results.remainingAttempts} tentatives restantes.`)
      }
      
      // On rafraîchit l'état pour mettre à jour correctlyPlacedPositions et remainingAttempts
      await fetchGameState(false)
    } catch (error) {
      console.error('[useGameActions] Failed to validate board:', error)
      notifyError('Échec de la validation.')
    } finally {
      setIsValidationPending(false)
    }
  }

  const broadcastBoardRotation = async (cumulativeRotation: number) => {
    if (!gameId || !playerId) return
    if (typeof cumulativeRotation !== 'number' || isNaN(cumulativeRotation)) {
      console.warn('[useGameActions] Invalid rotation value:', cumulativeRotation)
      return
    }
    try {
      await gameApi.rotateBoard(gameId, playerId, cumulativeRotation);
    } catch (error) {
      console.error('[useGameActions] Failed to persist board rotation:', error);
    }
  };

  const nextBoard = async () => {
    if (!gameId || !playerId) return

    setLoading(true)
    try {
      await gameApi.moveToNextBoard(gameId, playerId)
      // Fetch updated state immediately to reflect phase change (Guessing -> Scoring)
      // This ensures ScoringPage receives state before 30s deadline completes
      await fetchGameState(false)
    } catch (error) {
      console.error('[useGameActions] Failed to move to next board:', error)
      notifyError('Impossible de passer au plateau suivant.')
    } finally {
      setLoading(false)
    }
  }

  return {
    loading,
    fetchGameState,
    submitClue,
    submitBoard,
    placeGuessingCard,
    swapGuessingCards,
    swapOutsidePoolCards,
    returnGuessingCard,
    validateBoard,
    broadcastBoardRotation,
    nextBoard
  }
}
