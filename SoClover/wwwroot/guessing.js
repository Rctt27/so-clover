// Scope isolation to avoid global `let` collisions with app.js (e.g. `playerName`)
(function(){
'use strict';

//TODO: Try add live mouse tracking of all guessing players when guessing a board. Do not track owner mouse movements, only guessing players.

// Guessing Phase logic
let gameId = null;
let playerName = '';
let playerId = null;
let boardRotation = 0; // Current board rotation normalized (0, 90, 180, 270)
let cumulativeRotation = 0; // Cumulative rotation in degrees for smooth animation
let guessingState = null;
let isSpectator = false;
let lastUpdatedElements = []; // Array of { index: string|number, isOutside: boolean }
let mouseTrackerWorker = null;
let lastMouseSampleTime = 0;
const MOUSE_SAMPLE_INTERVAL = 30; // ms (plus granulaire que 50ms)

// DOM elements
const guessingPlayerNameDisplay = document.getElementById('guessingPlayerName');
const boardOwnerNameDisplay = document.getElementById('boardOwnerName');
const spectatorMessage = document.getElementById('spectatorMessage');
const leftOutsideCardsContainer = document.getElementById('leftOutsideCards');
const rightOutsideCardsContainer = document.getElementById('rightOutsideCards');
const cloverBoard = document.getElementById('cloverBoard');
const btnRotateLeft = document.getElementById('btnRotateLeft');
const btnRotateRight = document.getElementById('btnRotateRight');
const rotationIndicator = document.getElementById('rotationIndicator');
const btnConfirmBoard = document.getElementById('btnConfirmBoard');
const attemptsInfo = document.getElementById('attemptsInfo');
const guessingStatusMessage = document.getElementById('guessingStatusMessage');

// Clue displays
const clueDisplays = {
    top: document.getElementById('clueTop'),
    right: document.getElementById('clueRight'),
    bottom: document.getElementById('clueBottom'),
    left: document.getElementById('clueLeft')
};

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    try { if (typeof initPhaseTimer === 'function') initPhaseTimer(); } catch {}
    loadGuessingState();
    // Branch to SignalR (centralized in app.js)
    try {
        await (window.realTime?.ensureConnected?.());
        await (window.realTime?.joinCurrentGame?.(gameId, playerId));
    } catch {}
    await fetchAndDisplayGuessingPhase();
    setupRotationControls();
    setupConfirmButton();
    setupKeyboardShortcuts();

    // Subscribe to server pushes
    let offUpdated = () => {};
    let offNotif = () => {};
    let offMouse = () => {};
    try {
        offMouse = window.realTime?.onGuessingMouseMoved?.((data) => {
            console.log('[DEBUG_LOG] GuessingMouseMoved received', data);
            MouseMovementBuilder.renderRemoteMouse(data);
        });

        offUpdated = window.realTime?.onGameStateUpdated?.(async (payload) => {
            console.log('[DEBUG_LOG] GameStateUpdated payload:', payload);
            // Identifier les éléments modifiés selon l'événement
            if (payload && payload.eventData) {
                const data = payload.eventData;
                const positionMap = {
                    0: "TopLeft",
                    1: "TopRight",
                    2: "BottomRight",
                    3: "BottomLeft"
                };

                const getPositionString = (pos) => {
                    if (typeof pos === 'number') return positionMap[pos] || pos;
                    return pos;
                };

                switch (payload.eventType) {
                    case "GuessingCardPlaced":
                        lastUpdatedElements = [{ index: getPositionString(data.Position || data.position), isOutside: false }];
                        break;
                    case "GuessingCardsSwapped":
                        lastUpdatedElements = [
                            { index: getPositionString(data.Position1 || data.position1), isOutside: false },
                            { index: getPositionString(data.Position2 || data.position2), isOutside: false }
                        ];
                        break;
                    case "CardRotated":
                        if (data.BoardPosition !== null && data.BoardPosition !== undefined || data.boardPosition !== null && data.boardPosition !== undefined) {
                            lastUpdatedElements = [{ index: getPositionString(data.BoardPosition ?? data.boardPosition), isOutside: false }];
                        } else if (data.OutsideCardIndex !== null && data.OutsideCardIndex !== undefined || data.outsideCardIndex !== null && data.outsideCardIndex !== undefined) {
                            lastUpdatedElements = [{ index: data.OutsideCardIndex ?? data.outsideCardIndex, isOutside: true }];
                        }
                        break;
                    case "GuessingBoardValidated":
                        // Animer les cartes qui ont été retournées (incorrectes)
                        if (data.IncorrectPositions && data.IncorrectPositions.length > 0) {
                            // On anime les positions sur le plateau qui ont échoué
                            lastUpdatedElements = data.IncorrectPositions.map(pos => ({
                                index: getPositionString(pos),
                                isOutside: false
                            }));

                            // ET on anime aussi toutes les cartes à l'extérieur car le pool a changé
                            // Comme on ne connaît pas encore les futurs index, on utilisera une astuce dans createDraggableCard
                            // ou on marque simplement qu'on veut animer le pool extérieur
                            // TODO: Fixer cette logique d'animation car ne fonctionne pas actuellement mais c'est pas très grave
                            lastUpdatedElements.push({ isOutsidePoolUpdate: true });
                        }
                        break;
                    case "MovedToNextBoard":
                        // Optionnel : on pourrait animer tout le board ici, 
                        // mais fetchAndDisplayGuessingPhase s'en charge déjà indirectement
                        break;
                }
                console.log('[DEBUG_LOG] Identified lastUpdatedElements:', lastUpdatedElements);
            }

            // On any state update, refetch current state
            try { 
                await fetchAndDisplayGuessingPhase(); 
            } finally {
                lastUpdatedElements = []; // Reset après rendu
            }
        }) || (() => {});
        offNotif = window.realTime?.onServerNotification?.((n) => {
            if (!n) return;
            const type = n.type === 'warning' ? 'warning' : 'info';
            try { showGuessingStatusMessage(n.message || 'Server notification', type); } catch {}
        }) || (() => {});
    } catch {}

    // Cleanup handlers
    window.addEventListener('beforeunload', () => {
        stopMouseTracking();
        try { offUpdated(); offNotif(); offMouse(); } catch {}
    });
});

