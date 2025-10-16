// Lobby page logic
let gameId = null;
let playerName = '';
let playerId = null;
let isCreator = false;
let pollInterval = null;

// DOM elements
const lobbyGameIdDisplay = document.getElementById('lobbyGameId');
const lobbyPlayerNameDisplay = document.getElementById('lobbyPlayerName');
const playersList = document.getElementById('playersList');
const playerCountDisplay = document.getElementById('playerCount');
const startGameBtn = document.getElementById('startGameBtn');
const lobbyCancelBtn = document.getElementById('lobbyCancelBtn');
const lobbyLeaveBtn = document.getElementById('lobbyLeaveBtn');
const lobbyCopyBtn = document.getElementById('lobbyeCopyBtn');
const lobbyStatusMessage = document.getElementById('lobbyStatusMessage');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    console.log('[Lobby] DOMContentLoaded - Initializing...');
    loadLobbyState();
    setupEventListeners();
    console.log('[Lobby] About to start polling, gameId:', gameId);
    startPollingGameState();
});

function setupEventListeners() {
    startGameBtn.addEventListener('click', handleStartGame);
    lobbyCancelBtn.addEventListener('click', handleCancelGame);
    lobbyLeaveBtn.addEventListener('click', handleLeaveGame);
    lobbyCopyBtn.addEventListener('click', handleCopyGameId);
}

function loadLobbyState() {
    console.log('[Lobby] Loading lobby state...');
    const savedState = sessionStorage.getItem('soCloverState');
    console.log('[Lobby] Saved state:', savedState);

    if (!savedState) {
        console.log('[Lobby] No saved state found!');
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

        console.log('[Lobby] State loaded - gameId:', gameId, 'playerName:', playerName, 'playerId:', playerId, 'isCreator:', isCreator);

        if (!gameId || !playerName || !playerId) {
            console.log('[Lobby] Invalid state - missing required fields');
            throw new Error('Invalid state');
        }

        lobbyGameIdDisplay.textContent = gameId;
        lobbyPlayerNameDisplay.textContent = playerName;

        // Show/hide buttons based on creator status
        if (isCreator) {
            startGameBtn.style.display = 'inline-flex';
            lobbyCancelBtn.style.display = 'inline-flex';
            lobbyLeaveBtn.style.display = 'none';
        } else {
            startGameBtn.style.display = 'none';
            lobbyCancelBtn.style.display = 'none';
            lobbyLeaveBtn.style.display = 'inline-flex';
        }

        // Fetch and display all players
        fetchAndUpdatePlayers();

    } catch (error) {
        console.error('Error loading lobby state:', error);
        showLobbyStatusMessage('Failed to load game data. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
    }
}

async function fetchAndUpdatePlayers() {
    try {
        console.log('[Lobby] Polling game state...');
        const response = await fetch(`/api/games/${gameId}/state?includeSecrets=false`);

        if (!response.ok) {
            if (response.status === 404) {
                showLobbyStatusMessage('Game no longer exists. Redirecting...', 'error');
                sessionStorage.removeItem('soCloverState');
                setTimeout(() => window.location.href = '/index.html', 2000);
                return;
            }
            throw new Error('Failed to fetch game state');
        }

        const gameState = await response.json();
        console.log('[Lobby] Game state received:', gameState);
        console.log('[Lobby] Number of players:', gameState.players?.length);
        updatePlayersList(gameState.players);

    } catch (error) {
        console.error('Error fetching players:', error);
    }
}

function updatePlayersList(players) {
    console.log('[Lobby] updatePlayersList called with:', players);

    if (!players || players.length === 0) {
        console.log('[Lobby] No players to display');
        return;
    }

    playersList.innerHTML = '';

    players.forEach(player => {
        console.log('[Lobby] Adding player:', player.name, 'ID:', player.playerId);
        const playerItem = document.createElement('div');
        const isCurrentPlayer = player.playerId === playerId;
        const playerIsCreator = players[0].playerId === player.playerId; // First player is creator

        playerItem.className = `player-item ${playerIsCreator ? 'creator' : ''}`;
        playerItem.innerHTML = `
            <div class="player-info">
                <span class="player-icon">👤</span>
                <strong>${player.name}${isCurrentPlayer ? ' (You)' : ''}</strong>
                ${playerIsCreator ? '<span class="player-badge">Creator</span>' : ''}
            </div>
        `;
        playersList.appendChild(playerItem);
    });

    console.log('[Lobby] Total players displayed:', players.length);
    playerCountDisplay.textContent = `(${players.length})`;
}

function startPollingGameState() {
    console.log('[Lobby] startPollingGameState called with gameId:', gameId);

    if (!gameId) {
        console.log('[Lobby] Cannot start polling - no gameId available');
        return;
    }

    console.log('[Lobby] Starting polling interval...');
    // Poll every 5 seconds to update players list
    pollInterval = setInterval(() => {
        console.log('[Lobby] Polling tick - calling fetchAndUpdatePlayers');
        fetchAndUpdatePlayers();
    }, 5000);

    // Clear interval when leaving page
    window.addEventListener('beforeunload', () => {
        if (pollInterval) {
            clearInterval(pollInterval);
        }
    });

    console.log('[Lobby] Polling started successfully');
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

    // Stop polling
    if (pollInterval) {
        clearInterval(pollInterval);
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

function handleLeaveGame() {
    if (!confirm('Are you sure you want to leave this game?')) {
        return;
    }

    // Stop polling
    if (pollInterval) {
        clearInterval(pollInterval);
    }

    // Clear session storage and redirect to index
    sessionStorage.removeItem('soCloverState');
    showLobbyStatusMessage('You left the game. Redirecting...', 'info');

    setTimeout(() => {
        window.location.href = '/index.html';
    }, 1000);
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
