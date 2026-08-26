import { AiPlayersUnavailableReason } from '../../api/game-api'

/**
 * Union littérale et non `string` : les clés de `t()` sont typées à partir des fichiers
 * de locale, un `string` large serait rejeté à la compilation.
 */
export type AddAiTooltipKey = 'players.addAiDisabled' | 'players.addAiApiUnavailable'

/**
 * Clé i18n du message de survol du bouton « ajouter un joueur IA », ou `undefined`
 * quand il n'y a rien à expliquer.
 *
 * Extrait du composant pour rester testable : la suite vitest tourne en environnement
 * `node`, sans DOM, donc la logique conditionnelle ne doit pas vivre dans le JSX.
 */
export const addAiTooltipKey = (
  aiPlayersEnabled: boolean | null,
  reason: AiPlayersUnavailableReason | null,
): AddAiTooltipKey | undefined => {
  // Config pas encore chargée : le bouton est désactivé, mais annoncer une cause
  // qu'on ignore serait mensonger.
  if (aiPlayersEnabled === null) return undefined
  if (aiPlayersEnabled) return undefined

  if (reason === 'apiKeyUnavailable') return 'players.addAiApiUnavailable'

  // 'disabled', ou absence de cause si le serveur est antérieur à la sonde de clé API.
  return 'players.addAiDisabled'
}
