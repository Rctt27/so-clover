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
    const playersList = state.players.map(p => ({
      playerId: p.playerId,
      name: p.name,
      cursorColorIndex: p.cursorColorIndex
    }));
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
      const guessingState = useGuessingStore.getState();
      const currentOwnerId = guessingState.currentBoardOwnerId;
      const isNewBoard = currentOwnerId !== state.guessingState.currentBoardOwnerId;
      const isFirstLoad = currentOwnerId === null;

      // Mettre à jour la rotation UNIQUEMENT dans ces cas :
      // 1. Premier chargement (page refresh) - restaurer depuis le serveur
      // 2. Changement de board - le serveur réinitialise à 0
      // Dans tous les autres cas, la rotation locale fait foi (évite les race conditions)
      const shouldUpdateRotation = isFirstLoad || isNewBoard;

      // TOUJOURS inclure cumulativeBoardRotation pour éviter les valeurs stale pendant les re-renders
      // Choisir la source en fonction du contexte :
      // - Premier chargement ou nouveau board → utiliser valeur serveur (réinitialisation à 0)
      // - Autres cas → préserver rotation locale (éviter race conditions avec rotations utilisateur)
      const rotationToUse = shouldUpdateRotation
        ? (state.guessingState.cumulativeBoardRotation ?? 0)  // Serveur
        : guessingState.cumulativeBoardRotation;               // Local (préservation)

      setCurrentBoardOwner(state.guessingState.currentBoardOwnerId);
      setGuessingState({
        currentBoardOwnerId: state.guessingState.currentBoardOwnerId,
        currentBoardOwnerName: state.guessingState.currentBoardOwnerName,
        outsideCards: state.guessingState.outsideCards,
        guessedPositions: state.guessingState.guessedPositions,
        correctlyPlacedPositions: state.guessingState.correctlyPlacedPositions,
        remainingAttempts: state.guessingState.remainingAttempts,
        currentBoardClues: state.guessingState.currentBoardClues,
        cumulativeBoardRotation: rotationToUse  // TOUJOURS inclus
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
