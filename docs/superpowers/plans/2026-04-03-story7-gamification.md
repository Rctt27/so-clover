# Story 7 — Gamification: Sons et Animations de Feedback

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter sons contextuels, confettis de célébration et animations de feedback enrichies pour transformer la GuessingPage en expérience de jeu.

**Architecture:** Créer un module `sounds.ts` centralisé (Howler.js) importé là où des sons doivent être déclenchés. Les sons sont importés comme assets Vite (`.mp3` depuis `src/public/sounds/`). Les animations Framer Motion enrichies sont définie dans `constants.ts` et consommées dans `DraggableCard`. Les confettis (`canvas-confetti`) et les sons de validation sont déclenchés dans `GuessingControls` via `useEffect`.

**Tech Stack:** Howler.js v2, canvas-confetti, Framer Motion (déjà installé), Vite asset imports, Lucide React (déjà installé)

---

### Task 1 : Déclaration TypeScript pour les fichiers .mp3

**Files:**
- Modify: `SoClover/client/src/vite-env.d.ts`

Les sons sont dans `SoClover/client/src/public/sounds/`. Vite peut les bundler comme assets si on les importe, mais TypeScript ne connaît pas le module `*.mp3`. Il faut ajouter la déclaration.

- [ ] **Step 1 : Ajouter la déclaration `*.mp3` dans vite-env.d.ts**

Ajouter à la fin du fichier `SoClover/client/src/vite-env.d.ts` :

```typescript
declare module '*.mp3' {
  const value: string;
  export default value;
}
```

- [ ] **Step 2 : Vérifier que le fichier compile**

```bash
cd SoClover/client && npx tsc --noEmit 2>&1 | head -20
```

Expected : aucune erreur liée à `.mp3`.

- [ ] **Step 3 : Commit**

```bash
cd SoClover/client && git add src/vite-env.d.ts
git commit -m "feat: add .mp3 module declaration for Vite asset imports"
```

---

### Task 2 : Installer howler et canvas-confetti

**Files:**
- Modify: `SoClover/client/package.json`

- [ ] **Step 1 : Installer les dépendances**

```bash
cd SoClover/client && npm install howler canvas-confetti
npm install -D @types/howler @types/canvas-confetti
```

- [ ] **Step 2 : Vérifier que package.json contient les dépendances**

`package.json` doit contenir dans `dependencies` :
```json
"howler": "^2.x.x",
"canvas-confetti": "^1.x.x"
```
Et dans `devDependencies` :
```json
"@types/howler": "^2.x.x",
"@types/canvas-confetti": "^1.x.x"
```

- [ ] **Step 3 : Commit**

```bash
cd SoClover/client && git add package.json package-lock.json
git commit -m "feat: install howler and canvas-confetti for Story 7 gamification"
```

---

### Task 3 : Créer sounds.ts

**Files:**
- Create: `SoClover/client/src/core/sounds.ts`

Module centralisé. Les fichiers audio sont importés comme assets Vite depuis `src/public/sounds/` — Vite les transforme en URL hashées lors du build. La clé `cardPlace` mappe sur `card-place.mp3` (déjà présent dans `src/public/sounds/`).

- [ ] **Step 1 : Créer `SoClover/client/src/core/sounds.ts`**

```typescript
import { Howl } from 'howler';
import cardPlaceUrl from '../public/sounds/card-place.mp3';
import cardSwapUrl from '../public/sounds/card-swap.mp3';
import cardRotateUrl from '../public/sounds/card-rotate.mp3';
import boardRotateUrl from '../public/sounds/board-rotate.mp3';
import correctUrl from '../public/sounds/correct.mp3';
import incorrectUrl from '../public/sounds/incorrect.mp3';
import timerWarningUrl from '../public/sounds/timer-warning.mp3';

const sounds = {
  cardPlace: new Howl({ src: [cardPlaceUrl], volume: 0.5 }),
  cardSwap: new Howl({ src: [cardSwapUrl], volume: 0.5 }),
  cardRotate: new Howl({ src: [cardRotateUrl], volume: 0.5 }),
  boardRotate: new Howl({ src: [boardRotateUrl], volume: 0.5 }),
  boardValidationCorrect: new Howl({ src: [correctUrl], volume: 0.6 }),
  boardValidationIncorrect: new Howl({ src: [incorrectUrl], volume: 0.4 }),
  timerWarning: new Howl({ src: [timerWarningUrl], volume: 0.3 }),
};

export function playSound(name: keyof typeof sounds) {
  if (localStorage.getItem('so-clover-muted') === 'true') return;
  sounds[name].play();
}

export function toggleMute() {
  const current = localStorage.getItem('so-clover-muted') === 'true';
  localStorage.setItem('so-clover-muted', String(!current));
}

export function isMuted(): boolean {
  return localStorage.getItem('so-clover-muted') === 'true';
}
```

