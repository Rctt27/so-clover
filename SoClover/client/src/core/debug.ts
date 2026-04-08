export const isDebug = import.meta.env.VITE_DEBUG_MODE === 'true';

export function debugLog(source: string, message: string, ...args: unknown[]): void {
  if (isDebug) {
    console.log(`%c[${source}] ${message}`, 'color: #6366f1', ...args);
  }
}
