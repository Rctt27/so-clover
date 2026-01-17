export const CONSTANTS = {
  SIGNALR_HUB_URL: '/hubs/game',
  MOUSE_THROTTLE_MS: 30,
  GUESSING_BOARD_ROTATION_THROTTLE_MS: 50,
  GUESSING_CARD_ROTATION_THROTTLE_MS: 50,
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
        transition: { duration: 0.8, ease: "easeOut" }
      },
      card: {
        initial: { opacity: 0, scale: 0.8 },
        animate: { opacity: 1, scale: 1 },
        transition: { duration: 0.5, ease: "backOut", delay: 0.8 } // Délai pour attendre la fin de l'animation du Board
      }
    }
  }
};
