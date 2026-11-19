import { getLocale, type Locale } from './i18n'

const UNITS: Record<Locale, string[]> = {
  en: ['B', 'KB', 'MB', 'GB', 'TB'],
  ru: ['Б', 'КБ', 'МБ', 'ГБ', 'ТБ']
}

export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return '0'
  }

  const units = UNITS[getLocale()]
  let value = bytes
  let unit = 0

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }

  return `${value >= 10 || unit === 0 ? Math.round(value) : value.toFixed(1)} ${units[unit]}`
}
