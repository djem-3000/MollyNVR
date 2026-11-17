import type { CameraProfile } from './api'
import { t } from './i18n'

export type CodecTone = 'good' | 'warn' | 'bad' | 'muted'

export type CodecAssessment = {
  tone: CodecTone
  tooltip: string
}

type CodecSupport = 'supported' | 'unsupported' | 'unknown'
type CapabilityKind = 'video' | 'audio'

type ReceiverCapabilities = {
  getCapabilities?: (kind: CapabilityKind) => RTCRtpCapabilities | null
}

const capabilityCache = new Map<CapabilityKind, Set<string> | null>()

function normalizeCodec(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, '')
}

function getCapabilities(kind: CapabilityKind): Set<string> | null {
  if (capabilityCache.has(kind)) {
    return capabilityCache.get(kind) ?? null
  }

  const receiver = (globalThis as unknown as { RTCRtpReceiver?: ReceiverCapabilities }).RTCRtpReceiver
  if (!receiver?.getCapabilities) {
    capabilityCache.set(kind, null)
    return null
  }

  try {
    const capabilities = receiver.getCapabilities(kind)
    const codecs = capabilities?.codecs.map((codec) => codec.mimeType.toLowerCase()) ?? []
    const result = new Set(codecs)
    capabilityCache.set(kind, result)
    return result
  } catch {
    capabilityCache.set(kind, null)
    return null
  }
}

function videoMimeTypes(codec: string): string[] {
  const normalized = normalizeCodec(codec)

  if (normalized.includes('265') || normalized.includes('hevc')) {
    return ['video/h265', 'video/hevc']
  }

  if (normalized.includes('264') || normalized.includes('avc')) {
    return ['video/h264']
  }

  if (normalized.includes('mjpeg') || normalized.includes('mjpg')) {
    return ['video/mjpeg', 'video/jpeg']
  }

  return []
}

function audioMimeTypes(codec: string): string[] {
  const normalized = normalizeCodec(codec)

  if (normalized.includes('ulaw') || normalized.includes('pcmu')) {
    return ['audio/pcmu']
  }

  if (normalized.includes('alaw') || normalized.includes('pcma')) {
    return ['audio/pcma']
  }

  if (normalized.includes('opus')) {
    return ['audio/opus']
  }

  if (normalized.includes('aac') || normalized.includes('mpeg4generic')) {
    return ['audio/aac', 'audio/mpeg4-generic']
  }

  if (normalized.includes('g7221')) {
    return ['audio/g7221']
  }

  if (normalized.includes('g722')) {
    return ['audio/g722']
  }

  return []
}

function codecSupport(kind: CapabilityKind, codec: string): CodecSupport {
  const capabilities = getCapabilities(kind)
  if (!capabilities) {
    return 'unknown'
  }

  const candidates = kind === 'video' ? videoMimeTypes(codec) : audioMimeTypes(codec)
  if (candidates.length > 0) {
    return candidates.some((candidate) => capabilities.has(candidate))
      ? 'supported'
      : 'unsupported'
  }

  const normalized = normalizeCodec(codec)
  if (!normalized) {
    return 'unknown'
  }

  return [...capabilities].some((mimeType) => normalizeCodec(mimeType).includes(normalized))
    ? 'supported'
    : 'unsupported'
}

export function videoCodecAssessment(profile: CameraProfile): CodecAssessment {
  const codec = profile.videoCodec
  const support = codecSupport('video', codec)

  if (support === 'supported') {
    return { tone: 'good', tooltip: t('camera.codec.supported', { codec }) }
  }

  if (profile.videoPlayback === 'transcode-available') {
    return {
      tone: 'warn',
      tooltip: t('camera.codec.transcoded.video', {
        codec,
        target: profile.videoTargetCodec ?? 'H.264'
      })
    }
  }

  if (support === 'unsupported') {
    return { tone: 'bad', tooltip: t('camera.codec.unsupported', { codec }) }
  }

  return { tone: 'good', tooltip: t('camera.codec.supported', { codec }) }
}

export function audioCodecAssessment(profile: CameraProfile): CodecAssessment {
  if (!profile.audioEnabled || !profile.audioCodec) {
    return { tone: 'muted', tooltip: t('camera.codec.audioDisabled') }
  }

  const codec = profile.audioCodec
  const support = codecSupport('audio', codec)

  if (support === 'supported') {
    return { tone: 'good', tooltip: t('camera.codec.supported', { codec }) }
  }

  if (profile.audioPlayback === 'transcoded') {
    return {
      tone: 'warn',
      tooltip: t('camera.codec.transcoded.audio', {
        codec,
        target: profile.audioTargetCodec ?? 'Opus'
      })
    }
  }

  if (support === 'unsupported') {
    return { tone: 'bad', tooltip: t('camera.codec.unsupported', { codec }) }
  }

  if (profile.audioPlayback === 'unsupported') {
    return { tone: 'bad', tooltip: t('camera.codec.unsupported', { codec }) }
  }

  return { tone: 'good', tooltip: t('camera.codec.supported', { codec }) }
}

export function codecToneClass(tone: CodecTone): string {
  return `codec-${tone}`
}
