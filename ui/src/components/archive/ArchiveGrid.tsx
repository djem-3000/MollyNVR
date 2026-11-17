import { useEffect, useRef, useState, type DragEvent as ReactDragEvent } from 'react'
import type { ArchiveCamera } from '../../api'
import { t } from '../../i18n'
import { ArchiveRecBadge, FullscreenIcon, RecordIcon, ScreenshotIcon } from './ArchivePlayer'
import { useArchiveCapture } from './useArchiveCapture'
import type { ArchivePlaybackController as Controller } from './useArchivePlayback'

type Props = {
  slots: (string | null)[]
  cameras: ArchiveCamera[]
  controllers: Controller[]
  focusedIndex: number
  muted: boolean
  onFocus: (index: number) => void
  onSlotsChange: (next: (string | null)[]) => void
}

type TileProps = {
  index: number
  cameraName: string | null
  controller: Controller
  focused: boolean
  muted: boolean
  onFocus: () => void
  onDropCamera: (index: number, cameraId: string) => void
  onSwap: (from: number, to: number) => void
}

function ArchiveGridTile({
  index,
  cameraName,
  controller,
  focused,
  muted,
  onFocus,
  onDropCamera,
  onSwap
}: TileProps) {
  const containerRef = useRef<HTMLElement | null>(null)
  const capture = useArchiveCapture(controller, cameraName ?? 'archive')
  const [dragOver, setDragOver] = useState(false)
  const [fullscreen, setFullscreen] = useState(false)

  useEffect(() => {
    const handleChange = () => setFullscreen(document.fullscreenElement === containerRef.current)
    document.addEventListener('fullscreenchange', handleChange)
    return () => document.removeEventListener('fullscreenchange', handleChange)
  }, [])

  function toggleFullscreen() {
    if (document.fullscreenElement) {
      void document.exitFullscreen()
      return
    }

    void containerRef.current?.requestFullscreen().catch(() => {
      console.warn('Не удалось открыть полноэкранный режим')
    })
  }

  function handleDrop(event: ReactDragEvent<HTMLElement>) {
    event.preventDefault()
    setDragOver(false)

    const rawIndex = event.dataTransfer.getData('application/x-molly-slot')

    if (rawIndex !== '') {
      const from = Number(rawIndex)

      if (Number.isInteger(from) && from >= 0 && from !== index) {
        onSwap(from, index)
        return
      }
    }

    const cameraId =
      event.dataTransfer.getData('application/x-molly-camera') ||
      event.dataTransfer.getData('text/plain')

    if (cameraId) {
      onDropCamera(index, cameraId)
    }
  }

  return (
    <section
      ref={containerRef}
      className={`archive-tile${cameraName ? '' : ' empty'}${dragOver ? ' drop-active' : ''}${
        focused ? ' focused' : ''
      }`}
      tabIndex={0}
      role="group"
      aria-label={cameraName ?? t('archive.grid.empty')}
      draggable={cameraName !== null}
      onClick={() => {
        if (cameraName) {
          onFocus()
        }
      }}
      onDragStart={(event) => {
        if (!cameraName) {
          event.preventDefault()
          return
        }

        event.dataTransfer.effectAllowed = 'move'
        event.dataTransfer.setData('application/x-molly-slot', String(index))
      }}
      onDragOver={(event) => {
        const types = event.dataTransfer.types
        const cameraDrag =
          types.includes('application/x-molly-camera') || types.includes('text/plain')
        const slotDrag = types.includes('application/x-molly-slot')

        if (!cameraDrag && !slotDrag) {
          return
        }

        event.preventDefault()
        // dropEffect должен совпадать с effectAllowed источника, иначе drop не произойдёт.
        event.dataTransfer.dropEffect = cameraDrag ? 'copy' : 'move'
        setDragOver(true)
      }}
      onDragLeave={(event) => {
        const related = event.relatedTarget
        if (related instanceof Node && event.currentTarget.contains(related)) {
          return
        }

        setDragOver(false)
      }}
      onDrop={handleDrop}
    >
      {cameraName ? (
        <>
          <video ref={controller.videoRef} playsInline muted={muted || !focused} />

          <div className="archive-tile-topline">
            <span className="archive-tile-name">{cameraName}</span>
            {capture.recording ? <ArchiveRecBadge seconds={capture.seconds} /> : null}
          </div>

          {controller.error ? (
            <div className="archive-tile-hint error">
              {t('archive.player.error', { error: controller.error })}
            </div>
          ) : null}
          {controller.loading ? (
            <div className="archive-tile-hint">{t('archive.player.loading')}</div>
          ) : null}

          <div className="archive-stage-overlay">
            <button
              type="button"
              className={
                capture.recording ? 'archive-stage-button record active' : 'archive-stage-button record'
              }
              title={capture.recording ? t('archive.record.stop') : t('archive.record.start')}
              aria-label={capture.recording ? t('archive.record.stop') : t('archive.record.start')}
              disabled={!controller.playback}
              onClick={capture.toggleRecording}
            >
              <RecordIcon />
            </button>
            <button
              type="button"
              className="archive-stage-button"
              title={t('archive.screenshot')}
              aria-label={t('archive.screenshot')}
              disabled={!controller.playback}
              onClick={capture.takeSnapshot}
            >
              <ScreenshotIcon />
            </button>
            <button
              type="button"
              className="archive-stage-button"
              title={fullscreen ? t('archive.fullscreen.exit') : t('archive.fullscreen')}
              aria-label={fullscreen ? t('archive.fullscreen.exit') : t('archive.fullscreen')}
              onClick={toggleFullscreen}
            >
              <FullscreenIcon />
            </button>
          </div>

          {capture.error ? <p className="archive-action-error">{capture.error}</p> : null}
        </>
      ) : (
        <div className="archive-tile-empty">
          <span>{t('archive.grid.empty')}</span>
        </div>
      )}
    </section>
  )
}

export default function ArchiveGrid({
  slots,
  cameras,
  controllers,
  focusedIndex,
  muted,
  onFocus,
  onSlotsChange
}: Props) {
  function nameOf(cameraId: string | null): string | null {
    if (!cameraId) {
      return null
    }

    return cameras.find((camera) => camera.id === cameraId)?.name ?? null
  }

  function dropCamera(index: number, cameraId: string) {
    if (!cameras.some((camera) => camera.id === cameraId)) {
      return
    }

    const next = [...slots]
    const existing = next.indexOf(cameraId)

    if (existing >= 0) {
      next[existing] = next[index]
    }

    next[index] = cameraId
    onSlotsChange(next)
  }

  function swap(from: number, to: number) {
    const next = [...slots]
    const moved = next[from]
    next[from] = next[to]
    next[to] = moved
    onSlotsChange(next)
  }

  return (
    <div className="archive-grid">
      {slots.map((cameraId, index) => (
        <ArchiveGridTile
          key={index}
          index={index}
          cameraName={nameOf(cameraId)}
          controller={controllers[index]}
          focused={focusedIndex === index}
          muted={muted}
          onFocus={() => onFocus(index)}
          onDropCamera={dropCamera}
          onSwap={swap}
        />
      ))}
    </div>
  )
}
