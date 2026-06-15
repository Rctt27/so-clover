import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// Nécessite une partie en phase Écriture (admin géré manuellement / fixture).
// Le test s'adapte à l'orientation du projet Playwright courant :
//  - mobile-portrait  → le filet « Tournez votre appareil » doit être visible.
//  - mobile-landscape → filet masqué + indices pivotés (corrélés au sens du plateau).
test("Écriture : filet en portrait, indices corrélés au board en paysage", async ({ page }) => {
  await joinGame(page, GAME, 'E2EWrite')

  const vp = page.viewportSize()!
  const isPortrait = vp.height > vp.width

  if (isPortrait) {
    await expect(page.getByText('Tournez votre appareil')).toBeVisible()
    return
  }

  // Paysage tactile : le filet est masqué, la saisie est active.
  await expect(page.getByText('Tournez votre appareil')).toBeHidden()

  // L'indice « right » suit le sens du plateau (pivoté 90°) comme en desktop : sa
  // transform propre est translate(-50%,-50%) rotate(90deg) → matrix(0, 1, -1, 0, …).
  // Un champ dé-pivoté donnerait matrix(1, 0, 0, 1, …).
  const rightWrapper = page.locator('[data-clue-wrapper][data-clue-position="right"]')
  await expect(rightWrapper).toBeVisible()
  const transform = await rightWrapper.evaluate((el) => getComputedStyle(el).transform)
  expect(transform).toMatch(/^matrix\(0, 1, -1, 0,/)
})
