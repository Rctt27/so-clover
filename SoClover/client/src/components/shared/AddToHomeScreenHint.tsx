import React, { useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Share, X } from 'lucide-react'
import { shouldShowA2HSHint, dismissA2HSHint } from '../../core/pwa'

/**
 * Bannière discrète invitant les utilisateurs iOS (Safari, hors standalone) à ajouter le
 * jeu à leur écran d'accueil — seul moyen d'obtenir un vrai plein écran sur iPhone (cf.
 * core/pwa.ts). iOS n'offre pas d'invite d'installation automatique, d'où ce guidage manuel.
 *
 * Décision figée au montage : le mode standalone ne change pas en cours de session, et un
 * dismiss ne doit pas se ré-afficher. Le rejet est persisté (localStorage) via dismissA2HSHint.
 */
export const AddToHomeScreenHint: React.FC = () => {
  const [visible, setVisible] = useState<boolean>(() => shouldShowA2HSHint())

  const handleDismiss = () => {
    dismissA2HSHint()
    setVisible(false)
  }

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: 16 }}
          role="note"
          className="fixed inset-x-3 bottom-3 z-[90] mx-auto max-w-md rounded-2xl border border-clover/30 bg-white px-4 py-3 shadow-lg flex items-start gap-3"
        >
          <Share className="mt-0.5 h-5 w-5 shrink-0 text-clover-dark" aria-hidden="true" />
          <p className="flex-1 text-sm leading-snug text-gray-700">
            Pour un vrai plein écran : appuie sur <span className="font-semibold">Partager</span>{' '}
            puis <span className="font-semibold">« Ajouter à l'écran d'accueil »</span>.
          </p>
          <button
            type="button"
            onClick={handleDismiss}
            aria-label="Masquer le conseil d'installation"
            className="-mr-1 -mt-1 shrink-0 p-1 text-gray-400 hover:text-gray-600"
          >
            <X className="h-4 w-4" />
          </button>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
