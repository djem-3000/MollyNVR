import type { ArchiveCamera } from '../../api'
import { t } from '../../i18n'

type Props = {
  cameras: ArchiveCamera[]
  selectedId: string | null
  loading: boolean
  onSelect: (cameraId: string) => void
}

export default function ArchiveCameraList({ cameras, selectedId, loading, onSelect }: Props) {
  return (
    <aside className="archive-camera-list">
      <h2>{t('archive.cameras')}</h2>

      {loading ? <p className="subtitle">{t('archive.cameras.loading')}</p> : null}
      {!loading && cameras.length === 0 ? (
        <p className="subtitle">{t('archive.cameras.empty')}</p>
      ) : null}

      <div className="archive-camera-items">
        {cameras.map((camera) => {
          const classes = ['archive-camera-item']

          if (camera.id === selectedId) {
            classes.push('active')
          }

          if (!camera.isActive) {
            classes.push('inactive')
          }

          return (
            <button
              key={camera.id}
              type="button"
              className={classes.join(' ')}
              title={camera.hasRecordingsToday ? t('archive.cameras.today') : undefined}
              onClick={() => onSelect(camera.id)}
            >
              <span className="archive-camera-name">{camera.name}</span>
              {camera.hasRecordingsToday ? (
                <span className="archive-camera-dot" aria-label={t('archive.cameras.today')} />
              ) : null}
            </button>
          )
        })}
      </div>
    </aside>
  )
}
