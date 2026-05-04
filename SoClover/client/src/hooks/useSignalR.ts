import { useEffect, useCallback, useRef } from 'react';
import { signalRClient } from '../api/signalr-client';
import { useGameStore, useGuessingStore } from '../core/store';
import { HubConnectionState } from '@microsoft/signalr';
import { gameApi } from '../api/game-api';
import { useNotifications } from './useNotifications';
import { useGameStateUpdate } from './useGameStateUpdate';
import { debugLog } from '../core/debug'
import { detectRotationGap } from '../core/rotationGapDetector';

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

  const setCumulativeBoardRotation = useGuessingStore(s => s.setCumulativeBoardRotation);
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
    } catch (err) {
      console.error('[useSignalR] Failed to refresh game state', err);
    } finally {
      setIsInitializing(false);
    }
  }, [gameId, playerId, updateStateFromResponse, setIsInitializing]);

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

    const handleStateUpdated = (data: any) => {
      debugLog('useSignalR', `GameStateUpdated reçu — phase="${data?.phase}", eventType="${data?.eventData?.eventType ?? data?.eventType ?? '?'}", hasGameState=${!!data?.gameState}`);

      // Si le message contient le state complet, on l'utilise directement pour éviter un fetch
      if (data && data.gameState) {
        debugLog('useSignalR', `→ utilise gameState embarqué (phase="${data.gameState.phase}")`);
        updateStateFromResponse(data.gameState);
        return;
      }

      // Fallback: On rafraîchit l'état via API si nécessaire
      const guessingEvents = [
        'CardRotated',
        'GuessingCardPlaced',
        'GuessingCardsSwapped',
        'GuessingCardReturned',
        'GuessingCardRotated',
        'OutsidePoolCardsSwapped'
      ];

      if (data && (guessingEvents.includes(data.eventType) || guessingEvents.includes(data.eventData?.eventType))) {
        debugLog('useSignalR', '→ guessing event, refreshGameState()');
        refreshGameState();
      } else {
        debugLog('useSignalR', '→ fallback refreshGameState()');
        refreshGameState();
      }
    };

    const handleServerNotification = (data: any) => {
      if (!data) return;

      const { type, message, senderId } = data;

      // Filter out notifications sent by the current player (e.g., Board submitted)
      if (senderId && senderId === playerId) {
        debugLog('useSignalR', 'Skipping notification for current player');
        return;
      }

      if (type === 'warning') {
        notifyWarning(message);
      } else {
        notifyInfo(message);
      }
    };

    const handlePlayerJoined = (data: any) => {
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

    const handlePlayerKicked = (data: any) => {
      if (data?.kickedPlayerId === playerId) {
        notifyWarning("Vous avez ete retire de la partie par l'admin.");
        resetAuth();
      }
    };

    const handleBoardRotationUpdated = (data: any) => {
      if (data && typeof data.cumulativeRotation === 'number') {
        // Ignore own rotation events to prevent overwriting local state
        if (data.playerId === playerId) {
          return;
        }

        // Check if we recently made a local rotation (within 500ms)
        const lastLocalRotation = useGuessingStore.getState().lastLocalRotationTimestamp;
        const timeSinceLocalRotation = Date.now() - lastLocalRotation;
        if (timeSinceLocalRotation < 500) {
          return;
        }

        const prev = useGuessingStore.getState().cumulativeBoardRotation;
        detectRotationGap({ source: 'BoardRotationUpdated', from: prev, to: data.cumulativeRotation });

        setCumulativeBoardRotation(data.cumulativeRotation);
      }
    };

    const handleGuessingBoardValidated = (_data: any) => {
      // Handled by GameStateUpdated
    };

    signalRClient.on('GameStateUpdated', handleStateUpdated);
    signalRClient.on('ServerNotification', handleServerNotification);
    signalRClient.on('PlayerJoined', handlePlayerJoined);
    signalRClient.on('GameDeleted', handleGameDeleted);
    signalRClient.on('PlayerKicked', handlePlayerKicked);
    signalRClient.on('BoardRotationUpdated', handleBoardRotationUpdated);
    signalRClient.on('GuessingBoardValidated', handleGuessingBoardValidated);

    const connection = signalRClient.getConnection();

    const onReconnecting = () => setConnectionStatus('Reconnecting');
    const onReconnected = () => setConnectionStatus('Connected');
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
    };
  // Note: 'phase' intentionnellement absent des deps — géré via phaseRef.
  // Ajouter phase ici recyclerait l'effect à chaque transition → refreshGameState()
  // pendant l'exit animation → re-renders AnimatePresence → safeToRemove périmé → page blanche.
  }, [gameId, playerId, setConnectionStatus, setIsInitializing, notifyInfo, notifyWarning, refreshGameState, resetAuth]);
};
