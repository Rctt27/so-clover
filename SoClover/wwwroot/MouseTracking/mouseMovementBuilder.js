/**
 * Builder pour la reconstruction des mouvements de souris des autres joueurs
 */
const MouseMovementBuilder = (function() {
    'use strict';

    const remoteCursors = new Map(); // playerId -> { element, lastPos, colorClass }
    const playerColors = new Map(); // playerId -> colorIndex
    const CURSOR_COLORS_COUNT = 10;
    let cursorsContainer = null;
    let containerRect = null;

    /**
     * Initialise ou récupère le conteneur des curseurs
     */
    function getCursorsContainer() {
        if (!cursorsContainer) {
            cursorsContainer = document.getElementById('remoteCursorsLayer');
        }
        
        return cursorsContainer;
    }

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

        const container = getCursorsContainer();
        if (!container) {
            console.warn('[MouseTracking] No cursors container found');
            return;
        }

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
            container.appendChild(el);

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
            console.log(`[MouseTracking] Starting queue processing for ${rPlayerName}`);
            processQueue(rPlayerId);
        }
    }

    /**
     * Traite la file d'attente des positions pour un joueur
     * @param {string} playerId 
     */
    function processQueue(playerId) {
        const cursor = remoteCursors.get(playerId);
        if (!cursor) return;

        if (cursor.positionQueue.length === 0) {
            cursor.isProcessing = false;
            return;
        }

        cursor.isProcessing = true;
        const nextPos = cursor.positionQueue.shift();
        
        // Robustesse sur les noms de propriétés (nx/ny ou NX/NY)
        const nx = nextPos.nx !== undefined ? nextPos.nx : (nextPos.NX !== undefined ? nextPos.NX : 0);
        const ny = nextPos.ny !== undefined ? nextPos.ny : (nextPos.NY !== undefined ? nextPos.NY : 0);
        const t = nextPos.t !== undefined ? nextPos.t : (nextPos.T !== undefined ? nextPos.T : 0);

        if (cursor.element) {
            const board = document.getElementById('cloverBoard');
            const container = getCursorsContainer();

            if (board && container) {
                const boardRect = board.getBoundingClientRect();
                const containerRect = container.getBoundingClientRect();

                // On utilise les dimensions du board pour plus de précision si le container est très proche
                const finalX = (boardRect.left - containerRect.left) + (boardRect.width / 2) + (nx * boardRect.width);
                const finalY = (boardRect.top - containerRect.top) + (boardRect.height / 2) + (ny * boardRect.height);

                cursor.element.style.transform = `translate3d(${finalX}px, ${finalY}px, 0)`;
            }
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
