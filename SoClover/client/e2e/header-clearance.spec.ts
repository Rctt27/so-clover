import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// Le chip de connexion ne doit jamais recouvrir le titre de phase.
test('chip de connexion et titre Écriture ne se chevauchent pas', async ({ page }) => {
  await joinGame(page, GAME, 'E2EHeader')
  // L'admin doit avoir lancé la phase d'écriture pour que ce titre existe.
  const title = page.getByRole('heading', { name: "Phase d'Écriture" })
  await expect(title).toBeVisible()
  const chip = page.locator('[data-testid="connection-chip"]')
  const t = await title.boundingBox()
  const c = await chip.boundingBox()
  expect(t).not.toBeNull()
  expect(c).not.toBeNull()
  // Pas d'intersection des rectangles.
  const overlap =
    t!.x < c!.x + c!.width && t!.x + t!.width > c!.x &&
    t!.y < c!.y + c!.height && t!.y + t!.height > c!.y
  expect(overlap).toBe(false)
})
