import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// Nécessite une partie en phase Écriture (admin géré manuellement / fixture).
// Le test s'adapte à l'orientation du projet Playwright courant :
//  - mobile-portrait  → le filet « Tournez votre appareil » doit être visible.
//  - mobile-landscape → filet masqué + champs d'indice dé-pivotés (lisibles).
test("Écriture : filet en portrait, indices non pivotés en paysage", async ({ page }) => {
  await joinGame(page, GAME, 'E2EWrite')

  const vp = page.viewportSize()!
  const isPortrait = vp.height > vp.width

  if (isPortrait) {
    await expect(page.getByText('Tournez votre appareil')).toBeVisible()
    return
  }

  // Paysage tactile : le filet est masqué, la saisie est active.
  await expect(page.getByText('Tournez votre appareil')).toBeHidden()

  const wrapper = page.locator('[data-clue-wrapper]').first()
  await expect(wrapper).toBeVisible()

  // Sous pointeur grossier, getCluePlacement neutralise la rotation : la transform
  // propre de l'enveloppe est translate(-50%,-50%) rotate(0deg) → matrix(1, 0, 0, 1, …).
  // Une rotation 90/180/-90 donnerait matrix(0,1,…)/matrix(-1,0,…).
  const transform = await wrapper.evaluate((el) => getComputedStyle(el).transform)
  expect(transform).toMatch(/^matrix\(1, 0, 0, 1,/)
})
