import React, { useEffect, useState, useRef } from 'react';
import { useGameStore } from '../../core/store';
import { gameApi } from '../../api/game-api';
import {
  LOBBY_SEMANTIC_TOGGLE_LABEL,
  LOBBY_SEMANTIC_TOGGLE_TOOLTIP_DISABLED,
  LOBBY_GUESS_AI_BOARD_ONLY_LABEL,
  LOBBY_GUESS_AI_BOARD_ONLY_TOOLTIP_DISABLED,
} from '../../core/clueValidationMessages';
import { supportsSemanticCheck } from '../../core/clueValidation';

export const GameSettings: React.FC = () => {
  const { gameId, playerId, isGameAdmin, settings, setSettings, players } = useGameStore();
  const [dictionaries, setDictionaries] = useState<{ key: string, name: string }[]>([]);
  const [loading, setLoading] = useState(false);

  // État local pour les sliders pour une réactivité immédiate de l'UI
  const [localCluesDuration, setLocalCluesDuration] = useState(settings.cluesDurationSeconds);
  const [localGuessDuration, setLocalGuessDuration] = useState(settings.guessDurationSeconds);
  
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const semanticSupported = supportsSemanticCheck(settings.language)
  const semanticEnabled = settings.semanticClueCheckEnabled
  const hasAIPlayer = players.some(p => p.isAI)
  const guessAiBoardOnlyEnabled = settings.guessAiBoardOnly

  useEffect(() => {
    setLocalCluesDuration(settings.cluesDurationSeconds);
  }, [settings.cluesDurationSeconds]);

  useEffect(() => {
    setLocalGuessDuration(settings.guessDurationSeconds);
  }, [settings.guessDurationSeconds]);

  useEffect(() => {
    const loadDictionaries = async () => {
      try {
        const data = await gameApi.getDictionaries();
        setDictionaries(data);
      } catch (err) {
        console.error('Failed to load dictionaries', err);
      }
    };
    loadDictionaries();
  }, []);

  const updateSettings = async (newSettings: {
    language: string,
    cluesDuration: number,
    guessDuration: number,
    semanticClueCheckEnabled?: boolean,
    guessAiBoardOnly?: boolean,
  }) => {
    if (!isGameAdmin || !gameId || !playerId) return;
    setLoading(true);
    try {
      const updated = await gameApi.updateSettings(gameId, playerId, newSettings);
      setSettings({
        language: updated.language,
        cluesDurationSeconds: updated.cluesDuration,
        guessDurationSeconds: updated.guessDuration,
        semanticClueCheckEnabled: updated.semanticClueCheckEnabled,
        guessAiBoardOnly: updated.guessAiBoardOnly,
      });
    } catch (err) {
      console.error('Failed to update settings', err);
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) => {
    if (!isGameAdmin) return;

    const { name, value } = e.target;
    
    if (name === 'language') {
      const newSettings = {
        language: value,
        cluesDuration: localCluesDuration,
        guessDuration: localGuessDuration,
        semanticClueCheckEnabled: supportsSemanticCheck(value) ? settings.semanticClueCheckEnabled : false,
      }
      updateSettings(newSettings)
      setSettings({ ...settings, language: value, semanticClueCheckEnabled: newSettings.semanticClueCheckEnabled })
      return
    } else {
      const val = parseInt(value);
      if (name === 'cluesDurationSeconds') {
        setLocalCluesDuration(val);
      } else if (name === 'guessDurationSeconds') {
        setLocalGuessDuration(val);
      }

      // Debounce logic
      if (debounceTimer.current) {
        clearTimeout(debounceTimer.current);
      }
      debounceTimer.current = setTimeout(() => {
        const newSettings = {
          language: settings.language,
          cluesDuration: name === 'cluesDurationSeconds' ? val : localCluesDuration,
          guessDuration: name === 'guessDurationSeconds' ? val : localGuessDuration,
          semanticClueCheckEnabled: settings.semanticClueCheckEnabled,
        };
        updateSettings(newSettings);
      }, 500);
    }
  };

  const handleToggleSemantic = async (checked: boolean) => {
    if (!isGameAdmin || !gameId || !playerId) return
    await updateSettings({
      language: settings.language,
      cluesDuration: localCluesDuration,
      guessDuration: localGuessDuration,
      semanticClueCheckEnabled: checked,
    })
  }

  const handleToggleGuessAiBoardOnly = async (checked: boolean) => {
    if (!isGameAdmin || !gameId || !playerId) return
    await updateSettings({
      language: settings.language,
      cluesDuration: localCluesDuration,
      guessDuration: localGuessDuration,
      semanticClueCheckEnabled: settings.semanticClueCheckEnabled,
      guessAiBoardOnly: checked,
    })
  }

  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6 space-y-6">
      <h3 className="font-semibold text-slate-700 border-b border-slate-100 pb-3">Configuration de la partie</h3>
      
      <div className="space-y-4">
        <div>
          <label htmlFor="language" className="block text-sm font-medium text-slate-600 mb-1">Dictionnaire (Langue)</label>
          <select
            id="language"
            name="language"
            value={settings.language}
            onChange={handleChange}
            disabled={!isGameAdmin || loading}
            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-50"
          >
            {dictionaries.map(d => (
              <option key={d.key} value={d.key}>{d.name}</option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="cluesDurationSeconds" className="block text-sm font-medium text-slate-600 mb-1">
            Temps de rédaction (sec) : {localCluesDuration}s
          </label>
          <input
            type="range"
            id="cluesDurationSeconds"
            name="cluesDurationSeconds"
            min="60"
            max="600"
            step="30"
            value={localCluesDuration}
            onChange={handleChange}
            disabled={!isGameAdmin || loading}
            className="w-full h-2 bg-slate-200 rounded-lg appearance-none cursor-pointer accent-emerald-500 disabled:opacity-50"
          />
        </div>

        <div>
          <label htmlFor="guessDurationSeconds" className="block text-sm font-medium text-slate-600 mb-1">
            Temps de déduction (sec) : {localGuessDuration}s
          </label>
          <input
            type="range"
            id="guessDurationSeconds"
            name="guessDurationSeconds"
            min="60"
            max="600"
            step="30"
            value={localGuessDuration}
            onChange={handleChange}
            disabled={!isGameAdmin || loading}
            className="w-full h-2 bg-slate-200 rounded-lg appearance-none cursor-pointer accent-emerald-500 disabled:opacity-50"
          />
        </div>

        <div>
          <label
            className="flex items-center gap-2 text-sm font-medium text-slate-600"
            title={!semanticSupported ? LOBBY_SEMANTIC_TOGGLE_TOOLTIP_DISABLED : undefined}
          >
            <input
              type="checkbox"
              checked={semanticEnabled && semanticSupported}
              disabled={!isGameAdmin || loading || !semanticSupported}
              onChange={(e) => handleToggleSemantic(e.target.checked)}
              className="accent-emerald-500 disabled:opacity-50"
            />
            <span className={!semanticSupported ? 'text-slate-400' : undefined}>
              {LOBBY_SEMANTIC_TOGGLE_LABEL}
            </span>
          </label>
          {!semanticSupported && (
            <p className="text-xs text-slate-400 italic mt-1">
              {LOBBY_SEMANTIC_TOGGLE_TOOLTIP_DISABLED}
            </p>
          )}
        </div>

        <div>
          <label
            className="flex items-center gap-2 text-sm font-medium text-slate-600"
            title={!hasAIPlayer ? LOBBY_GUESS_AI_BOARD_ONLY_TOOLTIP_DISABLED : undefined}
          >
            <input
              type="checkbox"
              checked={guessAiBoardOnlyEnabled && hasAIPlayer}
              disabled={!isGameAdmin || loading || !hasAIPlayer}
              onChange={(e) => handleToggleGuessAiBoardOnly(e.target.checked)}
              className="accent-emerald-500 disabled:opacity-50"
            />
            <span className={!hasAIPlayer ? 'text-slate-400' : undefined}>
              {LOBBY_GUESS_AI_BOARD_ONLY_LABEL}
            </span>
          </label>
          {!hasAIPlayer && (
            <p className="text-xs text-slate-400 italic mt-1">
              {LOBBY_GUESS_AI_BOARD_ONLY_TOOLTIP_DISABLED}
            </p>
          )}
        </div>
      </div>

      {!isGameAdmin && (
        <p className="text-xs text-slate-400 italic">
          Seul l'administrateur peut modifier ces paramètres.
        </p>
      )}
    </div>
  );
};
