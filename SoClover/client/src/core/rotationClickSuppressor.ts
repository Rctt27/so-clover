/**
 * Garde anti-« click fantôme » des coins de rotation.
 *
 * Un clic sur un coin de rotation déclenche `handleRotationStart` (pointerdown), mais le
 * navigateur émet aussi un `click` natif au pointerup. Ce `click` remonte jusqu'au `onClick`
 * de la carte → `handleSlotClick` (mode clic-clic), qui construit une sélection silencieuse
 * puis finit par déclencher un swap involontaire (`handleDragEnd`). Cf. cause racine confirmée
 * par trace : rotation A → sélection A, rotation B → swap A↔B.
 *
 * Ce garde permet à la zone de rotation d'« armer » une suppression : le `click` synthétique
 * qui suit immédiatement est avalé une seule fois. Fenêtre temporelle pour qu'un armement
 * orphelin (rotation sans click, ex. gros mouvement) n'avale pas un vrai clic ultérieur.
 */
export function createRotationClickSuppressor(windowMs = 300) {
  let armedAt: number | null = null

  return {
    /** Appelé au démarrage d'une rotation (pointerdown sur un coin). */
    arm(now: number = Date.now()): void {
      armedAt = now
    },
    /**
     * Appelé par le `onClick` de la carte. Retourne true (et désarme) si ce click doit être
     * supprimé parce qu'il provient d'une rotation récente. Single-shot + borné dans le temps.
     */
    consume(now: number = Date.now()): boolean {
      if (armedAt !== null && now - armedAt <= windowMs) {
        armedAt = null
        return true
      }
      return false
    },
  }
}
