import { describe, it, expect } from 'vitest'
import { HubConnectionState } from '@microsoft/signalr'
import { shouldReconnectOnForeground } from './foregroundReconnect'

describe('shouldReconnectOnForeground', () => {
  it('reconnecte quand visible ET connexion fermée (Disconnected)', () => {
    expect(shouldReconnectOnForeground(true, HubConnectionState.Disconnected)).toBe(true)
  })

  it('ne fait rien si la page n\'est pas visible', () => {
    expect(shouldReconnectOnForeground(false, HubConnectionState.Disconnected)).toBe(false)
  })

  it('ne fait rien si déjà connecté', () => {
    expect(shouldReconnectOnForeground(true, HubConnectionState.Connected)).toBe(false)
  })

  it('laisse SignalR finir s\'il est déjà en Reconnecting', () => {
    expect(shouldReconnectOnForeground(true, HubConnectionState.Reconnecting)).toBe(false)
  })
})