- [ ] **Step 2 : Vérifier la compilation**

```bash
cd SoClover/client && npx tsc --noEmit 2>&1 | head -20
```

Expected : aucune erreur dans `sounds.ts`.

- [ ] **Step 3 : Commit**

```bash
cd SoClover/client && git add src/core/sounds.ts
git commit -m "feat: add centralized audio manager (sounds.ts) with howler"
```

---

### Task 4 : Enrichir les animations dans constants.ts

**Files:**
- Modify: `SoClover/client/src/core/constants.ts`

Ajouter les animations `incorrectShake`, `correctPulse` et `cardSnap` dans l'objet `CONSTANTS.THEME_CONFIG.animations`.

- [ ] **Step 1 : Modifier `constants.ts`**

Remplacer l'objet `animations` existant (lignes 25-36) par la version étendue :

```typescript
    animations: {
      board: {
        initial: { opacity: 0, scale: 0.9 },
        animate: { opacity: 1, scale: 1 },
        transition: { duration: 0.8, ease: "easeOut" }
      },
      card: {
        initial: { opacity: 0, scale: 0.8 },
        animate: { opacity: 1, scale: 1 },
        transition: { duration: 0.5, ease: "backOut", delay: 0.8 }
      },
      incorrectShake: {
        animate: {
          x: [0, -10, 10, -10, 10, 0],
          transition: { duration: 0.5 }
        }
      },
      correctPulse: {
        animate: {
          scale: [1, 1.05, 1],
          boxShadow: ['0 0 0 0 rgba(74, 222, 128, 0)', '0 0 0 8px rgba(74, 222, 128, 0.4)', '0 0 0 0 rgba(74, 222, 128, 0)'],
          transition: { duration: 0.8, repeat: 2 }
        }
      },
      cardSnap: {
        animate: {
          scale: [1.05, 0.98, 1],
          transition: { duration: 0.3, ease: 'easeOut' }
        }
      }
    }
```

- [ ] **Step 2 : Vérifier la compilation**

```bash
cd SoClover/client && npx tsc --noEmit 2>&1 | head -20
```

Expected : aucune erreur.

- [ ] **Step 3 : Commit**

```bash
cd SoClover/client && git add src/core/constants.ts
git commit -m "feat: add incorrectShake, correctPulse, cardSnap animations to constants"
```

---

### Task 5 : Sons de drag dans useDragOrchestration.ts

**Files:**
- Modify: `SoClover/client/src/hooks/useDragOrchestration.ts`

Ajouter `playSound('cardSwap')` pour les cas Board→Board et Pool→Pool, et `playSound('cardPlace')` pour Pool→Board.

- [ ] **Step 1 : Ajouter l'import de playSound**

En haut de `SoClover/client/src/hooks/useDragOrchestration.ts`, après les imports existants :

```typescript
import { playSound } from '../core/sounds';
```

- [ ] **Step 2 : Déclencher les sons dans handleDragEnd**

Dans `handleDragEnd`, après chaque `await` d'API réussi, ajouter l'appel son correspondant.

**Case 1 — Board→Board swap** (ligne ~118, après `await swapGuessingCards(...)`):
```typescript
if (fromBoard && toBoard) {
  setDisplacedSlot(targetSlot)
  setSwapAnimationKey((k) => k + 1)
  await swapGuessingCards(sourceSlot, targetSlot)
  playSound('cardSwap')
  scheduleAnimationReset()
  return
}
```

**Case 2 — Pool→Board place** (ligne ~128, après `await placeGuessingCard(...)`):
```typescript
if (cardAtIndex) {
  setDisplacedSlot(targetSlot)
  setSwapAnimationKey((k) => k + 1)
  await placeGuessingCard(poolIndex, targetSlot)
  playSound('cardPlace')
  scheduleAnimationReset()
}
```

**Case 3 — Board→Pool return** (ligne ~152, après `await returnGuessingCard(...)`):
```typescript
if (fromBoard && toPool) {
  setDisplacedSlot(sourceSlot)
  setSwapAnimationKey((k) => k + 1)
  await returnGuessingCard(sourceSlot)
  playSound('cardPlace')
  scheduleAnimationReset()
  return
}
```

**Case 4 — Pool→Pool swap** (ligne ~163, après `await swapOutsidePoolCards(...)`):
```typescript
if (fromPool && toPool) {
  const sIdx = parsePoolIndex(sourceSlot)
  const tIdx = parsePoolIndex(targetSlot)
  if (isNaN(sIdx) || isNaN(tIdx) || sIdx === tIdx || sIdx < 0 || sIdx > 5 || tIdx < 0 || tIdx > 5) return
  await swapOutsidePoolCards(sIdx, tIdx)
  playSound('cardSwap')
}
```

