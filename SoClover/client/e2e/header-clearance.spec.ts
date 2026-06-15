import { test, expect } from '@playwright/test'
import { joinGame } from './helpers/game'

const GAME = process.env.E2E_GAME_CODE ?? 'gasoline-plastic-mouse-hairdresser'

// Sur mobile (tactile), le header de phase est masqué (`hide-on-coarse`) pour rendre la
// hauteur au plateau → plus aucun titre susceptible d'être recouvert par le chip. On
// vérifie que le titre d'Écriture est bien absent de la mise en page mobile.
test("le titre d'Écriture est masqué sur mobile (header retiré)", async ({ page }) => {
  await joinGame(page, GAME, 'E2EHeader')
  await expect(page.getByRole('heading', { name: "Phase d'Écriture" })).toBeHidden()
  // Le chip de connexion, lui, reste présent.
  await expect(page.locator('[data-testid="connection-chip"]')).toBeVisible()
})
