import { Page, expect } from '@playwright/test'

/** Rejoint une partie existante depuis l'écran d'accueil via l'URL /g/<code>. */
export async function joinGame(page: Page, gameCode: string, playerName: string) {
  await page.goto(`/g/${gameCode}/`)
  await page.getByRole('textbox', { name: 'Votre Nom' }).fill(playerName)
  const joinBtn = page.getByRole('button', { name: 'Rejoindre la Partie' })
  await expect(joinBtn).toBeEnabled()
  await joinBtn.click()
}

/** Vrai si le contexte courant est tactile/coarse (prouve l'émulation device). */
export async function isCoarsePointer(page: Page): Promise<boolean> {
  return page.evaluate(() => window.matchMedia('(pointer: coarse)').matches)
}
