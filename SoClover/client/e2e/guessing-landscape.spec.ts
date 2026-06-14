import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// En paysage, le plateau ne doit pas remonter au-dessus de l'en-tête (y >= bas du header).
// Nécessite une partie en phase Déduction (admin géré manuellement / fixture).
test("le board de déduction reste sous l'en-tête en paysage", async ({ page }) => {
  await joinGame(page, GAME, 'E2ELand')

  const vp = page.viewportSize()!
  test.skip(vp.height > vp.width, 'Pertinent uniquement en paysage')

  const header = page.locator('[data-testid="guessing-header"]')
  const board = page.locator('[data-testid="guessing-board"]')
  const h = await header.boundingBox()
  const b = await board.boundingBox()
  expect(h).not.toBeNull()
  expect(b).not.toBeNull()
  expect(b!.y).toBeGreaterThanOrEqual(h!.y + h!.height - 1)
})
