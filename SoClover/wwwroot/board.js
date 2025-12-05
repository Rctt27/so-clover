// Scope isolation to avoid global `let` collisions with app.js (e.g. `playerName`)
(function(){
'use strict';

// Board page logic
let gameId = null;
let playerName = '';
let playerId = null;
let boardRotation = 0; // Current board rotation: 0, 90, 180, or 270

// Track saved clues
const savedClues = {
    top: false,
    right: false,
    bottom: false,
    left: false
};

// DOM elements
const boardPlayerNameDisplay = document.getElementById('boardPlayerName');
const gamePhaseDisplay = document.getElementById('gamePhase');
const boardStatusMessage = document.getElementById('boardStatusMessage');
const cloverBoard = document.getElementById('cloverBoard');
const btnRotateLeft = document.getElementById('btnRotateLeft');
const btnRotateRight = document.getElementById('btnRotateRight');
const rotationIndicator = document.getElementById('rotationIndicator');
const btnSubmitBoard = document.getElementById('btnSubmitBoard');

// Clue text inputs
const clueInputs = {
    top: document.getElementById('clueTop'),
    right: document.getElementById('clueRight'),
    bottom: document.getElementById('clueBottom'),
    left: document.getElementById('clueLeft')
};

// Card word elements
const cardWords = {
    topLeft: {
        top: document.getElementById('tlTop'),
        right: document.getElementById('tlRight'),
        bottom: document.getElementById('tlBottom'),
        left: document.getElementById('tlLeft')
    },
    topRight: {
        top: document.getElementById('trTop'),
        right: document.getElementById('trRight'),
        bottom: document.getElementById('trBottom'),
        left: document.getElementById('trLeft')
    },
    bottomRight: {
        top: document.getElementById('brTop'),
        right: document.getElementById('brRight'),
        bottom: document.getElementById('brBottom'),
        left: document.getElementById('brLeft')
    },
    bottomLeft: {
        top: document.getElementById('blTop'),
        right: document.getElementById('blRight'),
        bottom: document.getElementById('blBottom'),
        left: document.getElementById('blLeft')
    }
};

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    try { if (typeof initPhaseTimer === 'function') initPhaseTimer(); } catch {}
    loadBoardState();
    // Hook SignalR (centralized in app.js)
    try {
        await (window.realTime?.ensureConnected?.());
        await (window.realTime?.joinCurrentGame?.(gameId, playerId));
    } catch {}
    fetchAndDisplayBoard();
    setupRotationControls();
    setupClueInputs();
    setupSubmitButton();

    // Subscribe to server pushes
    let offUpdated = () => {};
    let offNotif = () => {};
    try {
        offUpdated = window.realTime?.onGameStateUpdated?.(async (_) => {
            try { await fetchAndDisplayBoard(); } catch {}
        }) || (() => {});
        offNotif = window.realTime?.onServerNotification?.((n) => {
            if (!n) return;
            try { showBoardStatusMessage(n.message || 'Server notification', n.type === 'warning' ? 'warning' : 'info'); } catch {}
        }) || (() => {});
    } catch {}

    window.addEventListener('beforeunload', () => { try { offUpdated(); offNotif(); } catch {} });
});

