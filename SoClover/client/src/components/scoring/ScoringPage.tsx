import React, { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useGameStore } from '../../core/store';
import { gameApi } from '../../api/game-api';
import { GameScoringResponse, ScoringBoardResponse } from '../../types/game';
import { Trophy, Users, XCircle, Clock, Target, LogOut } from 'lucide-react';
import { debugLog } from '../../core/debug';

const formatDuration = (seconds: number): string => {
  if (seconds < 60) {
    return `${seconds}s`;
  }
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  return `${minutes}m ${remainingSeconds}s`;
};

const RankBadge: React.FC<{ rank: number }> = ({ rank }) => {
  const medals: Record<number, string> = { 1: '🥇', 2: '🥈', 3: '🥉' };
  const medal = medals[rank];

  return (
    <span className="font-bold text-lg">
      {medal ? `${medal} ${rank}` : rank}
    </span>
  );
};

export const ScoringPage: React.FC = () => {
  const gameId = useGameStore(s => s.gameId);
  const playerId = useGameStore(s => s.playerId);
  const isGameAdmin = useGameStore(s => s.isGameAdmin);
  const resetAuth = useGameStore(s => s.resetAuth);

  const [scoringData, setScoringData] = useState<GameScoringResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEndingGame, setIsEndingGame] = useState(false);

  // Charger les données de scoring
  const phase = useGameStore(s => s.phase);

  // [DEBUG] Mount / Unmount
  useEffect(() => {
    debugLog('ScoringPage', 'MOUNTED');
    return () => debugLog('ScoringPage', 'UNMOUNTED');
  }, []);

  useEffect(() => {
    const fetchScoring = async () => {
      if (!gameId) return;

      try {
        setIsLoading(true);
        const data = await gameApi.getScoring(gameId);
        setScoringData(data);
        setError(null);
      } catch (err) {
        console.error('Erreur lors du chargement des scores:', err);
        setError('Impossible de charger les scores');
      } finally {
        setIsLoading(false);
      }
    };

    fetchScoring();
  }, [gameId, phase]);

  // Note: La redirection quand la partie est terminée (GameDeleted) est gérée
  // automatiquement par useSignalR qui appelle resetAuth() sur l'événement GameDeleted

  const handleEndGame = async () => {
    if (!gameId || !playerId) return;

    const confirmed = window.confirm(
      'Êtes-vous sûr de vouloir terminer la partie ? Tous les joueurs seront redirigés vers l\'accueil.'
    );

    if (!confirmed) return;

    try {
      setIsEndingGame(true);
      await gameApi.completeGame(gameId, playerId);
      // La redirection sera gérée par l'événement GameDeleted via SignalR
      // qui appelle resetAuth() et remet la phase à 'Initial'
      resetAuth();
    } catch (err) {
      console.error('Erreur lors de la fin de partie:', err);
      setError('Impossible de terminer la partie');
      setIsEndingGame(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="mt-4 text-slate-500">Chargement des scores...</p>
      </div>
    );
  }

  const successfulBoards = scoringData?.successfulBoards || [];
  const failedBoards = scoringData?.failedBoards || [];

  return (
    <div className="flex flex-col items-center gap-8 w-full max-w-4xl mx-auto p-6">
      {/* En-tête */}
      <motion.div
        initial={{ scale: 0.9, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        className="text-center"
      >
        <Trophy className="w-20 h-20 text-yellow-500 mx-auto mb-4" />
        <h1 className="text-4xl font-black text-clover-dark mb-2">Partie Terminée !</h1>
        <p className="text-slate-500">Voici les résultats de la partie.</p>
      </motion.div>

      {/* Message d'erreur */}
      {error && (
        <div className="bg-red-50 text-red-600 px-4 py-3 rounded-xl border border-red-200 w-full">
          {error}
        </div>
      )}

      {/* Tableau des scores - Boards réussis */}
      <motion.div
        initial={{ y: 20, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ delay: 0.1 }}
        className="bg-white rounded-3xl p-8 shadow-xl border border-slate-100 w-full"
      >
        <div className="flex items-center gap-3 mb-6">
          <Trophy className="text-yellow-500 w-6 h-6" />
          <h2 className="text-xl font-bold text-slate-800">Classement</h2>
        </div>

        {successfulBoards.length === 0 ? (
          <div className="text-center py-8 text-slate-500">
            <Users className="w-12 h-12 mx-auto mb-3 opacity-50" />
            <p>Aucun plateau n'a été deviné correctement.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-slate-200">
                  <th className="text-left py-3 px-4 text-slate-600 font-semibold">Rang</th>
                  <th className="text-left py-3 px-4 text-slate-600 font-semibold">Joueur</th>
                  <th className="text-center py-3 px-4 text-slate-600 font-semibold">
                    <span className="flex items-center justify-center gap-1">
                      <Target className="w-4 h-4" /> Essais
                    </span>
                  </th>
                  <th className="text-center py-3 px-4 text-slate-600 font-semibold">
                    <span className="flex items-center justify-center gap-1">
                      <Clock className="w-4 h-4" /> Durée
                    </span>
                  </th>
                  <th className="text-center py-3 px-4 text-slate-600 font-semibold">Statut</th>
                </tr>
              </thead>
              <tbody>
                {successfulBoards.map((result: ScoringBoardResponse, index: number) => (
                  <tr
                    key={result.playerId}
                    className={`border-b border-slate-100 transition-colors ${
                      result.playerId === playerId
                        ? 'bg-clover/10 hover:bg-clover/15'
                        : 'hover:bg-slate-50'
                    }`}
                  >
                    <td className="py-4 px-4">
                      <RankBadge rank={index + 1} />
                    </td>
                    <td className="py-4 px-4">
                      <span className={`font-medium ${
                        result.playerId === playerId ? 'text-clover-dark' : 'text-slate-700'
                      }`}>
                        {result.playerName}
                        {result.playerId === playerId && (
                          <span className="ml-2 text-xs bg-clover/20 text-clover-dark px-2 py-0.5 rounded-full">
                            Vous
                          </span>
                        )}
                      </span>
                    </td>
                    <td className="py-4 px-4 text-center text-slate-600">
                      {result.attempts}
                    </td>
                    <td className="py-4 px-4 text-center text-slate-600">
                      {formatDuration(result.durationSeconds)}
                    </td>
                    <td className="py-4 px-4 text-center">
                      <span className="inline-flex items-center gap-1 bg-green-100 text-green-700 px-3 py-1 rounded-full text-sm font-medium">
                        ✓ Deviné
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </motion.div>

      {/* Boards échoués */}
      {failedBoards.length > 0 && (
        <motion.div
          initial={{ y: 20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ delay: 0.2 }}
          className="bg-white rounded-3xl p-8 shadow-xl border border-slate-100 w-full"
        >
          <div className="flex items-center gap-3 mb-6">
            <XCircle className="text-red-400 w-6 h-6" />
            <h2 className="text-xl font-bold text-slate-800">Plateaux non devinés</h2>
          </div>

          <ul className="space-y-3">
            {failedBoards.map((result: ScoringBoardResponse) => (
              <li
                key={result.playerId}
                className={`flex items-center justify-between p-4 rounded-xl ${
                  result.playerId === playerId
                    ? 'bg-red-50 border border-red-200'
                    : 'bg-slate-50'
                }`}
              >
                <div className="flex items-center gap-3">
                  <div className={`w-10 h-10 rounded-full flex items-center justify-center text-white font-bold ${
                    result.playerId === playerId ? 'bg-red-400' : 'bg-slate-400'
                  }`}>
                    {result.playerName.charAt(0).toUpperCase()}
                  </div>
                  <span className={`font-medium ${
                    result.playerId === playerId ? 'text-red-700' : 'text-slate-700'
                  }`}>
                    {result.playerName}
                    {result.isDisconnected && (
                      <span className="ml-2 text-xs bg-slate-200 text-slate-600 px-2 py-0.5 rounded-full">
                        Deconnecte
                      </span>
                    )}
                    {result.playerId === playerId && (
                      <span className="ml-2 text-xs bg-red-200 text-red-700 px-2 py-0.5 rounded-full">
                        Vous
                      </span>
                    )}
                  </span>
                </div>
                <div className="text-sm text-slate-500">
                  {result.attempts} essai{result.attempts > 1 ? 's' : ''} • {formatDuration(result.durationSeconds)}
                </div>
              </li>
            ))}
          </ul>
        </motion.div>
      )}

      {/* Actions */}
      {isGameAdmin && (
        <motion.div
          initial={{ y: 20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ delay: 0.3 }}
        >
          <button
            onClick={handleEndGame}
            disabled={isEndingGame}
            className="flex items-center justify-center gap-2 px-8 py-3 bg-red-500 text-white rounded-full font-bold shadow-lg shadow-red-500/30 hover:bg-red-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <LogOut className="w-5 h-5" />
            {isEndingGame ? 'Fermeture...' : 'Terminer la partie'}
          </button>
        </motion.div>
      )}
    </div>
  );
};
