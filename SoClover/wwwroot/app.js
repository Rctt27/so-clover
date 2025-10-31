// State management
let currentGameId = null;
let playerName = '';

// DOM elements
let playerNameInput;
let charCountDisplay;
let displayNameElement;
let playerInfoDiv;
let createGameBtn;
let cancelGameBtn;
let copyBtn;
let noGameState;
let activeGameState;
let gameIdDisplay;
let statusMessage;
let gameIdInput;

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    // Initialize DOM element references
    playerNameInput = document.getElementById('playerName');
    charCountDisplay = document.getElementById('charCount');
    displayNameElement = document.getElementById('displayName');
    playerInfoDiv = document.getElementById('playerInfo');
    createGameBtn = document.getElementById('createGameBtn');
    cancelGameBtn = document.getElementById('cancelGameBtn');
    copyBtn = document.getElementById('copyBtn');
    noGameState = document.getElementById('noGameState');
    activeGameState = document.getElementById('activeGameState');
    gameIdDisplay = document.getElementById('gameIdDisplay');
    statusMessage = document.getElementById('statusMessage');
    gameIdInput = document.getElementById('gameIdInput');

    setupEventListeners();
    loadState();
});

function setupEventListeners() {
    // Player name input
    playerNameInput.addEventListener('input', handlePlayerNameInput);

    // Game ID input
    gameIdInput.addEventListener('input', handleGameIdInput);

    // Game management buttons
    createGameBtn.addEventListener('click', handleCreateOrJoinGame);
    cancelGameBtn.addEventListener('click', handleCancelGame);
    copyBtn.addEventListener('click', handleCopyGameId);

    // Check game validity when page becomes visible
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden && currentGameId) {
            validateAndUpdateGameState();
        }
    });

    // Check game validity when window gains focus
    window.addEventListener('focus', () => {
        if (currentGameId) {
            validateAndUpdateGameState();
        }
    });
}

function handlePlayerNameInput(e) {
    const value = e.target.value.trim();
    charCountDisplay.textContent = value.length;

    if (value.length > 0) {
        playerName = value;
        displayNameElement.textContent = playerName;
        playerInfoDiv.style.display = 'block';
        createGameBtn.disabled = false;
        updateButtonLabel();
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

function handleGameIdInput(e) {
    updateButtonLabel();
}

function updateButtonLabel() {
    const gameIdValue = gameIdInput.value.trim();
    if (gameIdValue) {
        createGameBtn.innerHTML = '<span class="btn-icon">🚪</span> Join Game';
    } else {
        createGameBtn.innerHTML = '<span class="btn-icon">🎮</span> Create Game';
    }
}

async function handleCreateOrJoinGame() {
    const gameIdValue = gameIdInput.value.trim();

    if (gameIdValue) {
        await handleJoinGame(gameIdValue);
    } else {
        await handleCreateGame();
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
        // Load game settings to get language preference
        // Priority: sessionStorage > game_settings.json > default
        let language = 'Français'; // Default

        // Check sessionStorage first (set by admin in lobby)
        const savedSettings = sessionStorage.getItem('gameSettings');
        if (savedSettings) {
            try {
                const settings = JSON.parse(savedSettings);
                language = settings.language || language;
            } catch (error) {
                console.warn('Could not parse saved game settings');
            }
        } else {
            // Fall back to game_settings.json
            try {
                const settingsResponse = await fetch('/game_settings.json');
                if (settingsResponse.ok) {
                    const settings = await settingsResponse.json();
                    language = settings.language;
                }
            } catch (error) {
                console.warn('Could not load game settings, using default language');
            }
        }

        // Create game with player name and language (creator is automatically added as admin)
        const createResponse = await fetch('/api/games', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                playerName: playerName,
                language: language
            })
        });

        if (!createResponse.ok) {
            throw new Error('Failed to create game');
        }

        const createData = await createResponse.json();
        currentGameId = createData.gameId;
        const playerId = createData.playerId;

        // Save state with playerId and creator status
        const state = {
            playerName: playerName,
            currentGameId: currentGameId,
            playerId: playerId,
            isCreator: true
        };
        sessionStorage.setItem('soCloverState', JSON.stringify(state));

        showStatusMessage('Game created successfully! Redirecting to lobby...', 'success');

        // Navigate to lobby page
        setTimeout(() => {
            window.location.href = '/lobby.html';
        }, 1000);

    } catch (error) {
        console.error('Error creating game:', error);
        showStatusMessage('Failed to create game. Please try again.', 'error');
        createGameBtn.disabled = false;
        updateButtonLabel();
    }
}

