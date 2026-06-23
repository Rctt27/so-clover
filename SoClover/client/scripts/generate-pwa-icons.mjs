// Génère les icônes PWA (apple-touch-icon + icônes manifest) SANS dépendance supplémentaire,
// via le Chromium déjà fourni par @playwright/test. À relancer manuellement si le visuel change.
// Prérequis : le binaire Chromium de Playwright (cf. `npm run test:e2e:install`).
import { chromium } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { mkdir } from 'node:fs/promises'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const outDir = path.resolve(__dirname, '../public')

const GREEN = '#2dc653' // CONSTANTS.CANVAS_COLORS.cloverGreen — trèfle plein

// Trèfle 4 feuilles (quatrefoil) blanc sur fond vert plein. Le motif tient dans ~70 % du
// rayon → zone de sécurité « maskable » respectée. Fond plein → pas de transparence (iOS
// arrondit l'icône lui-même). Visuel ajustable si besoin (relancer le script ensuite).
const cloverSvg = `
<svg viewBox="0 0 512 512" xmlns="http://www.w3.org/2000/svg">
  <rect width="512" height="512" fill="${GREEN}"/>
  <path d="M250 300 C 262 356, 292 396, 344 424" stroke="#ffffff" stroke-width="22" fill="none" stroke-linecap="round"/>
  <g fill="#ffffff">
    <circle cx="256" cy="172" r="94"/>
    <circle cx="340" cy="256" r="94"/>
    <circle cx="256" cy="340" r="94"/>
    <circle cx="172" cy="256" r="94"/>
  </g>
</svg>`

const targets = [
  { name: 'apple-touch-icon.png', size: 180 },
  { name: 'icon-192.png', size: 192 },
  { name: 'icon-512.png', size: 512 },
]

const html = (size) => `<!doctype html><html><head><style>
  html,body{margin:0;padding:0}
  .icon{width:${size}px;height:${size}px;display:block}
  svg{width:100%;height:100%;display:block}
</style></head><body><div class="icon">${cloverSvg}</div></body></html>`

const browser = await chromium.launch()
try {
  await mkdir(outDir, { recursive: true })
  const page = await browser.newPage()
  for (const { name, size } of targets) {
    await page.setViewportSize({ width: size, height: size })
    await page.setContent(html(size), { waitUntil: 'load' })
    await page.locator('.icon').screenshot({ path: path.join(outDir, name) })
    console.log(`OK ${name} (${size}x${size})`)
  }
} finally {
  await browser.close()
}
