import { CONSTANTS } from '../../../core/constants'

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

interface CluePositionHandlerProps {
  words: [string, string, string, string]; // [top, right, bottom, left]
}

export const CluePositionHandler = ({ words }: CluePositionHandlerProps) => (
  <>
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
  </>
);
