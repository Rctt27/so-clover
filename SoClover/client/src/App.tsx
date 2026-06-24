import { lazy, Suspense, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { debugLog } from './core/debug'
import { motion, AnimatePresence } from 'framer-motion'
import { Wifi, WifiOff, Loader2 } from 'lucide-react'
import { useSignalR } from './hooks/useSignalR'
import type { ConnectionStatus } from './types/game'
import { useGameSounds } from './hooks/useGameSounds'
import { useTimeoutSafetyPolling } from './hooks/useTimeoutSafetyPolling'
import { useWritingCluesPhaseMusic } from './hooks/useWritingCluesPhaseMusic'
import { useGameStore, useAppConfigStore } from './core/store'
import { readGameCodeFromUrl, syncGameUrl, clearGameUrl } from './core/gameUrl'
import { HomeScreen } from './components/home/HomeScreen'
import { LobbyPage } from './components/lobby/LobbyPage'
import { ScoringPage } from './components/scoring/ScoringPage'
import { NotificationContainer } from './components/shared/NotificationContainer'
import { Timer } from './components/shared/Timer'
import { ConnectionOverlay } from './components/shared/ConnectionOverlay'
import { SoundToggleButton } from './components/shared/SoundToggleButton'
import { AddToHomeScreenHint } from './components/shared/AddToHomeScreenHint'
import { MOBILE_BOARD_CONTROLS_SLOT_ID } from './components/shared/MobileBoardControlsPortal'

const WritingBoard = lazy(() => import('./components/writing/WritingBoard').then(m => ({ default: m.WritingBoard })))
const WaitingForAiBoards = lazy(() => import('./components/writing/WaitingForAiBoards').then(m => ({ default: m.WaitingForAiBoards })))
const GuessingPage = lazy(() => import('./components/guessing/GuessingPage').then(m => ({ default: m.GuessingPage })))

const PHASE_TRANSITION_MS = 300

const phaseVariants = {
  initial: { opacity: 0, y: 20 },
  animate: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -20 },
}

const phaseTransition = { duration: PHASE_TRANSITION_MS / 1000 }

const PhaseLoader = () => (
  <div className="flex flex-col items-center justify-center min-h-svh">
    <Loader2 className="w-10 h-10 text-clover animate-spin" />
  </div>
)

