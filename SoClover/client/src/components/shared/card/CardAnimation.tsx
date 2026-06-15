import { motion } from 'framer-motion'
import { CONSTANTS } from '../../../core/constants'

interface CardAnimationProps {
  rotation?: number;
  animateEntry?: boolean;
  disableAnimation?: boolean;
  className?: string;
  children: React.ReactNode;
}

export const CardAnimation = ({
  rotation = 0,
  animateEntry = false,
  disableAnimation = false,
  className = '',
  children,
}: CardAnimationProps) => {
  // Desktop : la police des mots scale avec la largeur du viewport, bornée par
  // cardStandardFontSize. Sur mobile (pointer:coarse) le vw est trompeur (le board, donc
  // la carte, ne dépend pas de la largeur de viewport) → on bascule la police en unités
  // de container query relatives à LA CARTE (cf. `.card-font-root` dans index.css), pour
  // qu'elle grandisse avec la carte agrandie et reste lisible. `container-type: inline-size`
  // fait de cette div le conteneur de référence des `cqw` de ses descendants (.card-word).
  const fontSize = `min(1.7vw, ${CONSTANTS.THEME_CONFIG.cardStandardFontSize})`;
  const { card: cardAnim } = CONSTANTS.THEME_CONFIG.animations;

  return (
    <motion.div
      className={`card-font-root relative w-full h-full ${className}`}
      style={{ fontSize, containerType: 'inline-size' }}
      initial={animateEntry ? cardAnim.initial : false}
      animate={{
        ...cardAnim.animate,
        rotate: rotation,
      }}
      transition={disableAnimation ? { duration: 0 } : {
        rotate: { duration: 0.5, ease: 'easeInOut' as const },
        default: animateEntry ? cardAnim.transition : { duration: 0.5 },
      }}
    >
      {children}
    </motion.div>
  );
};
