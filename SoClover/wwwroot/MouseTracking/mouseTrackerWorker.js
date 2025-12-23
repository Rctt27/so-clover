let positions = [];
let lastPosition = null;
let gameId = null;
let playerId = null;
const TOLERANCE = 0.0005; // Tolérance sur les coordonnées normalisées
const MAX_POSITIONS = 50; // On accumule plus avant d'envoyer
const INACTIVITY_DELAY = 250; // ms d'inactivité avant d'envoyer le payload (fin de mouvement)
let inactivityTimer = null;

self.onmessage = function(e) {
    const { type, data } = e.data;

    if (type === 'INIT') {
        gameId = data.gameId;
        playerId = data.playerId;
        positions = [];
        lastPosition = null;
        if (inactivityTimer) clearTimeout(inactivityTimer);
    } else if (type === 'MOUSE_MOVE') {
        if (!gameId || !playerId) return;

        const { nx, ny, timestamp } = data;
        
        // Comparaison avec la position précédente (tolérance faible sur les valeurs normalisées)
        const isSamePosition = lastPosition && 
            Math.abs(lastPosition.nx - nx) < TOLERANCE && 
            Math.abs(lastPosition.ny - ny) < TOLERANCE;

        if (isSamePosition) {
            return;
        }

        const newPos = { nx, ny, t: timestamp };
        positions.push(newPos);
        lastPosition = newPos;

        // On réinitialise le timer d'inactivité à chaque mouvement
        if (inactivityTimer) clearTimeout(inactivityTimer);
        
        // Si on atteint la limite de positions, on envoie immédiatement pour ne pas perdre en fluidité
        // ou créer des payloads trop lourds. Sinon, on attend la fin du mouvement.
        if (positions.length >= MAX_POSITIONS) {
            sendPayload();
        } else {
            inactivityTimer = setTimeout(sendPayload, INACTIVITY_DELAY);
        }
    }
};

function sendPayload() {
    if (inactivityTimer) {
        clearTimeout(inactivityTimer);
        inactivityTimer = null;
    }

    if (positions.length === 0) return;

    self.postMessage({
        gameId,
        playerId,
        positions: [...positions]
    });

    // Réinitialisation après envoi
    positions = [];
}
