import React from 'react';
import { useGameStore } from '../../core/store';

export const PlayerList: React.FC = () => {
  const { players, playerId } = useGameStore();

  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div className="bg-slate-50 px-4 py-3 border-b border-slate-200 flex justify-between items-center">
        <h3 className="font-semibold text-slate-700">Joueurs</h3>
        <span className="bg-slate-200 text-slate-600 text-xs font-bold px-2 py-1 rounded-full">
          {players.length}
        </span>
      </div>
      <div className="divide-y divide-slate-100">
        {players.map((player, index) => {
          const isMe = player.playerId === playerId;
          const isCreator = index === 0; // Le premier joueur dans la liste est le créateur par convention backend

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
              {isCreator && (
                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-amber-100 text-amber-800">
                  Créateur
                </span>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
