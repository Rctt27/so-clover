import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'

import enCommon from './locales/en/common.json'
import enHome from './locales/en/home.json'
import enLobby from './locales/en/lobby.json'
import enWriting from './locales/en/writing.json'
import enGuessing from './locales/en/guessing.json'
import enScoring from './locales/en/scoring.json'
import frCommon from './locales/fr/common.json'
import frHome from './locales/fr/home.json'
import frLobby from './locales/fr/lobby.json'
import frWriting from './locales/fr/writing.json'
import frGuessing from './locales/fr/guessing.json'
import frScoring from './locales/fr/scoring.json'
import ptCommon from './locales/pt/common.json'
import ptHome from './locales/pt/home.json'
import ptLobby from './locales/pt/lobby.json'
import ptWriting from './locales/pt/writing.json'
import ptGuessing from './locales/pt/guessing.json'
import ptScoring from './locales/pt/scoring.json'

export const resources = {
  en: { common: enCommon, home: enHome, lobby: enLobby, writing: enWriting, guessing: enGuessing, scoring: enScoring },
  fr: { common: frCommon, home: frHome, lobby: frLobby, writing: frWriting, guessing: frGuessing, scoring: frScoring },
  pt: { common: ptCommon, home: ptHome, lobby: ptLobby, writing: ptWriting, guessing: ptGuessing, scoring: ptScoring },
} as const

export const NAMESPACES = ['common', 'home', 'lobby', 'writing', 'guessing', 'scoring'] as const

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'en',
    supportedLngs: ['en', 'fr', 'pt'],
    load: 'languageOnly',
    ns: NAMESPACES as unknown as string[],
    defaultNS: 'common',
    detection: { order: ['navigator'], caches: [] },
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  })

// <html lang> follows the resolved locale. Guarded for the node test env (no DOM).
if (typeof document !== 'undefined') {
  document.documentElement.lang = i18n.language
  i18n.on('languageChanged', (lng) => {
    document.documentElement.lang = lng
  })
}

export default i18n
