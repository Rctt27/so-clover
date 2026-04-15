import { motion } from 'framer-motion'
import { CONSTANTS } from '../../core/constants'
import { GameCard } from './GameCard'

export interface CardProps {
  words: [string, string, string, string]; // [top, right, bottom, left]
  rotation?: number;
  className?: string;
  animateEntry?: boolean;
  disableAnimation?: boolean;
}

const CardWord = ({ children, style }: { children: string, style?: React.CSSProperties }) => {
  const { cardFontClass, cardFontWeight, cardFontStyle, cardTextStroke, cardTextShadow } = CONSTANTS.THEME_CONFIG;
  return (
    <span 
      className={`text-[1em] ${cardFontClass} card-word`}
      style={{
        fontWeight: cardFontWeight,
        fontStyle: cardFontStyle,
        WebkitTextStroke: cardTextStroke,
        textShadow: cardTextShadow,
        ...style
      }}
    >
      {children}
    </span>
  );
};

export const Card = ({ words, rotation = 0, className = '', animateEntry = false, disableAnimation = false }: CardProps) => {
  // On calcule une taille de police qui s'adapte à la taille du plateau
  // cardStandardFontSize est utilisé comme plafond pour la taille responsive
  const fontSize = `min(1.7vw, ${CONSTANTS.THEME_CONFIG.cardStandardFontSize})`;
  const { card: cardAnim } = CONSTANTS.THEME_CONFIG.animations;

  return (
    <motion.div
      className={`relative w-full h-full ${className}`}
      style={{ 
        fontSize,
      }}
      initial={animateEntry ? cardAnim.initial : false}
      animate={{ 
        ...cardAnim.animate,
        rotate: rotation 
      }}
      transition={disableAnimation ? { duration: 0 } : {
        rotate: { duration: 0.5, ease: 'easeInOut' as const },
        default: animateEntry ? cardAnim.transition : { duration: 0.5 }
      }}
    >
      {/* Card background */}
      <GameCard className="w-full h-full pointer-events-none relative z-0" />

      {/* Words positioned on card edges */}
      {/* Top word */}
      <div 
        className="absolute top-[1.5%] left-1/2 -translate-x-1/2 text-center w-[90%] pointer-events-none z-10"
        style={{ height: '18.75%', display: 'flex', alignItems: 'center', justifyContent: 'center' }} 
      >
        <CardWord>{words[0]}</CardWord>
      </div>

      {/* Right word (rotated 90°) */}
      <div 
        className="absolute right-[1.5%] top-1/2 -translate-y-1/2 flex items-center justify-center pointer-events-none z-10"
        style={{ width: '18.75%', height: '90%' }}
      >
        <CardWord style={{ transform: 'rotate(90deg)' }}>
          {words[1]}
        </CardWord>
      </div>

      {/* Bottom word (rotated 180°) */}
      <div 
        className="absolute bottom-[1.5%] left-1/2 -translate-x-1/2 text-center w-[90%] pointer-events-none z-10"
        style={{ height: '18.75%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}
      >
        <CardWord style={{ transform: 'rotate(180deg)', display: 'inline-block' }}>
          {words[2]}
        </CardWord>
      </div>

      {/* Left word (rotated -90°/270°) */}
      <div 
        className="absolute left-[1.5%] top-1/2 -translate-y-1/2 flex items-center justify-center pointer-events-none z-10"
        style={{ width: '18.75%', height: '90%' }}
      >
        <CardWord style={{ transform: 'rotate(-90deg)' }}>
          {words[3]}
        </CardWord>
      </div>
    </motion.div>
  )
}
