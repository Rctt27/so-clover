// Scope isolation to avoid global `let` collisions with app.js (e.g. `playerName`)
(function(){
'use strict';

// Scoring Phase logic
let gameId = null;
let playerName = '';
let playerId = null;
let adminPlayerId = null;

// DOM elements
const scoringPlayerNameDisplay = document.getElementById('scoringPlayerName');
const scoringTableBody = document.getElementById('scoringTableBody');
const noResultsMessage = document.getElementById('noResultsMessage');
const failedBoardsSection = document.getElementById('failedBoardsSection');
const failedBoardsList = document.getElementById('failedBoardsList');
const scoringStatusMessage = document.getElementById('scoringStatusMessage');
const endGameSection = document.getElementById('endGameSection');
const endGameButton = document.getElementById('endGameButton');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    try { if (typeof disposePhaseTimer === 'function') disposePhaseTimer(); } catch {}
    loadScoringState();
    // Hook SignalR (centralized in app.js)
    try {
        await (window.realTime?.ensureConnected?.());
        await (window.realTime?.joinCurrentGame?.(gameId, playerId));
    } catch {}
    fetchAdminInfo();
    fetchAndDisplayScoring();

    // Attach End Game button click handler
    endGameButton.addEventListener('click', handleEndGame);

    // Subscribe to server pushes
    let offUpdated = () => {};
    let offNotif = () => {};
    try {
        offUpdated = window.realTime?.onGameStateUpdated?.(async (_) => {
            try {
                await fetchAndDisplayScoring();
                await checkGamePhase();
            } catch {}
        }) || (() => {});
        offNotif = window.realTime?.onServerNotification?.((n) => {
            if (!n) return;
            try { showScoringStatusMessage(n.message || 'Server notification', n.type === 'warning' ? 'warning' : 'info'); } catch {}
        }) || (() => {});
    } catch {}

    window.addEventListener('beforeunload', () => { try { offUpdated(); offNotif(); } catch {} });
});

function loadScoringState() {
    const savedState = sessionStorage.getItem('soCloverState');
    if (!savedState) {
        showScoringStatusMessage('No game session found. Redirecting...', 'error');
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

        scoringPlayerNameDisplay.textContent = playerName;

    } catch (error) {
        console.error('Error loading scoring state:', error);
        showScoringStatusMessage('Failed to load game data. Redirecting...', 'error');
        setTimeout(() => window.location.href = '/index.html', 2000);
    }
}

async function fetchAndDisplayScoring() {
    try {
        const response = await fetch(`/api/games/${gameId}/scoring`);

        if (!response.ok) {
            throw new Error('Failed to fetch scoring data');
        }

        const scoringData = await response.json();
        console.log('🏆 Scoring Data:', scoringData);

        displayScoringResults(scoringData);

    } catch (error) {
        console.error('Error fetching scoring:', error);
        showScoringStatusMessage('Failed to load scoring data', 'error');
    }
}

// (IIFE will be closed at end of file)

