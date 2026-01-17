import { motion, AnimatePresence } from 'framer-motion'
import { Wifi, WifiOff, Loader2 } from 'lucide-react'
import { useSignalR } from './hooks/useSignalR'
import { useGameStore } from './core/store'
import { HomeScreen } from './components/home/HomeScreen'
import { LobbyPage } from './components/lobby/LobbyPage'
import { WritingBoard } from './components/writing/WritingBoard'
import { GuessingPage } from './components/guessing/GuessingPage'
import { ScoringPage } from './components/scoring/ScoringPage'
import { NotificationContainer } from './components/shared/NotificationContainer'
import { Timer } from './components/shared/Timer'

function App() {
  useSignalR();
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
        {phase === 'WritingClues' && <WritingBoard />}
        {phase === 'Guessing' && <GuessingPage />}
        {phase === 'Scoring' && (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
            className="w-full max-w-4xl"
          >
            <ScoringPage />
          </motion.div>
        )}

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
    </div>
  )
}

export default App
