// Lobby page logic (scoped to avoid leaking globals)
(function () {
    'use strict';
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
const lobbyLeaveBtn = document.getElementById('lobbyLeaveBtn');
const lobbyCopyBtn = document.getElementById('lobbyeCopyBtn');
const lobbyStatusMessage = document.getElementById('lobbyStatusMessage');
const gameSettingsSection = document.getElementById('gameSettingsSection');
const languageSelector = document.getElementById('languageSelector');
const cluesDurationInput = document.getElementById('cluesDuration');
const guessDurationInput = document.getElementById('guessDuration');

// Game settings
let gameSettings = {
    language: 'Français',
    cluesDuration: 300,
    guessDuration: 300
};

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    console.log('[Lobby] DOMContentLoaded - Initializing...');
    try { if (typeof initPhaseTimer === 'function') initPhaseTimer(); } catch {}
    setupEventListeners();
    loadLobbyState();

    // Hook SignalR (centralized in app.js)
    try {
        await (window.realTime?.ensureConnected?.());
        // Join after state is loaded (gameId/playerId set in loadLobbyState)
        // Use a microtask to ensure state variables are set
        queueMicrotask(async () => {
            try { await (window.realTime?.joinCurrentGame?.(gameId, playerId)); } catch {}
        });

        // Subscribe to updates: when any server-side change occurs, refresh players list
        const offUpdated = window.realTime?.onGameStateUpdated?.(() => {
            try { fetchAndUpdatePlayers(); } catch {}
        }) || (() => {});

        const offNotif = window.realTime?.onServerNotification?.((n) => {
            if (!n) return;
            try { showLobbyStatusMessage(n.message || 'Server notification', n.type === 'warning' ? 'warning' : 'info'); } catch {}
        }) || (() => {});

        window.addEventListener('beforeunload', () => { try { offUpdated(); offNotif(); } catch {} });
    } catch {}
});

function setupEventListeners() {
    startGameBtn.addEventListener('click', handleStartGame);
    lobbyCancelBtn.addEventListener('click', handleCancelGame);
    lobbyLeaveBtn.addEventListener('click', handleLeaveGame);
    lobbyCopyBtn.addEventListener('click', handleCopyGameId);

    // Game settings listeners
    languageSelector.addEventListener('change', handleSettingsChange);
    cluesDurationInput.addEventListener('input', handleSettingsChange);
    guessDurationInput.addEventListener('input', handleSettingsChange);
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
            gameSettingsSection.style.display = 'block';
        } else {
            startGameBtn.style.display = 'none';
            lobbyCancelBtn.style.display = 'none';
            lobbyLeaveBtn.style.display = 'inline-flex';
            gameSettingsSection.style.display = 'none';
        }

        // Load game settings
        loadGameSettings();

        // Fetch and display all players once at load
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
        // Hide countdown in Lobby (management-only); do not show timer to players here
        try { if (typeof setPhaseDeadline === 'function') setPhaseDeadline(null); } catch {}
        console.log('[Lobby] Current phase:', gameState.phase);
        console.log('[Lobby] Number of players:', gameState.players?.length);
        
        // Check if game has started (phase changed from Lobby)
        if (gameState.phase !== 'Lobby') {
            console.log('[Lobby] Game has started! Redirecting to board...');
            showLobbyStatusMessage('Game starting! Redirecting to your board...', 'success');

            // Redirect to board page
            setTimeout(() => {
                window.location.href = '/board.html';
            }, 1000);
            return;
        }
        
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

