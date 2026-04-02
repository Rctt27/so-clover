# UX Transitions — Story 5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Améliorer l'UX lors des transitions de phase, déconnexion réseau, et attente entre joueurs.

**Architecture:** 2 nouveaux composants (`ConnectionOverlay`, `SubmissionProgress`) + modifications de 3 fichiers existants (`App.tsx`, `Timer.tsx`, `WritingBoard.tsx`). Les données de progression viennent de `useBoardStore` (`myBoard.isSubmitted` + `otherBoards`). Aucune dépendance npm à ajouter — `framer-motion` est déjà présent.

**Tech Stack:** React 18, Framer Motion, Tailwind CSS, Zustand (useGameStore, useBoardStore)

---

## Fichiers impactés

| Fichier | Action |
|---------|--------|
| `SoClover/client/src/components/shared/ConnectionOverlay.tsx` | Créer |
| `SoClover/client/src/components/writing/SubmissionProgress.tsx` | Créer |
| `SoClover/client/src/components/shared/Timer.tsx` | Modifier |
| `SoClover/client/src/components/writing/WritingBoard.tsx` | Modifier |
| `SoClover/client/src/App.tsx` | Modifier (3 sous-tâches : ConnectionOverlay + AnimatePresence phases + lazy loading) |

---

## Task 1 : Créer ConnectionOverlay.tsx

**Fichiers :**
- Créer : `SoClover/client/src/components/shared/ConnectionOverlay.tsx`

**Contexte :**
- `connectionStatus` est de type `'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'` (voir `src/types/game.ts`)
- Overlay affiché UNIQUEMENT pour `'Reconnecting'` et `'Disconnected'`
- Styling : Tailwind CSS uniquement, cohérent avec le reste du projet
- Textes en français hardcodés (l'app n'utilise pas react-i18next en pratique)

- [ ] **Étape 1.1 : Créer le fichier**

```tsx
import { ConnectionStatus } from '../../types/game'

interface ConnectionOverlayProps {
  status: ConnectionStatus
}

export const ConnectionOverlay = ({ status }: ConnectionOverlayProps) => {
  if (status === 'Reconnecting') {
    return (
      <div className="fixed inset-0 bg-black/50 z-[200] flex flex-col items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-white mb-4" />
        <p className="text-white text-lg font-semibold">Connexion perdue. Reconnexion en cours...</p>
      </div>
    )
  }

  if (status === 'Disconnected') {
    return (
      <div className="fixed inset-0 bg-black/70 z-[200] flex flex-col items-center justify-center gap-4">
        <p className="text-white text-xl font-bold">Connexion perdue</p>
        <button
          onClick={() => window.location.reload()}
          className="bg-white text-gray-900 font-semibold px-6 py-2 rounded-full hover:bg-gray-100 transition-colors"
        >
          Rafraîchir la page
        </button>
      </div>
    )
  }

  return null
}
```

- [ ] **Étape 1.2 : Vérifier le lint**

Depuis `SoClover/client/` :
```bash
npm run lint
```
Attendu : aucun warning ni erreur.

- [ ] **Étape 1.3 : Commit**

```bash
git add SoClover/client/src/components/shared/ConnectionOverlay.tsx
git commit -m "feat: add ConnectionOverlay component for reconnecting/disconnected states"
```

---

## Task 2 : Créer SubmissionProgress.tsx

**Fichiers :**
- Créer : `SoClover/client/src/components/writing/SubmissionProgress.tsx`

**Contexte :**
- Les données de soumission ne sont PAS dans `useGameStore().players` (qui ne stocke que `{ playerId, name, cursorColorIndex }`)
- Elles sont dans `useBoardStore` :
  - `myBoard.isSubmitted` — mon propre plateau
  - `otherBoards` — `Record<string, BoardData>` où `BoardData.isSubmitted` indique la soumission de chaque autre joueur
- `players.length` (depuis `useGameStore`) donne le nombre total de joueurs

- [ ] **Étape 2.1 : Créer le fichier**

