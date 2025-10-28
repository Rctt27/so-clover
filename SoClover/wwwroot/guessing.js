// Guessing Phase logic
let gameId = null;
let playerName = '';
let playerId = null;
let boardRotation = 0;
let guessingState = null;
let isSpectator = false;
let pollingInterval = null;

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
document.addEventListener('DOMContentLoaded', () => {
    loadGuessingState();
    fetchAndDisplayGuessingPhase();
    setupRotationControls();
    setupConfirmButton();
    startPolling();
});

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

        if (gameState.phase !== 'Guessing') {
            showGuessingStatusMessage('Not in guessing phase', 'error');
            return;
        }

        if (!gameState.guessingState) {
            showGuessingStatusMessage('No guessing state available', 'error');
            return;
        }

        guessingState = gameState.guessingState;

        // Check if player is spectator (board owner)
        isSpectator = guessingState.currentBoardOwnerId === playerId;

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
    const cardDiv = document.createElement('div');
    cardDiv.className = 'card-slot';
    cardDiv.draggable = !isSpectator;
    cardDiv.dataset.cardIndex = index;
    cardDiv.dataset.isOutside = isOutside;

    // Get rotation angle and apply to card-slot
    const rotationAngle = getRotationAngle(card.rotation);
    cardDiv.style.transform = `rotate(${rotationAngle}deg)`;

    cardDiv.innerHTML = `
        <div class="card-rotation-controls" style="display: none;">
            <button class="btn-rotate-card" data-direction="left">↶</button>
            <button class="btn-rotate-card" data-direction="right">↷</button>
        </div>
        <div class="card-content">
            <div class="word top">${card.topWord}</div>
            <div class="word right">${card.rightWord}</div>
            <div class="word bottom">${card.bottomWord}</div>
            <div class="word left">${card.leftWord}</div>
        </div>
    `;

    // Show rotation controls on hover
    if (!isSpectator) {
        cardDiv.addEventListener('mouseenter', () => {
            cardDiv.querySelector('.card-rotation-controls').style.display = 'flex';
        });

        cardDiv.addEventListener('mouseleave', () => {
            cardDiv.querySelector('.card-rotation-controls').style.display = 'none';
        });

        // Setup rotation button handlers
        const rotateButtons = cardDiv.querySelectorAll('.btn-rotate-card');
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

    return cardDiv;
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
    const position = dropZone.dataset.position;
    const cardIndex = parseInt(draggedCard.dataset.cardIndex);
    const isOutside = draggedCard.dataset.isOutside === 'true';

    if (!isOutside) {
        // Card is being moved from one board position to another
        showGuessingStatusMessage('Cannot move cards between board positions. Remove and place again.', 'error');
        return;
    }

    try {
        await placeCardOnBoard(cardIndex, position);
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

async function rotateOutsideCard(index, direction) {
    try {
        // Find the card element and animate it locally first
        const cardElements = document.querySelectorAll(`.card-slot[data-card-index="${index}"][data-is-outside="true"]`);
        const rotationDelta = direction === 'right' ? 90 : -90;
        
        cardElements.forEach(cardElement => {
            // Get current rotation from card-slot itself
            const currentTransform = cardElement.style.transform || 'rotate(0deg)';
            const currentAngle = parseInt(currentTransform.match(/rotate\((-?\d+)deg\)/)?.[1] || 0);
            // Calculate new angle based on direction
            const newAngle = (currentAngle + rotationDelta + 360) % 360;
            cardElement.style.transform = `rotate(${newAngle}deg)`;
        });

        const response = await fetch(`/api/games/${gameId}/rotate-outside-card`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId,
                outsideCardIndex: index,
                direction: direction
            })
        });

        if (!response.ok) {
            throw new Error('Failed to rotate outside card');
        }

        // Wait for animation to complete before refreshing
        setTimeout(async () => {
            await fetchAndDisplayGuessingPhase();
        }, 500);
    } catch (error) {
        console.error('Error rotating outside card:', error);
        showGuessingStatusMessage('Failed to rotate card. Please try again.', 'error');
    }
}

async function rotateBoardCard(position, direction) {
    try {
        // Find the card element in the drop zone and animate it locally first
        const dropZone = document.getElementById(`dropZone${position}`);
        const rotationDelta = direction === 'right' ? 90 : -90;
        
        if (dropZone) {
            const cardElement = dropZone.querySelector('.card-slot');
            if (cardElement) {
                // Get current rotation from card-slot itself
                const currentTransform = cardElement.style.transform || 'rotate(0deg)';
                const currentAngle = parseInt(currentTransform.match(/rotate\((-?\d+)deg\)/)?.[1] || 0);
                // Calculate new angle based on direction
                const newAngle = (currentAngle + rotationDelta + 360) % 360;
                cardElement.style.transform = `rotate(${newAngle}deg)`;
            }
        }

        const response = await fetch(`/api/games/${gameId}/rotate-board-card`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId,
                position: position,
                direction: direction
            })
        });

        if (!response.ok) {
            throw new Error('Failed to rotate board card');
        }

        // Wait for animation to complete before refreshing
        setTimeout(async () => {
            await fetchAndDisplayGuessingPhase();
        }, 500);
    } catch (error) {
        console.error('Error rotating board card:', error);
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

function rotateBoardLeft() {
    boardRotation = (boardRotation - 90 + 360) % 360;
    updateBoardRotation();
}

function rotateBoardRight() {
    boardRotation = (boardRotation + 90) % 360;
    updateBoardRotation();
}

function updateBoardRotation() {
    const rotationClass = `rotate-${boardRotation}`;
    cloverBoard.className = `clover-board ${rotationClass}`;
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

// Polling for real-time updates
function startPolling() {
    pollingInterval = setInterval(async () => {
        await fetchAndDisplayGuessingPhase();
    }, 1000); // Poll every 1 second
}

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    if (pollingInterval) {
        clearInterval(pollingInterval);
    }
});
