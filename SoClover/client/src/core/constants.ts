export const CONSTANTS = {
  // À mettre à jour à chaque release de version (cf. CLAUDE.md § Versioning)
  APP_VERSION: '2.13.0',
  GAME_URL_PREFIX: '/g/',
  SIGNALR_HUB_URL: '/hubs/game',
  RECONNECT: {
    // Politique de reconnexion mobile : agressive au début, espacée ensuite.
    // Délais (ms) indexés par previousRetryCount ; après épuisement → dernier délai répété.
    delaysMs: [0, 2000, 5000, 10000, 20000, 30000, 60000],
    // Au-delà de ce temps cumulé sans reconnexion → abandon (null).
    maxElapsedMs: 5 * 60 * 1000,
    // Timeouts client alignés avec le keepalive serveur (cf. Program.cs).
    serverTimeoutMs: 60000,
    keepAliveMs: 15000,
  },
  MOUSE_THROTTLE_MS: 30,
  GUESSING_BOARD_ROTATION_THROTTLE_MS: 50,
  GUESSING_CARD_ROTATION_THROTTLE_MS: 50,
  // Nombre total de tentatives par plateau (miroir de Domain/Game.cs:RemainingAttempts=3).
  // Sert de dénominateur au compteur « tentatives restantes / total » de la Déduction.
  GUESSING_TOTAL_ATTEMPTS: 3,
  TIMER_SOUND_WARNING_SECONDS: 5,
  STORAGE_KEYS: {
    PLAYER_ID: 'so_clover_player_id',
    PLAYER_NAME: 'so_clover_player_name',
  },
  
  
  CLUE_VALIDATION: {
    debounceMs: 200,
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
    },
    rotationCorner: {
      // Taille de chaque zone de rotation de coin, exprimée en FRACTION de la carte (et non en
      // pixels fixes). Garantit des zones proportionnelles à la carte : sur un plateau tablette
      // maximisé où la taille des cartes varie, une zone fixe (48px) devenait trop petite (mis-aim
      // → swap involontaire) ou se recouvrait sur petites cartes. 4×30% laissent une croix centrale
      // de 40% dédiée au déplacement, sans recouvrement entre coins, quelle que soit la taille.
      sizeRatio: 0.3,
      // Ratio agrandi sous pointeur tactile (pointer:coarse). Sur une carte ~100-120px en
      // portrait, 30% donne ~30-36px (sous le seuil 44px des cibles tactiles). 40% porte la
      // zone à ~44px sur cartes moyennes ; la croix centrale de déplacement (clic-clic) reste
      // à 20% (1 - 2×0.4). Appliqué via media-query CSS (cf. .rotation-corner-zone, index.css),
      // pas de détection JS — le type de pointeur ne change pas à l'exécution.
      sizeRatioCoarse: 0.4,
    },
    // Opacité des cartes-solution révélées (non devinées) pendant le cooldown de débrief,
    // pour les distinguer des cartes correctement devinées (opacité pleine).
    revealedSolutionOpacity: 0.45,
    warningOverlay: {
      // outline INSET (box-shadow) → la carte conserve ses dimensions, zéro impact layout.
      // Les cartes sont carrées (aucun coin arrondi) : pas de rayon, l'outline épouse le contour exact.
      outlineClass: 'ring-2 ring-orange-500 ring-inset',
      iconClass: 'w-7 h-7 drop-shadow', // couleurs portées par les fill-* du SVG (triangle orange, exclamation blanche)
      offsetClass: 'top-1 right-1',
      zIndex: 121, // au-dessus du contenu carte (110), ≈ niveau correct (120)
      iconZIndex: 122,
      // Délai d'apparition calé sur la durée de rotation de la carte (CardAnimation rotate = 0.5s)
      // pour que le warning ne « pop » pas avant la fin du pivot. Fade-in doux ensuite.
      appearDelaySec: 0.25,
      fadeDurationSec: 0.25,
    },
  },
  
  CANVAS_COLORS: {
    cloverGreen: '#2dc653',   // Pétales et placeholders de cartes
    darkGreen: '#2abb4e',     // Dégradé sombre (gradient stop)
    accentGreen: '#25a244',   // Repères de coin sur les trous
    cardGreen: '#7AC943',     // Couleur de remplissage de la face de carte
  },

  GAME_CARD: {
    ellipseDepth: 75,         // Profondeur des creux concaves (rayon mineur des ellipses)
    ellipseRx: 140,           // Rayon majeur des ellipses (horizontal/vertical selon arête)
    centerCutSize: 90,        // Taille du trou central (~28% de cardSize)
    centerCutRadius: 6,       // Arrondi du trou central
    borderRadius: 16,         // Arrondi du conteneur de carte
    boxShadow: '0 4px 12px rgba(0, 0, 0, 0.12)',
  },

  ASSET_REFERENCES: {
    board: {
      referenceSize: 1300,  // Canvas 1300×1300px — contenu visuel ~1258px (marge ~21px)
      cardSize: 320,
      cardGap: 4,           // Espacement (px) entre les cartes adjacentes sur le board
      minRenderedPx: 420,   // Plancher de lisibilité PRÉFÉRÉ du plateau (sizing height-aware) ; borné par la largeur du conteneur via min(minRenderedPx, 100cqw) dans Board.tsx → jamais de débordement horizontal sur écran < 420px (mobile)
      maxRenderedPx: 1000,  // Plafond du plateau rendu ; remplace l'ancien maxWidth codé en dur dans Board.tsx
    },
    pool: {
      slotMaxPx: 260,       // Taille max d'un slot de pool (Guessing) — reprend l'ancien w-[260px]/h-[260px]
      slotMinPx: 150,       // Plancher d'un slot de pool ; sous ce seuil, scroll gracieux dans la rangée centrale
      dragOverlayPx: 180,   // Taille de repli de l'overlay de carte draggée (extrait de l'inline 180×180 historique)
    },
  },

  CLUE_EXPLANATION_TOOLTIP: {
    maxWidthPx: 420,            // Largeur max du tooltip — vise 2-3 lignes pour une explication LLM typique
    fadeDurationSec: 0.15,      // Durée d'apparition/disparition Framer Motion
    offsetPx: 14,               // Espacement (px) entre le clue (bounding-rect) et le tooltip
    viewportMarginPx: 8,        // Marge minimale entre le tooltip et le bord du viewport
    zIndex: 9999,               // Rendu via Portal sur body : on veut être au-dessus de tout
  }
};
