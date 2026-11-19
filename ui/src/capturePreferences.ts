export type SnapshotFormat = 'png' | 'jpeg'
export type RecordFormat = 'webm' | 'mp4'

export type CapturePreferences = {
  snapshotFormat: SnapshotFormat
  recordFormat: RecordFormat
}

const STORAGE_KEY = 'mollynvr.capture'

export const DEFAULT_CAPTURE_PREFERENCES: CapturePreferences = {
  snapshotFormat: 'png',
  recordFormat: 'webm'
}

export function loadCapturePreferences(): CapturePreferences {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)

    if (!raw) {
      return DEFAULT_CAPTURE_PREFERENCES
    }

    const parsed = JSON.parse(raw) as Partial<CapturePreferences>
    const snapshotFormat = parsed.snapshotFormat === 'jpeg' ? 'jpeg' : 'png'
    const recordFormat = parsed.recordFormat === 'mp4' ? 'mp4' : 'webm'

    return { snapshotFormat, recordFormat }
  } catch (error) {
    console.warn('Could not read the snapshot settings', error)
    return DEFAULT_CAPTURE_PREFERENCES
  }
}

export function saveCapturePreferences(preferences: CapturePreferences): void {
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences))
}

export function supportsRecordFormat(format: RecordFormat): boolean {
  if (typeof MediaRecorder === 'undefined') {
    return false
  }

  const candidates =
    format === 'mp4'
      ? ['video/mp4;codecs=avc1.42E01E,mp4a.40.2', 'video/mp4']
      : ['video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm']

  return candidates.some((candidate) => MediaRecorder.isTypeSupported(candidate))
}
