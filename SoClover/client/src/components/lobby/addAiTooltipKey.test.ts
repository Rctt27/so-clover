import { describe, it, expect } from 'vitest'
import { addAiTooltipKey } from './addAiTooltipKey'

describe('addAiTooltipKey', () => {
  it('shows no tooltip when AI players are available', () => {
    expect(addAiTooltipKey(true, null)).toBeUndefined()
  })

  it('explains that the LLM API key is unavailable', () => {
    expect(addAiTooltipKey(false, 'apiKeyUnavailable')).toBe('players.addAiApiUnavailable')
  })

  it('explains that the feature is disabled server-side', () => {
    expect(addAiTooltipKey(false, 'disabled')).toBe('players.addAiDisabled')
  })

  it('falls back to the disabled message when the server gives no reason', () => {
    // Serveur antérieur à la sonde de clé API : seule l'ancienne cause existait.
    expect(addAiTooltipKey(false, null)).toBe('players.addAiDisabled')
  })

  it('shows no tooltip while the config is still loading', () => {
    // aiPlayersEnabled vaut null tant que /api/config n'a pas répondu — le bouton est
    // déjà désactivé, mais annoncer une cause qu'on ignore serait mensonger.
    expect(addAiTooltipKey(null, null)).toBeUndefined()
  })
})
