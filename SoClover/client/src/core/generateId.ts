/**
 * Generate a UUID v4 compatible ID
 * Uses crypto.getRandomValues() which is more widely supported than crypto.randomUUID()
 */
export function generateId(): string {
  // Try to use crypto.randomUUID() if available
  if (typeof crypto !== 'undefined' && crypto.randomUUID && typeof crypto.randomUUID === 'function') {
    try {
      return crypto.randomUUID()
    } catch {
      // Fall through to fallback
    }
  }

  // Fallback to crypto.getRandomValues() based UUID v4
  if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
    const bytes = new Uint8Array(16)
    crypto.getRandomValues(bytes)

    // Set version to 4 and variant to RFC 4122
    bytes[6] = (bytes[6] & 0x0f) | 0x40
    bytes[8] = (bytes[8] & 0x3f) | 0x80

    const hex = Array.from(bytes).map(b => b.toString(16).padStart(2, '0')).join('')
    return [
      hex.substring(0, 8),
      hex.substring(8, 12),
      hex.substring(12, 16),
      hex.substring(16, 20),
      hex.substring(20)
    ].join('-')
  }

  // Last resort: simple random string (not a true UUID but works for notifications)
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
}
