export const CONSTANTS = {
  SIGNALR_HUB_URL: '/hubs/game',
  MOUSE_THROTTLE_MS: 30,
  GUESSING_BOARD_ROTATION_THROTTLE_MS: 50,
  GUESSING_CARD_ROTATION_THROTTLE_MS: 50,
  TIMER_SOUND_WARNING_SECONDS: 5,
  STORAGE_KEYS: {
    PLAYER_ID: 'so_clover_player_id',
    PLAYER_NAME: 'so_clover_player_name',
  },
  
  THEME_CONFIG: {
    cardFontClass: 'mogra-regular',
    cardFontWeight: '400',
    cardFontStyle: 'normal',
    cardStandardFontSize: '1.1rem', // Taille de référence pour le calcul responsive
    cardTextStroke: '0.15px',
    cardTextShadow: '0 0 1px rgba(0,0,0,0.05)',
    clueFontClass: 'mogra-regular',
    clueFontWeight: '600',
    clueFontSize: '0.95rem',
    clueTextColor: '#2c3e50',
    clueBgColor: 'white',
    clueBorderColor: '#4CAF50', // Vert Trèfle
    clueBorderColorSaving: '#2196F3', // Bleu
    clueBorderColorError: '#F44336', // Rouge
    animations: {
      board: {
        initial: { opacity: 0, scale: 0.9 },
        animate: { opacity: 1, scale: 1 },
        transition: { duration: 0.8, ease: "easeOut" as const }
      },
      card: {
        initial: { opacity: 0, scale: 0.8 },
        animate: { opacity: 1, scale: 1 },
        transition: { duration: 0.5, ease: "backOut" as const, delay: 0.8 } // Délai pour attendre la fin de l'animation du Board
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
          transition: { duration: 0.3, ease: 'easeOut' as const }
        }
      }
    }
  },
  
  CANVAS_COLORS: {
    cloverGreen: '#2dc653',   // Pétales et placeholders de cartes
    darkGreen: '#2abb4e',     // Dégradé sombre (gradient stop)
    accentGreen: '#25a244',   // Repères de coin sur les trous
  },

  ASSET_REFERENCES: {
    board: {
      referenceSize: 1300,  // Canvas 1300×1300px — contenu visuel ~1258px (marge ~21px)
      cardSize: 320,
    },
    clueInput: {
      width: 270,
      offsetFromEdge: 216,  // Centre des cercles de pétales : coreTop(330) - penetrationDepth(253)×0.45 ≈ 216
    }
  }
};