function setupMouseTracking() {
    if (isSpectator) return;
    if (mouseTrackerWorker) return; // Already running

    if (window.Worker) {
        mouseTrackerWorker = new Worker('MouseTracking/mouseTrackerWorker.js');
        
        mouseTrackerWorker.onmessage = function(e) {
            const payload = e.data;
            // Send to server via SignalR
            window.realTime?.sendMousePositions?.(payload.gameId, payload.playerId, payload.positions);
        };

        mouseTrackerWorker.postMessage({
            type: 'INIT',
            data: { gameId, playerId }
        });

        document.addEventListener('mousemove', handleMouseMove);
    }
}

function handleMouseMove(e) {
    if (!mouseTrackerWorker) return;

    // Throttle to 50ms before sending to worker to avoid overwhelming message channel
    const now = Date.now();
    if (now - lastMouseSampleTime < MOUSE_SAMPLE_INTERVAL) return;
    lastMouseSampleTime = now;

    // Coordonnées relatives au <body>
    const x = e.pageX;
    const y = e.pageY;

    mouseTrackerWorker.postMessage({
        type: 'MOUSE_MOVE',
        data: { x, y, timestamp: now }
    });
}

function stopMouseTracking() {
    if (mouseTrackerWorker) {
        document.removeEventListener('mousemove', handleMouseMove);
        mouseTrackerWorker.terminate();
        mouseTrackerWorker = null;
    }
    
    // Nettoyer les curseurs distants via le builder
    MouseMovementBuilder.cleanupRemoteCursors();
}