function loadBoardState() {
    const savedState = sessionStorage.getItem('soCloverState');
    if (!savedState) {
        showBoardStatusMessage('No game session found. Redirecting...', 'error');
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

        boardPlayerNameDisplay.textContent = playerName;

    } catch (error) {
        console.error('Error loading board state:', error);
        showBoardStatusMessage('Failed to load game data. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
    }
}

async function fetchAndDisplayBoard() {
    try {
        const response = await fetch(`/api/games/${gameId}/state?playerId=${playerId}&includeSecrets=true`);

        if (!response.ok) {
            throw new Error('Failed to fetch game state');
        }

        const gameState = await response.json();
        console.log('🎮 Game State:', gameState);

        // Update phase countdown timer from server
        try { if (typeof setPhaseDeadline === 'function') setPhaseDeadline(gameState.phaseEndsAtUtc || null); } catch {}

        // Check if phase has changed to Guessing
        if (gameState.phase === 'Guessing') {
            console.log('[Board] Phase changed to Guessing! Redirecting...');
            showBoardStatusMessage('All boards submitted! Starting guessing phase...', 'success');

            // Redirect to guessing page
            setTimeout(() => {
                window.location.href = '/guessing.html';
            }, 1500);
            return;
        }

        // Update game phase
        gamePhaseDisplay.textContent = gameState.phase;

        // Find the current player's data
        const currentPlayer = gameState.players.find(p => p.playerId === playerId);

        if (!currentPlayer) {
            throw new Error('Player not found in game state');
        }

        console.log('👤 Current Player Board:', currentPlayer.board);

        // Display the board cards
        displayBoard(currentPlayer.board);

        // Load existing clues
        loadClues(currentPlayer.board);

    } catch (error) {
        console.error('Error fetching board:', error);
        showBoardStatusMessage('Failed to load your board. Please refresh the page.', 'error');
    }
}


function displayBoard(board) {
    console.log('🎴 Displaying board - Top:', board.top);
    console.log('🎴 Displaying board - Right:', board.right);
    console.log('🎴 Displaying board - Bottom:', board.bottom);
    console.log('🎴 Displaying board - Left:', board.left);

    // Top card (from Top direction)
    if (board.top.hasCard) {
        const topWords = extractWordsFromDirection(board.top);
        console.log('📝 Top words:', topWords);
        cardWords.topLeft.top.textContent = topWords[0] || '';
        cardWords.topLeft.right.textContent = topWords[1] || '';
        cardWords.topLeft.bottom.textContent = topWords[2] || '';
        cardWords.topLeft.left.textContent = topWords[3] || '';
    }

    // Right card (from Right direction)
    if (board.right.hasCard) {
        const rightWords = extractWordsFromDirection(board.right);
        console.log('📝 Right words:', rightWords);
        cardWords.topRight.top.textContent = rightWords[0] || '';
        cardWords.topRight.right.textContent = rightWords[1] || '';
        cardWords.topRight.bottom.textContent = rightWords[2] || '';
        cardWords.topRight.left.textContent = rightWords[3] || '';
    }

    // Bottom card (from Bottom direction)
    if (board.bottom.hasCard) {
        const bottomWords = extractWordsFromDirection(board.bottom);
        console.log('📝 Bottom words:', bottomWords);
        cardWords.bottomRight.top.textContent = bottomWords[0] || '';
        cardWords.bottomRight.right.textContent = bottomWords[1] || '';
        cardWords.bottomRight.bottom.textContent = bottomWords[2] || '';
        cardWords.bottomRight.left.textContent = bottomWords[3] || '';
    }

    // Left card (from Left direction)
    if (board.left.hasCard) {
        const leftWords = extractWordsFromDirection(board.left);
        console.log('📝 Left words:', leftWords);
        cardWords.bottomLeft.top.textContent = leftWords[0] || '';
        cardWords.bottomLeft.right.textContent = leftWords[1] || '';
        cardWords.bottomLeft.bottom.textContent = leftWords[2] || '';
        cardWords.bottomLeft.left.textContent = leftWords[3] || '';
    }
}

function extractWordsFromDirection(directionState) {
    console.log('🔍 Extracting words from directionState:', directionState);
    console.log('🔍 directionState.card:', directionState.card);

    // If we have the full card data, extract all 4 words
    if (directionState.card) {
        const words = [
            directionState.card.topWord,
            directionState.card.rightWord,
            directionState.card.bottomWord,
            directionState.card.leftWord
        ];
        console.log('✅ Returning words from card:', words);
        return words;
    }

    // Fallback: if no card data available
    console.log('⚠️ No card data available, returning empty strings');
    return ['', '', '', ''];
}

function showBoardStatusMessage(message, type = 'info') {
    boardStatusMessage.textContent = message;
    boardStatusMessage.className = `status-message ${type}`;
    boardStatusMessage.style.display = 'block';

    setTimeout(() => {
        boardStatusMessage.style.display = 'none';
    }, 4000);
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
    // Remove all rotation classes
    cloverBoard.classList.remove('rotate-0', 'rotate-90', 'rotate-180', 'rotate-270');

    // Add the current rotation class
    cloverBoard.classList.add(`rotate-${boardRotation}`);

    // Update rotation indicator
    rotationIndicator.textContent = `${boardRotation}°`;
}

// Clue input functions
function setupClueInputs() {
    // Add event listeners for auto-save on input change
    Object.keys(clueInputs).forEach(direction => {
        clueInputs[direction].addEventListener('blur', () => saveClue(direction));
        clueInputs[direction].addEventListener('input', (e) => {
            // Visual feedback when typing
            if (e.target.value.trim()) {
                e.target.style.borderColor = '#2196F3';
            } else {
                e.target.style.borderColor = '#4CAF50';
            }
        });
    });
}

async function saveClue(direction) {
    const clueText = clueInputs[direction].value.trim();

    if (!clueText) {
        return; // Don't save empty clues
    }

    try {
        // Convert direction to match API expected format (capitalize first letter)
        const apiDirection = direction.charAt(0).toUpperCase() + direction.slice(1);

        const response = await fetch(`/api/games/${gameId}/clues`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId,
                direction: apiDirection,
                clueText: clueText
            })
        });

        if (!response.ok) {
            throw new Error('Failed to save clue');
        }

        // Visual feedback for successful save
        clueInputs[direction].style.borderColor = '#27ae60';
        clueInputs[direction].style.backgroundColor = '#f0fff4';

        // Mark clue as saved
        savedClues[direction] = true;
        updateSubmitButtonState();

        console.log(`✅ Clue saved for ${direction}: ${clueText}`);

    } catch (error) {
        console.error(`Error saving clue for ${direction}:`, error);
        showBoardStatusMessage(`Failed to save ${direction} clue. Please try again.`, 'error');

        // Visual feedback for error
        clueInputs[direction].style.borderColor = '#f44336';
    }
}

