import { useGameStore } from '../core/store';
import { GameStateResponse, GameScoringResponse, ClueValidationRejection, ClueValidationResponse } from '../types/game';

export interface CreateGameResponse {
  gameId: string;
  playerId: string;
}

export interface JoinGameResponse {
  playerId: string;
}

export interface JoinGameConflictResponse {
  isConflict: true;
  existingPlayerId: string;
  message: string;
}

export type JoinGameResult = JoinGameResponse | JoinGameConflictResponse;

export const gameApi = {
  createGame: async (playerName: string, language: string = 'Français_OFF'): Promise<CreateGameResponse> => {
    const response = await fetch('/api/games', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerName, language }),
    });

    if (!response.ok) {
      if (response.status === 400) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || 'Failed to create game');
      }
      throw new Error('Failed to create game');
    }

    return response.json();
  },

  joinGame: async (gameId: string, playerName: string, replaceExisting: boolean = false): Promise<JoinGameResult> => {
    const response = await fetch(`/api/games/${gameId}/join`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerName, replaceExisting }),
    });

    if (response.status === 409) {
      const data = await response.json();
      return {
        isConflict: true,
        existingPlayerId: data.existingPlayerId,
        message: data.message,
      } as JoinGameConflictResponse;
    }

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      if (response.status === 404) {
        throw new Error('Game not found');
      }
      throw new Error(errorData.message || 'Failed to join game');
    }

    return response.json();
  },

  getGameState: async (gameId: string): Promise<GameStateResponse> => {
    const { playerId } = useGameStore.getState();
    const url = `/api/games/${gameId}/state?includeSecrets=false${playerId ? `&playerId=${playerId}` : ''}`;
    const response = await fetch(url);
    if (!response.ok) {
      if (response.status === 404) {
        const error = new Error('Game not found') as Error & { status: number };
        error.status = 404;
        throw error;
      }
      if (response.status === 401 || response.status === 403) {
        const error = new Error('Unauthorized') as Error & { status: number };
        error.status = response.status;
        throw error;
      }
      throw new Error('Failed to fetch game state');
    }
    return response.json();
  },

  updateSettings: async (
      gameId: string,
      playerId: string,
      settings: {
        language: string;
        cluesDuration: number;
        guessDuration: number;
        semanticClueCheckEnabled?: boolean;
        guessAiBoardOnly?: boolean;
      }
  ): Promise<{
    language: string;
    cluesDuration: number;
    guessDuration: number;
    semanticClueCheckEnabled: boolean;
    guessAiBoardOnly: boolean;
  }> => {
    const response = await fetch(`/api/games/${gameId}/settings`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId, ...settings }),
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to update settings');
    }
    return response.json();
  },

  startGame: async (gameId: string): Promise<{ disconnectedPlayers?: string[] }> => {
    const response = await fetch(`/api/games/${gameId}/start`, {
      method: 'POST',
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      if (errorData.disconnectedPlayers) {
        return { disconnectedPlayers: errorData.disconnectedPlayers };
      }
      throw new Error(errorData.message || 'Failed to start game');
    }
    return {};
  },

  addAIPlayer: async (gameId: string, adminPlayerId: string, playerName: string): Promise<{ playerId: string }> => {
    const response = await fetch(`/api/games/${gameId}/ai-players`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ adminPlayerId, playerName }),
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to add AI player');
    }
    return response.json();
  },

  cancelGame: async (gameId: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}`, {
      method: 'DELETE',
    });
    if (!response.ok && response.status !== 404) {
      throw new Error('Failed to cancel game');
    }
  },

  kickPlayer: async (gameId: string, targetPlayerId: string, adminPlayerId: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/kick`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId: targetPlayerId, adminPlayerId }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to kick player');
    }
  },

  leaveGame: async (gameId: string, playerId: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/leave`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId }),
    });

    if (!response.ok && response.status !== 404) {
      throw new Error('Failed to leave game');
    }
  },

  submitClue: async (gameId: string, playerId: string, direction: string, clueText: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/clues`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId, direction, clueText }),
    });

    if (response.status === 400) {
      const errorData = await response.json().catch(() => ({}));
      if (Array.isArray(errorData.errors)) {
        throw new ClueValidationRejection(errorData.errors);
      }
      throw new Error(errorData.message || 'Failed to submit clue');
    }

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to submit clue');
    }
  },

  submitBoard: async (gameId: string, playerId: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/submit-board`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to submit board');
    }
  },

  placeGuessingCard: async (gameId: string, playerId: string, outsideCardIndex: number, position: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/place-guessing-card`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId, outsideCardIndex, position }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to place card');
    }
  },

  swapGuessingCards: async (gameId: string, playerId: string, position1: string, position2: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/swap-guessing-cards`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId, position1, position2 }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to swap cards');
    }
  },

  swapOutsidePoolCards: async (gameId: string, playerId: string, index1: number, index2: number): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/swap-outside-pool-cards`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId, index1, index2 }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to swap pool cards');
    }
  },

  rotateGuessingCard: async (
    gameId: string, 
    playerId: string, 
    steps: number, 
    position?: string, 
    outsideCardIndex?: number
  ): Promise<void> => {
    // Le backend utilise l'endpoint /rotate-card
    const response = await fetch(`/api/games/${gameId}/rotate-card`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId, steps, position, outsideCardIndex }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to rotate card');
    }
  },

  rotateBoard: async (gameId: string, playerId: string, cumulativeRotation: number): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/rotate-board`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId, cumulativeRotation }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to rotate board');
    }
  },

  returnGuessingCard: async (gameId: string, playerId: string, position: string): Promise<void> => {
    // Note: Le backend utilise actuellement SwapGuessingCards avec une position vide ou une logique interne 
    // pour retirer une carte, ou cet endpoint n'est pas encore implémenté côté serveur.
    // Selon le code vanilla, le retour au pool se faisait via une logique différente ou n'était pas un endpoint dédié.
    // Si l'erreur 500 persiste, c'est probablement que cet endpoint n'existe pas.
    const response = await fetch(`/api/games/${gameId}/return-guessing-card`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId, position }),
    });

    if (!response.ok) {
      // Si l'endpoint n'existe pas (404), on log l'info mais on ne bloque pas forcément l'UI si le swap peut gérer
      if (response.status === 404) {
        console.warn(`Endpoint /return-guessing-card non trouvé sur le serveur.`);
      }
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to return card to pool');
    }
  },

  validateGuessingBoard: async (gameId: string, playerId: string): Promise<any> => {
    const response = await fetch(`/api/games/${gameId}/validate-guessing-board`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to validate guessing board');
    }
    return response.json();
  },

  moveToNextBoard: async (gameId: string, playerId: string): Promise<any> => {
    const response = await fetch(`/api/games/${gameId}/move-to-next-board`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ playerId }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Failed to move to next board');
    }
    return response.json();
  },

  getDictionaries: async (): Promise<{ key: string, name: string }[]> => {
    const response = await fetch('/api/dictionaries');
    if (!response.ok) {
      throw new Error('Failed to load dictionaries');
    }
    return response.json();
  },

  getScoring: async (gameId: string): Promise<GameScoringResponse> => {
    const response = await fetch(`/api/games/${gameId}/scoring`);
    if (!response.ok) {
      throw new Error('Impossible de récupérer les données de scoring');
    }
    return response.json();
  },

  completeGame: async (gameId: string, playerId: string): Promise<void> => {
    const response = await fetch(`/api/games/${gameId}/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId })
    });
    if (!response.ok) {
      throw new Error('Impossible de terminer la partie');
    }
  },

  validateClue: async (
      gameId: string,
      playerId: string,
      direction: string,
      clueText: string,
      signal?: AbortSignal
  ): Promise<ClueValidationResponse> => {
    const response = await fetch(`/api/games/${gameId}/clues/validate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId, direction, clueText }),
      signal,
    });
    if (!response.ok) {
      throw new Error('Failed to validate clue');
    }
    return response.json();
  },
};
