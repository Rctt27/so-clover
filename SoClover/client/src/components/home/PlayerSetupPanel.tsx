import React from 'react';
import { useGameStore } from '../../core/store';

export const PlayerSetupPanel: React.FC = () => {
  const { playerName, setPlayerName } = useGameStore();
  const MAX_LENGTH = 16;

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value.slice(0, MAX_LENGTH);
    setPlayerName(value);
  };

  return (
    <section className="bg-white rounded-xl shadow-md p-6 mb-6 w-full max-w-md">
      <h2 className="text-xl font-bold mb-4 text-gray-800">Configuration du Joueur</h2>
      <div className="flex flex-col gap-2">
        <label htmlFor="playerName" className="text-sm font-medium text-gray-700">
          Votre Nom
        </label>
        <div className="relative">
          <input
            type="text"
            id="playerName"
            value={playerName || ''}
            onChange={handleChange}
            placeholder="Entrez votre pseudo"
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-clover focus:border-transparent outline-none transition-all"
            autoComplete="off"
            autoCapitalize="words"
            autoCorrect="off"
            spellCheck={false}
            inputMode="text"
            enterKeyHint="done"
            onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); }}
          />
          <span className="absolute right-3 top-2.5 text-xs text-gray-400">
            {playerName?.length || 0}/{MAX_LENGTH}
          </span>
        </div>
        {playerName && (
          <p className="mt-2 text-sm text-gray-600">
            👤 Joue en tant que : <strong className="text-clover-dark">{playerName}</strong>
          </p>
        )}
      </div>
    </section>
  );
};
