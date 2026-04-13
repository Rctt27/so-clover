import { useRef, useEffect } from 'react';
import { CONSTANTS } from '../../core/constants';

export function CloverBoard() {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const { referenceSize: size, cardSize } = CONSTANTS.ASSET_REFERENCES.board;

    useEffect(() => {
        const canvas = canvasRef.current;
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        const center = size / 2;

        // Clear canvas with white background
        ctx.fillStyle = 'transparent';
        ctx.fillRect(0, 0, size, size);

        // Color scheme - elegant shades of green
        const cloverGreen = '#2dc653';
        const darkGreen = '#2abb4e';
        const accentGreen = '#25a244';
        const cardPlaceHolderGreen = "#2dc653";

        // Card and core dimensions
        const holeSize = 100; // Hole in center of card is 100px × 100px
        const coreSize = cardSize * 2; // 640px - the central square (2×2 grid)
        const coreLeft = center - coreSize / 2;
        const coreTop = center - coreSize / 2;

        // Double-circle edge extending from one side of the core
        const drawEdge = (x: number, y: number, angle: number) => {
            ctx.save();
            ctx.translate(x, y);
            ctx.rotate(angle);

            const edgeWidth = coreSize; // 640px - width along the core edge
            const circleRadius = 195; // Radius for each circle scaled to match 320px cards
            const circleSpacing = edgeWidth * 0.25; // Distance between circle centers
            const penetrationDepth = 253; // How far circles extend outward from core

            // Two circle centers positioned symmetrically
            const leftCircleX = -circleSpacing;
            const leftCircleY = -penetrationDepth * 0.45; // Adjusted to push circles into core square

            const rightCircleX = circleSpacing;
            const rightCircleY = -penetrationDepth * 0.45;

            // Create gradient for the circles
            const gradient = ctx.createLinearGradient(0, 0, 0, -penetrationDepth);
            gradient.addColorStop(0, cloverGreen);
            gradient.addColorStop(1, darkGreen);

            // Draw left circle
            ctx.beginPath();
            ctx.arc(leftCircleX, leftCircleY, circleRadius, 0, Math.PI * 2);
            ctx.fillStyle = gradient;
            ctx.fill();

            // Draw right circle (will blend with left)
            ctx.beginPath();
            ctx.arc(rightCircleX, rightCircleY, circleRadius, 0, Math.PI * 2);
            ctx.fillStyle = gradient;
            ctx.fill();

            ctx.restore();
        };

        // DRAW EDGES FIRST (so core will be on top)
        // Top edge - extends upward from top edge
        drawEdge(center, coreTop, 0);
        // Right edge - extends rightward from right edge
        drawEdge(coreLeft + coreSize, center, Math.PI / 2);
        // Bottom edge - extends downward from bottom edge
        drawEdge(center, coreTop + coreSize, Math.PI);
        // Left edge - extends leftward from left edge
        drawEdge(coreLeft, center, -Math.PI / 2);

        // NOW DRAW the core square with 4 card placeholders in 2x2 grid
        for (let row = 0; row < 2; row++) {
            for (let col = 0; col < 2; col++) {
                const cardX = coreLeft + col * cardSize;
                const cardY = coreTop + row * cardSize;

                // Draw the lighter green card placeholder area
                ctx.fillStyle = cardPlaceHolderGreen;
                ctx.fillRect(cardX, cardY, cardSize, cardSize);

                // Draw the darker green hole in the center (100px × 100px)
                const holeX = cardX + (cardSize - holeSize) / 2;
                const holeY = cardY + (cardSize - holeSize) / 2;
                ctx.fillStyle = cloverGreen;
                ctx.fillRect(holeX, holeY, holeSize, holeSize);

                // Add a single rounded circle in the center of the darker green hole with depth shadows
                const holeCenterX = holeX + holeSize / 2;
                const holeCenterY = holeY + holeSize / 2;
                const circleRadius = 85; // Circle radius with padding from edges

                // Create the actual hole depression with gradient
                const depthGradient = ctx.createRadialGradient(
                    holeCenterX, holeCenterY, 0,
                    holeCenterX, holeCenterY, circleRadius * 0.5
                );
                depthGradient.addColorStop(0.7, cloverGreen); // Darker center
                depthGradient.addColorStop(1, darkGreen); // Blend with hole color

                ctx.beginPath();
                ctx.arc(holeCenterX, holeCenterY, circleRadius * 0.6, 0, Math.PI * 2);
                ctx.fillStyle = depthGradient;
                ctx.fill();

                // Border around card area
                ctx.strokeStyle = 'rgba(90, 138, 84, 0.4)';
                ctx.lineWidth = 2;
                ctx.strokeRect(cardX, cardY, cardSize, cardSize);

                // Border around hole
                ctx.strokeStyle = 'rgba(111, 169, 104, 0.6)';
                ctx.lineWidth = 1.5;
                ctx.strokeRect(holeX, holeY, holeSize, holeSize);

                // Corner guides for card placement on the hole
                const cornerSize = 8;
                const inset = 3;
                ctx.strokeStyle = accentGreen;
                ctx.lineWidth = 1.5;

                // Four corners on the hole
                const corners = [
                    { dx: inset, dy: inset, hDir: 1, vDir: 1 },
                    { dx: holeSize - inset, dy: inset, hDir: -1, vDir: 1 },
                    { dx: inset, dy: holeSize - inset, hDir: 1, vDir: -1 },
                    { dx: holeSize - inset, dy: holeSize - inset, hDir: -1, vDir: -1 },
                ];

                corners.forEach(corner => {
                    const cx = holeX + corner.dx;
                    const cy = holeY + corner.dy;

                    // Horizontal line
                    ctx.beginPath();
                    ctx.moveTo(cx, cy);
                    ctx.lineTo(cx + corner.hDir * cornerSize, cy);
                    ctx.stroke();

                    // Vertical line
                    ctx.beginPath();
                    ctx.moveTo(cx, cy);
                    ctx.lineTo(cx, cy + corner.vDir * cornerSize);
                    ctx.stroke();
                });
            }
        }

    }, [size, cardSize]);

    return (
        <canvas
            ref={canvasRef}
            width={size}
            height={size}
            className="absolute inset-0 w-full h-full pointer-events-none"
        />
    );
}