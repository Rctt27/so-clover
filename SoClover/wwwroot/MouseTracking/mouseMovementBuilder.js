/**
 * Builder pour la reconstruction des mouvements de souris des autres joueurs
 */
const MouseMovementBuilder = (function() {
    'use strict';

    const remoteCursors = new Map(); // playerId -> { element, lastPos, colorClass }
    const playerColors = new Map(); // playerId -> colorIndex
    const CURSOR_COLORS_COUNT = 10;

    /**
     * Récupère ou attribue une couleur persistante pour un joueur
     * @param {string} playerId 
     * @returns {number} colorIndex (1-10)
     */
    function getPlayerColorIndex(playerId) {
        if (playerColors.has(playerId)) {
            return playerColors.get(playerId);
        }
        const colorIndex = Math.floor(Math.random() * CURSOR_COLORS_COUNT) + 1;
        playerColors.set(playerId, colorIndex);
        return colorIndex;
    }

    /**
     * Rend le mouvement d'une souris distante
     * @param {Object} data { playerId, playerName, positions: [{x, y, t}] }
     */
    function renderRemoteMouse(data) {
        if (!data) return;
        const rPlayerId = data.playerId || data.PlayerId;
        const rPlayerName = data.playerName || data.PlayerName;
        const positions = data.positions || data.Positions;

        if (!rPlayerId || !positions || positions.length === 0) return;

        console.debug(`[MouseTracking] Receiving ${positions.length} positions from ${rPlayerName} (${rPlayerId})`, positions);

        let cursor = remoteCursors.get(rPlayerId);

        // Création du curseur s'il n'existe pas
        if (!cursor) {
            const el = document.createElement('div');
            el.className = 'remote-cursor';
            
            // Récupération de la couleur persistante
            const colorIndex = getPlayerColorIndex(rPlayerId);
            el.classList.add(`cursor-color-${colorIndex}`);

            const icon = document.createElement('div');
            icon.className = 'remote-cursor-icon';
            
            const label = document.createElement('div');
            label.className = 'remote-cursor-label';
            label.textContent = rPlayerName;

            el.appendChild(icon);
            el.appendChild(label);
            document.body.appendChild(el);

            cursor = { 
                element: el, 
                colorClass: `cursor-color-${colorIndex}`,
                positionQueue: [],
                isProcessing: false
            };
            remoteCursors.set(rPlayerId, cursor);
        }

        // Ajout des nouvelles positions à la file d'attente
        cursor.positionQueue.push(...positions);

        // Démarrage du traitement de la file si non déjà en cours
        if (!cursor.isProcessing) {
            processQueue(rPlayerId);
        }
    }

    /**
     * Traite la file d'attente des positions pour un joueur
     * @param {string} playerId 
     */
    function processQueue(playerId) {
        const cursor = remoteCursors.get(playerId);
        if (!cursor || cursor.positionQueue.length === 0) {
            if (cursor) cursor.isProcessing = false;
            return;
        }

        cursor.isProcessing = true;
        const nextPos = cursor.positionQueue.shift();
        const x = nextPos.x ?? nextPos.X;
        const y = nextPos.y ?? nextPos.Y;
        const t = nextPos.t ?? nextPos.T;

        if (cursor.element) {
            cursor.element.style.transform = `translate(${x}px, ${y}px)`;
        }

        // Calcul du délai pour le prochain point
        let delay = 0;
        if (cursor.positionQueue.length > 0) {
            const followingPos = cursor.positionQueue[0];
            const nextT = followingPos.t ?? followingPos.T;
            delay = Math.max(0, nextT - t);
            
            // Sécurité : on limite le délai pour éviter des blocages si les timestamps sont incohérents
            // Ou si le payload est trop volumineux et qu'on prend du retard
            if (delay > 500) delay = 50; 
            
            // Si on a trop de retard (trop de points en attente), on accélère la lecture
            if (cursor.positionQueue.length > 50) {
                delay = Math.floor(delay / 2);
            }
        }

        // On utilise requestAnimationFrame pour le rendu fluide
        if (delay > 0) {
            setTimeout(() => {
                requestAnimationFrame(() => processQueue(playerId));
            }, delay);
        } else {
            requestAnimationFrame(() => processQueue(playerId));
        }
    }

    /**
     * Nettoie tous les curseurs distants du DOM
     */
    function cleanupRemoteCursors() {
        remoteCursors.forEach(cursor => {
            if (cursor.element && cursor.element.parentNode) {
                cursor.element.parentNode.removeChild(cursor.element);
            }
        });
        remoteCursors.clear();
    }

    return {
        renderRemoteMouse,
        cleanupRemoteCursors
    };
})();