function loadGuessingState() {
    const savedState = sessionStorage.getItem('soCloverState');
    if (!savedState) {
        showGuessingStatusMessage('No game session found. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
        return;
    }

    try {
        const state = JSON.parse(savedState);
        gameId = state.currentGameId;
        playerName = state.playerName;
        playerId = state.playerId;

        if (!gameId || !playerName || !playerId) {
            throw new Error('Invalid state');
        }

        guessingPlayerNameDisplay.textContent = playerName;

    } catch (error) {
        console.error('Error loading guessing state:', error);
        showGuessingStatusMessage('Failed to load game data. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
    }
}

async function fetchAndDisplayGuessingPhase() {
    try {
        const response = await fetch(`/api/games/${gameId}/state?playerId=${playerId}&includeSecrets=false`);

        if (!response.ok) {
            throw new Error('Failed to fetch game state');
        }

        const gameState = await response.json();
        console.log('🎮 Game State:', gameState);
        // Update phase countdown timer from server
        try { if (typeof setPhaseDeadline === 'function') setPhaseDeadline(gameState.phaseEndsAtUtc || null); } catch {}

        // Check if game has moved to Scoring phase
        if (gameState.phase === 'Scoring') {
            stopMouseTracking();
            showGuessingStatusMessage('All boards completed! Moving to scoring...', 'success');
            setTimeout(() => {
                window.location.href = '/scoring.html';
            }, 2000);
            return;
        }

        if (gameState.phase !== 'Guessing') {
            stopMouseTracking();
            showGuessingStatusMessage('Not in guessing phase', 'error');
            return;
        }

        if (!gameState.guessingState) {
            showGuessingStatusMessage('No guessing state available', 'error');
            return;
        }

        guessingState = gameState.guessingState;

        // Check if player is spectator (board owner)
        const oldIsSpectator = isSpectator;
        isSpectator = guessingState.currentBoardOwnerId === playerId;

        // Reset or setup mouse tracking if role changed or board swapped
        // We always cleanup cursors when board changes to avoid ghost cursors from previous board
        MouseMovementBuilder.cleanupRemoteCursors();
        
        if (isSpectator) {
            stopMouseTracking();
        } else {
            // Re-setup to ensure we are tracking if not spectator
            // setupMouseTracking handles the "already running" check if needed, 
            // but here we want to ensure it's active for the new board.
            setupMouseTracking();
        }

        if (isSpectator) {
            spectatorMessage.style.display = 'block';
            btnConfirmBoard.disabled = true;
        } else {
            spectatorMessage.style.display = 'none';
        }

        // Display board owner name
        boardOwnerNameDisplay.textContent = guessingState.currentBoardOwnerName || 'Unknown';

        // Display clues
        displayClues(guessingState.currentBoardClues);

        // Display outside cards
        displayOutsideCards(guessingState.outsideCards);

        // Display guessed positions
        displayGuessedPositions(guessingState.guessedPositions, guessingState.correctlyPlacedPositions);

        // Update attempts info
        updateAttemptsInfo(guessingState.remainingAttempts);

        // Update confirm button state
        updateConfirmButtonState();

    } catch (error) {
        console.error('Error fetching guessing phase:', error);
        showGuessingStatusMessage('Failed to load guessing phase. Please refresh the page.', 'error');
    }
}

function displayClues(clues) {
    // Clear all clues first
    Object.values(clueDisplays).forEach(display => display.value = '');

    // Display each clue
    clues.forEach(clue => {
        const direction = clue.direction.toLowerCase();
        if (clueDisplays[direction]) {
            clueDisplays[direction].value = clue.text;
        }
    });
}

function displayOutsideCards(outsideCards) {
    leftOutsideCardsContainer.innerHTML = '';
    rightOutsideCardsContainer.innerHTML = '';

    outsideCards.forEach((card, index) => {
        const cardElement = createDraggableCard(card, index, true);

        // Distribute cards: 3 on left, 2 on right (for 5 total cards)
        // For any number of cards: put first 3 on left, rest on right
        if (index < 3) {
            leftOutsideCardsContainer.appendChild(cardElement);
        } else {
            rightOutsideCardsContainer.appendChild(cardElement);
        }
    });
}

function createDraggableCard(card, index, isOutside) {
    // Create wrapper container for card + controls
    const wrapper = document.createElement('div');

    // Animation conditionnelle : seulement si l'élément correspond à l'un des éléments de la dernière mise à jour
    // Note: index peut être un number (outside) ou un string (board position "TopLeft", etc.)
    const shouldAnimate = lastUpdatedElements.some(el => {
        if (el.isOutsidePoolUpdate && isOutside) return true;
        
        const indexMatch = String(el.index) === String(index);
        const outsideMatch = Boolean(el.isOutside) === Boolean(isOutside);
        return indexMatch && outsideMatch;
    });

    if (shouldAnimate) {
        console.log(`[DEBUG_LOG] Animating card at index: ${index}, isOutside: ${isOutside}`);
    }

    wrapper.className = `card-wrapper ${shouldAnimate ? 'card-fade-in' : ''}`;
    wrapper.style.position = 'relative';
    wrapper.style.width = '280px';
    wrapper.style.height = '280px';

    // Create card element
    const cardDiv = document.createElement('div');
    cardDiv.className = 'card-slot';
    cardDiv.draggable = !isSpectator;
    cardDiv.dataset.cardIndex = index;
    cardDiv.dataset.isOutside = isOutside;

    // Get rotation angle and apply to card-slot
    const rotationAngle = getRotationAngle(card.rotation);
    cardDiv.style.transform = `rotate(${rotationAngle}deg)`;

    cardDiv.innerHTML = `
        <div class="card-content">
            <div class="word top">${card.topWord}</div>
            <div class="word right">${card.rightWord}</div>
            <div class="word bottom">${card.bottomWord}</div>
            <div class="word left">${card.leftWord}</div>
        </div>
    `;

    // Create rotation controls outside the card
    const rotationControls = document.createElement('div');
    rotationControls.className = 'card-rotation-controls';
    rotationControls.style.display = 'none';
    rotationControls.innerHTML = `
        <button class="btn-rotate-card" data-direction="left">↶</button>
        <button class="btn-rotate-card" data-direction="right">↷</button>
    `;

    wrapper.appendChild(cardDiv);
    wrapper.appendChild(rotationControls);

    // Show rotation controls on hover
    if (!isSpectator) {
        wrapper.addEventListener('mouseenter', () => {
            rotationControls.style.display = 'flex';
        });

        wrapper.addEventListener('mouseleave', () => {
            rotationControls.style.display = 'none';
        });

        // Setup rotation button handlers
        const rotateButtons = rotationControls.querySelectorAll('.btn-rotate-card');
        rotateButtons.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const direction = btn.dataset.direction;
                if (isOutside) {
                    rotateOutsideCard(index, direction);
                } else {
                    rotateBoardCard(index, direction);
                }
            });
        });

        // Drag and drop handlers
        cardDiv.addEventListener('dragstart', handleDragStart);
        cardDiv.addEventListener('dragend', handleDragEnd);
    }

    return wrapper;
}