function App() {
  useSignalR();
  useGameSounds();
  useTimeoutSafetyPolling();
  useWritingCluesPhaseMusic();
  const { t } = useTranslation('common')
  const connectionStatus = useGameStore(s => s.connectionStatus);
  const connectionLabels: Record<ConnectionStatus, string> = {
    Connected: t('connection.connected'),
    Disconnected: t('connection.disconnected'),
    Connecting: t('connection.connecting'),
    Reconnecting: t('connection.reconnecting'),
  };
  const phase = useGameStore(s => s.phase);
  const gameId = useGameStore(s => s.gameId);
  const isInitializing = useGameStore(s => s.isInitializing);
  const hasDeadline = useGameStore(state => !!state.phaseEndsAtUtc);
  const role = useGameStore(state => state.role);
  const guessAiBoardOnly = useGameStore(s => s.settings.guessAiBoardOnly);

  // ─── Sync URL ↔ partie courante (History API léger) ─────────────────────────
  const prevGameIdRef = useRef<string | null>(gameId);
  useEffect(() => {
    const prev = prevGameIdRef.current;
    if (gameId) {
      syncGameUrl(gameId);          // dans une partie → /g/<code>
    } else if (prev) {
      clearGameUrl();               // on avait une partie, plus maintenant → /
    }
    // (gameId null + prev null = arrivée fraîche sur /g/<code> : on garde l'URL
    //  pour le pré-remplissage du formulaire de join, cf. GameManagementPanel)
    prevGameIdRef.current = gameId;
  }, [gameId]);

  // Bouton « précédent » du navigateur : si l'URL ne pointe plus vers la partie, on quitte.
  useEffect(() => {
    const onPop = () => {
      const code = readGameCodeFromUrl();
      const gid = useGameStore.getState().gameId;
      if (!code && gid) {
        useGameStore.getState().resetAuth();
      }
    };
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);
  // ────────────────────────────────────────────────────────────────────────────

  const loadConfig = useAppConfigStore(s => s.loadConfig);
  useEffect(() => {
    loadConfig().catch(err => debugLog('App', `Failed to load public config: ${err}`));
  }, [loadConfig]);

  // ─── [DEBUG] Tracker les changements de phase ───────────────────────────────
  const prevPhaseRef = useRef(phase);
  useEffect(() => {
    if (phase !== prevPhaseRef.current) {
      debugLog('App', `Phase: ${prevPhaseRef.current} → ${phase}`);
      prevPhaseRef.current = phase;
    }
  }, [phase]);
  // ────────────────────────────────────────────────────────────────────────────

  return (
    <div className="min-h-svh bg-clover-light flex flex-col p-safe">
      {/* Notification System */}
      <NotificationContainer />

      {/* Hint d'installation iOS (plein écran) — uniquement sur l'accueil, jamais en jeu */}
      {phase === 'Initial' && <AddToHomeScreenHint />}

      {/* Cluster HUD fixe haut-droite : à GAUCHE le slot de contrôles de plateau (rotation,
          projetée par les phases sur mobile via MobileBoardControlsPortal), à DROITE le chip
          de connexion (son / wifi / timer). Sur desktop le slot reste vide. */}
      <div className="fixed inset-safe-top inset-safe-right flex items-center gap-2 z-50">
        <div id={MOBILE_BOARD_CONTROLS_SLOT_ID} className="flex items-center" />

        <div data-testid="connection-chip" className="flex items-center gap-2 [@media(pointer:coarse)]:gap-1 bg-white px-3 py-1.5 [@media(pointer:coarse)]:px-2 [@media(pointer:coarse)]:py-1 rounded-full shadow-md">
          {/* Bouton son — rond, à gauche de l'icône wifi avec un espace vide entre les deux.
              Sur mobile (coarse) l'espace est resserré : le chip est informatif et l'utilisateur
              est proche de l'écran → pas besoin des marges aérées du desktop. */}
          <div className="mr-3 [@media(pointer:coarse)]:mr-1">
            <SoundToggleButton />
          </div>

          {connectionStatus === 'Connected' ? (
            <Wifi size={18} className="text-green-500" />
          ) : (
            <WifiOff size={18} className="text-red-500" />
          )}

          {connectionStatus === 'Connected' && hasDeadline ? (
            <Timer />
          ) : (
            <span className="text-sm font-medium text-gray-700">{connectionLabels[connectionStatus]}</span>
          )}
        </div>
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
            <p className="text-lg font-bold text-clover-dark">{t('init')}</p>
          </motion.div>
        )}
      </AnimatePresence>

      <main className="flex-1 flex flex-col items-center justify-center w-full pt-[var(--app-chip-clearance)]">
        <AnimatePresence mode="wait">
          {phase === 'Initial' && (
            <motion.div
              key="initial"
              variants={phaseVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              transition={phaseTransition}
              className="w-full"
            >
              <HomeScreen />
            </motion.div>
          )}
          {phase === 'Lobby' && (
            <motion.div
              key="lobby"
              variants={phaseVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              transition={phaseTransition}
              className="w-full"
            >
              <LobbyPage />
            </motion.div>
          )}
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
              <Suspense fallback={<PhaseLoader />}>
                {guessAiBoardOnly ? <WaitingForAiBoards /> : <WritingBoard />}
              </Suspense>
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
              <Suspense fallback={<PhaseLoader />}>
                <GuessingPage />
              </Suspense>
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

        {phase !== 'Initial' && phase !== 'Lobby' && phase !== 'WritingClues' && phase !== 'Guessing' && phase !== 'Scoring' && (
          <div className="text-center bg-white p-8 rounded-3xl shadow-xl border border-slate-100 max-w-lg">
            <h2 className="text-2xl font-bold text-clover-dark mb-4">{t('devPhase', { phase })}</h2>
            <p className="text-gray-600 mb-6">{t('devWip')}</p>
            <div className="p-3 bg-slate-50 rounded-xl text-xs text-slate-500 font-mono flex flex-col gap-1">
              <div>{t('devGame', { gameId })}</div>
              <div>{t('devRole', { role })}</div>
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
