export interface RecoveryDeps {
  /** Lit l'identité persistée (sessionStorage via le store). */
  getAuth: () => { gameId: string | null; playerId: string | null }
  /** Invoque une méthode du hub SignalR. */
  invoke: (method: string, ...args: unknown[]) => Promise<unknown>
  /** Recharge l'état complet de la partie (GET /state). */
  refreshGameState: () => Promise<void>
  /**
   * Appelé quand le serveur rejette le rejoin parce que le joueur n'est plus
   * dans la partie (grâce expirée → LeaveGame). Permet de purger l'identité
   * périmée côté client (resetAuth) plutôt que de rester sur un écran mort.
   */
  onUnauthorized?: () => void
  /** Log optionnel (debugLog). */
  log?: (message: string) => void
}

/**
 * Rejoue JoinGame après une reconnexion (nouveau ConnectionId côté serveur),
 * puis resynchronise l'état. Annule de fait le timer de grâce serveur.
 * Ne throw jamais : renvoie false en cas d'échec ou d'identité absente.
 */
export async function recoverConnection(deps: RecoveryDeps): Promise<boolean> {
  const { gameId, playerId } = deps.getAuth()
  if (!gameId || !playerId) {
    deps.log?.('recover ignoré — pas d\'identité persistée')
    return false
  }
  try {
    deps.log?.(`recover → JoinGame(${gameId}, ${playerId}) + refreshGameState`)
    await deps.invoke('JoinGame', gameId, playerId)
    await deps.refreshGameState()
    return true
  } catch (err) {
    deps.log?.(`recover échec : ${String(err)}`)
    // Le serveur renvoie "Unauthorized: player not in game" quand la grâce a
    // expiré et que le joueur a été retiré : l'identité persistée est périmée.
    if (String(err).includes('Unauthorized')) {
      deps.log?.('recover → identité périmée (grâce expirée), onUnauthorized')
      deps.onUnauthorized?.()
    }
    return false
  }
}