function displayGuessedPositions(guessedPositions, correctlyPlacedPositions) {
    const positions = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'];

    positions.forEach(position => {
        const dropZone = document.getElementById(`dropZone${position}`);
        dropZone.innerHTML = '';
        dropZone.className = 'drop-zone';

        const card = guessedPositions[position];

        // Check if position is correctly placed (locked)
        const isLocked = correctlyPlacedPositions.includes(position);

        if (isLocked) {
            dropZone.classList.add('locked');
        }

        if (card) {
            const cardElement = createDraggableCard(card, position, false);
            
            if (isLocked) {
                cardElement.classList.add('locked');
                cardElement.draggable = false;
            }

            dropZone.appendChild(cardElement);
        }

        // Setup drop zone handlers
        if (!isSpectator && !isLocked) {
            dropZone.addEventListener('dragover', handleDragOver);
            dropZone.addEventListener('drop', handleDrop);
            dropZone.addEventListener('dragleave', handleDragLeave);
        }
    });
}

let draggedCard = null;

function handleDragStart(e) {
    draggedCard = e.target;
    e.target.classList.add('dragging');
    e.dataTransfer.effectAllowed = 'move';
}

function handleDragEnd(e) {
    e.target.classList.remove('dragging');
    draggedCard = null;
}

function handleDragOver(e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    e.currentTarget.classList.add('drag-over');
}

function handleDragLeave(e) {
    e.currentTarget.classList.remove('drag-over');
}

async function handleDrop(e) {
    e.preventDefault();
    e.currentTarget.classList.remove('drag-over');

    if (!draggedCard) return;

    const dropZone = e.currentTarget;
    const targetPosition = dropZone.dataset.position;
    const isOutside = draggedCard.dataset.isOutside === 'true';

    if (!isOutside) {
        // Card is being moved from one board position to another - swap them
        const sourcePosition = draggedCard.dataset.cardIndex; // For board cards, cardIndex is the position string (e.g., "TopLeft")
        await swapBoardCards(sourcePosition, targetPosition);
        await fetchAndDisplayGuessingPhase(); // Refresh display
        return;
    }

    try {
        const cardIndex = parseInt(draggedCard.dataset.cardIndex);
        await placeCardOnBoard(cardIndex, targetPosition);
        await fetchAndDisplayGuessingPhase(); // Refresh display
    } catch (error) {
        console.error('Error placing card:', error);
        showGuessingStatusMessage('Failed to place card. Please try again.', 'error');
    }
}

