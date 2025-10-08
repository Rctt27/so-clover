// State management
let currentGameId = null;
let playerName = '';

// DOM elements
const playerNameInput = document.getElementById('playerName');
const charCountDisplay = document.getElementById('charCount');
const displayNameElement = document.getElementById('displayName');
const playerInfoDiv = document.getElementById('playerInfo');
const createGameBtn = document.getElementById('createGameBtn');
const cancelGameBtn = document.getElementById('cancelGameBtn');
const copyBtn = document.getElementById('copyBtn');
const noGameState = document.getElementById('noGameState');
const activeGameState = document.getElementById('activeGameState');
const gameIdDisplay = document.getElementById('gameIdDisplay');
const statusMessage = document.getElementById('statusMessage');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();
    loadState();
});

function setupEventListeners() {
    // Player name input
    playerNameInput.addEventListener('input', handlePlayerNameInput);

    // Game management buttons
    createGameBtn.addEventListener('click', handleCreateGame);
    cancelGameBtn.addEventListener('click', handleCancelGame);
    copyBtn.addEventListener('click', handleCopyGameId);
}

function handlePlayerNameInput(e) {
    const value = e.target.value.trim();
    charCountDisplay.textContent = value.length;

    if (value.length > 0) {
        playerName = value;
        displayNameElement.textContent = playerName;
        playerInfoDiv.style.display = 'block';
        createGameBtn.disabled = false;
        document.querySelector('.hint').textContent = 'Ready to create a game!';
        saveState();
    } else {
        playerName = '';
        playerInfoDiv.style.display = 'none';
        if (!currentGameId) {
            createGameBtn.disabled = true;
            document.querySelector('.hint').textContent = 'Please enter your player name first';
        }
        saveState();
    }
}

async function handleCreateGame() {
    if (!playerName) {
        showStatusMessage('Please enter your player name first', 'error');
        return;
    }

    createGameBtn.disabled = true;
    createGameBtn.innerHTML = '<span class="btn-icon">⏳</span> Creating...';

    try {
        const response = await fetch('/api/games', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('Failed to create game');
        }

        const data = await response.json();
        currentGameId = data.gameId;

        showGameCreated();
        showStatusMessage('Game created successfully!', 'success');
        saveState();

    } catch (error) {
        console.error('Error creating game:', error);
        showStatusMessage('Failed to create game. Please try again.', 'error');
        createGameBtn.disabled = false;
        createGameBtn.innerHTML = '<span class="btn-icon">🎮</span> Create Game';
    }
}

async function handleCancelGame() {
    if (!currentGameId) {
        return;
    }

    if (!confirm('Are you sure you want to cancel this game? All game data will be deleted.')) {
        return;
    }

    cancelGameBtn.disabled = true;
    cancelGameBtn.innerHTML = '<span class="btn-icon">⏳</span> Canceling...';

    try {
        const response = await fetch(`/api/games/${currentGameId}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            throw new Error('Failed to cancel game');
        }

        currentGameId = null;
        showNoGame();
        showStatusMessage('Game canceled and deleted successfully!', 'info');
        saveState();

    } catch (error) {
        console.error('Error canceling game:', error);
        showStatusMessage('Failed to cancel game. Please try again.', 'error');
        cancelGameBtn.disabled = false;
        cancelGameBtn.innerHTML = '<span class="btn-icon">❌</span> Cancel Game';
    }
}

function handleCopyGameId() {
    if (!currentGameId) {
        return;
    }

    navigator.clipboard.writeText(currentGameId).then(() => {
        const originalContent = copyBtn.textContent;
        copyBtn.textContent = '✓';
        copyBtn.style.background = '#2e7d32';

        setTimeout(() => {
            copyBtn.textContent = originalContent;
            copyBtn.style.background = '';
        }, 1500);

        showStatusMessage('Game ID copied to clipboard!', 'success');
    }).catch(err => {
        console.error('Failed to copy:', err);
        showStatusMessage('Failed to copy Game ID', 'error');
    });
}

function showGameCreated() {
    noGameState.style.display = 'none';
    activeGameState.style.display = 'block';
    gameIdDisplay.textContent = currentGameId;
}

function showNoGame() {
    noGameState.style.display = 'block';
    activeGameState.style.display = 'none';
    createGameBtn.disabled = !playerName;
    createGameBtn.innerHTML = '<span class="btn-icon">🎮</span> Create Game';
    cancelGameBtn.innerHTML = '<span class="btn-icon">❌</span> Cancel Game';
}

function showStatusMessage(message, type = 'info') {
    statusMessage.textContent = message;
    statusMessage.className = `status-message ${type}`;
    statusMessage.style.display = 'block';

    setTimeout(() => {
        statusMessage.style.display = 'none';
    }, 4000);
}

function saveState() {
    const state = {
        playerName: playerName,
        currentGameId: currentGameId
    };
    localStorage.setItem('soCloverState', JSON.stringify(state));
}

function loadState() {
    const savedState = localStorage.getItem('soCloverState');
    if (savedState) {
        try {
            const state = JSON.parse(savedState);

            if (state.playerName) {
                playerName = state.playerName;
                playerNameInput.value = playerName;
                charCountDisplay.textContent = playerName.length;
                displayNameElement.textContent = playerName;
                playerInfoDiv.style.display = 'block';
            }

            if (state.currentGameId) {
                currentGameId = state.currentGameId;
                showGameCreated();
            }

            if (playerName && !currentGameId) {
                createGameBtn.disabled = false;
                document.querySelector('.hint').textContent = 'Ready to create a game!';
            }
        } catch (error) {
            console.error('Error loading state:', error);
        }
    }
}