- [ ] **Step 3 : Vérifier la compilation**

```bash
cd SoClover/client && npx tsc --noEmit 2>&1 | head -20
```

Expected : aucune erreur.

- [ ] **Step 4 : Commit**

```bash
cd SoClover/client && git add src/hooks/useDragOrchestration.ts
git commit -m "feat: play cardPlace/cardSwap sounds on drag completion"
```

---

### Task 6 : Son de rotation et animation snap dans DraggableCard.tsx

**Files:**
- Modify: `SoClover/client/src/components/guessing/DraggableCard.tsx`

Ajouter le son `cardRotate` dans `handleMouseUp` et mettre à jour l'animation `isDisplaced` pour utiliser `cardSnap` depuis `constants.ts`.

- [ ] **Step 1 : Ajouter l'import de playSound**

En haut du fichier `DraggableCard.tsx`, après les imports existants :

```typescript
import { playSound } from '../../core/sounds';
```

- [ ] **Step 2 : Jouer le son cardRotate dans handleMouseUp**

Dans le `useEffect` de rotation (ligne ~111), dans `handleMouseUp`, après le bloc `if (gameId && playerId)` :

Remplacer la section `handleMouseUp` complète par :

```typescript
const handleMouseUp = async () => {
  const steps = Math.round(rotationVisualOffset / 90)

  setIsRotating(false)
  setRotationVisualOffset(0)

  if (gameId && playerId) {
    const finalSteps = steps === 0 ? 1 : steps;

    playSound('cardRotate')

    try {
      const SlotPositions = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft']
      const boardPosition = !isOutside ? SlotPositions[index] : undefined
      const outsideIndex = isOutside ? index : undefined

      await gameApi.rotateGuessingCard(gameId, playerId, finalSteps, boardPosition, outsideIndex !== undefined ? outsideIndex : undefined)
    } catch (error) {
      console.error('Failed to rotate card:', error)
    }
  }
}
```

- [ ] **Step 3 : Mettre à jour l'animation cardSnap sur isDisplaced**

Dans le `motion.div` principal (ligne ~184), remplacer la branche `isDisplaced` dans `animate` (scale `[1, 1.1, 1.1, 1]`) par l'effet snap :

```typescript
animate={{
  opacity: 1,
  scale: 1,
  rotate: dragRotationCompensation,
  ...(isCorrect ? {
    scale: [1, 1.1, 1],
  } : {}),
  ...(isDisplaced ? {
    scale: [1.05, 0.98, 1],
  } : {})
}}
```

Et dans `transition`, mettre à jour la branche `isDisplaced` :

```typescript
transition={{
  layout: { duration: 0.3, ease: 'easeOut' },
  opacity: { duration: 0.2 },
  scale: isDisplaced
    ? { duration: 0.3, ease: 'easeOut' }
    : { duration: 0.2 },
  rotate: { duration: 0 },
}}
```

> Note : `CONSTANTS.THEME_CONFIG.animations.cardSnap` est défini en Task 4 comme référence documentaire. Dans DraggableCard, on utilise les valeurs inline directement pour éviter le spread d'un objet `transition` imbriqué dans la prop `animate` de Framer Motion (comportement non standard).

- [ ] **Step 4 : Vérifier la compilation**

```bash
cd SoClover/client && npx tsc --noEmit 2>&1 | head -20
```

Expected : aucune erreur.

- [ ] **Step 5 : Commit**

```bash
cd SoClover/client && git add src/components/guessing/DraggableCard.tsx
git commit -m "feat: play cardRotate sound on card rotation + cardSnap animation on placement"
```

---

### Task 7 : Confettis, sons de validation, son de rotation board, bouton mute (GuessingControls.tsx)

**Files:**
- Modify: `SoClover/client/src/components/guessing/GuessingControls.tsx`

C'est la tâche la plus importante. Elle ajoute :
1. Confettis + son `boardValidationCorrect` quand `isBoardGuessed` passe à `true`
2. Son `boardValidationIncorrect` quand `remainingAttempts` diminue (validation échouée)
3. Son `boardRotate` en wrappant `onRotate`
4. Bouton mute/unmute (Lucide `Volume2` / `VolumeX`) avec état React

- [ ] **Step 1 : Mettre à jour les imports**

Remplacer le début du fichier jusqu'à l'interface par :

```typescript
import React, { useEffect, useRef, useState } from 'react'
import confetti from 'canvas-confetti'
import { Volume2, VolumeX } from 'lucide-react'
import { BoardRotationControls } from '../shared/BoardRotationControls'
import { playSound, toggleMute, isMuted } from '../../core/sounds'
```

