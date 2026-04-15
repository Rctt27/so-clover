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
  const fontSize = `min(1.7vw, ${CONSTANTS.THEME_CONFIG.cardStandardFontSize})`;
  const { card: cardAnim } = CONSTANTS.THEME_CONFIG.animations;

  return (
    <motion.div
      className={`relative w-full h-full ${className}`}
      style={{ fontSize }}
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
