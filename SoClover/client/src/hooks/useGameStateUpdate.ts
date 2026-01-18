import { useCallback } from 'react';
import { useGameStore, useBoardStore, useGuessingStore } from '../core/store';
import { GameStateResponse } from '../types/game';
import { convertBackendBoardToClientBoard } from '../core/boardHelpers';

export const useGameStateUpdate = () => {
  const { 
    setPhase, 
    setPlayers, 
    setPhaseEndsAtUtc, 
    setSettings, 
    setRole,
    playerId: myPlayerId,
    setIsGameAdmin
  } = useGameStore();
  
  const { setMyBoard, setOtherBoard, setCurrentBoardOwner } = useBoardStore();
  const { setGuessingState } = useGuessingStore();

  const updateStateFromResponse = useCallback((state: GameStateResponse) => {
    if (!state) return;

    // 1. Mise à jour de l'état global
    setPhase(state.phase);
    setPhaseEndsAtUtc(state.phaseEndsAtUtc);
    setSettings({
      language: state.language,
      cluesDurationSeconds: state.cluesDurationSecondsOverride || 300,
      guessDurationSeconds: state.guessDurationSecondsOverride || 300,
    });

    // 2. Mise à jour des joueurs
    const playersList = state.players.map(p => ({ playerId: p.playerId, name: p.name }));
    setPlayers(playersList);

    // 3. Suis-je admin ?
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
      if (p.playerId === myPlayerId) {
        setMyBoard(clientBoard);
      } else {
        setOtherBoard(p.playerId, clientBoard);
      }
    });

    // 6. Mise à jour de l'état de guessing
    if (state.guessingState) {
      setCurrentBoardOwner(state.guessingState.currentBoardOwnerId);
      setGuessingState({
        currentBoardOwnerId: state.guessingState.currentBoardOwnerId,
        currentBoardOwnerName: state.guessingState.currentBoardOwnerName,
        outsideCards: state.guessingState.outsideCards,
        guessedPositions: state.guessingState.guessedPositions,
        correctlyPlacedPositions: state.guessingState.correctlyPlacedPositions,
        remainingAttempts: state.guessingState.remainingAttempts,
        currentBoardClues: state.guessingState.currentBoardClues,
        cumulativeBoardRotation: state.guessingState.cumulativeBoardRotation
      });
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
    setMyBoard, 
    setOtherBoard, 
    setCurrentBoardOwner, 
    setGuessingState
  ]);

  return { updateStateFromResponse };
};