function handleLeaveGame() {
    if (!confirm('Are you sure you want to leave this game?')) {
        return;
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

// Game Settings functions
async function loadGameSettings() {
    try {
        // Check sessionStorage first for previously saved settings
        const savedSettings = sessionStorage.getItem('gameSettings');
        if (savedSettings) {
            gameSettings = JSON.parse(savedSettings);
            console.log('[Lobby] Game settings loaded from sessionStorage:', gameSettings);
        } else {
            // Fall back to game_settings.json
            const response = await fetch('/game_settings.json');
            if (response.ok) {
                gameSettings = await response.json();
                console.log('[Lobby] Game settings loaded from file:', gameSettings);
            }
        }

        // 1) Fetch dictionaries and populate the selector
        await populateLanguageOptions();

        // 2) Update UI with loaded settings
        if (gameSettings.language) {
            // If the desired language is part of the list, select it; otherwise keep the first option
            const exists = Array.from(languageSelector.options).some(opt => opt.value === gameSettings.language);
            if (exists) languageSelector.value = gameSettings.language;
        }
        cluesDurationInput.value = gameSettings.cluesDuration;
        guessDurationInput.value = gameSettings.guessDuration;
    } catch (error) {
        console.error('Error loading game settings:', error);
        // Keep default values if loading fails
    }
}

async function populateLanguageOptions() {
    try {
        const resp = await fetch('/api/dictionaries');
        if (!resp.ok) throw new Error('Failed to load dictionaries');
        const items = await resp.json(); // [{ key, name }]

        // Clear existing options
        languageSelector.innerHTML = '';

        if (Array.isArray(items) && items.length > 0) {
            items.forEach(item => {
                const opt = document.createElement('option');
                opt.value = item.key || item.name;
                opt.textContent = item.name || item.key;
                languageSelector.appendChild(opt);
            });
        } else {
            // Fallback: ensure at least one option exists from current settings
            const opt = document.createElement('option');
            opt.value = gameSettings.language || 'Français';
            opt.textContent = gameSettings.language || 'Français';
            languageSelector.appendChild(opt);
        }
    } catch (e) {
        console.warn('[Lobby] Could not load dictionaries from backend:', e);
        // Leave whatever is in the selector; if empty, add the current setting as fallback
        if (!languageSelector.options.length) {
            const opt = document.createElement('option');
            opt.value = gameSettings.language || 'Français';
            opt.textContent = gameSettings.language || 'Français';
            languageSelector.appendChild(opt);
        }
    }
}

async function handleSettingsChange() {
    // Update gameSettings object with current values
    gameSettings.language = languageSelector.value;
    gameSettings.cluesDuration = parseInt(cluesDurationInput.value) || 300;
    gameSettings.guessDuration = parseInt(guessDurationInput.value) || 300;

    // Validate duration values
    gameSettings.cluesDuration = Math.max(60, Math.min(600, gameSettings.cluesDuration));
    gameSettings.guessDuration = Math.max(60, Math.min(600, gameSettings.guessDuration));

    // Update inputs in case values were clamped
    cluesDurationInput.value = gameSettings.cluesDuration;
    guessDurationInput.value = gameSettings.guessDuration;

    console.log('[Lobby] Game settings updated:', gameSettings);

    // Save settings to sessionStorage for next game creation
    saveGameSettings();

    // Update backend settings immediately so overrides are persisted for this game
    await updateBackendSettings();
}

function saveGameSettings() {
    try {
        // Save to sessionStorage so settings persist for next game creation
        sessionStorage.setItem('gameSettings', JSON.stringify(gameSettings));
        console.log('[Lobby] Settings saved to sessionStorage for next game');
    } catch (error) {
        console.error('Error saving game settings:', error);
    }
}

async function updateBackendSettings() {
    if (!isCreator || !gameId || !playerId) {
        console.log('[Lobby] Cannot update backend settings - not creator or missing IDs');
        return;
    }

    try {
        const response = await fetch(`/api/games/${gameId}/settings`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerId: playerId,
                language: gameSettings.language,
                cluesDuration: gameSettings.cluesDuration,
                guessDuration: gameSettings.guessDuration
            })
        });

        if (!response.ok) {
            const err = await response.json().catch(() => ({}));
            throw new Error(err.message || 'Failed to update game settings on backend');
        }

        const data = await response.json();
        console.log('[Lobby] Backend settings updated:', data);
        // Optionally reflect server-clamped values back into the UI
        if (typeof data.cluesDuration === 'number') {
            gameSettings.cluesDuration = data.cluesDuration;
            cluesDurationInput.value = data.cluesDuration;
        }
        if (typeof data.guessDuration === 'number') {
            gameSettings.guessDuration = data.guessDuration;
            guessDurationInput.value = data.guessDuration;
        }
    } catch (error) {
        console.error('Error updating backend settings:', error);
        showLobbyStatusMessage('Failed to update game settings', 'error');
    }
}

// End of lobby scope IIFE
})();
