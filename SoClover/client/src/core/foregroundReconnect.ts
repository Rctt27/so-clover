import { HubConnectionState } from '@microsoft/signalr'

/**
 * Au retour au premier plan : ne forcer un start()+recover que si la connexion
 * est totalement fermée. Si elle est déjà en Reconnecting, SignalR gère seul et
 * onreconnected déclenchera le recover.
 */
export function shouldReconnectOnForeground(
  isVisible: boolean,
  state: HubConnectionState,
): boolean {
  return isVisible && state === HubConnectionState.Disconnected
}