```tsx
import { useBoardStore, useGameStore } from '../../core/store'

export const SubmissionProgress = () => {
  const myBoard = useBoardStore(s => s.myBoard)
  const otherBoards = useBoardStore(s => s.otherBoards)
  const players = useGameStore(s => s.players)

  const totalCount = players.length
  const otherSubmittedCount = Object.values(otherBoards).filter(b => b.isSubmitted).length
  const mySubmitted = myBoard?.isSubmitted ? 1 : 0
  const submittedCount = mySubmitted + otherSubmittedCount

  return (
    <div className="flex flex-col items-center gap-2">
      <p className="text-sm text-gray-600 font-medium">
        {submittedCount}/{totalCount} joueurs ont soumis leur plateau
      </p>
      <div className="flex gap-2">
        {players.map((_player, index) => (
          <div
            key={index}
            className={`w-3 h-3 rounded-full transition-colors duration-500 ${
              index < submittedCount ? 'bg-green-500' : 'bg-gray-300'
            }`}
          />
        ))}
      </div>
    </div>
  )
}
```

**Note sur les indicateurs visuels :** Les cercles verts indiquent le *nombre* de soumissions, pas l'identité des joueurs soumis (les données du store ne permettent pas de relier un index de joueur à son statut de soumission fiable). C'est intentionnel et suffisant pour la story.

- [ ] **Étape 2.2 : Vérifier le lint**

Depuis `SoClover/client/` :
```bash
npm run lint
```
Attendu : aucun warning ni erreur.

- [ ] **Étape 2.3 : Commit**

```bash
git add SoClover/client/src/components/writing/SubmissionProgress.tsx
git commit -m "feat: add SubmissionProgress component showing board submission count"
```

---

## Task 3 : Modifier Timer.tsx — feedback à l'expiration

**Fichiers :**
- Modifier : `SoClover/client/src/components/shared/Timer.tsx`

**Contexte :**
- Actuellement, quand `timeLeft === 0`, le timer affiche "00:00" et reste figé jusqu'au changement de phase côté serveur (latence 1-3s)
- La fix : remplacer l'affichage "00:00" par "Transition en cours..." avec animation pulse

**Fichier actuel (référence) :**
```tsx
// Ligne 38-46 — le return actuel
return (
  <span
    className={`text-sm font-bold font-mono transition-colors duration-300 ${
      isWarning ? 'text-red-500 animate-pulse' : 'text-gray-700'
    }`}
  >
    {minutes}:{seconds}
  </span>
)
```

- [ ] **Étape 3.1 : Modifier le fichier**

Ajouter le bloc `if (timeLeft === 0)` AVANT la logique de formatage `minutes`/`seconds`, juste après `if (timeLeft === null) return null` :

```tsx
import { useEffect, useState } from 'react'
import { useGameStore } from '../../core/store'

export const Timer = () => {
  const phaseEndsAtUtc = useGameStore((state) => state.phaseEndsAtUtc)
  const [timeLeft, setTimeLeft] = useState<number | null>(null)
  const [isWarning, setIsWarning] = useState(false)

  useEffect(() => {
    if (!phaseEndsAtUtc) {
      setTimeLeft(null)
      setIsWarning(false)
      return
    }

    const deadline = new Date(phaseEndsAtUtc).getTime()

    const calculateTimeLeft = () => {
      const now = new Date().getTime()
      const diff = Math.max(0, Math.ceil((deadline - now) / 1000))
      setTimeLeft(diff)
      setIsWarning(diff <= 30)
    }

    calculateTimeLeft()
    const interval = setInterval(calculateTimeLeft, 1000)

    return () => clearInterval(interval)
  }, [phaseEndsAtUtc])

  if (timeLeft === null) return null

  if (timeLeft === 0) {
    return (
      <span className="text-amber-500 animate-pulse text-sm font-medium">
        Transition en cours...
      </span>
    )
  }

  const minutes = Math.floor(timeLeft / 60)
    .toString()
    .padStart(2, '0')
  const seconds = (timeLeft % 60).toString().padStart(2, '0')

  return (
    <span
      className={`text-sm font-bold font-mono transition-colors duration-300 ${
        isWarning ? 'text-red-500 animate-pulse' : 'text-gray-700'
      }`}
    >
      {minutes}:{seconds}
    </span>
  )
}
```

- [ ] **Étape 3.2 : Vérifier le lint**

```bash
npm run lint
```
Attendu : aucun warning ni erreur.

- [ ] **Étape 3.3 : Commit**

```bash
git add SoClover/client/src/components/shared/Timer.tsx
git commit -m "feat: show 'Transition en cours...' when timer reaches zero"
```

