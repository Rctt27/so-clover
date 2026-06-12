import { test, expect } from '@playwright/test'
import { isCoarsePointer } from './helpers/game'

test('le contexte e2e émule bien un device tactile (pointer:coarse)', async ({ page }) => {
  await page.goto('/')
  expect(await isCoarsePointer(page)).toBe(true)
  expect(await page.evaluate(() => navigator.maxTouchPoints)).toBeGreaterThan(0)
})