async function handleJoinGame(gameId) {
    if (!playerName) {
        showStatusMessage('Please enter your player name first', 'error');
        return;
    }

    createGameBtn.disabled = true;
    createGameBtn.innerHTML = '<span class="btn-icon">⏳</span> Joining...';

    try {
        const joinResponse = await fetch(`/api/games/${gameId}/join`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ playerName: playerName })
        });

        if (!joinResponse.ok) {
            if (joinResponse.status === 404) {
                showStatusMessage('Partie non trouvée', 'error');
                createGameBtn.disabled = false;
                updateButtonLabel();
                return;
            }
            throw new Error('Failed to join game');
        }

        const joinData = await joinResponse.json();
        const playerId = joinData.playerId;

        currentGameId = gameId;

        // Save state with playerId (not creator)
        const state = {
            playerName: playerName,
            currentGameId: currentGameId,
            playerId: playerId,
            isCreator: false
        };
        sessionStorage.setItem('soCloverState', JSON.stringify(state));

        showStatusMessage('Game joined successfully! Redirecting to lobby...', 'success');

        // Navigate to lobby page
        setTimeout(() => {
            window.location.href = '/lobby.html';
        }, 1000);

    } catch (error) {
        console.error('Error joining game:', error);
        showStatusMessage('Failed to join game. Please try again.', 'error');
        createGameBtn.disabled = false;
        updateButtonLabel();
    }
}

async function handleCancelGame() {
    if (!currentGameId) {
        return;
    }

    // Validate that the game still exists before attempting to cancel
    const gameExists = await validateGameExists(currentGameId);
    if (!gameExists) {
        showStatusMessage('Game no longer exists on server. Resetting...', 'error');
        resetToInitialState();
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
            if (response.status === 404) {
                // Game was already deleted or doesn't exist
                showStatusMessage('Game no longer exists. Resetting...', 'info');
                resetToInitialState();
                return;
            }
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
    cancelGameBtn.disabled = false;
    cancelGameBtn.innerHTML = '<span class="btn-icon">❌</span> Cancel Game';
}

function showNoGame() {
    noGameState.style.display = 'block';
    activeGameState.style.display = 'none';
    createGameBtn.disabled = !playerName;
    updateButtonLabel();
    cancelGameBtn.disabled = true;
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
    sessionStorage.setItem('soCloverState', JSON.stringify(state));
}

async function validateGameExists(gameId) {
    try {
        const response = await fetch(`/api/games/${gameId}`);
        return response.ok;
    } catch (error) {
        console.error('Error validating game:', error);
        return false;
    }
}

function resetToInitialState() {
    currentGameId = null;
    sessionStorage.removeItem('soCloverState');
    showNoGame();
    console.log('Front-end reset to initial state');
}

async function validateAndUpdateGameState() {
    if (!currentGameId) {
        return;
    }

    const gameExists = await validateGameExists(currentGameId);
    if (!gameExists) {
        console.log('Game no longer exists on server, resetting to initial state');
        resetToInitialState();
        showStatusMessage('Game session expired. The server may have restarted.', 'info');
    }
}

async function loadState() {
    const savedState = sessionStorage.getItem('soCloverState');
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
                // Validate that the game still exists on the backend
                const gameExists = await validateGameExists(state.currentGameId);
                if (gameExists) {
                    currentGameId = state.currentGameId;
                    showGameCreated();
                } else {
                    // Game no longer exists, reset to initial state
                    console.log('Cached game no longer exists on server, resetting to initial state');
                    resetToInitialState();
                    showStatusMessage('Previous game no longer exists. Please create a new game.', 'info');
                    return;
                }
            }

            if (playerName && !currentGameId) {
                createGameBtn.disabled = false;
                updateButtonLabel();
                document.querySelector('.hint').textContent = 'Ready to create a game!';
            }
        } catch (error) {
            console.error('Error loading state:', error);
            // Clear potentially corrupted state
            resetToInitialState();
        }
    }
}
