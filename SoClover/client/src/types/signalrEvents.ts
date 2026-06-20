import type { GameStateResponse } from './game'

/** `GameStateUpdated` : porte parfois l'état complet embarqué (`gameState`), sinon un type d'event. */
export interface GameStateUpdatedEvent {
  phase?: string
  gameState?: GameStateResponse
  eventType?: string
  eventData?: { eventType?: string }
}

/** `ServerNotification` : message broadcast à afficher (info / warning). */
export interface ServerNotificationEvent {
  type?: 'info' | 'warning' | string
  message?: string
  senderId?: string
}

/** `PlayerJoined` : un joueur rejoint la partie. */
export interface PlayerJoinedEvent {
  playerId?: string
  playerName?: string
}

/** `PlayerKicked` : l'admin a retiré un joueur. */
export interface PlayerKickedEvent {
  kickedPlayerId?: string
}

/** `BoardRotationUpdated` : rotation cumulée d'un plateau de déduction (protocole de révision). */
export interface BoardRotationUpdatedEvent {
  playerId?: string
  cumulativeRotation?: number
  revision?: number
}

/** `AiClueGenerationRequested` : un joueur IA commence à générer ses indices. */
export interface AiClueGenerationRequestedEvent {
  playerId?: string
}

/** `AiClueProgressUpdate` : progression de la génération d'indices d'un joueur IA. */
export interface AiClueProgressUpdateEvent {
  playerId?: string
  cluesSubmitted?: number
  retriesByDirection?: Partial<Record<'top' | 'right' | 'bottom' | 'left', number>>
}