async function placeCardOnBoard(outsideCardIndex, position) {
    const response = await fetch(`/api/games/${gameId}/place-guessing-card`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            playerId: playerId,
            outsideCardIndex: outsideCardIndex,
            position: position
        })
    });

    if (!response.ok) {
        throw new Error('Failed to place card');
    }
}

async function swapBoardCards(sourcePosition, targetPosition) {
    try {
        const response = await fetch(`/api/games/${gameId}/swap-guessing-cards`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId,
                position1: sourcePosition,
                position2: targetPosition
            })
        });

        if (!response.ok) {
            throw new Error('Failed to swap cards');
        }
    } catch (error) {
        console.error('Error swapping cards:', error);
        showGuessingStatusMessage('Failed to swap cards. Please try again.', 'error');
    }
}

async function rotateOutsideCard(index, direction) {
    await rotateCard(null, index, direction);
}

async function rotateBoardCard(position, direction) {
    await rotateCard(position, null, direction);
}

async function rotateCard(position, outsideCardIndex, direction) {
    try {
        const rotationDelta = direction === 'right' ? 90 : -90;

        // Find and animate the card element locally first
        let cardElement = null;
        if (position !== null) {
            // Rotating board card
            const dropZone = document.getElementById(`dropZone${position}`);
            if (dropZone) {
                const wrapper = dropZone.querySelector('.card-wrapper');
                if (wrapper) {
                    cardElement = wrapper.querySelector('.card-slot');
                }
            }
        } else if (outsideCardIndex !== null) {
            // Rotating outside card
            const wrappers = document.querySelectorAll('.card-wrapper');
            wrappers.forEach(wrapper => {
                const card = wrapper.querySelector('.card-slot');
                if (card && card.dataset.cardIndex == outsideCardIndex && card.dataset.isOutside === 'true') {
                    const currentTransform = card.style.transform || 'rotate(0deg)';
                    const currentAngle = parseInt(currentTransform.match(/rotate\((-?\d+)deg\)/)?.[1] || 0);
                    const newAngle = (currentAngle + rotationDelta + 360) % 360;
                    card.style.transform = `rotate(${newAngle}deg)`;
                }
            });
        }

        // Animate the card element
        if (cardElement) {
            const currentTransform = cardElement.style.transform || 'rotate(0deg)';
            const currentAngle = parseInt(currentTransform.match(/rotate\((-?\d+)deg\)/)?.[1] || 0);
            const newAngle = (currentAngle + rotationDelta + 360) % 360;
            cardElement.style.transform = `rotate(${newAngle}deg)`;
        }

        // Build request body
        const requestBody = {
            playerId: playerId,
            direction: direction
        };

        if (position !== null) {
            requestBody.position = position;
        } else if (outsideCardIndex !== null) {
            requestBody.outsideCardIndex = outsideCardIndex;
        }

        const response = await fetch(`/api/games/${gameId}/rotate-card`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestBody)
        });

        if (!response.ok) {
            throw new Error('Failed to rotate card');
        }

        // Wait for animation to complete before refreshing
        setTimeout(async () => {
            await fetchAndDisplayGuessingPhase();
        }, 500);
    } catch (error) {
        console.error('Error rotating card:', error);
        showGuessingStatusMessage('Failed to rotate card. Please try again.', 'error');
    }
}

function getRotationAngle(rotation) {
    const rotations = {
        'None': 0,
        'Right90': 90,
        'Right180': 180,
        'Right270': 270
    };
    return rotations[rotation] || 0;
}

// Board rotation functions
function setupRotationControls() {
    btnRotateLeft.addEventListener('click', () => rotateBoardLeft());
    btnRotateRight.addEventListener('click', () => rotateBoardRight());
}

function setupKeyboardShortcuts() {
    if (window.ShortcutManager) {
        window.ShortcutManager.register('ArrowLeft', () => rotateBoardLeft());
        window.ShortcutManager.register('ArrowRight', () => rotateBoardRight());
    }
}

function rotateBoardLeft() {
    cumulativeRotation -= 90;
    boardRotation = (cumulativeRotation % 360 + 360) % 360;
    updateBoardRotation();
}

function rotateBoardRight() {
    cumulativeRotation += 90;
    boardRotation = (cumulativeRotation % 360 + 360) % 360;
    updateBoardRotation();
}

