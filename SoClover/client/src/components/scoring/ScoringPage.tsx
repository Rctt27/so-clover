import React from 'react';
import { motion } from 'framer-motion';
import { useGameStore } from '../../core/store';
import { Trophy, Users } from 'lucide-react';

export const ScoringPage: React.FC = () => {
  const players = useGameStore(s => s.players);

  return (
    <div className="flex flex-col items-center gap-8 w-full max-w-4xl mx-auto p-6">
      <motion.div 
        initial={{ scale: 0.9, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        className="text-center"
      >
        <Trophy className="w-20 h-20 text-yellow-500 mx-auto mb-4" />
        <h1 className="text-4xl font-black text-clover-dark mb-2">Partie Terminée !</h1>
        <p className="text-slate-500">Félicitations à tous les joueurs.</p>
      </motion.div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 w-full">
        <div className="bg-white rounded-3xl p-8 shadow-xl border border-slate-100">
          <div className="flex items-center gap-3 mb-6">
            <Users className="text-clover w-6 h-6" />
            <h2 className="text-xl font-bold text-slate-800">Participants</h2>
          </div>
          <ul className="space-y-3">
            {players.map(player => (
              <li key={player.playerId} className="flex items-center gap-3 p-3 bg-slate-50 rounded-xl">
                <div className="w-10 h-10 rounded-full bg-clover/10 flex items-center justify-center text-clover font-bold">
                  {player.name.charAt(0).toUpperCase()}
                </div>
                <span className="font-medium text-slate-700">{player.name}</span>
              </li>
            ))}
          </ul>
        </div>

        <div className="bg-white rounded-3xl p-8 shadow-xl border border-slate-100 flex flex-col justify-center items-center">
            <p className="text-slate-500 mb-4">Le détail des scores arrive bientôt !</p>
            <button 
              onClick={() => window.location.reload()}
              className="px-6 py-3 bg-clover text-white rounded-full font-bold shadow-lg shadow-clover/30 hover:bg-clover-dark transition-colors"
            >
              Retour à l'accueil
            </button>
        </div>
      </div>
    </div>
  );
};
