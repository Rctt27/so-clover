import { useCallback } from 'react';
import { useGameStore, useBoardStore, useGuessingStore } from '../core/store';
import { GamePhase, GameStateResponse } from '../types/game';
import { convertBackendBoardToClientBoard } from '../core/boardHelpers';
import { debugLog } from '../core/debug';

// La phase ne peut qu'avancer — jamais régresser (évite les oscillations dues aux events SignalR tardifs)
const PHASE_ORDER: GamePhase[] = ['Initial', 'Lobby', 'WritingClues', 'Guessing', 'Scoring'];

export const useGameStateUpdate = () => {
  const {
    setPhase,
    setPlayers,
    setPhaseEndsAtUtc,
    setSettings,
    setRole,
    playerId: myPlayerId,
    setIsGameAdmin,
    setAdminPlayerId,
  } = useGameStore();

  const { setMyBoard, setOtherBoard, setCurrentBoardOwner } = useBoardStore();
  const { setGuessingState } = useGuessingStore();

  const updateStateFromResponse = useCallback((state: GameStateResponse) => {
    if (!state) return;

    debugLog('useGameStateUpdate', `appelé — phase entrante: "${state.phase}"`);

    // 1. Mise à jour de l'état global
    // La phase ne peut qu'avancer : un event SignalR tardif (ex: validation Guessing arrivant
    // après que fetchGameState ait déjà setté Scoring) ne doit pas faire régresser la phase.
    const currentPhase = useGameStore.getState().phase;
    const incomingPhaseIndex = PHASE_ORDER.indexOf(state.phase);
    const currentPhaseIndex = PHASE_ORDER.indexOf(currentPhase);
    if (incomingPhaseIndex >= currentPhaseIndex) {
      if (state.phase !== currentPhase) {
        debugLog('useGameStateUpdate', `setPhase: "${currentPhase}" → "${state.phase}"`);
      }
      setPhase(state.phase);
    } else {
      console.warn(
        `%c[updateStateFromResponse] RÉGRESSION BLOQUÉE: "${currentPhase}" → "${state.phase}" (event SignalR tardif)`,
        'color: #dc2626; font-weight: bold'
      );
    }
    setPhaseEndsAtUtc(state.phaseEndsAtUtc);
    setSettings({
      language: state.language,
      cluesDurationSeconds: state.cluesDurationSecondsOverride ?? 300,
      guessDurationSeconds: state.guessDurationSecondsOverride ?? 300,
      semanticClueCheckEnabled: state.semanticClueCheckEnabled,
      guessAiBoardOnly: state.guessAiBoardOnly,
    });

    // 2. Mise à jour des joueurs
    const playersList = state.players.map(p => ({
      playerId: p.playerId,
      name: p.name,
      cursorColorIndex: p.cursorColorIndex,
      isAI: p.isAI ?? false,
    }));
    setPlayers(playersList);

    // 3. Suis-je admin ?
    setAdminPlayerId(state.adminPlayerId);
    if (myPlayerId && state.adminPlayerId) {
      setIsGameAdmin(myPlayerId === state.adminPlayerId);
    }

    // 4. Déterminer mon rôle
    if (myPlayerId) {
      if (state.phase === 'WritingClues') {
        setRole('PlayerWritingClue');
      } else if (state.phase === 'Guessing' && state.guessingState) {
        if (state.guessingState.currentBoardOwnerId === myPlayerId) {
          setRole('PlayerBoardOwner');
        } else {
          setRole('PlayerGuesser');
        }
      } else if (state.phase === 'Scoring') {
          setRole('Spectator');
      }
    }

    // 5. Mise à jour des plateaux
    state.players.forEach(p => {
      const clientBoard = convertBackendBoardToClientBoard(p.board, p.playerId);

      // Preserve local rotation during WritingClues phase as it's not stored on server
      if (state.phase === 'WritingClues') {
        const currentMyBoard = useBoardStore.getState().myBoard;
        const currentOtherBoards = useBoardStore.getState().otherBoards;

        const existingBoard = p.playerId === myPlayerId ? currentMyBoard : currentOtherBoards[p.playerId];
        if (existingBoard) {
          clientBoard.rotation = existingBoard.rotation;
        }
      }

      if (p.playerId === myPlayerId) {
        setMyBoard(clientBoard);
      } else {
        setOtherBoard(p.playerId, clientBoard);
      }
    });

    // 6. Mise à jour de l'état de guessing
    if (state.guessingState) {
      const prevState = useGuessingStore.getState();
      const currentOwnerId = prevState.currentBoardOwnerId;
      const isNewBoard = currentOwnerId !== state.guessingState.currentBoardOwnerId;

      setCurrentBoardOwner(state.guessingState.currentBoardOwnerId);

      if (isNewBoard) {
        // New board (or first load): trust server completely, reset revision baseline.
        setGuessingState({
          currentBoardOwnerId: state.guessingState.currentBoardOwnerId,
          currentBoardOwnerName: state.guessingState.currentBoardOwnerName,
          outsideCards: state.guessingState.outsideCards,
          guessedPositions: state.guessingState.guessedPositions,
          correctlyPlacedPositions: state.guessingState.correctlyPlacedPositions,
          remainingAttempts: state.guessingState.remainingAttempts,
          currentBoardClues: state.guessingState.currentBoardClues,
          cumulativeBoardRotation: state.guessingState.cumulativeBoardRotation ?? 0,
          lastAppliedRotationRevision: state.revision,
          failedPlacements: state.guessingState.failedPlacements ?? [],
        });
      } else {
        // Same board: write everything except rotation, which is gated by revision.
        setGuessingState({
          currentBoardOwnerName: state.guessingState.currentBoardOwnerName,
          outsideCards: state.guessingState.outsideCards,
          guessedPositions: state.guessingState.guessedPositions,
          correctlyPlacedPositions: state.guessingState.correctlyPlacedPositions,
          remainingAttempts: state.guessingState.remainingAttempts,
          currentBoardClues: state.guessingState.currentBoardClues,
          failedPlacements: state.guessingState.failedPlacements ?? [],
        });
        prevState.applyServerRotation(state.guessingState.cumulativeBoardRotation ?? 0, state.revision);
      }
    } else {
      setCurrentBoardOwner(null);
    }
  }, [
    setPhase,
    setPlayers,
    setPhaseEndsAtUtc,
    setSettings,
    setRole,
    myPlayerId,
    setIsGameAdmin,
    setAdminPlayerId,
    setMyBoard,
    setOtherBoard,
    setCurrentBoardOwner,
    setGuessingState,
  ]);

  return { updateStateFromResponse };
};
