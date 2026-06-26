import { defineConfig, devices } from '@playwright/test'

/**
 * Harnais e2e mobile. Les projets émulent un VRAI device tactile (isMobile + hasTouch
 * → matchMedia('(pointer: coarse)') === true), ce qui exerce les chemins UX mobiles
 * gated `pointer:coarse` (LandscapePrompt, cibles 44px, tooltips tap…) — impossible à
 * tester depuis un simple resize de fenêtre desktop.
 *
 * BASE_URL : URL d'une instance SoClover déjà lancée (dev server ou conteneur).
 * Défaut local : http://127.0.0.1:8081
 */
const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:8081'

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: { baseURL, trace: 'on-first-retry' },
  projects: [
    {
      name: 'mobile-portrait',
      testIgnore: /desktop-.*\.spec\.ts/,
      use: { ...devices['iPhone 13'], browserName: 'chromium' }, // 390×844, hasTouch, isMobile
    },
    {
      name: 'mobile-landscape',
      testIgnore: /desktop-.*\.spec\.ts/,
      use: { ...devices['iPhone 13 landscape'], browserName: 'chromium' }, // 844×390
    },
    {
      // Laptop / souris : pointer:fine, maxTouchPoints:0, paysage 1280×720.
      // Exerce la disposition UNIFIÉE (ex-mobile) désormais appliquée sur desktop.
      name: 'desktop',
      testMatch: /desktop-.*\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] }, // 1280×720, pointer:fine
    },
  ],
})
