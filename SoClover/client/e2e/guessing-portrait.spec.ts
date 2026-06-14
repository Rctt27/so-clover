import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// Nécessite que la partie soit en phase Déduction (admin géré manuellement / fixture).
// Le filet `LandscapePrompt` est gaté CSS `(orientation: portrait) and (pointer: coarse)` :
// visible en portrait tactile, masqué en paysage. Le test s'adapte au projet courant.
test.describe('Déduction — filet paysage', () => {
  test('LandscapePrompt visible en portrait tactile, masqué en paysage', async ({ page }) => {
    await joinGame(page, GAME, 'E2EGuess')

    const vp = page.viewportSize()!
    const prompt = page.getByText('Tournez votre appareil')

    if (vp.height > vp.width) {
      await expect(prompt).toBeVisible()
    } else {
      await expect(prompt).toBeHidden()
    }
  })
})
