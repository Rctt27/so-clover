import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// Le contexte desktop prouve qu'on n'est PAS en émulation tactile.
test('desktop est bien pointer:fine (souris)', async ({ page }) => {
  await page.goto('/')
  const coarse = await page.evaluate(() => window.matchMedia('(pointer: coarse)').matches)
  expect(coarse).toBe(false)
})

// Disposition unifiée en Déduction sur laptop : en-tête retiré, rotation dans le HUD
// haut-droite, CTA fixe haut-droite, et une SEULE instance de contrôles de rotation.
// Prérequis fixture : partie `GAME` déjà en phase Déduction.
test('Déduction laptop : header retiré, rotation dans le HUD, CTA fixe, instance unique', async ({ page }) => {
  await joinGame(page, GAME, 'E2EDeskGuess')

  const board = page.locator('[data-testid="guessing-board"]')
  test.skip(!(await board.count()), 'Fixture pas en phase Déduction')

  // 1. En-tête de phase retiré.
  await expect(page.locator('[data-testid="guessing-header"]')).toBeHidden()

  // 2. Contrôles de rotation présents DANS le slot HUD haut-droite, et visibles.
  const hudRotation = page.locator('#mobile-board-controls-slot .rotation-controls')
  await expect(hudRotation).toBeVisible()

  // 3. CTA principal ancré en position fixe (haut-droite).
  await expect(page.locator('.mobile-fixed-cta')).toBeVisible()

  // 4. Une seule instance de contrôles de rotation dans tout le document
  //    (l'ancienne instance in-flow desktop a été supprimée).
  await expect(page.locator('.rotation-controls')).toHaveCount(1)
})

// Disposition unifiée en Écriture sur laptop : titre de phase retiré, CTA fixe.
// Prérequis fixture : partie `GAME` déjà en phase Écriture.
test('Écriture laptop : titre retiré, CTA fixe', async ({ page }) => {
  await joinGame(page, GAME, 'E2EDeskWrite')

  const heading = page.getByRole('heading', { name: "Phase d'Écriture" })
  const ctaFixed = page.locator('.mobile-fixed-cta')
  test.skip(!(await ctaFixed.count()) && !(await heading.count()), 'Fixture pas en phase Écriture')

  await expect(heading).toBeHidden()
  await expect(ctaFixed).toBeVisible()
})