function displayScoringResults(scoringData) {
    // Clear existing content
    scoringTableBody.innerHTML = '';
    failedBoardsList.innerHTML = '';

    const successfulBoards = scoringData.successfulBoards || [];
    const failedBoards = scoringData.failedBoards || [];

    // Display successful boards in ranking table
    if (successfulBoards.length === 0) {
        noResultsMessage.style.display = 'block';
        document.querySelector('.scoring-table-wrapper').style.display = 'none';
    } else {
        noResultsMessage.style.display = 'none';
        document.querySelector('.scoring-table-wrapper').style.display = 'block';

        successfulBoards.forEach((result, index) => {
            const row = document.createElement('tr');

            // Highlight current player's row
            if (result.playerId === playerId) {
                row.classList.add('current-player-row');
            }

            // Rank column
            const rankCell = document.createElement('td');
            rankCell.textContent = index + 1;
            rankCell.classList.add('rank-cell');
            if (index === 0) {
                rankCell.innerHTML = '🥇 ' + (index + 1);
            } else if (index === 1) {
                rankCell.innerHTML = '🥈 ' + (index + 1);
            } else if (index === 2) {
                rankCell.innerHTML = '🥉 ' + (index + 1);
            }
            row.appendChild(rankCell);

            // Player name column
            const playerCell = document.createElement('td');
            playerCell.textContent = result.playerName;
            playerCell.classList.add('player-name-cell');
            row.appendChild(playerCell);

            // Attempts column
            const attemptsCell = document.createElement('td');
            attemptsCell.textContent = result.attempts;
            attemptsCell.classList.add('attempts-cell');
            row.appendChild(attemptsCell);

            // Duration column
            const durationCell = document.createElement('td');
            durationCell.textContent = formatDuration(result.durationSeconds);
            durationCell.classList.add('duration-cell');
            row.appendChild(durationCell);

            // Status column
            const statusCell = document.createElement('td');
            const statusBadge = document.createElement('span');
            statusBadge.classList.add('status-badge', 'success');
            statusBadge.textContent = '✓ Guessed';
            statusCell.appendChild(statusBadge);
            row.appendChild(statusCell);

            scoringTableBody.appendChild(row);
        });
    }

    // Display failed boards
    if (failedBoards.length > 0) {
        failedBoardsSection.style.display = 'block';
        failedBoards.forEach(result => {
            const li = document.createElement('li');
            li.textContent = `${result.playerName} - ${result.attempts} attempt(s) in ${formatDuration(result.durationSeconds)}`;

            if (result.playerId === playerId) {
                li.classList.add('current-player-failed');
            }

            failedBoardsList.appendChild(li);
        });
    } else {
        failedBoardsSection.style.display = 'none';
    }
}

function formatDuration(seconds) {
    if (seconds < 60) {
        return `${seconds}s`;
    }
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}m ${remainingSeconds}s`;
}

function showScoringStatusMessage(message, type = 'info') {
    scoringStatusMessage.textContent = message;
    scoringStatusMessage.className = `status-message ${type}`;
    scoringStatusMessage.style.display = 'block';

    setTimeout(() => {
        scoringStatusMessage.style.display = 'none';
    }, 5000);
}

async function fetchAdminInfo() {
    try {
        const response = await fetch(`/api/games/${gameId}/state?playerId=${playerId}&includeSecrets=false`);

        if (!response.ok) {
            throw new Error('Failed to fetch game state');
        }

        const gameState = await response.json();
        adminPlayerId = gameState.adminPlayerId;

        // Show End Game button if current player is admin
        if (adminPlayerId === playerId) {
            endGameSection.style.display = 'block';
        }

    } catch (error) {
        console.error('Error fetching admin info:', error);
    }
}

async function handleEndGame() {
    if (!confirm('Are you sure you want to end the game? All players will be redirected to the home page.')) {
        return;
    }

    try {
        const response = await fetch(`/api/games/${gameId}/complete`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ playerId })
        });

        if (!response.ok) {
            throw new Error('Failed to complete game');
        }

        showScoringStatusMessage('Game completed! Redirecting...', 'success');

        // Clean up and redirect
        cleanupGameData();
        setTimeout(() => {
            window.location.href = '/index.html';
        }, 1500);

    } catch (error) {
        console.error('Error ending game:', error);
        showScoringStatusMessage('Failed to end game', 'error');
    }
}

function cleanupGameData() {
    sessionStorage.removeItem('soCloverState');
}

async function checkGamePhase() {
    try {
        const response = await fetch(`/api/games/${gameId}/state?playerId=${playerId}&includeSecrets=false`);

        if (!response.ok) {
            return;
        }

        const gameState = await response.json();

        // If game is completed, redirect to index.html
        if (gameState.phase === 'Completed') {
            showScoringStatusMessage('Game has ended. Redirecting...', 'info');
            cleanupGameData();
            setTimeout(() => {
                window.location.href = '/index.html';
            }, 1500);
        }

    } catch (error) {
        console.error('Error checking game phase:', error);
    }
}

})();
