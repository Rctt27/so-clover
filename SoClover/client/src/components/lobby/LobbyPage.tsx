import React, { useEffect, useState } from 'react';
import { useGameStore } from '../../core/store';
import { PlayerList } from './PlayerList';
import { GameSettings } from './GameSettings';
import { gameApi } from '../../api/game-api';

export const LobbyPage: React.FC = () => {
  const { gameId, playerId, isGameAdmin, players, resetAuth, setSettings } = useGameStore();
  const canStart = players.length >= 2;
  const [loading, setLoading] = useState(false);
  const [copied, setCopied] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);

  useEffect(() => {
    const fetchInitialState = async () => {
      if (!gameId) return;
      try {
        const state = await gameApi.getGameState(gameId);
        if (state) {
          setSettings({
            language: state.language || 'Français_OFF',
            cluesDurationSeconds: state.cluesDurationSecondsOverride ?? 300,
            guessDurationSeconds: state.guessDurationSecondsOverride ?? 300,
            semanticClueCheckEnabled: state.semanticClueCheckEnabled ?? true,
            guessAiBoardOnly: state.guessAiBoardOnly ?? false,
          });
        }
      } catch (err) {
        console.error('Failed to fetch initial game state', err);
      }
    };

    fetchInitialState();
  }, [gameId, setSettings]);

  const handleStartGame = async () => {
    if (!gameId || !isGameAdmin) return;
    setLoading(true);
    setStartError(null);
    try {
      const result = await gameApi.startGame(gameId);
      if (result.disconnectedPlayers && result.disconnectedPlayers.length > 0) {
        setStartError(
          `Impossible de lancer : ${result.disconnectedPlayers.join(', ')} semblent deconnectes. Retirez-les avant de continuer.`
        );
        setLoading(false);
        return;
      }
      // La redirection sera gérée par le changement de phase via SignalR
    } catch (err) {
      console.error('Failed to start game', err);
      setLoading(false);
    }
  };

  const handleCancelGame = async () => {
    if (!gameId || !isGameAdmin) return;
    if (!window.confirm('Êtes-vous sûr de vouloir annuler cette partie ?')) return;

    setLoading(true);
    try {
      await gameApi.cancelGame(gameId);
      resetAuth();
    } catch (err) {
      console.error('Failed to cancel game', err);
      setLoading(false);
    }
  };

  const handleLeaveGame = async () => {
    if (!window.confirm('Êtes-vous sûr de vouloir quitter la partie ?')) return;
    
    if (gameId && playerId) {
      try {
        await gameApi.leaveGame(gameId, playerId);
      } catch (err) {
        console.error('Failed to leave game', err);
      }
    }
    resetAuth();
  };

  const copyToClipboard = () => {
    if (gameId) {
      navigator.clipboard.writeText(gameId);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <header className="mb-8 text-center">
        <h1 className="text-4xl font-black text-emerald-600 mb-2">SO CLOVER!</h1>
        <div className="inline-flex items-center gap-2 bg-slate-100 px-4 py-2 rounded-full border border-slate-200">
          <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Code de partie</span>
          <code className="text-emerald-700 font-mono font-bold">{gameId}</code>
          <button 
            onClick={copyToClipboard}
            className={`ml-2 p-1 rounded transition-colors ${copied ? 'bg-emerald-500 text-white' : 'hover:bg-slate-200 text-slate-400'}`}
          >
            {copied ? '✓' : '📋'}
          </button>
        </div>
      </header>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        <div className="md:col-span-1">
          <PlayerList />
        </div>
        
        <div className="md:col-span-2 space-y-6">
          <GameSettings />
          
          {startError && (
            <div className="bg-amber-50 text-amber-700 p-3 rounded-lg text-sm border border-amber-200">
              {startError}
            </div>
          )}
          {isGameAdmin && !canStart && (
            <p className="text-xs text-slate-400 text-right">
              Il faut au moins 2 joueurs pour lancer la partie.
            </p>
          )}

          <div className="flex flex-col sm:flex-row gap-4 justify-end pt-4">
            {isGameAdmin ? (
              <>
                <button
                  onClick={handleCancelGame}
                  disabled={loading}
                  className="px-6 py-3 rounded-xl font-bold text-slate-600 border border-slate-200 hover:bg-slate-50 transition-all disabled:opacity-50"
                >
                  Annuler la partie
                </button>
                <button
                  onClick={handleStartGame}
                  disabled={loading || !canStart}
                  className="px-8 py-3 rounded-xl font-bold text-white bg-emerald-600 hover:bg-emerald-700 shadow-lg shadow-emerald-200 transition-all disabled:opacity-50 flex items-center justify-center gap-2"
                >
                  {loading && <span className="animate-spin">⏳</span>}
                  🚀 Lancer la partie
                </button>
              </>
            ) : (
              <button
                onClick={handleLeaveGame}
                className="px-6 py-3 rounded-xl font-bold text-slate-600 border border-slate-200 hover:bg-slate-50 transition-all"
              >
                Quitter la partie
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
