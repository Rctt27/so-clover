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
      // browserName chromium : on n'installe que Chromium (test:e2e:install). L'émulation
      // iPhone 13 (viewport 390×844 + hasTouch + isMobile → pointer:coarse) reste valide.
      use: { ...devices['iPhone 13'], browserName: 'chromium' }, // 390×844, hasTouch, isMobile
    },
    {
      name: 'mobile-landscape',
      use: { ...devices['iPhone 13 landscape'], browserName: 'chromium' }, // 844×390
    },
  ],
})
