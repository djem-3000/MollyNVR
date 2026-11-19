import { useSyncExternalStore } from 'react'
import { getLocale, subscribeLocale, type Locale } from './i18n'

/** Current UI locale; subscribing re-renders the caller when the language changes. */
export function useLocale(): Locale {
  return useSyncExternalStore(subscribeLocale, getLocale, getLocale)
}
