import type { RetryContext } from '@microsoft/signalr'
import { CONSTANTS } from './constants'

/**
 * Politique de reconnexion custom pour withAutomaticReconnect.
 * Renvoie le délai (ms) avant le prochain essai, ou null pour abandonner.
 */
export function nextReconnectDelay(
  ctx: Pick<RetryContext, 'previousRetryCount' | 'elapsedMilliseconds'>,
): number | null {
  const { delaysMs, maxElapsedMs } = CONSTANTS.RECONNECT
  if (ctx.elapsedMilliseconds >= maxElapsedMs) {
    return null
  }
  const index = Math.min(ctx.previousRetryCount, delaysMs.length - 1)
  return delaysMs[index]
}
