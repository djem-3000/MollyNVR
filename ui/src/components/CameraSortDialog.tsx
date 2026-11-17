import { useEffect, useState, type PointerEvent as ReactPointerEvent } from 'react'
import type { SavedCamera } from '../api'
import { t } from '../i18n'
import { orderCameras } from '../livePreferences'

type Props = {
  cameras: SavedCamera[]
  order: string[]
  onCancel: () => void
  onSave: (order: string[]) => void
}

function HandleIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 7h14M5 12h14M5 17h14" />
    </svg>
  )
}

export default function CameraSortDialog({ cameras, order, onCancel, onSave }: Props) {
  const [draft, setDraft] = useState<string[]>(() =>
    orderCameras(cameras, order).map((camera) => camera.id)
  )
  const [draggingId, setDraggingId] = useState<string | null>(null)
  const [overId, setOverId] = useState<string | null>(null)

  const byId = new Map(cameras.map((camera) => [camera.id, camera]))

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onCancel()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onCancel])

  function handlePointerDown(event: ReactPointerEvent<HTMLButtonElement>, cameraId: string) {
    event.preventDefault()

    try {
      event.currentTarget.setPointerCapture(event.pointerId)
    } catch {
      // синтетические события могут не поддерживать capture
    }

    setDraggingId(cameraId)
    setOverId(cameraId)
  }

  function handlePointerMove(event: ReactPointerEvent<HTMLButtonElement>) {
    if (!draggingId) {
      return
    }

    const element = document.elementFromPoint(event.clientX, event.clientY)
    const row = element?.closest<HTMLElement>('[data-camera-id]')
    const targetId = row?.dataset.cameraId

    if (!targetId || targetId === draggingId) {
      return
    }

    setOverId(targetId)
    setDraft((current) => {
      const from = current.indexOf(draggingId)
      const to = current.indexOf(targetId)

      if (from < 0 || to < 0 || from === to) {
        return current
      }

      const next = [...current]
      next.splice(from, 1)
      next.splice(to, 0, draggingId)
      return next
    })
  }

  function handlePointerUp(event: ReactPointerEvent<HTMLButtonElement>) {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }

    setDraggingId(null)
    setOverId(null)
  }

  return (
    <div className="sort-dialog-backdrop">
      <section
        className="sort-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={t('grid.sort.title')}
      >
        <header className="sort-dialog-head">
          <button type="button" className="sort-dialog-button" onClick={onCancel}>
            {t('grid.sort.cancel')}
          </button>
          <h2>{t('grid.sort.title')}</h2>
          <button
            type="button"
            className="sort-dialog-button primary"
            onClick={() => onSave(draft)}
          >
            {t('grid.sort.save')}
          </button>
        </header>

        <div className="sort-dialog-list">
          {draft.map((cameraId) => {
            const camera = byId.get(cameraId)

            if (!camera) {
              return null
            }

            const classes = ['sort-camera']

            if (cameraId === draggingId) {
              classes.push('dragging')
            } else if (cameraId === overId) {
              classes.push('over')
            }

            return (
              <div key={cameraId} className={classes.join(' ')} data-camera-id={cameraId}>
                <span className="sort-camera-name">{camera.name}</span>
                <button
                  type="button"
                  className="sort-handle"
                  aria-label={`${t('grid.sort.handle')}: ${camera.name}`}
                  onPointerDown={(event) => handlePointerDown(event, cameraId)}
                  onPointerMove={handlePointerMove}
                  onPointerUp={handlePointerUp}
                  onPointerCancel={handlePointerUp}
                >
                  <HandleIcon />
                </button>
              </div>
            )
          })}
        </div>
      </section>
    </div>
  )
}
