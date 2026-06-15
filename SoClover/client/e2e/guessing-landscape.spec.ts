import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// En paysage mobile, le header de phase est masqué (`hide-on-coarse`) pour rendre la
// hauteur au plateau ; le board ne doit pas déborder par le haut du viewport (y >= 0).
// Nécessite une partie en phase Déduction (admin géré manuellement / fixture).
test("le board de déduction ne déborde pas par le haut en paysage", async ({ page }) => {
  await joinGame(page, GAME, 'E2ELand')

  const vp = page.viewportSize()!
  test.skip(vp.height > vp.width, 'Pertinent uniquement en paysage')

  // Header masqué sur mobile tactile → absent de la mise en page.
  await expect(page.locator('[data-testid="guessing-header"]')).toBeHidden()

  const board = page.locator('[data-testid="guessing-board"]')
  const b = await board.boundingBox()
  expect(b).not.toBeNull()
  expect(b!.y).toBeGreaterThanOrEqual(0)
})
