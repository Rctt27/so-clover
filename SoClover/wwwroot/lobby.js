// Lobby page logic
let gameId = null;
let playerName = '';
let playerId = null;
let isCreator = false;

// DOM elements
const lobbyGameIdDisplay = document.getElementById('lobbyGameId');
const lobbyPlayerNameDisplay = document.getElementById('lobbyPlayerName');
const playersList = document.getElementById('playersList');
const playerCountDisplay = document.getElementById('playerCount');
const startGameBtn = document.getElementById('startGameBtn');
const lobbyCancelBtn = document.getElementById('lobbyCancelBtn');
const lobbyCopyBtn = document.getElementById('lobbyeCopyBtn');
const lobbyStatusMessage = document.getElementById('lobbyStatusMessage');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadLobbyState();
    setupEventListeners();
});

function setupEventListeners() {
    startGameBtn.addEventListener('click', handleStartGame);
    lobbyCancelBtn.addEventListener('click', handleCancelGame);
    lobbyCopyBtn.addEventListener('click', handleCopyGameId);
}

function loadLobbyState() {
    const savedState = sessionStorage.getItem('soCloverState');
    if (!savedState) {
        showLobbyStatusMessage('No game session found. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
        return;
    }

    try {
        const state = JSON.parse(savedState);
        gameId = state.currentGameId;
        playerName = state.playerName;
        playerId = state.playerId;
        isCreator = state.isCreator || false;

        if (!gameId || !playerName || !playerId) {
            throw new Error('Invalid state');
        }

        lobbyGameIdDisplay.textContent = gameId;
        lobbyPlayerNameDisplay.textContent = playerName;

        // Show/hide start button based on creator status
        if (isCreator) {
            startGameBtn.style.display = 'inline-flex';
        } else {
            startGameBtn.style.display = 'none';
        }

        // Display current player
        updatePlayersList();

    } catch (error) {
        console.error('Error loading lobby state:', error);
        showLobbyStatusMessage('Failed to load game data. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
    }
}

function updatePlayersList() {
    playersList.innerHTML = '';

    const playerItem = document.createElement('div');
    playerItem.className = `player-item ${isCreator ? 'creator' : ''}`;
    playerItem.innerHTML = `
        <div class="player-info">
            <span class="player-icon">👤</span>
            <strong>${playerName}</strong>
            ${isCreator ? '<span class="player-badge">Creator</span>' : ''}
        </div>
    `;
    playersList.appendChild(playerItem);

    playerCountDisplay.textContent = `(1)`;
}

async function handleStartGame() {
    if (!isCreator) {
        showLobbyStatusMessage('Only the game creator can start the game', 'error');
        return;
    }

    startGameBtn.disabled = true;
    startGameBtn.innerHTML = '<span class="btn-icon">⏳</span> Starting...';

    try {
        const response = await fetch(`/api/games/${gameId}/start`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('Failed to start game');
        }

        showLobbyStatusMessage('Game starting! Redirecting to your board...', 'success');

        // Navigate to board page after a short delay
        setTimeout(() => {
            window.location.href = '/board.html';
        }, 1500);

    } catch (error) {
        console.error('Error starting game:', error);
        showLobbyStatusMessage('Failed to start game. Please try again.', 'error');
        startGameBtn.disabled = false;
        startGameBtn.innerHTML = '<span class="btn-icon">🚀</span> Start Game';
    }
}

async function handleCancelGame() {
    if (!confirm('Are you sure you want to cancel this game? All game data will be deleted.')) {
        return;
    }

    lobbyCancelBtn.disabled = true;
    lobbyCancelBtn.innerHTML = '<span class="btn-icon">⏳</span> Canceling...';

    try {
        const response = await fetch(`/api/games/${gameId}`, {
            method: 'DELETE'
        });

        if (!response.ok && response.status !== 404) {
            throw new Error('Failed to cancel game');
        }

        sessionStorage.removeItem('soCloverState');
        showLobbyStatusMessage('Game canceled successfully! Redirecting...', 'info');

        setTimeout(() => {
            window.location.href = '/index.html';
        }, 1500);

    } catch (error) {
        console.error('Error canceling game:', error);
        showLobbyStatusMessage('Failed to cancel game. Please try again.', 'error');
        lobbyCancelBtn.disabled = false;
        lobbyCancelBtn.innerHTML = '<span class="btn-icon">❌</span> Cancel Game';
    }
}

function handleCopyGameId() {
    navigator.clipboard.writeText(gameId).then(() => {
        const originalContent = lobbyCopyBtn.textContent;
        lobbyCopyBtn.textContent = '✓';
        lobbyCopyBtn.style.background = '#2e7d32';

        setTimeout(() => {
            lobbyCopyBtn.textContent = originalContent;
            lobbyCopyBtn.style.background = '';
        }, 1500);

        showLobbyStatusMessage('Game ID copied to clipboard!', 'success');
    }).catch(err => {
        console.error('Failed to copy:', err);
        showLobbyStatusMessage('Failed to copy Game ID', 'error');
    });
}

function showLobbyStatusMessage(message, type = 'info') {
    lobbyStatusMessage.textContent = message;
    lobbyStatusMessage.className = `status-message ${type}`;
    lobbyStatusMessage.style.display = 'block';

    setTimeout(() => {
        lobbyStatusMessage.style.display = 'none';
    }, 4000);
}
