import { useEffect, useRef, useState, type DragEvent as ReactDragEvent } from 'react'
import { getRecordingStatus, type RecordingLiveStatus, type SavedCamera } from '../api'
import { t } from '../i18n'
import {
  LAYOUT_GRID,
  LAYOUT_TILE_AREAS,
  LIVE_LAYOUTS,
  loadLivePreferences,
  orderCameras,
  saveLivePreferences,
  tileCount,
  type CameraLayout,
  type LivePreferences
} from '../livePreferences'
import { useIsMobile } from '../useMediaQuery'
import CameraSortDialog from './CameraSortDialog'
import CameraTile from './CameraTile'
import PageLayout from './PageLayout'

type Props = {
  cameras: SavedCamera[]
  onOpenCameraSettings: (camera: SavedCamera) => void
  onOpenArchive: (camera: SavedCamera) => void
}

function LayoutIcon({ layout }: { layout: CameraLayout }) {
  const grid = LAYOUT_GRID[layout]
  const areas = LAYOUT_TILE_AREAS[layout]
  const cells = areas ?? Array.from({ length: tileCount(layout) }, () => undefined)

  return (
    <span
      className="layout-icon"
      style={{
        gridTemplateColumns: `repeat(${grid.columns}, 1fr)`,
        gridTemplateRows: `repeat(${grid.rows}, 1fr)`
      }}
      aria-hidden="true"
    >
      {cells.map((area, index) => (
        <span key={index} style={area ? { gridArea: area } : undefined} />
      ))}
    </span>
  )
}

function StretchIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M14 4h6v6M20 4l-7 7M10 20H4v-6M4 20l7-7" />
    </svg>
  )
}

function SortIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M9 5v14M6 8l3-3 3 3" />
      <path d="M15 19V5M12 16l3 3 3-3" />
    </svg>
  )
}

function FullscreenIcon({ exit }: { exit: boolean }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      {exit ? (
        <path d="M9 4v5H4M15 4v5h5M20 15h-5v5M4 15h5v5" />
      ) : (
        <path d="M4 9V4h5M15 4h5v5M20 15v5h-5M9 20H4v-5" />
      )}
    </svg>
  )
}

function SpeakerIcon({ muted }: { muted: boolean }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" style={{ width: 27, height: 27 }}>
      <path d="M4 9h3l4-3v12l-4-3H4z" fill="currentColor" stroke="none" />
      {muted ? (
        <path
          d="M15.5 9.5l5 5M20.5 9.5l-5 5"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
        />
      ) : (
        <path
          d="M15 9a4 4 0 0 1 0 6M17.5 6.5a8 8 0 0 1 0 11"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
        />
      )}
    </svg>
  )
}

/**
 * Размещает активные камеры по слотам: сохранённые позиции сохраняются,
 * освободившиеся места заполняются камерами, которых ещё нет в сетке.
 */
function resolveSlots(
  saved: (string | null)[] | undefined,
  count: number,
  activeCameras: SavedCamera[]
): (string | null)[] {
  const activeIds = new Set(activeCameras.map((camera) => camera.id))
  const used = new Set<string>()

  const slots = Array.from({ length: count }, (_, index) => {
    const cameraId = saved?.[index] ?? null
    if (cameraId === null || !activeIds.has(cameraId) || used.has(cameraId)) {
      return null
    }

    used.add(cameraId)
    return cameraId
  })

  const freeCameras = activeCameras.filter((camera) => !used.has(camera.id))
  let next = 0

  for (let index = 0; index < slots.length && next < freeCameras.length; index++) {
    if (slots[index] === null) {
      slots[index] = freeCameras[next].id
      used.add(freeCameras[next].id)
      next++
    }
  }

  return slots
}

