import { CardStyle } from './CardStyle'
import { CardAnimation } from './CardAnimation'
import { CluePositionHandler } from './CluePositionHandler'

export interface CardAssemblerProps {
  words: [string, string, string, string];
  rotation?: number;
  className?: string;
  animateEntry?: boolean;
  disableAnimation?: boolean;
}

export const CardAssembler = ({
  words,
  rotation,
  className,
  animateEntry,
  disableAnimation,
}: CardAssemblerProps) => (
  <CardAnimation
    rotation={rotation}
    animateEntry={animateEntry}
    disableAnimation={disableAnimation}
    className={className}
  >
    <CardStyle className="w-full h-full pointer-events-none relative z-0" />
    <CluePositionHandler words={words} />
  </CardAnimation>
);
