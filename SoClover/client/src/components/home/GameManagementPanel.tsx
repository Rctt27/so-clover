import React, { useState } from 'react';
import { Gamepad2, LogIn, Loader2 } from 'lucide-react';
import { useGameStore } from '../../core/store';
import { gameApi, JoinGameResponse } from '../../api/game-api';

export const GameManagementPanel: React.FC = () => {
  const { playerName, setGameId, setPlayerId, setIsGameAdmin, setPhase, setPlayers } = useGameStore();
  const [gameIdInput, setGameIdInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isNameEmpty = !playerName || playerName.trim().length === 0;

  const handleCreateGame = async () => {
    if (isNameEmpty) return;
    setIsLoading(true);
    setError(null);
    try {
      const response = await gameApi.createGame(playerName!);
      setGameId(response.gameId);
      setPlayerId(response.playerId);
      setIsGameAdmin(true);
      setPhase('Lobby');
      // Optimistic update: add self to players list to avoid empty list during SignalR connection
      setPlayers([{ playerId: response.playerId, name: playerName!, cursorColorIndex: 0, isAI: false }]);
      // On laisse useSignalR gérer le refreshGameState une fois connecté
    } catch (err: any) {
      setError(err.message || 'Erreur lors de la création de la partie');
    } finally {
      setIsLoading(false);
    }
  };

  const handleJoinGame = async (replaceExisting: boolean = false) => {
    if (isNameEmpty || !gameIdInput) return;
    setIsLoading(true);
    setError(null);
    try {
      const result = await gameApi.joinGame(gameIdInput, playerName!, replaceExisting);

      if ('isConflict' in result && result.isConflict) {
        setIsLoading(false);
        const confirmed = window.confirm(
          `Un joueur nomme "${playerName}" existe deja dans cette partie. Voulez-vous le remplacer ?`
        );
        if (confirmed) {
          await handleJoinGame(true);
        }
        return;
      }

      const response = result as JoinGameResponse;
      setGameId(gameIdInput);
      setPlayerId(response.playerId);
      setIsGameAdmin(false);
      setPhase('Lobby');
      // On laisse useSignalR gérer le rafraîchissement complet des joueurs via le serveur
    } catch (err: any) {
      setError(err.message || 'Erreur lors de la connexion à la partie');
    } finally {
      setIsLoading(false);
    }
  };

  const isJoining = gameIdInput.trim().length > 0;

  return (
    <section className="bg-white rounded-xl shadow-md p-6 w-full max-w-md">
      <h2 className="text-xl font-bold mb-4 text-gray-800">Gestion de la Partie</h2>

      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <label htmlFor="gameIdInput" className="text-sm font-medium text-gray-700">
            ID de la Partie (optionnel)
          </label>
          <input
            type="text"
            id="gameIdInput"
            value={gameIdInput}
            onChange={(e) => setGameIdInput(e.target.value)}
            placeholder="Collez l'ID pour rejoindre"
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-clover focus:border-transparent outline-none transition-all"
            autoComplete="off"
          />
        </div>

        {error && (
          <div className="bg-red-50 text-red-600 p-3 rounded-lg text-sm border border-red-100">
            {error}
          </div>
        )}

        <button
          onClick={isJoining ? () => handleJoinGame() : handleCreateGame}
          disabled={isNameEmpty || isLoading}
          className={`flex items-center justify-center gap-2 w-full py-3 rounded-lg font-bold text-white transition-all shadow-md
            ${isNameEmpty ? 'bg-gray-300 cursor-not-allowed' : 'bg-clover hover:bg-clover-dark active:scale-95'}
          `}
        >
          {isLoading ? (
            <Loader2 className="animate-spin" size={20} />
          ) : isJoining ? (
            <LogIn size={20} />
          ) : (
            <Gamepad2 size={20} />
          )}
          {isLoading ? 'Traitement...' : isJoining ? 'Rejoindre la Partie' : 'Créer une Partie'}
        </button>

        {isNameEmpty && (
          <p className="text-xs text-center text-gray-500 italic">
            Veuillez entrer votre nom pour commencer
          </p>
        )}
      </div>
    </section>
  );
};