- [ ] **Step 2 : Ajouter les effets sons + confettis dans le composant**

Dans `GuessingControls`, avant le `return`, ajouter :

```typescript
  // Mute state (synced with localStorage)
  const [muted, setMuted] = useState(isMuted)

  const handleToggleMute = () => {
    toggleMute()
    setMuted(isMuted())
  }

  // Confettis + son correct quand le board est complètement deviné
  const prevIsBoardGuessedRef = useRef(false)
  useEffect(() => {
    if (isBoardGuessed && !prevIsBoardGuessedRef.current) {
      playSound('boardValidationCorrect')
      confetti({
        particleCount: 100,
        spread: 70,
        origin: { y: 0.6 },
        colors: ['#4ade80', '#22c55e', '#16a34a'],
      })
    }
    prevIsBoardGuessedRef.current = isBoardGuessed
  }, [isBoardGuessed])

  // Son incorrect quand remainingAttempts diminue sans que le board soit deviné
  const prevRemainingAttemptsRef = useRef(remainingAttempts)
  useEffect(() => {
    if (
      remainingAttempts < prevRemainingAttemptsRef.current &&
      !isBoardGuessed
    ) {
      playSound('boardValidationIncorrect')
    }
    prevRemainingAttemptsRef.current = remainingAttempts
  }, [remainingAttempts, isBoardGuessed])

  // Wrapper onRotate pour jouer le son boardRotate
  const handleRotate = (direction: 'left' | 'right') => {
    playSound('boardRotate')
    onRotate(direction)
  }
```

- [ ] **Step 3 : Utiliser handleRotate à la place de onRotate dans le JSX**

Dans le JSX, remplacer `onRotate={onRotate}` par `onRotate={handleRotate}` dans `BoardRotationControls`.

- [ ] **Step 4 : Ajouter le bouton mute dans le JSX**

Dans le `return`, ajouter le bouton mute en bas du composant, après le `div` de texte d'information :

```tsx
      {/* Bouton mute */}
      <button
        onClick={handleToggleMute}
        className="flex items-center gap-2 px-3 py-2 rounded-full bg-white/30 hover:bg-white/50 transition-colors text-gray-700 text-sm"
        title={muted ? 'Activer le son' : 'Couper le son'}
      >
        {muted ? <VolumeX size={18} /> : <Volume2 size={18} />}
        <span>{muted ? 'Son coupé' : 'Son activé'}</span>
      </button>
```

- [ ] **Step 5 : Vérifier la compilation et le lint**

```bash
cd SoClover/client && npx tsc --noEmit 2>&1 | head -30
npm run lint 2>&1 | tail -20
```

Expected : aucune erreur de compilation, aucun warning lint.

- [ ] **Step 6 : Commit**

```bash
cd SoClover/client && git add src/components/guessing/GuessingControls.tsx
git commit -m "feat: add confetti, validation sounds, board rotate sound, mute button in GuessingControls"
```

---

### Task 8 : Vérification finale (lint + build)

**Files:** aucun

- [ ] **Step 1 : Lint complet**

```bash
cd SoClover/client && npm run lint
```

Expected : `0 warnings, 0 errors`.

- [ ] **Step 2 : Build de production**

```bash
cd SoClover/client && npm run build
```

Expected : Build réussi, assets `sounds/*.mp3` présents dans `dist/assets/`.

- [ ] **Step 3 : Vérifier les critères d'acceptation**

Checklist :
- [ ] `howler` et `canvas-confetti` dans `package.json` (dependencies)
- [ ] `sounds.ts` expose `playSound()`, `toggleMute()`, `isMuted()`
- [ ] Sons déclenchés pour : placement carte (`cardPlace`), rotation carte (`cardRotate`), rotation board (`boardRotate`), validation correcte (`boardValidationCorrect`), validation incorrecte (`boardValidationIncorrect`), board complet (même que validation correcte + confettis)
- [ ] Confettis affichés quand `isBoardGuessed` passe à `true`
- [ ] Animation `cardSnap` (scale 1.05 → 0.98 → 1) sur placement de carte
- [ ] Animations `incorrectShake`, `correctPulse`, `cardSnap` définies dans `constants.ts`
- [ ] Bouton mute/unmute accessible dans `GuessingControls`
- [ ] État mute persisté en `localStorage` (clé `so-clover-muted`)
- [ ] `npm run lint` : 0 warnings
- [ ] `npm run build` : succès

- [ ] **Step 4 : Commit final si nécessaire**

```bash
cd SoClover/client && git status
# Si des fichiers sont non committés :
git add -A && git commit -m "chore: story 7 gamification complete"
```
