import React, { useState } from 'react';
import { X } from 'lucide-react';
import { useGameStore } from '../../core/store';
import { gameApi } from '../../api/game-api';

export const PlayerList: React.FC = () => {
  const { players, playerId, isGameAdmin, adminPlayerId, gameId } = useGameStore();
  const [kickingPlayerId, setKickingPlayerId] = useState<string | null>(null);
  const [addingAI, setAddingAI] = useState(false);
  const [addAIError, setAddAIError] = useState<string | null>(null);

  const aiPlayers = players.filter(p => p.isAI);
  const aiPlayerCount = aiPlayers.length;

  const nextAIPlayerName = (): string => {
    const usedNumbers = new Set(
      aiPlayers.map(p => {
        const match = p.name.match(/^AI Player (\d+)$/);
        return match ? parseInt(match[1], 10) : null;
      }).filter((n): n is number => n !== null)
    );
    let n = 1;
    while (usedNumbers.has(n)) n++;
    return `AI Player ${n}`;
  };

  const handleKick = async (targetPlayerId: string, targetName: string) => {
    if (!gameId || !playerId) return;

    const confirmed = window.confirm(`Retirer ${targetName} de la partie ?`);
    if (!confirmed) return;

    setKickingPlayerId(targetPlayerId);
    try {
      await gameApi.kickPlayer(gameId, targetPlayerId, playerId);
    } catch (err) {
      console.error('Failed to kick player', err);
    } finally {
      setKickingPlayerId(null);
    }
  };

  const handleAddAIPlayer = async () => {
    if (!gameId || !playerId) return;
    setAddingAI(true);
    setAddAIError(null);
    try {
      await gameApi.addAIPlayer(gameId, playerId, nextAIPlayerName());
    } catch (err) {
      setAddAIError(err instanceof Error ? err.message : 'Erreur lors de l\'ajout');
    } finally {
      setAddingAI(false);
    }
  };

  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div className="bg-slate-50 px-4 py-3 border-b border-slate-200 flex justify-between items-center">
        <h3 className="font-semibold text-slate-700">Joueurs</h3>
        <span className="bg-slate-200 text-slate-600 text-xs font-bold px-2 py-1 rounded-full">
          {players.length}
        </span>
      </div>
      <div className="divide-y divide-slate-100">
        {players.map((player) => {
          const isMe = player.playerId === playerId;
          const isAdmin = player.playerId === adminPlayerId;
          const canKick = isGameAdmin && !isMe;

          return (
            <div key={player.playerId} className="px-4 py-3 flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center font-bold text-sm">
                {player.name.charAt(0).toUpperCase()}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-slate-900 truncate">
                  {player.name} {isMe && <span className="text-slate-400 font-normal text-xs ml-1">(Vous)</span>}
                </p>
              </div>
              {player.isAI && (
                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-violet-100 text-violet-800">
                  IA
                </span>
              )}
              {isAdmin && (
                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-amber-100 text-amber-800">
                  Createur
                </span>
              )}
              {canKick && (
                <button
                  onClick={() => handleKick(player.playerId, player.name)}
                  disabled={kickingPlayerId === player.playerId}
                  className="p-1 rounded-full text-slate-400 hover:text-red-500 hover:bg-red-50 transition-colors disabled:opacity-50"
                  title={`Retirer ${player.name}`}
                >
                  <X size={16} />
                </button>
              )}
            </div>
          );
        })}
      </div>
      {isGameAdmin && aiPlayerCount < 4 && (
        <div className="px-4 py-2 border-t border-slate-100">
          {addAIError && (
            <p className="text-xs text-red-500 mb-1">{addAIError}</p>
          )}
          <button
            onClick={handleAddAIPlayer}
            disabled={addingAI}
            className="w-full text-sm font-medium text-violet-600 hover:text-violet-700 hover:bg-violet-50 rounded-lg py-1.5 transition-colors disabled:opacity-50"
          >
            {addingAI ? 'Ajout en cours...' : '+ Ajouter un joueur IA'}
          </button>
        </div>
      )}
    </div>
  );
};
