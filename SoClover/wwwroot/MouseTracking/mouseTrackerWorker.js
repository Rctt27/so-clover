let positions = [];
let lastPosition = null;
let gameId = null;
let playerId = null;
const TOLERANCE = 1;
const MAX_POSITIONS = 30; // Un peu plus pour compenser la fréquence accrue
const SEND_INTERVAL = 100; // ms - Envoi périodique forcé
let flushTimer = null;

self.onmessage = function(e) {
    const { type, data } = e.data;

    if (type === 'INIT') {
        gameId = data.gameId;
        playerId = data.playerId;
        positions = [];
        lastPosition = null;
        if (flushTimer) clearInterval(flushTimer);
        flushTimer = setInterval(sendPayload, SEND_INTERVAL);
    } else if (type === 'MOUSE_MOVE') {
        if (!gameId || !playerId) return;

        const { x, y, timestamp } = data;
        
        // Comparaison avec la position précédente (tolérance 1px)
        const isSamePosition = lastPosition && 
            Math.abs(lastPosition.x - x) <= TOLERANCE && 
            Math.abs(lastPosition.y - y) <= TOLERANCE;

        if (isSamePosition) {
            return;
        }

        const newPos = { x, y, t: timestamp };
        positions.push(newPos);
        lastPosition = newPos;

        if (positions.length >= MAX_POSITIONS) {
            sendPayload();
        }
    }
};

function sendPayload() {
    if (positions.length === 0) return;

    self.postMessage({
        gameId,
        playerId,
        positions: [...positions]
    });

    // Réinitialisation après envoi
    positions = [];
}
