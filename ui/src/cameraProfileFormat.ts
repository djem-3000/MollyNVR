import type { CameraProfile } from './api'
import { t } from './i18n'

export function profileLabel(profile: CameraProfile): string {
  return t(profile.kind === 'main' ? 'camera.profile.main' : 'camera.profile.secondary')
}

function resolutionClassOf(height: number): string | null {
  if (height >= 2160) {
    return t('camera.resolution.4k')
  }

  if (height >= 1440) {
    return t('camera.resolution.2k')
  }

  if (height >= 1080) {
    return t('camera.resolution.fullHd')
  }

  if (height >= 720) {
    return t('camera.resolution.hd')
  }

  return height >= 240 ? t('camera.resolution.sd') : null
}

function greatestCommonDivisor(left: number, right: number): number {
  let a = left
  let b = right

  while (b !== 0) {
    const remainder = a % b
    a = b
    b = remainder
  }

  return a
}

function aspectRatioLabel(width: number, height: number): string | null {
  if (width <= 0 || height <= 0) {
    return null
  }

  const divisor = greatestCommonDivisor(width, height)
  return `${width / divisor}:${height / divisor}`
}

function megapixelsLabel(width: number, height: number): string {
  const megapixels = (width * height) / 1_000_000
  const formatted = megapixels.toLocaleString('ru-RU', {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1
  })
  return `${formatted} ${t('camera.megapixels')}`
}

export function resolutionLabel(profile: CameraProfile): string {
  if (profile.width <= 0 || profile.height <= 0) {
    return t('camera.unknown')
  }

  const parts = [`${profile.width}×${profile.height}`]
  const resolutionClass = resolutionClassOf(profile.height)
  if (resolutionClass) {
    parts.push(resolutionClass)
  }

  parts.push(megapixelsLabel(profile.width, profile.height))

  const aspectRatio = aspectRatioLabel(profile.width, profile.height)
  if (aspectRatio) {
    parts.push(aspectRatio)
  }

  return parts.join(' · ')
}

/** Короткая подпись для оверлея: «640×480 · SD». */
export function resolutionShortLabel(profile: CameraProfile): string {
  if (profile.width <= 0 || profile.height <= 0) {
    return t('camera.unknown')
  }

  const resolution = `${profile.width}×${profile.height}`
  const resolutionClass = resolutionClassOf(profile.height)
  return resolutionClass ? `${resolution} · ${resolutionClass}` : resolution
}

export function audioLabel(profile: CameraProfile): string {
  if (!profile.audioEnabled || !profile.audioCodec) {
    return t('camera.unknown')
  }

  if (!profile.audioSampleRate) {
    return profile.audioCodec
  }

  return `${profile.audioCodec}, ${Math.round(profile.audioSampleRate / 1000)} ${t('camera.audio.rate')}`
}

export type BitrateTone = 'good' | 'warn' | 'bad'

export type BitrateAssessment = {
  tone: BitrateTone
  bitsPerPixel: number
}

export function bitrateAssessment(profile: CameraProfile): BitrateAssessment | null {
  if (profile.width <= 0 || profile.height <= 0 || profile.frameRate <= 0 || profile.bitrateKbps <= 0) {
    return null
  }

  const bitsPerPixel =
    (profile.bitrateKbps * 1000) / (profile.width * profile.height * profile.frameRate)
  const isHevc = /265|hevc/i.test(profile.videoCodec)
  const goodThreshold = isHevc ? 0.05 : 0.08
  const warnThreshold = isHevc ? 0.025 : 0.04
  const tone: BitrateTone =
    bitsPerPixel >= goodThreshold ? 'good' : bitsPerPixel >= warnThreshold ? 'warn' : 'bad'

  return { tone, bitsPerPixel }
}

export function subProfileOptionLabel(profile: CameraProfile): string {
  return `${resolutionLabel(profile)} · ${profile.videoCodec}`
}
