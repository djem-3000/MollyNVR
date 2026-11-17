import type { CameraInfo, CameraProfile, DiscoveredCamera } from './api'
import { t } from './i18n'

export type WizardStep = 1 | 2 | 3 | 4

export type SearchMode = 'multicast' | 'unicast' | 'ip'

export type SearchState = {
  mode: SearchMode
  range: string
  ipHost: string
  ipVendor: string
  devices: DiscoveredCamera[] | null
  searchedMode: SearchMode | null
  busy: boolean
  error: string | null
}

export type ConnectionState = {
  host: string
  userName: string
  password: string
  vendor: string
  busy: boolean
  camera: CameraInfo | null
  error: string | null
  changed: boolean
}

export type SnapshotEntry = {
  url: string
  profileId: string
  label: string
}

export const STEP_LABELS: Record<WizardStep, string> = {
  1: 'wizard.step.search',
  2: 'wizard.step.verify',
  3: 'wizard.step.profiles',
  4: 'wizard.step.save'
}

export function moduleLabel(key: string): string {
  if (key.toLowerCase() === 'onvif') {
    return 'ONVIF'
  }

  if (key.toLowerCase() === 'hikvision') {
    return 'Hikvision'
  }

  if (key.toLowerCase() === 'reolink') {
    return 'Reolink'
  }

  return key
}

export function profileArea(profile: CameraProfile): number {
  return Math.max(0, profile.width) * Math.max(0, profile.height)
}

export function pickMainProfile(profiles: CameraProfile[]): CameraProfile | null {
  return profiles.reduce<CameraProfile | null>(
    (best, profile) => (best === null || profileArea(profile) > profileArea(best) ? profile : best),
    null
  )
}

export function pickDefaultSubProfile(
  profiles: CameraProfile[],
  mainProfileId: string | null
): CameraProfile | null {
  return profiles
    .filter((profile) => profile.id !== mainProfileId)
    .reduce<CameraProfile | null>(
      (best, profile) => (best === null || profileArea(profile) < profileArea(best) ? profile : best),
      null
    )
}

export function profileRoleLabel(
  profile: CameraProfile,
  profiles: CameraProfile[],
  mainProfileId: string
): string {
  if (profile.id === mainProfileId) {
    return t('camera.profile.main')
  }

  const index = profiles
    .filter((candidate) => candidate.id !== mainProfileId)
    .findIndex((candidate) => candidate.id === profile.id)

  return index >= 0
    ? t('camera.profile.secondaryNumbered', { number: index + 1 })
    : t('camera.profile.secondary')
}