function updateBoardRotation() {
    // Apply the cumulative rotation directly to the style for smooth animation
    cloverBoard.style.transform = `rotate(${cumulativeRotation}deg)`;
    rotationIndicator.textContent = `${boardRotation}°`;
}

// Confirm button
function setupConfirmButton() {
    btnConfirmBoard.addEventListener('click', handleConfirmButtonClick);
}

function handleConfirmButtonClick() {
    // Check if button is in "Move to next Board" mode
    if (btnConfirmBoard.dataset.mode === 'moveToNext') {
        moveToNextBoard();
    } else {
        confirmBoard();
    }
}

function updateConfirmButtonState() {
    if (isSpectator) {
        btnConfirmBoard.disabled = true;
        btnConfirmBoard.textContent = 'Confirm Board';
        btnConfirmBoard.dataset.mode = 'confirm';
        return;
    }

    // Check if board is validated (complete or no attempts left)
    const boardValidated = guessingState.remainingAttempts === 0 ||
                           guessingState.correctlyPlacedPositions.length === 4;

    if (boardValidated) {
        // Change button to "Move to next Board"
        btnConfirmBoard.textContent = 'Move to next Board';
        btnConfirmBoard.disabled = false;
        btnConfirmBoard.dataset.mode = 'moveToNext';
    } else {
        // Normal "Confirm Board" mode
        btnConfirmBoard.textContent = 'Confirm Board';
        btnConfirmBoard.dataset.mode = 'confirm';

        // Enable button only if all 4 positions are filled
        const positions = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'];
        const allFilled = positions.every(position => {
            return guessingState.guessedPositions[position] !== null;
        });

        btnConfirmBoard.disabled = !allFilled;
    }
}

async function confirmBoard() {
    try {
        btnConfirmBoard.disabled = true;
        btnConfirmBoard.textContent = 'Validating...';

        const response = await fetch(`/api/games/${gameId}/validate-guessing-board`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId
            })
        });

        if (!response.ok) {
            throw new Error('Failed to validate board');
        }

        const result = await response.json();

        // Show result message
        if (result.isComplete) {
            showGuessingStatusMessage('🎉 Perfect! All cards correctly placed!', 'success');
        } else {
            const correctCount = result.correctPositions.length;
            showGuessingStatusMessage(
                `${correctCount} card(s) correct. ${result.incorrectPositions.length} card(s) returned. ${result.remainingAttempts} attempts left.`,
                'info'
            );
        }

        // Refresh display to update button state
        await fetchAndDisplayGuessingPhase();

    } catch (error) {
        console.error('Error confirming board:', error);
        showGuessingStatusMessage('Failed to validate board. Please try again.', 'error');
        btnConfirmBoard.disabled = false;
        btnConfirmBoard.textContent = 'Confirm Board';
    }
}

async function moveToNextBoard() {
    try {
        btnConfirmBoard.disabled = true;
        btnConfirmBoard.textContent = 'Loading...';

        const response = await fetch(`/api/games/${gameId}/move-to-next-board`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || 'Failed to move to next board');
        }

        const result = await response.json();

        // Check if we're moving to Scoring phase
        if (result.phase === 'Scoring') {
            showGuessingStatusMessage('All boards completed! Moving to scoring...', 'success');
            setTimeout(() => {
                // Redirect to scoring page when implemented
                window.location.href = '/scoring.html';
            }, 2000);
        } else {
            // Moving to next board
            showGuessingStatusMessage('Loading next board...', 'info');
            // Refresh immediately to load the new board
            await fetchAndDisplayGuessingPhase();
        }

    } catch (error) {
        console.error('Error moving to next board:', error);
        showGuessingStatusMessage(error.message || 'Failed to move to next board. Please try again.', 'error');
        btnConfirmBoard.disabled = false;
        btnConfirmBoard.textContent = 'Move to next Board';
    }
}

function updateAttemptsInfo(remainingAttempts) {
    attemptsInfo.innerHTML = `Remaining attempts: <strong>${remainingAttempts}</strong>`;
}

function showGuessingStatusMessage(message, type = 'info') {
    guessingStatusMessage.textContent = message;
    guessingStatusMessage.className = `status-message ${type}`;
    guessingStatusMessage.style.display = 'block';

    setTimeout(() => {
        guessingStatusMessage.style.display = 'none';
    }, 4000);
}

})();