---

## Task 4 : Modifier WritingBoard.tsx — intégrer SubmissionProgress

**Fichiers :**
- Modifier : `SoClover/client/src/components/writing/WritingBoard.tsx`

**Contexte :**
- `myBoard.isSubmitted` est déjà dans le store (mis à jour par `setMyBoardSubmitted` dans `boardSlice.ts`)
- Quand `myBoard.isSubmitted === true`, le plateau est désactivé (`disabled={myBoard.isSubmitted}` est déjà en place)
- Il faut remplacer/augmenter le message d'attente statique par `SubmissionProgress`

**Fichier actuel :**
Le `WritingBoard.tsx` actuel n'a pas de message "En attente des autres joueurs..." — le plateau est simplement `disabled`. Il faut donc AJOUTER le composant après `<WritingControls />` quand `isSubmitted` est vrai.

- [ ] **Étape 4.1 : Modifier WritingBoard.tsx**

Remplacer l'intégralité du fichier :

```tsx
import { useEffect } from 'react'
import { Board } from '../shared/Board'
import { useBoardStore, useGameStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { WritingControls } from './WritingControls'
import { SubmissionProgress } from './SubmissionProgress'

export const WritingBoard = () => {
  const myBoard = useBoardStore(s => s.myBoard)
  const resetBoards = useBoardStore(s => s.resetBoards)
  const playerId = useGameStore(s => s.playerId)
  const phase = useGameStore(s => s.phase)
  const { fetchGameState, submitClue, loading } = useGameActions()

  // Load board data from backend when entering Writing phase
  useEffect(() => {
    fetchGameState()

    // Setup polling or SignalR would be better here, 
    // but for now we follow the existing logic with improved stability
    
    // Cleanup: reset boards when component unmounts
    return () => {
      resetBoards()
    }
  }, [fetchGameState, resetBoards]) 

  if (!myBoard || loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="text-gray-500">Chargement de votre plateau...</p>
        <p className="text-xs text-gray-400">Phase: {phase} | ID: {playerId}</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-screen gap-8 py-8 w-full max-w-[1200px] mx-auto">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-clover-dark mb-2">Phase d'Écriture</h1>
        <p className="text-gray-600">Observez vos 4 cartes et les 8 mots formés par leurs paires</p>
      </div>

      <div className="flex items-center justify-center w-full px-4 overflow-visible">
        <Board 
          cards={myBoard.cards} 
          rotation={myBoard.rotation} 
          animateEntry={true} 
          showClueInputs={true}
          onClueSave={submitClue}
          disabled={myBoard.isSubmitted}
          clues={{
            top: myBoard.clues.top.text,
            right: myBoard.clues.right.text,
            bottom: myBoard.clues.bottom.text,
            left: myBoard.clues.left.text,
          }}
        />
      </div>

      <WritingControls />

      {myBoard.isSubmitted && (
        <div className="flex flex-col items-center gap-2 py-4">
          <p className="text-green-600 font-semibold">Plateau soumis !</p>
          <p className="text-gray-500 text-sm">En attente des autres joueurs...</p>
          <SubmissionProgress />
        </div>
      )}

      <div className="text-center text-sm text-gray-500 max-w-2xl px-4 mt-8">
        <p className="mb-2">
          <strong>Les 8 mots communs</strong> sont formés par les bords adjacents des cartes :
        </p>
        <ul className="space-y-1">
          <li><strong>Indice Haut :</strong> {myBoard.cards[0]?.words[0]} + {myBoard.cards[1]?.words[0]}</li>
          <li><strong>Indice Droite :</strong> {myBoard.cards[1]?.words[1]} + {myBoard.cards[3]?.words[1]}</li>
          <li><strong>Indice Bas :</strong> {myBoard.cards[2]?.words[2]} + {myBoard.cards[3]?.words[2]}</li>
          <li><strong>Indice Gauche :</strong> {myBoard.cards[0]?.words[3]} + {myBoard.cards[2]?.words[3]}</li>
        </ul>
      </div>
    </div>
  )
}
```

- [ ] **Étape 4.2 : Vérifier le lint**

```bash
npm run lint
```
Attendu : aucun warning ni erreur.

- [ ] **Étape 4.3 : Commit**

