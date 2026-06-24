import common from './locales/en/common.json'
import home from './locales/en/home.json'
import lobby from './locales/en/lobby.json'
import writing from './locales/en/writing.json'
import guessing from './locales/en/guessing.json'
import scoring from './locales/en/scoring.json'

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common'
    resources: {
      common: typeof common
      home: typeof home
      lobby: typeof lobby
      writing: typeof writing
      guessing: typeof guessing
      scoring: typeof scoring
    }
  }
}