function loadClues(board) {
    // Load existing clues from board data if available
    if (board.topClue?.text) {
        clueInputs.top.value = board.topClue.text;
        clueInputs.top.style.borderColor = '#27ae60';
        clueInputs.top.style.backgroundColor = '#f0fff4';
        savedClues.top = true;
    }
    if (board.rightClue?.text) {
        clueInputs.right.value = board.rightClue.text;
        clueInputs.right.style.borderColor = '#27ae60';
        clueInputs.right.style.backgroundColor = '#f0fff4';
        savedClues.right = true;
    }
    if (board.bottomClue?.text) {
        clueInputs.bottom.value = board.bottomClue.text;
        clueInputs.bottom.style.borderColor = '#27ae60';
        clueInputs.bottom.style.backgroundColor = '#f0fff4';
        savedClues.bottom = true;
    }
    if (board.leftClue?.text) {
        clueInputs.left.value = board.leftClue.text;
        clueInputs.left.style.borderColor = '#27ae60';
        clueInputs.left.style.backgroundColor = '#f0fff4';
        savedClues.left = true;
    }

    // Update submit button state after loading clues
    updateSubmitButtonState();
}

// Submit board functions
function setupSubmitButton() {
    btnSubmitBoard.addEventListener('click', submitBoard);
}

function updateSubmitButtonState() {
    const allCluesSaved = savedClues.top && savedClues.right && savedClues.bottom && savedClues.left;
    btnSubmitBoard.disabled = !allCluesSaved;
}

async function submitBoard() {
    try {
        btnSubmitBoard.disabled = true;
        btnSubmitBoard.textContent = 'Submitting...';

        const response = await fetch(`/api/games/${gameId}/submit-board`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId
            })
        });

        if (!response.ok) {
            throw new Error('Failed to submit board');
        }

        showBoardStatusMessage('Board submitted successfully!', 'success');
        btnSubmitBoard.textContent = 'Board Submitted ✓';
        btnSubmitBoard.style.backgroundColor = '#27ae60';

        // Disable all clue inputs after submission
        Object.values(clueInputs).forEach(input => {
            input.disabled = true;
        });

    } catch (error) {
        console.error('Error submitting board:', error);
        showBoardStatusMessage('Failed to submit board. Please try again.', 'error');
        btnSubmitBoard.disabled = false;
        btnSubmitBoard.textContent = 'Submit Board';
    }
}

})();
