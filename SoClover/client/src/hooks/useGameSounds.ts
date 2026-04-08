import { useEffect, useRef } from 'react';
import { signalRClient } from '../api/signalr-client';
import { useGameStore } from '../core/store';
import { playSound } from '../core/sounds';

interface GameStateUpdatedPayload {
  eventType: string;
  eventData?: {
    playerId?: string;
    wasSlotOccupied?: boolean;
  };
}

interface BoardRotationUpdatedPayload {
  cumulativeRotation: number;
  playerId?: string;
}

/**
 * Joue les sons de jeu pour les SPECTATEURS en réaction aux events SignalR.
 *
 * Principe (voir Story 9) :
 * - Le joueur DÉCLENCHEUR joue le son localement, au moment du geste (zéro latence).
 *   Voir useDragOrchestration, DraggableCard, BoardRotationControls.
 * - Ce hook joue le son pour tous les AUTRES joueurs sur réception de l'event SignalR.
 *
 * Mécanisme de dé-duplication : guard `playerId === self`. Si l'event provient de soi-même,
 * on skip — le son a déjà été joué localement au déclenchement de l'action.
 * Pattern déjà utilisé dans useSignalR.ts pour handleBoardRotationUpdated.
 *
 * Aucun guard de phase : en WritingClues, aucun event SignalR de gameplay n'est émis
 * (RotateBoard.cs rejette hors Guessing ; rotations locales via updateMyBoardRotation).
 *
 * NB : BoardRotated est géré EXCLUSIVEMENT via le listener 'BoardRotationUpdated' ci-dessous.
 * Ne jamais ajouter de case 'BoardRotated' dans le switch GameStateUpdated.
 */
export const useGameSounds = () => {
  // Dé-duplication du son GuessingCardReturned : plusieurs cartes peuvent être retournées
  // en rafale (fin de round), mais un seul son suffit pour signaler le mouvement.
  const cardReturnedPlayedRef = useRef(false);
  const cardReturnedTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const isSelf = (eventPlayerId: string | undefined) => {
      if (!eventPlayerId) return false;
      return eventPlayerId === useGameStore.getState().playerId;
    };

    const handleGameStateUpdated = (data: GameStateUpdatedPayload) => {
      const eventType = data?.eventType;
      const eventData = data?.eventData ?? {};

      // Guard : si l'event provient de soi-même, le son a déjà été joué localement.
      if (isSelf(eventData.playerId)) return;

      switch (eventType) {
        case 'GuessingCardPlaced':
          // Distinction cardPlace / cardSwap grâce au champ WasSlotOccupied (Story 9.1)
          playSound(eventData.wasSlotOccupied ? 'cardSwap' : 'cardPlace');
          break;
        case 'GuessingCardsSwapped':
          playSound('cardSwap');
          break;
        case 'OutsidePoolCardsSwapped':
          playSound('cardSwap');
          break;
        case 'CardRotated':
          playSound('cardRotate');
          break;
        case 'GuessingCardReturned':
          // Board→Pool : plusieurs cartes peuvent revenir en rafale (fin de round).
          // On joue cardPlace une seule fois par vague — le son suffit à signaler le mouvement.
          if (!cardReturnedPlayedRef.current) {
            cardReturnedPlayedRef.current = true;
            playSound('cardPlace');
            cardReturnedTimeoutRef.current = setTimeout(() => {
              cardReturnedPlayedRef.current = false;
            }, 300);
          }
          break;
        default:
          break;
      }
    };

    // BoardRotated est broadcasté via son propre canal 'BoardRotationUpdated', séparé de GameStateUpdated.
    // C'est l'UNIQUE source du son boardRotate pour les spectateurs — ne pas écouter BoardRotated via GameStateUpdated.
    const handleBoardRotationUpdated = (data: BoardRotationUpdatedPayload) => {
      if (isSelf(data?.playerId)) return;
      playSound('boardRotate');
    };

    signalRClient.on('GameStateUpdated', handleGameStateUpdated);
    signalRClient.on('BoardRotationUpdated', handleBoardRotationUpdated);

    return () => {
      signalRClient.off('GameStateUpdated', handleGameStateUpdated);
      signalRClient.off('BoardRotationUpdated', handleBoardRotationUpdated);
      if (cardReturnedTimeoutRef.current) clearTimeout(cardReturnedTimeoutRef.current);
    };
  }, []);
};
