import {
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type CSSProperties,
  type DragEvent as ReactDragEvent,
  type MouseEvent,
  type PointerEvent as ReactPointerEvent
} from 'react'
import type { CameraProfile, RecordingLiveStatus, SavedCamera } from '../api'
import { resolutionShortLabel } from '../cameraProfileFormat'
import { eventLabel, t } from '../i18n'
import { useIsMobile } from '../useMediaQuery'
import { connectCamera, readVideoCodec, type StreamSession } from '../webrtc'
import EventIcon from './EventIcon'

type Props = {
  index: number
  camera: SavedCamera | null
  profile: 'main' | 'sub'
  focused: boolean
  stretch: boolean
  muted: boolean
  gridArea?: string
  dragging?: boolean
  dragOver?: boolean
  onDragStart?: (event: ReactDragEvent<HTMLDivElement>) => void
  onDragOver?: (event: ReactDragEvent<HTMLDivElement>) => void
  onDragLeave?: (event: ReactDragEvent<HTMLDivElement>) => void
  onDrop?: (event: ReactDragEvent<HTMLDivElement>) => void
  onDragEnd?: () => void
  onFocus: () => void
  onOpenSettings: () => void
  onOpenArchive: () => void
  recording: RecordingLiveStatus | null
}

type TileState = 'idle' | 'connecting' | 'live' | 'failed'

type View = {
  zoom: number
  x: number
  y: number
}

type PanState = {
  pointerId: number
  startX: number
  startY: number
  originX: number
  originY: number
  moved: boolean
}

type PinchState = {
  startDistance: number
  startZoom: number
  startX: number
  startY: number
  centerX: number
  centerY: number
}

const MIN_ZOOM = 1
const MAX_ZOOM = 6

function clampView(view: View, width: number, height: number): View {
  if (view.zoom <= MIN_ZOOM) {
    return { zoom: MIN_ZOOM, x: 0, y: 0 }
  }

  const maxX = (width * (view.zoom - 1)) / 2
  const maxY = (height * (view.zoom - 1)) / 2

  return {
    zoom: view.zoom,
    x: Math.min(maxX, Math.max(-maxX, view.x)),
    y: Math.min(maxY, Math.max(-maxY, view.y))
  }
}

function SoundIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 9h3l4-3v12l-4-3H4z" />
      <path d="M15 9a4 4 0 0 1 0 6M17.5 6.5a8 8 0 0 1 0 11" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  )
}

function MutedIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 9h3l4-3v12l-4-3H4z" fill="currentColor" stroke="none" />
      <path
        d="M15.5 9.5l5 5M20.5 9.5l-5 5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  )
}

function UnsupportedIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" style={{ width: 18.2, height: 18.2 }}>
      <path d="M4 9h3l4-3v12l-4-3H4z" />
      <circle cx="18" cy="12" r="4.6" fill="none" stroke="currentColor" strokeWidth="1.6" />
      <path d="M14.8 15.2L21.2 8.8" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  )
}

function ArchiveTileIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M19.5617 7C19.7904 5.69523 18.7863 4.5 17.4617 4.5H6.53788C5.21323 4.5 4.20922 5.69523 4.43784 7" />
      <path d="M17.4999 4.5C17.5283 4.24092 17.5425 4.11135 17.5427 4.00435C17.545 2.98072 16.7739 2.12064 15.7561 2.01142C15.6497 2 15.5194 2 15.2588 2H8.74099C8.48035 2 8.35002 2 8.24362 2.01142C7.22584 2.12064 6.45481 2.98072 6.45704 4.00434C6.45727 4.11135 6.47146 4.2409 6.49983 4.5" />
      <path d="M21.1935 16.793C20.8437 19.2739 20.6689 20.5143 19.7717 21.2572C18.8745 22 17.5512 22 14.9046 22H9.09536C6.44881 22 5.12553 22 4.22834 21.2572C3.33115 20.5143 3.15626 19.2739 2.80648 16.793L2.38351 13.793C1.93748 10.6294 1.71447 9.04765 2.66232 8.02383C3.61017 7 5.29758 7 8.67239 7H15.3276C18.7024 7 20.3898 7 21.3377 8.02383C22.2855 9.0477 22.0625 10.6294 21.6165 13.793L21.1935 16.793Z" />
      <path d="M14.5812 13.6159C15.1396 13.9621 15.1396 14.8582 14.5812 15.2044L11.2096 17.2945C10.6669 17.6309 10 17.1931 10 16.5003L10 12.32C10 11.6273 10.6669 11.1894 11.2096 11.5258L14.5812 13.6159Z" />
    </svg>
  )
}

function ScheduleTileIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="8" />
      <path d="M12 7.5V12l3 2" />
    </svg>
  )
}

function SettingsIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  )
}

function displayCodec(codec: string | null): string {
  if (!codec) {
    return t('camera.unknown')
  }

  const normalized = normalizeCodec(codec)
  return normalized === 'H264' ? 'H.264' : normalized === 'H265' ? 'H.265' : codec
}

function normalizeCodec(codec: string | null): string | null {
  if (!codec) {
    return null
  }

  const normalized = codec.replace(/[^a-z0-9]/gi, '').toUpperCase()

  if (normalized === 'HEVC') {
    return 'H265'
  }

  return normalized === 'AVC' ? 'H264' : normalized
}

function selectProfile(camera: SavedCamera | null, kind: 'main' | 'sub'): CameraProfile | null {
  if (!camera) {
    return null
  }

  if (kind === 'main') {
    return camera.profiles.find((profile) => profile.kind === 'main') ?? null
  }

  return (
    camera.profiles.find(
      (profile) => profile.kind === 'sub' && profile.id === camera.subProfileId
    ) ??
    camera.profiles.find((profile) => profile.kind === 'sub') ??
    null
  )
}

export default function CameraTile({
  index,
  camera,
  profile,
  focused,
  stretch,
  muted,
  gridArea,
  dragging = false,
  dragOver = false,
  onDragStart,
  onDragOver,
  onDragLeave,
  onDrop,
  onDragEnd,
  onFocus,
  onOpenSettings,
  onOpenArchive,
  recording
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  const volumeRef = useRef<HTMLDivElement>(null)
  const panRef = useRef<PanState | null>(null)
  const pinchRef = useRef<PinchState | null>(null)
  const pointersRef = useRef<Map<number, { x: number; y: number }>>(new Map())
  const hideControlsTimerRef = useRef<number | null>(null)
  const viewRef = useRef<View>({ zoom: MIN_ZOOM, x: 0, y: 0 })
  const suppressClickRef = useRef(false)
  const isMobile = useIsMobile()
  const [state, setState] = useState<TileState>('idle')
  const [volume, setVolume] = useState(1)
  const [volumeOpen, setVolumeOpen] = useState(false)
  const [controlsVisible, setControlsVisible] = useState(false)
  const [audioAvailable, setAudioAvailable] = useState(true)
  const [actualVideoCodec, setActualVideoCodec] = useState<string | null>(null)
  const [retryToken, setRetryToken] = useState(0)
  const [view, setView] = useState<View>({ zoom: MIN_ZOOM, x: 0, y: 0 })
  viewRef.current = view
  const [panning, setPanning] = useState(false)
  const cameraId = camera?.id ?? null
  const cameraProfile = selectProfile(camera, profile)
  const selectedProfileId = profile === 'sub' ? cameraProfile?.id : undefined
  const videoTranscoded =
    actualVideoCodec !== null &&
    cameraProfile !== null &&
    normalizeCodec(actualVideoCodec) !== normalizeCodec(cameraProfile.videoCodec)
  const codecUnsupported =
    cameraProfile?.audioPlayback === 'unsupported' || cameraProfile?.audioPlayback === 'disabled'
  const audioUnavailable = codecUnsupported || (state === 'live' && !audioAvailable)
  const isMuted = volume === 0
  const effectiveMuted = !focused || isMuted || audioUnavailable || muted

  useEffect(() => {
    const video = videoRef.current
    if (!video || !cameraId) {
      setState('idle')
      return
    }

    let cancelled = false
    let session: StreamSession | null = null
    let stream: MediaStream | null = null
    let codecTimer: number | null = null
    let retryTimer: number | null = null
    let retryScheduled = false

    setState('connecting')
    setAudioAvailable(true)
    setActualVideoCodec(null)

    const updateAudio = () => {
      const mediaStream = video.srcObject as MediaStream | null
      setAudioAvailable((mediaStream?.getAudioTracks().length ?? 0) > 0)
    }

    const handlePlaying = () => {
      setState('live')
      updateAudio()
    }

    const scheduleRetry = () => {
      if (cancelled || retryScheduled) {
        return
      }

      retryScheduled = true
      retryTimer = window.setTimeout(() => {
        if (!cancelled) {
          setRetryToken((value) => value + 1)
        }
      }, 5000)
    }

    video.addEventListener('playing', handlePlaying)

    const connecting = connectCamera(cameraId, profile, video, selectedProfileId)
    stream = video.srcObject as MediaStream | null
    stream?.addEventListener('addtrack', updateAudio)

    connecting
      .then((created) => {
        if (cancelled) {
          created.close()
          return
        }

        session = created
        updateAudio()

        const refreshCodec = () => {
          void readVideoCodec(created.connection)
            .then((codec) => {
              if (!cancelled && codec) {
                setActualVideoCodec(codec)
              }
            })
            .catch(() => undefined)
        }

        refreshCodec()
        codecTimer = window.setTimeout(refreshCodec, 1000)

        created.connection.addEventListener('connectionstatechange', () => {
          if (cancelled) {
            return
          }

          const value = created.connection.connectionState
          if (value === 'failed' || value === 'closed') {
            setState('failed')
            created.close()
            scheduleRetry()
          }
        })
      })
      .catch(() => {
        if (!cancelled) {
          setState('failed')
          scheduleRetry()
        }
      })

    return () => {
      cancelled = true
      if (codecTimer !== null) {
        window.clearTimeout(codecTimer)
      }
      if (retryTimer !== null) {
        window.clearTimeout(retryTimer)
      }
      video.removeEventListener('playing', handlePlaying)
      stream?.removeEventListener('addtrack', updateAudio)
      session?.close()
    }
  }, [cameraId, profile, selectedProfileId, retryToken])

  useEffect(() => {
    const video = videoRef.current
    if (video) {
      video.muted = effectiveMuted
      video.volume = volume
    }
  }, [cameraId, effectiveMuted, volume])

  useEffect(() => {
    setView({ zoom: MIN_ZOOM, x: 0, y: 0 })
    setPanning(false)
    setControlsVisible(false)
    panRef.current = null
    suppressClickRef.current = false
  }, [cameraId, profile])

  useEffect(() => {
    const container = containerRef.current
    if (!container) {
      return
    }

    const handleWheel = (event: WheelEvent) => {
      const video = videoRef.current
      if (!video || event.target !== video || state !== 'live') {
        return
      }

      const rect = container.getBoundingClientRect()
      if (rect.width <= 0 || rect.height <= 0 || event.deltaY === 0) {
        return
      }

      event.preventDefault()

      const delta = event.deltaMode === WheelEvent.DOM_DELTA_LINE ? event.deltaY * 16 : event.deltaY
      const factor = Math.exp(-delta * 0.0015)
      const pointerX = event.clientX - rect.left
      const pointerY = event.clientY - rect.top

      setView((current) => {
        const nextZoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, current.zoom * factor))
        if (nextZoom === current.zoom) {
          return current
        }

        const ratio = nextZoom / current.zoom
        const offsetX = pointerX - rect.width / 2
        const offsetY = pointerY - rect.height / 2

        return clampView(
          {
            zoom: nextZoom,
            x: offsetX - ratio * (offsetX - current.x),
            y: offsetY - ratio * (offsetY - current.y)
          },
          rect.width,
          rect.height
        )
      })
    }

    container.addEventListener('wheel', handleWheel, { passive: false })
    return () => container.removeEventListener('wheel', handleWheel)
  }, [state])

  useEffect(() => {
    const container = containerRef.current
    if (!container) {
      return
    }

    const observer = new ResizeObserver(() => {
      const rect = container.getBoundingClientRect()
      if (rect.width > 0 && rect.height > 0) {
        setView((current) => clampView(current, rect.width, rect.height))
      }
    })

    observer.observe(container)
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    if (!focused) {
      setVolumeOpen(false)
    }
  }, [focused])

  useEffect(() => {
    if (!volumeOpen) {
      return
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (volumeRef.current && !volumeRef.current.contains(event.target as Node)) {
        setVolumeOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [volumeOpen])

  useEffect(
    () => () => {
      if (hideControlsTimerRef.current !== null) {
        window.clearTimeout(hideControlsTimerRef.current)
      }
    },
    []
  )

  async function handleDoubleClick() {
    const container = containerRef.current
    if (!container) {
      return
    }

    try {
      if (document.fullscreenElement === container) {
        await document.exitFullscreen()
      } else {
        await container.requestFullscreen()
      }
    } catch {
      // Браузер может запретить fullscreen без пользовательского жеста.
    }
  }

  function handlePointerDown(event: ReactPointerEvent<HTMLVideoElement>) {
    if (event.button !== 0 || view.zoom <= MIN_ZOOM) {
      return
    }

    event.currentTarget.setPointerCapture(event.pointerId)
    panRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: view.x,
      originY: view.y,
      moved: false
    }
    setPanning(true)
    onFocus()
  }

  function handlePointerMove(event: ReactPointerEvent<HTMLVideoElement>) {
    const pan = panRef.current
    if (!pan || pan.pointerId !== event.pointerId || pinchRef.current) {
      return
    }

    const dx = event.clientX - pan.startX
    const dy = event.clientY - pan.startY
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
      pan.moved = true
      suppressClickRef.current = true
    }

    const container = containerRef.current
    if (!container) {
      return
    }

    const rect = container.getBoundingClientRect()
    setView((current) =>
      clampView(
        {
          ...current,
          x: pan.originX + dx,
          y: pan.originY + dy
        },
        rect.width,
        rect.height
      )
    )
  }

  function handlePointerUp(event: ReactPointerEvent<HTMLVideoElement>) {
    const pan = panRef.current
    if (!pan || pan.pointerId !== event.pointerId) {
      return
    }

    panRef.current = null
    setPanning(false)
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }

    if (pan.moved) {
      window.setTimeout(() => {
        suppressClickRef.current = false
      }, 300)
    }
  }

  function toggleVolume(event: MouseEvent<HTMLButtonElement>) {
    event.stopPropagation()

    if (audioUnavailable) {
      return
    }

    onFocus()
    setVolumeOpen((open) => !open)
  }

  function handleVolume(event: ChangeEvent<HTMLInputElement>) {
    event.stopPropagation()
    setVolume(Number(event.target.value))
    onFocus()
  }

  function handleTilePointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    if (isMobile) {
      showMobileControls()
    }

    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })

    if (pointersRef.current.size === 2) {
      beginPinch()
    }
  }

  function showMobileControls() {
    if (!isMobile) {
      return
    }

    setControlsVisible(true)

    if (hideControlsTimerRef.current !== null) {
      window.clearTimeout(hideControlsTimerRef.current)
    }

    hideControlsTimerRef.current = window.setTimeout(() => {
      hideControlsTimerRef.current = null
      setControlsVisible(false)
    }, 5000)
  }

  function beginPinch() {
    const rect = containerRef.current?.getBoundingClientRect()
    const points = [...pointersRef.current.values()]

    if (!rect || points.length < 2 || rect.width <= 0 || rect.height <= 0) {
      return
    }

    const distance = Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y)
    const current = viewRef.current

    pinchRef.current = {
      startDistance: Math.max(1, distance),
      startZoom: current.zoom,
      startX: current.x,
      startY: current.y,
      centerX: (points[0].x + points[1].x) / 2 - rect.left,
      centerY: (points[0].y + points[1].y) / 2 - rect.top
    }

    panRef.current = null
    setPanning(false)
    suppressClickRef.current = true
    onFocus()
  }

  function handleTilePointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    if (pointersRef.current.has(event.pointerId)) {
      pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    }

    const pinch = pinchRef.current

    if (pinch && pointersRef.current.size >= 2) {
      const points = [...pointersRef.current.values()]
      const rect = containerRef.current?.getBoundingClientRect()

      if (rect && points.length >= 2) {
        event.preventDefault()

        const distance = Math.hypot(
          points[0].x - points[1].x,
          points[0].y - points[1].y
        )
        const factor = distance / pinch.startDistance
        const nextZoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, pinch.startZoom * factor))

        if (nextZoom !== pinch.startZoom) {
          const ratio = nextZoom / pinch.startZoom
          const offsetX = pinch.centerX - rect.width / 2
          const offsetY = pinch.centerY - rect.height / 2

          setView(
            clampView(
              {
                zoom: nextZoom,
                x: offsetX - ratio * (offsetX - pinch.startX),
                y: offsetY - ratio * (offsetY - pinch.startY)
              },
              rect.width,
              rect.height
            )
          )
        }
      }

      return
    }

    if (isMobile) {
      return
    }

    const rect = event.currentTarget.getBoundingClientRect()
    const target = event.target
    const overActions =
      target instanceof Element && target.closest('.tile-actions') !== null
    const overBottomThird =
      rect.height > 0 && event.clientY >= rect.top + rect.height * (2 / 3)

    setControlsVisible(overBottomThird || overActions)
  }

  function handleTilePointerEnd(event: ReactPointerEvent<HTMLDivElement>) {
    pointersRef.current.delete(event.pointerId)

    if (pointersRef.current.size < 2) {
      pinchRef.current = null
    }
  }

  function handleTileDragStart(event: ReactDragEvent<HTMLDivElement>) {
    const target = event.target
    if (target instanceof Element && target.closest('button, input, .tile-actions, .tile-controls')) {
      event.preventDefault()
      return
    }

    onDragStart?.(event)
  }

  const tileClassName = [
    'camera-tile',
    focused && 'focused',
    stretch && 'stretch',
    controlsVisible && 'controls-visible',
    view.zoom > MIN_ZOOM && 'zoomed',
    panning && 'panning',
    dragging && 'dragging',
    dragOver && 'drag-over'
  ]
    .filter(Boolean)
    .join(' ')

  const stateClass = state === 'failed' ? 'bad' : 'warn'
  const stateLabel = state === 'failed' ? t('player.failed') : t('player.connecting')
  const volumeTitle = audioUnavailable
    ? `${t('grid.audioUnavailable')}${
        codecUnsupported && cameraProfile?.audioCodec ? `: ${cameraProfile.audioCodec}` : ''
      }`
    : t('grid.volume')

  const infoVideoSource = cameraProfile ? displayCodec(cameraProfile.videoCodec) : null
  const infoVideoTarget = videoTranscoded
    ? displayCodec(actualVideoCodec ?? cameraProfile?.videoTargetCodec ?? null)
    : null
  const infoVideo = infoVideoTarget ? `${infoVideoSource} → ${infoVideoTarget}` : infoVideoSource
  const infoVideoBad =
    cameraProfile?.videoPlayback === 'transcode-available' && !cameraProfile.videoTargetCodec
  const infoAudioSource = cameraProfile?.audioCodec ?? t('camera.unknown')
  const infoAudio =
    cameraProfile?.audioPlayback === 'transcoded'
      ? `${infoAudioSource} → ${cameraProfile.audioTargetCodec ?? 'AAC'}`
      : infoAudioSource
  const infoAudioBad = codecUnsupported

  return (
    <div
      ref={containerRef}
      data-tile-index={index}
      role="button"
      tabIndex={0}
      aria-label={camera?.name ?? t('grid.tile.empty')}
      className={tileClassName}
      style={gridArea ? { gridArea } : undefined}
      draggable={camera !== null && view.zoom <= MIN_ZOOM}
      onPointerDown={handleTilePointerDown}
      onPointerMove={handleTilePointerMove}
      onPointerUp={handleTilePointerEnd}
      onPointerCancel={handleTilePointerEnd}
      onPointerLeave={() => {
        if (!isMobile) {
          setControlsVisible(false)
        }
      }}
      onDragStart={handleTileDragStart}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      onDragEnd={onDragEnd}
      onClick={() => {
        if (suppressClickRef.current) {
          suppressClickRef.current = false
          return
        }

        onFocus()
      }}
      onDoubleClick={() => {
        if (suppressClickRef.current) {
          suppressClickRef.current = false
          return
        }

        void handleDoubleClick()
      }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault()
          onFocus()
        }
      }}
    >
      {camera ? (
        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted={effectiveMuted}
          draggable={false}
          style={{ transform: `translate(${view.x}px, ${view.y}px) scale(${view.zoom})` }}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerCancel={handlePointerUp}
        />
      ) : (
        <div className="tile-placeholder">
          <img className="tile-placeholder-logo" src="/logo.png" alt="" draggable={false} />
        </div>
      )}

      {camera && recording ? (
        <div className="tile-rec" aria-label={t('grid.rec.active')}>
          <span className="tile-rec-dot" aria-hidden="true" />
          <span className="tile-rec-label">REC</span>
          {recording.schedule ? (
            <span className="tile-rec-trigger" title={t('grid.rec.schedule')}>
              <ScheduleTileIcon />
            </span>
          ) : null}
          {recording.events.map((type) => (
            <span key={type} className="tile-rec-trigger" title={eventLabel(type)}>
              <EventIcon type={type} />
            </span>
          ))}
        </div>
      ) : null}

      {controlsVisible && camera && cameraProfile ? (
        <div className="tile-info">
          <div className="info-row">
            <span className="info-label">{`${t('grid.info.quality')}:`}</span>
            <span className="info-value">{resolutionShortLabel(cameraProfile)}</span>
          </div>
          <div className="info-row">
            <span className="info-label">{`${t('grid.info.bitrate')}:`}</span>
            <span className="info-value">{`${cameraProfile.bitrateKbps} ${t('grid.info.kbps')}`}</span>
          </div>
          <div className="info-row">
            <span className="info-label">{`${t('grid.info.videoShort')}:`}</span>
            <span className={infoVideoBad ? 'info-value bad' : 'info-value'}>{infoVideo}</span>
          </div>
          <div className="info-row">
            <span className="info-label">{`${t('grid.info.audioShort')}:`}</span>
            <span className={infoAudioBad ? 'info-value bad' : 'info-value'}>{infoAudio}</span>
          </div>
        </div>
      ) : null}

      {camera ? (
        <div
          className="tile-actions"
          onClick={(event) => event.stopPropagation()}
          onDoubleClick={(event) => event.stopPropagation()}
        >
          <button
            type="button"
            className="tile-action"
            title={t('tile.archive')}
            aria-label={t('tile.archive')}
            onClick={(event) => {
              event.stopPropagation()
              onOpenArchive()
            }}
          >
            <ArchiveTileIcon />
          </button>
          <button
            type="button"
            className="tile-action"
            title={t('tile.settings')}
            aria-label={t('tile.settings')}
            onClick={(event) => {
              event.stopPropagation()
              onOpenSettings()
            }}
          >
            <SettingsIcon />
          </button>
        </div>
      ) : null}

      <div className="tile-overlay">
        {camera ? (
          <div className="tile-controls" onClick={(event) => event.stopPropagation()}>
            {focused ? (
              <div
                ref={volumeRef}
                className="volume"
                onDragStart={(event) => {
                  event.preventDefault()
                  event.stopPropagation()
                }}
              >
                <button
                  type="button"
                  className={`badge volume-button ${
                    isMuted || audioUnavailable || muted ? 'muted' : ''
                  }`}
                  title={volumeTitle}
                  aria-label={volumeTitle}
                  aria-expanded={volumeOpen}
                  aria-disabled={audioUnavailable}
                  onClick={toggleVolume}
                >
                  {codecUnsupported ? (
                    <UnsupportedIcon />
                  ) : isMuted || audioUnavailable || muted ? (
                    <MutedIcon />
                  ) : (
                    <SoundIcon />
                  )}
                </button>

                {volumeOpen && !audioUnavailable ? (
                  <div
                    className="volume-popover"
                    onClick={(event) => event.stopPropagation()}
                    onDoubleClick={(event) => event.stopPropagation()}
                  >
                    <input
                      className="volume-slider"
                      type="range"
                      min={0}
                      max={1}
                      step={0.01}
                      value={volume}
                      style={{ '--volume': `${Math.round(volume * 100)}%` } as CSSProperties}
                      aria-label={t('grid.volume')}
                      onChange={handleVolume}
                    />
                    <span className="volume-value">{`${Math.round(volume * 100)}%`}</span>
                  </div>
                ) : null}
              </div>
            ) : null}

            {videoTranscoded ? (
              <span className="badge warn transcode-badge" title={t('grid.info.videoTranscoded')}>
                {t('grid.transcode')}
              </span>
            ) : null}

            {state !== 'live' ? (
              <span className={`badge ${stateClass}`} title={stateLabel} aria-label={stateLabel}>
                {stateLabel}
              </span>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  )
}
