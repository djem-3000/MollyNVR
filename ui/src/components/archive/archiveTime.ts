import type { ArchivePlayback } from '../../api'

export const DAY_MS = 24 * 60 * 60 * 1000

export type TimeRange = {
  startMs: number
  endMs: number
  start: string
  end: string
}

export function pad2(value: number): string {
  return value.toString().padStart(2, '0')
}

export function localDateKey(date: Date): string {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

export function startOfDay(dateKey: string): Date {
  return new Date(`${dateKey}T00:00:00`)
}

export function formatClock(date: Date): string {
  return `${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`
}

export function formatDuration(totalSeconds: number): string {
  if (!Number.isFinite(totalSeconds) || totalSeconds <= 0) {
    return '00:00'
  }

  const seconds = Math.round(totalSeconds)
  const hours = Math.floor(seconds / 3600)

  if (hours > 0) {
    return `${hours}:${pad2(Math.floor((seconds % 3600) / 60))}:${pad2(seconds % 60)}`
  }

  return `${pad2(Math.floor(seconds / 60))}:${pad2(seconds % 60)}`
}

export function formatRecordingTimer(totalSeconds: number): string {
  const hours = Math.floor(totalSeconds / 3600)
  return `${pad2(hours)}:${pad2(Math.floor((totalSeconds % 3600) / 60))}:${pad2(totalSeconds % 60)}`
}

/** Timeline (stitched) time -> real recording time. */
export function mediaToWallMs(playback: ArchivePlayback, mediaTime: number): number {
  const segments = playback.segments

  if (segments.length === 0) {
    return Date.parse(playback.windowStart)
  }

  const segment =
    segments.find((item) => mediaTime >= item.mediaStart && mediaTime < item.mediaEnd) ??
    (mediaTime < segments[0].mediaStart ? segments[0] : segments[segments.length - 1])

  const startMs = Date.parse(segment.start)
  const offset = Math.max(0, mediaTime - segment.mediaStart)

  return startMs + offset * 1000
}

/** Real recording time -> timeline time; null when the current window has no such point. */
export function wallToMedia(playback: ArchivePlayback, wallMs: number): number | null {
  for (const segment of playback.segments) {
    const startMs = Date.parse(segment.start)
    const endMs = Date.parse(segment.end)

    if (wallMs >= startMs && wallMs < endMs) {
      return segment.mediaStart + (wallMs - startMs) / 1000
    }
  }

  return null
}

/** Merges schedule and event intervals into a single list for navigation. */
export function buildRanges(
  intervals: Array<{ start: string; end: string }>,
  events: Array<{ type: string; start: string; end: string }>,
  activeTypes: Set<string>,
  scheduleEnabled: boolean
): TimeRange[] {
  const ranges: TimeRange[] = []

  if (scheduleEnabled) {
    for (const interval of intervals) {
      const startMs = Date.parse(interval.start)
      const endMs = Date.parse(interval.end)

      if (endMs > startMs) {
        ranges.push({ startMs, endMs, start: interval.start, end: interval.end })
      }
    }
  }

  for (const event of events) {
    if (!activeTypes.has(event.type)) {
      continue
    }

    const startMs = Date.parse(event.start)
    const endMs = Date.parse(event.end)

    if (endMs > startMs) {
      ranges.push({ startMs, endMs, start: event.start, end: event.end })
    }
  }

  ranges.sort((left, right) => left.startMs - right.startMs)

  const merged: TimeRange[] = []

  for (const range of ranges) {
    const last = merged[merged.length - 1]

    if (last && range.startMs <= last.endMs + 1000) {
      if (range.endMs > last.endMs) {
        last.endMs = range.endMs
        last.end = range.end
      }

      continue
    }

    merged.push({ ...range })
  }

  return merged
}

/** Nearest recording to the right (or the current one when the point is inside a recording). */
export function findStartRight(ranges: TimeRange[], wallMs: number): number | null {
  for (const range of ranges) {
    if (wallMs >= range.startMs && wallMs < range.endMs) {
      return wallMs
    }

    if (range.startMs > wallMs) {
      return range.startMs
    }
  }

  return null
}

/** Previous recording: start of the nearest interval left of the current point. */
export function findPreviousStart(ranges: TimeRange[], wallMs: number): number | null {
  let result: number | null = null

  for (const range of ranges) {
    if (range.startMs < wallMs - 1000) {
      result = range.startMs
      continue
    }

    break
  }

  return result
}

/** Next recording: start of the interval right of the current point. */
export function findNextStart(ranges: TimeRange[], wallMs: number): number | null {
  for (const range of ranges) {
    if (range.startMs > wallMs + 1000) {
      return range.startMs
    }
  }

  return null
}

/** Whether the browser supports HEVC in MSE/native HLS. */
export function browserSupportsHevc(): boolean {
  if (typeof MediaSource !== 'undefined' && typeof MediaSource.isTypeSupported === 'function') {
    try {
      if (MediaSource.isTypeSupported('video/mp4; codecs="hvc1.1.6.L93.B0"')) {
        return true
      }
    } catch {
      // some browsers throw on an unknown codec string
    }
  }

  const video = document.createElement('video')
  const hvc1 = video.canPlayType('video/mp4; codecs="hvc1"')
  const hev1 = video.canPlayType('video/mp4; codecs="hev1"')

  return hvc1 === 'probably' || hev1 === 'probably'
}