export default function LiveGrid({ cameras, onOpenCameraSettings, onOpenArchive }: Props) {
  const isMobile = useIsMobile()
  const [preferences, setPreferences] = useState<LivePreferences>(loadLivePreferences)
  const [recording, setRecording] = useState<Record<string, RecordingLiveStatus>>({})
  const [fullscreenTile, setFullscreenTile] = useState<number | null>(null)
  const [gridFullscreen, setGridFullscreen] = useState(false)
  const [layoutOpen, setLayoutOpen] = useState(false)
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [sortOpen, setSortOpen] = useState(false)
  const pickerRef = useRef<HTMLDivElement>(null)
  const gridRef = useRef<HTMLDivElement>(null)

  const activeCameras = cameras.filter((camera) => camera.isActive)
  const mobileCameras = orderCameras(activeCameras, preferences.mobileOrder)
  const layout = preferences.layout
  const count = tileCount(layout)
  const slots = resolveSlots(preferences.layouts[layout], count, activeCameras)
  const hiddenCount = Math.max(0, activeCameras.length - count)

  useEffect(() => {
    try {
      saveLivePreferences(preferences)
      setError(null)
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : String(saveError))
    }
  }, [preferences])

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      try {
        const list = await getRecordingStatus()

        if (!cancelled) {
          setRecording(
            Object.fromEntries(list.map((item) => [item.cameraId, item]))
          )
        }
      } catch (statusError) {
        if (!cancelled) {
          console.warn('Не удалось получить состояние записи', statusError)
        }
      }
    }

    void load()
    const timer = window.setInterval(() => void load(), 5000)
    const refreshWhenVisible = () => {
      if (!document.hidden) {
        void load()
      }
    }

    document.addEventListener('visibilitychange', refreshWhenVisible)
    window.addEventListener('focus', refreshWhenVisible)

    return () => {
      cancelled = true
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
      window.removeEventListener('focus', refreshWhenVisible)
    }
  }, [])

  useEffect(() => {
    const handleFullscreenChange = () => {
      const element = document.fullscreenElement
      setGridFullscreen(element !== null && element === gridRef.current)

      const rawIndex = element?.getAttribute('data-tile-index')
      const index = rawIndex === null || rawIndex === undefined ? null : Number(rawIndex)
      setFullscreenTile(index !== null && Number.isInteger(index) ? index : null)
    }

    document.addEventListener('fullscreenchange', handleFullscreenChange)
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange)
  }, [])

  useEffect(() => {
    if (!layoutOpen) {
      return
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (pickerRef.current && !pickerRef.current.contains(event.target as Node)) {
        setLayoutOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [layoutOpen])

  function changeLayout(nextLayout: CameraLayout) {
    setDragIndex(null)
    setDragOverIndex(null)
    setPreferences((current) => {
      const focusedTile =
        current.focusedTile !== null && current.focusedTile < tileCount(nextLayout)
          ? current.focusedTile
          : null

      return { ...current, layout: nextLayout, focusedTile }
    })
  }

  function focusTile(index: number) {
    setPreferences((current) => ({ ...current, focusedTile: index }))
  }

  function toggleStretch() {
    setPreferences((current) => ({ ...current, stretch: !current.stretch }))
  }

  function toggleMuted() {
    setPreferences((current) => ({ ...current, muted: !current.muted }))
  }

  async function toggleGridFullscreen() {
    const grid = gridRef.current
    if (!grid) {
      return
    }

    try {
      if (document.fullscreenElement === grid) {
        await document.exitFullscreen()
      } else {
        await grid.requestFullscreen()
      }
    } catch {
      setError(t('grid.fullscreen.failed'))
    }
  }

  function handleTileDragStart(index: number, event: ReactDragEvent<HTMLDivElement>) {
    setDragIndex(index)
    setDragOverIndex(null)
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', String(index))
  }

  function handleTileDragOver(index: number, event: ReactDragEvent<HTMLDivElement>) {
    if (dragIndex === null || dragIndex === index) {
      return
    }

    event.preventDefault()
    event.dataTransfer.dropEffect = 'move'
    setDragOverIndex(index)
  }

  function handleTileDragLeave(index: number, event: ReactDragEvent<HTMLDivElement>) {
    const related = event.relatedTarget
    if (related instanceof Node && event.currentTarget.contains(related)) {
      return
    }

    setDragOverIndex((current) => (current === index ? null : current))
  }

  function handleTileDrop(targetIndex: number, event: ReactDragEvent<HTMLDivElement>) {
    event.preventDefault()
    const sourceIndex = dragIndex ?? Number(event.dataTransfer.getData('text/plain'))
    setDragIndex(null)
    setDragOverIndex(null)

    if (!Number.isInteger(sourceIndex) || sourceIndex === targetIndex) {
      return
    }

    setPreferences((current) => {
      const currentCount = tileCount(current.layout)
      if (
        sourceIndex < 0 ||
        sourceIndex >= currentCount ||
        targetIndex < 0 ||
        targetIndex >= currentCount
      ) {
        return current
      }

      const currentSlots = resolveSlots(
        current.layouts[current.layout],
        currentCount,
        activeCameras
      )
      const nextSlots = [...currentSlots]
      const moved = nextSlots[sourceIndex]
      nextSlots[sourceIndex] = nextSlots[targetIndex]
      nextSlots[targetIndex] = moved

      return {
        ...current,
        layouts: { ...current.layouts, [current.layout]: nextSlots }
      }
    })
  }

  function clearDragState() {
    setDragIndex(null)
    setDragOverIndex(null)
  }

  return (
    <PageLayout
      title={t('live.title')}
      bodyClassName="live-body"
      actions={
        <>
          <button
            type="button"
            className={`icon-button ${preferences.muted ? 'muted' : ''}`}
            title={preferences.muted ? t('grid.unmute') : t('grid.mute')}
            aria-label={preferences.muted ? t('grid.unmute') : t('grid.mute')}
            aria-pressed={preferences.muted}
            onClick={toggleMuted}
          >
            <SpeakerIcon muted={preferences.muted} />
          </button>

          {!isMobile ? (
            <div className="layout-picker" ref={pickerRef}>
            <button
              type="button"
              className="layout-trigger"
              title={t('grid.layout')}
              aria-label={t('grid.layout')}
              aria-haspopup="true"
              aria-expanded={layoutOpen}
              onClick={() => setLayoutOpen((open) => !open)}
            >
              <LayoutIcon layout={layout} />
              <span className="layout-chevron">▾</span>
            </button>

            {layoutOpen ? (
              <div className="layout-popover" role="menu">
                {LIVE_LAYOUTS.map((option) => (
                  <button
                    key={option}
                    type="button"
                    role="menuitemradio"
                    aria-checked={layout === option}
                    className={`layout-option ${layout === option ? 'active' : ''}`}
                    onClick={() => {
                      changeLayout(option)
                      setLayoutOpen(false)
                    }}
                  >
                    <LayoutIcon layout={option} />
                    <span className="layout-count">{tileCount(option)}</span>
                  </button>
                ))}
              </div>
            ) : null}
            </div>
          ) : null}

          {!isMobile ? (
            <button
              type="button"
              className={`icon-button ${gridFullscreen ? 'active' : ''}`}
              title={gridFullscreen ? t('grid.fullscreen.exit') : t('grid.fullscreen')}
              aria-label={gridFullscreen ? t('grid.fullscreen.exit') : t('grid.fullscreen')}
              aria-pressed={gridFullscreen}
              onClick={() => void toggleGridFullscreen()}
            >
              <FullscreenIcon exit={gridFullscreen} />
            </button>
          ) : null}

          <button
            type="button"
            className={`icon-button ${preferences.stretch ? 'active' : ''}`}
            title={t('grid.stretch')}
            aria-label={t('grid.stretch')}
            aria-pressed={preferences.stretch}
            onClick={toggleStretch}
          >
            <StretchIcon />
          </button>

          {isMobile ? (
            <button
              type="button"
              className="icon-button"
              title={t('grid.sort')}
              aria-label={t('grid.sort')}
              onClick={() => setSortOpen(true)}
            >
              <SortIcon />
            </button>
          ) : null}
        </>
      }
    >
      {error ? <p className="error">{error}</p> : null}

      <div
        ref={gridRef}
        className={isMobile ? 'live-grid mobile' : 'live-grid'}
        style={
          isMobile
            ? undefined
            : {
                gridTemplateColumns: `repeat(${LAYOUT_GRID[layout].columns}, 1fr)`,
                gridTemplateRows: `repeat(${LAYOUT_GRID[layout].rows}, 1fr)`
              }
        }
      >
        {(isMobile
          ? mobileCameras.map((camera, index) => ({ camera, index }))
          : slots.map((cameraId, index) => ({
              camera: activeCameras.find((item) => item.id === cameraId) ?? null,
              index
            }))
        ).map(({ camera, index }) => (
            <CameraTile
              key={index}
              index={index}
              camera={camera}
              profile={fullscreenTile === index ? 'main' : 'sub'}
              focused={preferences.focusedTile === index}
              stretch={preferences.stretch}
              muted={preferences.muted}
              gridArea={isMobile ? undefined : LAYOUT_TILE_AREAS[layout]?.[index]}
              dragging={!isMobile && dragIndex === index}
              dragOver={!isMobile && dragOverIndex === index}
              onDragStart={(event) => handleTileDragStart(index, event)}
              onDragOver={(event) => handleTileDragOver(index, event)}
              onDragLeave={(event) => handleTileDragLeave(index, event)}
              onDrop={(event) => handleTileDrop(index, event)}
              onDragEnd={clearDragState}
              onFocus={() => focusTile(index)}
              onOpenSettings={() => {
                if (camera) {
                  onOpenCameraSettings(camera)
                }
              }}
              onOpenArchive={() => {
                if (camera) {
                  onOpenArchive(camera)
                }
              }}
              recording={camera ? recording[camera.id] ?? null : null}
            />
          ))}
      </div>

      {activeCameras.length === 0 ? <p className="subtitle">{t('grid.noActive')}</p> : null}
      {!isMobile && hiddenCount > 0 ? (
        <p className="subtitle">{`${t('grid.hidden')} ${hiddenCount}`}</p>
      ) : null}

      {sortOpen ? (
        <CameraSortDialog
          cameras={activeCameras}
          order={preferences.mobileOrder}
          onCancel={() => setSortOpen(false)}
          onSave={(order) => {
            setPreferences((current) => ({ ...current, mobileOrder: order }))
            setSortOpen(false)
          }}
        />
      ) : null}
    </PageLayout>
  )
}
