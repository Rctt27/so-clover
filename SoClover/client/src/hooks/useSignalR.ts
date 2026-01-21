import { useEffect, useCallback } from 'react';
import { signalRClient } from '../api/signalr-client';
import { useGameStore, useGuessingStore } from '../core/store';
import { HubConnectionState } from '@microsoft/signalr';
import { gameApi } from '../api/game-api';
import { useNotifications } from './useNotifications';
import { useGameStateUpdate } from './useGameStateUpdate';

export const useSignalR = () => {
  const gameId = useGameStore(s => s.gameId);
  const playerId = useGameStore(s => s.playerId);
  const phase = useGameStore(s => s.phase);
  const setConnectionStatus = useGameStore(s => s.setConnectionStatus);
  const setIsInitializing = useGameStore(s => s.setIsInitializing);
  const resetAuth = useGameStore(s => s.resetAuth);

  const setCumulativeBoardRotation = useGuessingStore(s => s.setCumulativeBoardRotation);
  const { notifyInfo, notifyWarning } = useNotifications();
  const { updateStateFromResponse } = useGameStateUpdate();

  const refreshGameState = useCallback(async () => {
    if (!gameId) {
      // console.warn('[useSignalR] Cannot refresh state: missing gameId');
      return;
    }
    try {
      // console.log('[useSignalR] Refreshing game state for gameId:', gameId, 'playerId:', playerId);
      const state = await gameApi.getGameState(gameId);
      // console.log('[useSignalR] New state fetched:', state);
      
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
    const connect = async () => {
      setConnectionStatus('Connecting');
      if (gameId) setIsInitializing(true);
      try {
        await signalRClient.start();
        setConnectionStatus('Connected');

        if (gameId && playerId) {
          console.log(`[SignalR] Connected, joining game ${gameId} for player ${playerId}`);
          await signalRClient.invoke('JoinGame', gameId, playerId);
          refreshGameState();
        } else {
          console.log('[SignalR] Connected but missing gameId or playerId', { gameId, playerId });
          setIsInitializing(false);
        }
      } catch (err) {
        setConnectionStatus('Disconnected');
        setIsInitializing(false);
        console.error('Failed to connect to SignalR', err);
      }
    };

    const handleStateUpdated = (data: any) => {
      // console.log('[SignalR] GameStateUpdated received:', data);
      
      // Si le message contient le state complet, on l'utilise directement pour éviter un fetch
      if (data && data.gameState) {
        // console.log('[SignalR] Using embedded gameState from message');
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
        // console.log(`[SignalR] Guessing event detected (${data.eventType}), refreshing state`);
        refreshGameState();
      } else {
        // Pour les autres événements, le throttle ou le refresh global suffit
        refreshGameState();
      }
    };

    const handleServerNotification = (data: any) => {
      // console.log('[SignalR] ServerNotification', data);
      if (!data) return;

      const { type, message, senderId } = data;

      // Filter out notifications sent by the current player (e.g., Board submitted)
      if (senderId && senderId === playerId) {
        console.log('[SignalR] Skipping notification for current player');
        return;
      }

      if (type === 'warning') {
        notifyWarning(message);
      } else {
        notifyInfo(message);
      }
    };

    const handlePlayerJoined = (data: any) => {
      // console.log('[SignalR] PlayerJoined', data);

      if (!data || !data.playerName) return;

      // Évite de se notifier soi-même
      if (data.playerId === playerId) return;

      // Notification uniquement durant la phase Lobby
      if (phase === 'Lobby') {
        notifyInfo(`<strong>${data.playerName}</strong> a rejoint la partie`);
      }
    };

    // Note: GuessingMouseMoved est maintenant géré par RemoteCursorsLayer
    // L'ancien handler a été retiré pour éviter les duplications

    const handleGameDeleted = () => {
      console.log('[SignalR] GameDeleted');
      resetAuth();
    };

    const handleBoardRotationUpdated = (data: any) => {
      // console.log('[SignalR] BoardRotationUpdated received:', data);
      if (data && typeof data.cumulativeRotation === 'number') {
        // Ignore own rotation events to prevent overwriting local state
        // The local player already updated their state optimistically
        if (data.playerId === playerId) {
          // console.log('[SignalR] Ignoring own BoardRotationUpdated event');
          return;
        }

        // Check if we recently made a local rotation (within 500ms)
        // This prevents race conditions where an old update from another player
        // arrives after we've already made a new local rotation
        const lastLocalRotation = useGuessingStore.getState().lastLocalRotationTimestamp;
        const timeSinceLocalRotation = Date.now() - lastLocalRotation;
        if (timeSinceLocalRotation < 500) {
          // console.log('[SignalR] Ignoring BoardRotationUpdated - local rotation in progress');
          return;
        }

        setCumulativeBoardRotation(data.cumulativeRotation);
      }
    };

    const handleGuessingBoardValidated = (_data: any) => {
      // console.log('[SignalR] GuessingBoardValidated received (handled by GameStateUpdated):', _data);
    };

    signalRClient.on('GameStateUpdated', handleStateUpdated);
    signalRClient.on('ServerNotification', handleServerNotification);
    signalRClient.on('PlayerJoined', handlePlayerJoined);
    // GuessingMouseMoved est maintenant géré par RemoteCursorsLayer
    signalRClient.on('GameDeleted', handleGameDeleted);
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
        signalRClient.invoke('JoinGame', gameId, playerId);
        refreshGameState();
    }

    return () => {
      signalRClient.off('GameStateUpdated', handleStateUpdated);
      signalRClient.off('ServerNotification', handleServerNotification);
      signalRClient.off('PlayerJoined', handlePlayerJoined);
      // GuessingMouseMoved cleanup géré par RemoteCursorsLayer
      signalRClient.off('GameDeleted', handleGameDeleted);
      signalRClient.off('BoardRotationUpdated', handleBoardRotationUpdated);
      signalRClient.off('GuessingBoardValidated', handleGuessingBoardValidated);

      // Cleanup des callbacks de cycle de vie (approximatif pour SignalR JS client)
      // Note: SignalR n'a pas de méthode 'off' pour ces événements,
      // mais on peut les réinitialiser à des fonctions vides si nécessaire.
    };
  }, [gameId, playerId, phase, setConnectionStatus, setIsInitializing, notifyInfo, notifyWarning, refreshGameState, resetAuth]);
};
