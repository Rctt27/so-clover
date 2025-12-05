// securityHelper.js — helpers de sécurité front et interception des erreurs API
// Objectif: s’aligner avec la nouvelle bonne pratique back-end
// - Ne jamais utiliser Guid.Empty côté client
// - Intercepter les erreurs 400/401/403 et réagir proprement
// - Garantir une session valide (playerId non vide) avant d’appeler l’API

(function () {
    const REDIRECT_DELAY_MS = 1500;

    function getStatusElement() {
        // Renvoie l’élément de statut de la page si présent
        return (
            document.getElementById('statusMessage') ||
            document.getElementById('lobbyStatusMessage') ||
            document.getElementById('boardStatusMessage') ||
            document.getElementById('guessingStatusMessage') ||
            document.getElementById('scoringStatusMessage')
        );
    }

    function showStatus(msg, type) {
        const el = getStatusElement();
        if (!el) return;
        el.textContent = msg;
        el.className = `status-message ${type || 'info'}`;
        el.style.display = 'block';
    }

    function parseState() {
        try {
            const raw = sessionStorage.getItem('soCloverState');
            return raw ? JSON.parse(raw) : null;
        } catch {
            return null;
        }
    }

    function clearState() {
        try { sessionStorage.removeItem('soCloverState'); } catch {}
    }

    function isEmptyGuid(value) {
        return (
            !value ||
            value === '00000000-0000-0000-0000-000000000000' ||
            String(value).trim() === ''
        );
    }

    function ensureValidSession() {
        const state = parseState();
        if (!state || !state.currentGameId || !state.playerName || isEmptyGuid(state.playerId)) {
            showStatus('Invalid or missing session. Redirecting to home…', 'error');
            clearState();
            setTimeout(() => { window.location.href = '/index.html'; }, REDIRECT_DELAY_MS);
            return false;
        }
        return true;
    }

    // Expose minimal API
    window.SoCloverApi = {
        ensureValidSession
    };

    // Intercepteur fetch global pour améliorer les messages d’erreur
    const originalFetch = window.fetch.bind(window);
    window.fetch = async function (input, init) {
        try {
            // Pour les endpoints de jeu, s’assurer que la session est valide
            const url = typeof input === 'string' ? input : (input && input.url) || '';
            if (url.includes('/api/games')) {
                // Ne bloque pas la création/join, mais bloque les autres flux sans session
                const isCreateOrJoin = /\/api\/games($|\?|\/)?.*|(\/join)$/.test(url);
                if (!isCreateOrJoin) {
                    const ok = ensureValidSession();
                    if (!ok) {
                        // On arrête ici pour éviter des appels inutiles
                        return new Response(null, { status: 499, statusText: 'Client Session Invalidated' });
                    }
                }
            }

            const response = await originalFetch(input, init);

            if (!response.ok) {
                // Tente de lire un message JSON standard { message: "..." }
                let message = '';
                try {
                    const data = await response.clone().json();
                    message = (data && (data.message || data.error)) || '';
                } catch {}

                if (response.status === 400) {
                    const lower = (message || '').toLowerCase();
                    if (lower.includes('playerid') && lower.includes('empty')) {
                        // Message typique: "PlayerId must not be empty GUID"
                        showStatus('Your session is invalid (empty playerId). Redirecting…', 'error');
                        clearState();
                        setTimeout(() => { window.location.href = '/index.html'; }, REDIRECT_DELAY_MS);
                    } else if (message) {
                        showStatus(message, 'error');
                    }
                } else if (response.status === 401 || response.status === 403) {
                    showStatus('Access denied. Please restart from home page.', 'error');
                    clearState();
                    setTimeout(() => { window.location.href = '/index.html'; }, REDIRECT_DELAY_MS);
                }
            }

            return response;
        } catch (err) {
            showStatus('Network error. Please try again.', 'error');
            throw err;
        }
    };
})();
