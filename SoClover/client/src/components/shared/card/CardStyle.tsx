import { useId } from 'react'
import { CONSTANTS } from '../../../core/constants'

interface GameCardProps {
  className?: string;
}

export function CardStyle({ className = '' }: GameCardProps) {
  const uniqueId = useId();
  const maskId = `organicMask${uniqueId}`;

  const cardSize = CONSTANTS.ASSET_REFERENCES.board.cardSize;
  const { ellipseDepth, ellipseRx, centerCutSize, centerCutRadius, borderRadius, boxShadow } = CONSTANTS.GAME_CARD;
  const { cardGreen } = CONSTANTS.CANVAS_COLORS;

  const shapeSize = cardSize; // padding = 0, le vert touche les extrémités
  const centerShape = shapeSize / 2;

  const ellipses = [
    { cx: centerShape, cy: 0,         rx: ellipseRx,    ry: ellipseDepth }, // Arête supérieure
    { cx: shapeSize,   cy: centerShape, rx: ellipseDepth, ry: ellipseRx },  // Arête droite
    { cx: centerShape, cy: shapeSize,  rx: ellipseRx,    ry: ellipseDepth }, // Arête inférieure
    { cx: 0,           cy: centerShape, rx: ellipseDepth, ry: ellipseRx },  // Arête gauche
  ];

  const centerX = (cardSize - centerCutSize) / 2;
  const centerY = (cardSize - centerCutSize) / 2;

  return (
    <div
      className={`relative bg-white ${className}`}
      style={{ borderRadius, boxShadow }}
    >
      <svg
        width="100%"
        height="100%"
        viewBox={`0 0 ${cardSize} ${cardSize}`}
        className="absolute inset-0"
      >
        <defs>
          <mask id={maskId}>
            {/* Base blanche (tout visible) */}
            <rect x={0} y={0} width={shapeSize} height={shapeSize} fill="white" />

            {/* Soustraire les ellipses au centre des arêtes (creux concaves) */}
            {ellipses.map((ellipse, i) => (
              <ellipse
                key={i}
                cx={ellipse.cx}
                cy={ellipse.cy}
                rx={ellipse.rx}
                ry={ellipse.ry}
                fill="black"
              />
            ))}

            {/* Soustraire le carré central (trou transparent) */}
            <rect
              x={centerX}
              y={centerY}
              width={centerCutSize}
              height={centerCutSize}
              rx={centerCutRadius}
              fill="black"
            />
          </mask>
        </defs>

        <rect
          x={0}
          y={0}
          width={shapeSize}
          height={shapeSize}
          fill={cardGreen}
          mask={`url(#${maskId})`}
        />
      </svg>
    </div>
  );
}
