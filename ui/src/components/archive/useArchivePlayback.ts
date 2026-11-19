import Hls from 'hls.js'
import { useCallback, useEffect, useRef, useState } from 'react'
import { startArchivePlayback, stopArchivePlayback, type ArchivePlayback } from '../../api'
import { browserSupportsHevc, mediaToWallMs, wallToMedia } from './archiveTime'
import { t } from '../../i18n'

export type ArchivePlaybackController = {
  videoRef: (element: HTMLVideoElement | null) => void
  video: HTMLVideoElement | null
  playback: ArchivePlayback | null
  wallTime: Date | null
  playing: boolean
  loading: boolean
  error: string | null
  rate: number
  setRate: (rate: number) => void
  playFrom: (wall: Date) => void
  toggle: () => void
  seek: (wall: Date) => void
  stop: () => void
}

type Options = {
  cameraId: string | null
  onEnded?: () => void
}

/**
  * Archive playback over HLS. The video ref is a callback: listeners and hls.js
  * reconnect when the element appears (for example, after a camera is selected).
 */
export function useArchivePlayback({ cameraId, onEnded }: Options): ArchivePlaybackController {
  const videoElementRef = useRef<HTMLVideoElement | null>(null)
  const [video, setVideo] = useState<HTMLVideoElement | null>(null)
  const hlsRef = useRef<Hls | null>(null)
  const sessionRef = useRef<string | null>(null)
  const playbackRef = useRef<ArchivePlayback | null>(null)
  const pendingSeekRef = useRef<number | null>(null)
  const autoplayRef = useRef(false)
  const rateRef = useRef(1)
  const requestRef = useRef(0)
  const endedRef = useRef(onEnded)
  const wallRef = useRef<Date | null>(null)
  const seekTargetRef = useRef<number | null>(null)
  const seekAttemptsRef = useRef(0)
  const seekTimerRef = useRef<number | null>(null)
  const playFromRef = useRef<(wall: Date) => void>(() => {})
  const recoverAtRef = useRef(0)
  const recoverAttemptsRef = useRef(0)

  const [playback, setPlayback] = useState<ArchivePlayback | null>(null)
  const [wallTime, setWallTime] = useState<Date | null>(null)
  const [playing, setPlaying] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [rate, setRateState] = useState(1)

  const videoRef = useCallback((element: HTMLVideoElement | null) => {
    videoElementRef.current = element
    setVideo(element)
  }, [])

  useEffect(() => {
    endedRef.current = onEnded
  }, [onEnded])

  const applyWall = useCallback((value: Date | null) => {
    wallRef.current = value
    setWallTime(value)
  }, [])

  const releaseSession = useCallback(() => {
    seekTargetRef.current = null
    seekAttemptsRef.current = 0

    if (seekTimerRef.current !== null) {
      window.clearInterval(seekTimerRef.current)
      seekTimerRef.current = null
    }

    if (hlsRef.current) {
      hlsRef.current.destroy()
      hlsRef.current = null
    }

    const element = videoElementRef.current

    if (element) {
      element.pause()
      element.removeAttribute('src')
      element.load()
    }

    if (sessionRef.current) {
      void stopArchivePlayback(sessionRef.current)
      sessionRef.current = null
    }
  }, [])

  const startSeekLoop = useCallback(() => {
    if (seekTimerRef.current !== null) {
      return
    }

    seekAttemptsRef.current = 0

    seekTimerRef.current = window.setInterval(() => {
      const element = videoElementRef.current
      const target = seekTargetRef.current

      if (!element || target === null) {
        if (seekTimerRef.current !== null) {
          window.clearInterval(seekTimerRef.current)
          seekTimerRef.current = null
        }

        return
      }

      if (element.readyState >= 1 && Math.abs(element.currentTime - target) > 1) {
        try {
          element.currentTime = target
        } catch {
          // media is not ready yet
        }
      }

      seekAttemptsRef.current += 1

      if (Math.abs(element.currentTime - target) < 1 || seekAttemptsRef.current > 60) {
        seekTargetRef.current = null

        if (seekTimerRef.current !== null) {
          window.clearInterval(seekTimerRef.current)
          seekTimerRef.current = null
        }
      }
    }, 500)
  }, [])

  const stop = useCallback(() => {
    requestRef.current += 1
    releaseSession()
    playbackRef.current = null
    setPlayback(null)
    applyWall(null)
    setPlaying(false)
    setLoading(false)
  }, [applyWall, releaseSession])

  useEffect(() => {
    // Switching the camera in a slot must release the previous HLS session.
    return () => {
      requestRef.current += 1
      releaseSession()
      playbackRef.current = null
      setPlayback(null)
      wallRef.current = null
      setWallTime(null)
      setPlaying(false)
      setLoading(false)
    }
  }, [cameraId, releaseSession])

  const playFrom = useCallback(
    (wall: Date) => {
      if (!cameraId) {
        return
      }

      const request = ++requestRef.current
      setLoading(true)
      setError(null)
      autoplayRef.current = true
      recoverAttemptsRef.current = 0

      void startArchivePlayback(cameraId, wall.toISOString(), browserSupportsHevc())
        .then((next) => {
          if (request !== requestRef.current) {
            void stopArchivePlayback(next.sessionId)
            return
          }

          releaseSession()
          sessionRef.current = next.sessionId
          playbackRef.current = next
          pendingSeekRef.current = next.startOffsetSeconds
          setPlayback(next)
          applyWall(new Date(mediaToWallMs(next, next.startOffsetSeconds)))
        })
        .catch((reason: unknown) => {
          if (request !== requestRef.current) {
            return
          }

          setError(reason instanceof Error ? reason.message : String(reason))
          setPlaying(false)
        })
        .finally(() => {
          if (request === requestRef.current) {
            setLoading(false)
          }
        })
    },
    [applyWall, cameraId, releaseSession]
  )

  playFromRef.current = playFrom

  useEffect(() => {
    const current = playback
    const element = video

    if (!current || !element) {
      return
    }

    const applyStart = () => {
      const offset = pendingSeekRef.current ?? current.startOffsetSeconds
      pendingSeekRef.current = null

      if (Number.isFinite(offset) && offset > 0) {
        seekTargetRef.current = offset
        seekAttemptsRef.current = 0
        startSeekLoop()

        try {
          element.currentTime = offset
        } catch {
          // the browser is not ready to seek yet — retry on timeupdate
        }
      }

      element.playbackRate = rateRef.current

      if (autoplayRef.current) {
        void element.play().catch(() => setPlaying(false))
      }
    }

    if (Hls.isSupported()) {
      const hls = new Hls({
        maxBufferLength: 30,
        startPosition: current.startOffsetSeconds > 0 ? current.startOffsetSeconds : -1
      })
      hlsRef.current = hls
      hls.on(Hls.Events.MANIFEST_PARSED, () => {
        recoverAttemptsRef.current = 0
        applyStart()
      })
      hls.on(Hls.Events.ERROR, (_event, data) => {
        if (!data.fatal) {
          return
        }

        const now = Date.now()

        // Network or gap in fragments: restart the session from the current position.
        if (recoverAttemptsRef.current < 2 && now - recoverAtRef.current > 3000) {
          recoverAtRef.current = now
          recoverAttemptsRef.current += 1
          const target = wallRef.current ?? new Date(current.windowStart)
          setLoading(true)
          window.setTimeout(() => playFromRef.current(target), 250)
          return
        }

        setError(data.details)
        setLoading(false)
      })
      hls.loadSource(current.playlistUrl)
      hls.attachMedia(element)

      return () => {
        hls.destroy()

        if (hlsRef.current === hls) {
          hlsRef.current = null
        }
      }
    }

    if (element.canPlayType('application/vnd.apple.mpegurl')) {
      element.src = current.playlistUrl
      element.addEventListener('loadedmetadata', applyStart, { once: true })

      return () => element.removeEventListener('loadedmetadata', applyStart)
    }

    setError(t('archive.player.unsupported'))
    return undefined
  }, [playback, video, startSeekLoop])

  useEffect(() => {
    const element = video

    if (!element) {
      return
    }

    const onTimeUpdate = () => {
      const target = seekTargetRef.current

      if (target !== null) {
        if (Math.abs(element.currentTime - target) < 1) {
          seekTargetRef.current = null
        }

        return
      }

      const current = playbackRef.current

      if (current) {
        applyWall(new Date(mediaToWallMs(current, element.currentTime)))
      }
    }

    const onPlay = () => setPlaying(true)
    const onPause = () => setPlaying(false)
    const onEnded = () => {
      setPlaying(false)
      endedRef.current?.()
    }

    element.addEventListener('timeupdate', onTimeUpdate)
    element.addEventListener('play', onPlay)
    element.addEventListener('pause', onPause)
    element.addEventListener('ended', onEnded)

    return () => {
      element.removeEventListener('timeupdate', onTimeUpdate)
      element.removeEventListener('play', onPlay)
      element.removeEventListener('pause', onPause)
      element.removeEventListener('ended', onEnded)
    }
  }, [applyWall, video])

  useEffect(
    () => () => {
      requestRef.current += 1
      releaseSession()
    },
    [releaseSession]
  )

  const toggle = useCallback(() => {
    const element = videoElementRef.current

    if (!element || !playbackRef.current) {
      return
    }

    if (element.paused) {
      autoplayRef.current = true
      void element.play().catch(() => setPlaying(false))
    } else {
      element.pause()
    }
  }, [])

  const seek = useCallback(
    (wall: Date) => {
      const current = playbackRef.current
      const element = videoElementRef.current

      if (current && element) {
        const media = wallToMedia(current, wall.getTime())

        if (media !== null) {
          element.currentTime = media
          seekTargetRef.current = media
          seekAttemptsRef.current = 0
          startSeekLoop()
          applyWall(wall)
          autoplayRef.current = true
          void element.play().catch(() => setPlaying(false))
          return
        }
      }

      playFrom(wall)
    },
    [applyWall, playFrom, startSeekLoop]
  )

  const setRate = useCallback((value: number) => {
    rateRef.current = value
    setRateState(value)

    const element = videoElementRef.current

    if (element) {
      element.playbackRate = value
    }
  }, [])

  return {
    videoRef,
    video,
    playback,
    wallTime,
    playing,
    loading,
    error,
    rate,
    setRate,
    playFrom,
    toggle,
    seek,
    stop
  }
}
