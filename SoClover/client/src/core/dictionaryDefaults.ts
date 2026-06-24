export type AppLocale = 'en' | 'fr' | 'pt'

export const SUPPORTED_LOCALES = ['en', 'fr', 'pt'] as const
export const FALLBACK_LOCALE: AppLocale = 'en'

export function normalizeLocale(raw: string | null | undefined): AppLocale {
  if (!raw) return FALLBACK_LOCALE
  const lang = raw.toLowerCase().split('-')[0]
  return (SUPPORTED_LOCALES as readonly string[]).includes(lang)
    ? (lang as AppLocale)
    : FALLBACK_LOCALE
}

const DICTIONARY_BY_LOCALE: Record<AppLocale, string> = {
  fr: 'Français_OFF',
  en: 'English_(from_FR_OFF)',
  pt: 'Portuguese_(from_FR_OFF)',
}

/** Default game dictionary key for a given UI locale. UI locale and game dictionary
 *  stay decoupled — this only seeds the dropdown default at game creation. */
export function localeToDictionaryKey(locale: string): string {
  return DICTIONARY_BY_LOCALE[normalizeLocale(locale)]
}