```bash
git add SoClover/client/src/components/writing/WritingBoard.tsx
git commit -m "feat: show SubmissionProgress after board submission in WritingBoard"
```

---

## Task 5 : Modifier App.tsx — AnimatePresence phases + lazy loading + ConnectionOverlay

**Fichiers :**
- Modifier : `SoClover/client/src/App.tsx`

**Contexte :**
- `framer-motion` (`motion`, `AnimatePresence`) est déjà importé
- Il y a déjà un `AnimatePresence` pour le loader d'initialisation — **ne pas le toucher**, créer un nouveau `AnimatePresence` séparé autour des phases
- `HomeScreen` et `LobbyPage` restent en import statique (légers, chargés immédiatement)
- `WritingBoard`, `GuessingPage`, `ScoringPage` passent en `React.lazy`
- `Suspense` est importé depuis React
- `ConnectionOverlay` doit être placé APRÈS le `<main>` dans le JSX pour apparaître au-dessus

**Remplacement complet du fichier :**

- [ ] **Étape 5.1 : Réécrire App.tsx**

```tsx
import { lazy, Suspense } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Wifi, WifiOff, Loader2 } from 'lucide-react'
import { useSignalR } from './hooks/useSignalR'
import { useTimeoutSafetyPolling } from './hooks/useTimeoutSafetyPolling'
import { useGameStore } from './core/store'
import { HomeScreen } from './components/home/HomeScreen'
import { LobbyPage } from './components/lobby/LobbyPage'
import { NotificationContainer } from './components/shared/NotificationContainer'
import { Timer } from './components/shared/Timer'
import { ConnectionOverlay } from './components/shared/ConnectionOverlay'

const WritingBoard = lazy(() => import('./components/writing/WritingBoard').then(m => ({ default: m.WritingBoard })))
const GuessingPage = lazy(() => import('./components/guessing/GuessingPage').then(m => ({ default: m.GuessingPage })))
const ScoringPage = lazy(() => import('./components/scoring/ScoringPage').then(m => ({ default: m.ScoringPage })))

const phaseVariants = {
  initial: { opacity: 0, y: 20 },
  animate: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -20 },
}

const phaseTransition = { duration: 0.3 }

const PhaseLoader = () => (
  <div className="flex flex-col items-center justify-center min-h-screen gap-4">
    <Loader2 className="w-10 h-10 text-clover animate-spin" />
  </div>
)

function App() {
  useSignalR();
  useTimeoutSafetyPolling();
  const connectionStatus = useGameStore(s => s.connectionStatus);
  const phase = useGameStore(s => s.phase);
  const gameId = useGameStore(s => s.gameId);
  const isInitializing = useGameStore(s => s.isInitializing);
  const hasDeadline = useGameStore(state => !!state.phaseEndsAtUtc);

  console.log('[App] Render - phase:', phase, 'gameId:', gameId, 'connection:', connectionStatus);

  const role = useGameStore(state => state.role);

  return (
    <div className="min-h-screen bg-clover-light flex flex-col p-4">
      {/* Notification System */}
      <NotificationContainer />

      {/* Connection Status Indicator */}
      <div className="fixed top-4 right-4 flex items-center gap-2 bg-white px-3 py-1.5 rounded-full shadow-md z-50">
        {connectionStatus === 'Connected' ? (
          <Wifi size={18} className="text-green-500" />
        ) : (
          <WifiOff size={18} className="text-red-500" />
        )}
        
        {connectionStatus === 'Connected' && hasDeadline ? (
          <Timer />
        ) : (
          <span className="text-sm font-medium text-gray-700">{connectionStatus}</span>
        )}
      </div>

      {/* Global Loader - Only during initial lobby load */}
      <AnimatePresence>
        {isInitializing && phase === 'Lobby' && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-white/80 backdrop-blur-sm z-[100] flex flex-col items-center justify-center"
          >
            <Loader2 className="w-12 h-12 text-clover animate-spin mb-4" />
            <p className="text-lg font-bold text-clover-dark">Initialisation de la partie...</p>
          </motion.div>
        )}
      </AnimatePresence>

      <main className="flex-1 flex flex-col items-center justify-center w-full">
        {phase === 'Initial' && <HomeScreen />}
        {phase === 'Lobby' && <LobbyPage />}

        <Suspense fallback={<PhaseLoader />}>
          <AnimatePresence mode="wait">
            {phase === 'WritingClues' && (
              <motion.div
                key="writing"
                variants={phaseVariants}
                initial="initial"
                animate="animate"
                exit="exit"
                transition={phaseTransition}
                className="w-full"
              >
                <WritingBoard />
              </motion.div>
            )}
            {phase === 'Guessing' && (
              <motion.div
                key="guessing"
                variants={phaseVariants}
                initial="initial"
                animate="animate"
                exit="exit"
                transition={phaseTransition}
                className="w-full"
              >
                <GuessingPage />
              </motion.div>
            )}
            {phase === 'Scoring' && (
              <motion.div
                key="scoring"
                variants={phaseVariants}
                initial="initial"
                animate="animate"
                exit="exit"
                transition={phaseTransition}
                className="w-full max-w-4xl"
              >
                <ScoringPage />
              </motion.div>
            )}
          </AnimatePresence>
        </Suspense>

        {phase !== 'Initial' && phase !== 'Lobby' && phase !== 'WritingClues' && phase !== 'Guessing' && phase !== 'Scoring' && (
          <div className="text-center bg-white p-8 rounded-3xl shadow-xl border border-slate-100 max-w-lg">
            <h2 className="text-2xl font-bold text-clover-dark mb-4">Phase : {phase}</h2>
            <p className="text-gray-600 mb-6">Cette phase est en cours de développement.</p>
            <div className="p-3 bg-slate-50 rounded-xl text-xs text-slate-500 font-mono flex flex-col gap-1">
              <div>Partie : {gameId}</div>
              <div>Rôle : {role}</div>
            </div>
          </div>
        )}
      </main>

      {/* Connection Overlay — rendered above everything else */}
      <ConnectionOverlay status={connectionStatus} />
    </div>
  )
}

export default App
```

