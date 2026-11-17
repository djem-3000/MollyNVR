import { useState } from 'react'
import { deleteCamera, setCameraActive, type SavedCamera } from '../api'
import { t } from '../i18n'

type Props = {
  cameras: SavedCamera[]
  onChanged: () => void
  onEdit: (camera: SavedCamera) => void
}

function EditIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 20h9" />
      <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z" />
    </svg>
  )
}

function TrashIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M3 6h18" />
      <path d="M8 6V4h8v2" />
      <path d="M19 6l-1 14H6L5 6" />
      <path d="M10 11v5M14 11v5" />
    </svg>
  )
}

export default function CameraList({ cameras, onChanged, onEdit }: Props) {
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleDelete(camera: SavedCamera) {
    if (!window.confirm(t('cameras.delete.confirm'))) {
      return
    }

    setBusyId(camera.id)
    setError(null)

    try {
      await deleteCamera(camera.id)
      onChanged()
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : String(deleteError))
    } finally {
      setBusyId(null)
    }
  }

  async function handleToggle(camera: SavedCamera) {
    setBusyId(camera.id)
    setError(null)

    try {
      await setCameraActive(camera.id, !camera.isActive)
      onChanged()
    } catch (toggleError) {
      setError(toggleError instanceof Error ? toggleError.message : String(toggleError))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="page-section">
      {cameras.length === 0 ? <p className="subtitle">{t('cameras.empty')}</p> : null}

      {cameras.length > 0 ? (
        <table className="camera-profiles cameras-table">
          <thead>
            <tr>
              <th className="camera-active-column" aria-label={t('cameras.active')} />
              <th>{t('cameras.name')}</th>
              <th>{t('cameras.vendor')}</th>
              <th>{t('cameras.model')}</th>
              <th>{t('cameras.serial')}</th>
              <th>{t('cameras.address')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {cameras.map((camera) => (
              <tr key={camera.id} className={camera.isActive ? '' : 'inactive'}>
                <td className="camera-active-cell">
                  <label
                    className={`stream-toggle ${busyId === camera.id ? 'disabled' : ''}`}
                    title={camera.isActive ? t('cameras.active') : t('cameras.inactive')}
                  >
                    <input
                      type="checkbox"
                      checked={camera.isActive}
                      disabled={busyId === camera.id}
                      onChange={() => void handleToggle(camera)}
                      aria-label={`${t('cameras.active')}: ${camera.name}`}
                    />
                    <span className="stream-toggle-track" aria-hidden="true">
                      <span className="stream-toggle-thumb" />
                    </span>
                  </label>
                </td>
                <td>{camera.name}</td>
                <td>{camera.vendor || t('camera.unknown')}</td>
                <td>{camera.model || t('camera.unknown')}</td>
                <td>{camera.serialNumber ?? t('camera.unknown')}</td>
                <td>{camera.host}</td>
                <td className="camera-actions">
                  <button
                    type="button"
                    className="icon-action"
                    title={t('cameras.edit')}
                    aria-label={t('cameras.edit')}
                    disabled={busyId === camera.id}
                    onClick={() => onEdit(camera)}
                  >
                    <EditIcon />
                  </button>
                  <button
                    type="button"
                    className="icon-action danger"
                    title={t('cameras.delete')}
                    aria-label={t('cameras.delete')}
                    disabled={busyId === camera.id}
                    onClick={() => void handleDelete(camera)}
                  >
                    <TrashIcon />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      {error ? <p className="error">{error}</p> : null}
    </section>
  )
}
