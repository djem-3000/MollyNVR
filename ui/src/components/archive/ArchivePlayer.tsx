import { t } from '../../i18n'
import { formatRecordingTimer } from './archiveTime'
import { useArchiveCapture } from './useArchiveCapture'
import type { ArchivePlaybackController as Controller } from './useArchivePlayback'

type Props = {
  cameraName: string
  controller: Controller
  muted?: boolean
}

export function PlayIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M7 4v16l13-8z" />
    </svg>
  )
}

export function PauseIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="6.5" y="4.5" width="4" height="15" rx="1" />
      <rect x="13.5" y="4.5" width="4" height="15" rx="1" />
    </svg>
  )
}

export function RecordIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="6" />
    </svg>
  )
}

export function ScreenshotIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="7" width="18" height="13" rx="2" />
      <circle cx="12" cy="13" r="4" />
      <path d="M8 7l1.5-2h5L16 7" />
    </svg>
  )
}

export function FullscreenIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />
    </svg>
  )
}

export function ArchiveRecBadge({ seconds }: { seconds: number }) {
  return (
    <span className="archive-rec" aria-live="polite">
      <span className="archive-rec-dot" aria-hidden="true" />
      <span className="archive-rec-label">REC</span>
      <span className="archive-rec-timer">{formatRecordingTimer(seconds)}</span>
    </span>
  )
}

export default function ArchivePlayer({ cameraName, controller, muted = false }: Props) {
  const capture = useArchiveCapture(controller, cameraName)
  const hasPlayback = controller.playback !== null
  const hint = !hasPlayback && !controller.loading && !controller.error

  return (
    <section className="archive-player">
      <video ref={controller.videoRef} playsInline muted={muted} />

      <div className="archive-player-topline">
        <span className="archive-player-camera">{cameraName}</span>
        {capture.recording ? <ArchiveRecBadge seconds={capture.seconds} /> : null}
        {controller.playback?.transcoded ? (
          <span className="badge" title={t('archive.transcoded.hint')}>
            {t('archive.transcoded')}
          </span>
        ) : null}
      </div>

      {hint ? <div className="archive-player-hint">{t('archive.player.placeholder')}</div> : null}      {controller.loading ? (
        <div className="archive-player-hint">{t('archive.player.loading')}</div>
      ) : null}
      {controller.error ? (
        <div className="archive-player-hint error">
          {t('archive.player.error', { error: controller.error })}
        </div>
      ) : null}

      <div className="archive-stage-overlay">
        <button
          type="button"
          className={capture.recording ? 'archive-stage-button record active' : 'archive-stage-button record'}
          title={capture.recording ? t('archive.record.stop') : t('archive.record.start')}
          aria-label={capture.recording ? t('archive.record.stop') : t('archive.record.start')}
          disabled={!hasPlayback}
          onClick={capture.toggleRecording}
        >
          <RecordIcon />
        </button>
        <button
          type="button"
          className="archive-stage-button"
          title={t('archive.screenshot')}
          aria-label={t('archive.screenshot')}
          disabled={!hasPlayback}
          onClick={capture.takeSnapshot}
        >
          <ScreenshotIcon />
        </button>
      </div>

      {capture.error ? <p className="archive-action-error">{capture.error}</p> : null}
    </section>
  )
}
