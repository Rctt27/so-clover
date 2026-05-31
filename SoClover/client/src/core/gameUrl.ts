import { CONSTANTS } from './constants';

const PREFIX = CONSTANTS.GAME_URL_PREFIX; // '/g/'

/** Fonction pure : extrait le code de partie d'un pathname. Testable sans window. */
export function parseGameCode(pathname: string): string | null {
  if (!pathname.startsWith(PREFIX)) return null;
  const code = decodeURIComponent(pathname.slice(PREFIX.length)).replace(/\/+$/, '');
  return code.length > 0 ? code : null;
}

export function readGameCodeFromUrl(): string | null {
  return parseGameCode(window.location.pathname);
}

/** Lien partageable complet (origin + /g/<code>). */
export function gameShareUrl(code: string): string {
  return `${window.location.origin}${PREFIX}${encodeURIComponent(code)}`;
}

/** Reflète la partie courante dans l'URL sans empiler d'entrée d'historique. */
export function syncGameUrl(code: string): void {
  window.history.replaceState({}, '', `${PREFIX}${encodeURIComponent(code)}`);
}

/** Retour à la racine (sortie de partie). */
export function clearGameUrl(): void {
  window.history.pushState({}, '', '/');
}
