import { useEffect, useCallback, useRef } from 'react';
import { signalRClient } from '../api/signalr-client';
import { useGameStore, useGuessingStore, useBoardStore } from '../core/store';
import { HubConnectionState } from '@microsoft/signalr';
import { gameApi } from '../api/game-api';
import { useNotifications } from './useNotifications';
import { useGameStateUpdate } from './useGameStateUpdate';
import { debugLog } from '../core/debug'
import { detectRotationGap } from '../core/rotationGapDetector';
import { recoverConnection } from '../core/connectionRecovery';
import { shouldReconnectOnForeground } from '../core/foregroundReconnect';
import type {
  GameStateUpdatedEvent,
  ServerNotificationEvent,
  PlayerJoinedEvent,
  PlayerKickedEvent,
  BoardRotationUpdatedEvent,
  AiClueGenerationRequestedEvent,
  AiClueProgressUpdateEvent,
} from '../types/signalrEvents';

export const useSignalR = () => {
  const gameId = useGameStore(s => s.gameId);
  const playerId = useGameStore(s => s.playerId);
  const phase = useGameStore(s => s.phase);
  const setConnectionStatus = useGameStore(s => s.setConnectionStatus);
  const setIsInitializing = useGameStore(s => s.setIsInitializing);
  const resetAuth = useGameStore(s => s.resetAuth);

  // Ref pour éviter que phase soit dans les deps du useEffect principal.
  // Sans cette ref, chaque changement de phase recycle l'effect → refreshGameState()
  // pendant l'exit animation → re-renders d'AnimatePresence → PresenceContext périmé
  // → safeToRemove jamais appelé → page blanche.
  const phaseRef = useRef(phase);
  useEffect(() => { phaseRef.current = phase; }, [phase]);

  const markAiGenerating = useGameStore(s => s.markAiGenerating);
  const setAiClueProgress = useGameStore(s => s.setAiClueProgress);
  const { notifyInfo, notifyWarning } = useNotifications();
  const { updateStateFromResponse } = useGameStateUpdate();

  const refreshGameState = useCallback(async () => {
    if (!gameId) {
      return;
    }
    debugLog('useSignalR', 'refreshGameState — GET /state');
    try {
      const state = await gameApi.getGameState(gameId);

      if (!state) {
        console.error('[useSignalR] Fetched state is null or undefined');
        return;
      }

      updateStateFromResponse(state);
      clearSubmittedAiGenerating();
    } catch (err) {
      console.error('[useSignalR] Failed to refresh game state', err);
    } finally {
      setIsInitializing(false);
    }
  }, [gameId, playerId, updateStateFromResponse, setIsInitializing]);

  const clearSubmittedAiGenerating = useCallback(() => {
    const { aiGeneratingPlayerIds, clearAiGenerating, clearAiClueProgress } = useGameStore.getState();
    if (aiGeneratingPlayerIds.length === 0) return;
    const boards = useBoardStore.getState().otherBoards;
    aiGeneratingPlayerIds.forEach(pid => {
      if (boards[pid]?.isSubmitted) {
        clearAiGenerating(pid);
        clearAiClueProgress(pid);
      }
    });
  }, []);

  const recover = useCallback(() => recoverConnection({
    getAuth: () => {
      const { gameId: gid, playerId: pid } = useGameStore.getState();
      return { gameId: gid, playerId: pid };
    },
    invoke: (method, ...args) => signalRClient.invoke(method, ...args),
    refreshGameState,
    onUnauthorized: () => {
      notifyWarning('Votre session de jeu a expiré pendant votre absence.');
      resetAuth();
    },
    log: (msg) => debugLog('useSignalR', msg),
  }), [refreshGameState, notifyWarning, resetAuth]);

  useEffect(() => {
    debugLog('useSignalR', `useEffect setup (phase="${phase}", gameId="${gameId}")`);

    const connect = async () => {
      setConnectionStatus('Connecting');
      if (gameId) setIsInitializing(true);
      try {
        await signalRClient.start();
        setConnectionStatus('Connected');

        if (gameId && playerId) {
          debugLog('useSignalR', `JoinGame: gameId=${gameId}, playerId=${playerId}`);
          await signalRClient.invoke('JoinGame', gameId, playerId);
          refreshGameState();
        } else {
          debugLog('useSignalR', 'Connected but missing gameId or playerId', { gameId, playerId });
          setIsInitializing(false);
        }
      } catch (err) {
        setConnectionStatus('Disconnected');
        setIsInitializing(false);
        console.error('Failed to connect to SignalR', err);
      }
    };

    const handleStateUpdated = (data: GameStateUpdatedEvent) => {
      debugLog('useSignalR', `GameStateUpdated reçu — phase="${data?.phase}", eventType="${data?.eventData?.eventType ?? data?.eventType ?? '?'}", hasGameState=${!!data?.gameState}`);

      // Si le message contient le state complet, on l'utilise directement pour éviter un fetch
      if (data && data.gameState) {
        debugLog('useSignalR', `→ utilise gameState embarqué (phase="${data.gameState.phase}")`);
        updateStateFromResponse(data.gameState);
        clearSubmittedAiGenerating();
        return;
      }

      // Pas de state embarqué → on resynchronise via API (GET /state).
      debugLog('useSignalR', '→ pas de gameState embarqué, refreshGameState()');
      refreshGameState();
    };

    const handleServerNotification = (data: ServerNotificationEvent) => {
      if (!data) return;

      const { type, message, senderId } = data;

      // Filter out notifications sent by the current player (e.g., Board submitted)
      if (senderId && senderId === playerId) {
        debugLog('useSignalR', 'Skipping notification for current player');
        return;
      }

      if (type === 'warning') {
        notifyWarning(message ?? '');
      } else {
        notifyInfo(message ?? '');
      }
    };

    const handlePlayerJoined = (data: PlayerJoinedEvent) => {
      if (!data || !data.playerName) return;

      // Évite de se notifier soi-même
      if (data.playerId === playerId) return;

      // Notification uniquement durant la phase Lobby (via ref pour ne pas mettre phase dans les deps)
      if (phaseRef.current === 'Lobby') {
        notifyInfo(`<strong>${data.playerName}</strong> a rejoint la partie`);
      }
    };

    const handleGameDeleted = () => {
      debugLog('useSignalR', 'GameDeleted');
      resetAuth();
    };

    const handlePlayerKicked = (data: PlayerKickedEvent) => {
      if (data?.kickedPlayerId === playerId) {
        notifyWarning("Vous avez ete retire de la partie par l'admin.");
        resetAuth();
      }
    };

    const handleBoardRotationUpdated = (data: BoardRotationUpdatedEvent) => {
      if (!data || typeof data.cumulativeRotation !== 'number' || typeof data.revision !== 'number') {
        return;
      }
      // Self-echoes: applyServerRotation rejects duplicates by revision anyway, but skip early for clarity.
      if (data.playerId === playerId) return;

      const prev = useGuessingStore.getState().cumulativeBoardRotation;
      detectRotationGap({ source: 'BoardRotationUpdated', from: prev, to: data.cumulativeRotation });

      useGuessingStore.getState().applyServerRotation(data.cumulativeRotation, data.revision);
    };

    const handleGuessingBoardValidated = (_data: unknown) => {
      // Handled by GameStateUpdated
    };

    const handleAiClueGenerationRequested = (data: AiClueGenerationRequestedEvent) => {
      if (data?.playerId) markAiGenerating(data.playerId);
    };

    const handleAiClueProgressUpdate = (data: AiClueProgressUpdateEvent) => {
      if (!data?.playerId) return;
      const r = data.retriesByDirection ?? {};
      setAiClueProgress(data.playerId, data.cluesSubmitted ?? 0, {
        top: r.top ?? 0,
        right: r.right ?? 0,
        bottom: r.bottom ?? 0,
        left: r.left ?? 0,
      });
    };

    signalRClient.on('GameStateUpdated', handleStateUpdated);
    signalRClient.on('ServerNotification', handleServerNotification);
    signalRClient.on('PlayerJoined', handlePlayerJoined);
    signalRClient.on('GameDeleted', handleGameDeleted);
    signalRClient.on('PlayerKicked', handlePlayerKicked);
    signalRClient.on('BoardRotationUpdated', handleBoardRotationUpdated);
    signalRClient.on('GuessingBoardValidated', handleGuessingBoardValidated);
    signalRClient.on('AiClueGenerationRequested', handleAiClueGenerationRequested);
    signalRClient.on('AiClueProgressUpdate', handleAiClueProgressUpdate);

    const connection = signalRClient.getConnection();

    const onReconnecting = () => setConnectionStatus('Reconnecting');
    const onReconnected = () => { setConnectionStatus('Connected'); recover(); };
    const onClose = () => setConnectionStatus('Disconnected');

    connection.onreconnecting(onReconnecting);
    connection.onreconnected(onReconnected);
    connection.onclose(onClose);

    if (signalRClient.state === HubConnectionState.Disconnected) {
      connect();
    } else if (signalRClient.state === HubConnectionState.Connected && gameId && playerId) {
        debugLog('useSignalR', 'Déjà connecté → JoinGame + refreshGameState');
        signalRClient.invoke('JoinGame', gameId, playerId);
        refreshGameState();
    }

    return () => {
      debugLog('useSignalR', `useEffect cleanup (phase="${phase}")`);
      signalRClient.off('GameStateUpdated', handleStateUpdated);
      signalRClient.off('ServerNotification', handleServerNotification);
      signalRClient.off('PlayerJoined', handlePlayerJoined);
      signalRClient.off('GameDeleted', handleGameDeleted);
      signalRClient.off('PlayerKicked', handlePlayerKicked);
      signalRClient.off('BoardRotationUpdated', handleBoardRotationUpdated);
      signalRClient.off('GuessingBoardValidated', handleGuessingBoardValidated);
      signalRClient.off('AiClueGenerationRequested', handleAiClueGenerationRequested);
      signalRClient.off('AiClueProgressUpdate', handleAiClueProgressUpdate);
    };
  // Note: 'phase' intentionnellement absent des deps — géré via phaseRef.
  // Ajouter phase ici recyclerait l'effect à chaque transition → refreshGameState()
  // pendant l'exit animation → re-renders AnimatePresence → safeToRemove périmé → page blanche.
  }, [gameId, playerId, setConnectionStatus, setIsInitializing, notifyInfo, notifyWarning, refreshGameState, resetAuth, markAiGenerating, setAiClueProgress, clearSubmittedAiGenerating, recover]);

  // Lifecycle navigateur mobile : au retour au 1er plan, si la connexion a été
  // fermée par le gel de l'onglet (iOS/Android), forcer start() + re-JoinGame.
  // pageshow couvre la restauration depuis le bfcache iOS.
  useEffect(() => {
    const onForeground = async () => {
      if (!shouldReconnectOnForeground(document.visibilityState === 'visible', signalRClient.state)) {
        return;
      }
      try {
        debugLog('useSignalR', 'foreground → start() + recover');
        await signalRClient.start();
        await recover();
      } catch (err) {
        console.error('[useSignalR] foreground reconnect failed', err);
      }
    };
    document.addEventListener('visibilitychange', onForeground);
    window.addEventListener('pageshow', onForeground);
    return () => {
      document.removeEventListener('visibilitychange', onForeground);
      window.removeEventListener('pageshow', onForeground);
    };
  }, [recover]);
};