**Points d'attention :**
- Les exports nommés (`WritingBoard`, `GuessingPage`, `ScoringPage`) sont réexportés via `.then(m => ({ default: m.ComponentName }))` car les fichiers utilisent des exports nommés, pas `export default`
- `HomeScreen` et `LobbyPage` restent en import statique — pas de `Suspense` autour d'eux
- `phase === 'Initial'` et `phase === 'Lobby'` sont rendus HORS du `Suspense`/`AnimatePresence` pour ne pas déclencher le lazy loading au démarrage

- [ ] **Étape 5.2 : Vérifier que les exports des composants lazifiés sont bien nommés**

Vérifier que ces fichiers exportent bien avec le nom attendu :
```bash
grep "export const WritingBoard" SoClover/client/src/components/writing/WritingBoard.tsx
grep "export const GuessingPage" SoClover/client/src/components/guessing/GuessingPage.tsx
grep "export const ScoringPage" SoClover/client/src/components/scoring/ScoringPage.tsx
```
Attendu : chaque grep retourne une ligne.

- [ ] **Étape 5.3 : Lint + build**

Depuis `SoClover/client/` :
```bash
npm run lint && npm run build
```
Attendu : 0 warning ESLint, build Vite réussi.

- [ ] **Étape 5.4 : Commit**

```bash
git add SoClover/client/src/App.tsx
git commit -m "feat: add phase transitions, lazy loading, and ConnectionOverlay in App.tsx"
```

---

## Vérification finale

- [ ] **Lint global propre**

```bash
cd SoClover/client && npm run lint
```
Attendu : `✓ No ESLint warnings or errors`

- [ ] **Build propre**

```bash
cd SoClover/client && npm run build
```
Attendu : build sans erreur TypeScript ni Vite.

---

## Critères d'acceptation (référence depuis la Story)

- [ ] Un overlay semi-transparent s'affiche lors de la reconnexion SignalR
- [ ] Un overlay avec bouton "Rafraîchir" s'affiche en cas de déconnexion définitive
- [ ] En phase WritingClues, les joueurs ayant soumis voient "X/Y joueurs ont soumis leur plateau"
- [ ] Toutes les transitions de phase ont une animation d'entrée ET de sortie
- [ ] Le timer affiche "Transition en cours..." quand il atteint 0 (au lieu de "00:00")
- [ ] Les composants WritingBoard, GuessingPage, ScoringPage sont lazy-loaded
- [ ] `npm run lint` passe sans warning
- [ ] `npm run build` compile sans erreur
