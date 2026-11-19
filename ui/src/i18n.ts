import { en } from './locales/en'
import { ru } from './locales/ru'

export type Dictionary = Record<string, string>

export type Locale = 'en' | 'ru'

const DICTIONARIES: Record<Locale, Dictionary> = { en, ru }
const STORAGE_KEY = 'mollynvr.locale'
const DEFAULT_LOCALE: Locale = 'en'

type Listener = () => void

const listeners = new Set<Listener>()

let currentLocale: Locale = readStoredLocale()

export function getLocale(): Locale {
  return currentLocale
}

export function setLocale(locale: Locale): void {
  if (locale === currentLocale) {
    return
  }

  currentLocale = locale
  persistLocale(locale)
  applyDocumentLocale()
  listeners.forEach((listener) => listener())
}

export function subscribeLocale(listener: Listener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function t(key: string, values?: Record<string, string | number>): string {
  const template = DICTIONARIES[currentLocale][key] ?? DICTIONARIES.en[key] ?? key

  if (!values) {
    return template
  }

  return Object.entries(values).reduce(
    (text, [name, value]) => text.split(`{${name}}`).join(String(value)),
    template
  )
}

function readStoredLocale(): Locale {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    return stored === 'en' || stored === 'ru' ? stored : DEFAULT_LOCALE
  } catch {
    return DEFAULT_LOCALE
  }
}

function persistLocale(locale: Locale): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, locale)
  } catch {
    // private mode or blocked storage — the language stays for this session
  }
}

function applyDocumentLocale(): void {
  document.documentElement.lang = currentLocale
}

applyDocumentLocale()

const EVENT_LABEL_KEYS: Record<string, string> = {
  motion: 'event.motion',
  person: 'event.person',
  vehicle: 'event.vehicle',
  animal: 'event.animal',
  face: 'event.face',
  linecrossing: 'event.linecrossing',
  intrusion: 'event.intrusion',
  regionentrance: 'event.regionentrance',
  regionexiting: 'event.regionexiting',
  loitering: 'event.loitering',
  tamper: 'event.tamper',
  sound: 'event.sound'
}

export function eventLabel(type: string): string {
  const key = EVENT_LABEL_KEYS[type]
  return key ? t(key) : type
}
