import type { CameraInfo, CameraProfile } from '../api'
import { audioLabel, resolutionLabel } from '../cameraProfileFormat'
import { t } from '../i18n'
import { profileRoleLabel, type SnapshotEntry } from '../wizardTypes'
import { BitrateCell, BitrateLegend } from './BitrateCell'

type Props = {
  camera: CameraInfo
  host: string
  name: string
  nameValid: boolean
  mainProfileId: string
  profiles: CameraProfile[]
  snapshot: SnapshotEntry | null
  snapshotBusy: boolean
  snapshotError: string | null
  saveBusy: boolean
  error: string | null
  onNameChange: (value: string) => void
  onRetrySnapshot: () => void
  onBack: () => void
  onSave: () => void
}

export default function WizardSaveStep({
  camera,
  host,
  name,
  nameValid,
  mainProfileId,
  profiles,
  snapshot,
  snapshotBusy,
  snapshotError,
  saveBusy,
  error,
  onNameChange,
  onRetrySnapshot,
  onBack,
  onSave
}: Props) {
  return (
    <div className="camera-settings-content">
      <section className="page-section">
        <h2 tabIndex={-1}>{t('wizard.save.title')}</h2>
        <p className="subtitle">{t('wizard.save.hint')}</p>
      </section>

      <div className="camera-settings-top">
        <section className="camera-settings-preview">
          <h2>{t('wizard.save.preview')}</h2>
          {snapshot ? (
            <div className="camera-preview">
              <img src={snapshot.url} alt={t('wizard.save.preview')} />
            </div>
          ) : (
            <div className="camera-preview camera-preview-placeholder wizard-empty">
              <span>
                {snapshotBusy ? t('wizard.save.snapshotLoading') : t('wizard.save.snapshotEmpty')}
              </span>
              {!snapshotBusy ? (
                <button type="button" className="secondary" onClick={onRetrySnapshot}>
                  {t('wizard.save.snapshotRetry')}
                </button>
              ) : null}
            </div>
          )}
          {snapshotError ? (
            <p className="wizard-note error" role="status" aria-live="polite">
              {snapshotError}
            </p>
          ) : null}
        </section>

        <section className="camera-settings-info">
          <h2>{t('cameraSettings.metadata')}</h2>
          <dl className="camera-device">
            <div>
              <dt>{t('camera.device')}</dt>
              <dd>{camera.deviceName ?? camera.vendor ?? t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('camera.model')}</dt>
              <dd>{camera.model || t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('camera.firmware')}</dt>
              <dd>{camera.firmware || t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('camera.serial')}</dt>
              <dd>{camera.serialNumber ?? t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('wizard.connect.ip')}</dt>
              <dd>{host}</dd>
            </div>
          </dl>

          <label className="wizard-name">
            {t('wizard.name')}
            <input
              value={name}
              onChange={(event) => onNameChange(event.target.value)}
              placeholder={t('camera.name.placeholder')}
              autoComplete="off"
              required
              aria-required="true"
              aria-invalid={!nameValid}
            />
            {!nameValid ? <span className="subtitle">{t('wizard.name.required')}</span> : null}
          </label>
        </section>
      </div>

      {profiles.length > 0 ? (
        <section className="wizard-streams">
          <div className="streams-panel">
            <h2>{t('camera.selectedProfiles')}</h2>
            <table className="camera-profiles wizard-table" aria-label={t('camera.selectedProfiles')}>
              <thead>
                <tr>
                  <th scope="col">{t('camera.profile.kind')}</th>
                  <th scope="col">{t('camera.profile.codec')}</th>
                  <th scope="col">{t('camera.profile.resolution')}</th>
                  <th scope="col">{t('camera.profile.fps')}</th>
                  <th scope="col">{t('camera.profile.bitrate')}</th>
                  <th scope="col">{t('camera.profile.audio')}</th>
                </tr>
              </thead>
              <tbody>
                {profiles.map((profile) => (
                  <tr key={profile.id}>
                    <td>{profileRoleLabel(profile, camera.profiles, mainProfileId)}</td>
                    <td>{profile.videoCodec}</td>
                    <td>{resolutionLabel(profile)}</td>
                    <td>{profile.frameRate}</td>
                    <BitrateCell profile={profile} />
                    <td>{audioLabel(profile)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <BitrateLegend />
          </div>
        </section>
      ) : null}

      {error ? (
        <div className="wizard-note error" role="alert">
          <strong>{t('wizard.save.errorTitle')}</strong>
          <span className="detail">{error}</span>
        </div>
      ) : null}

      <div className="wizard-actions spread">
        <button type="button" className="secondary" onClick={onBack} disabled={saveBusy}>
          {t('wizard.back')}
        </button>
        <button type="button" disabled={saveBusy || !nameValid} onClick={onSave}>
          {saveBusy ? t('wizard.save.busy') : t('wizard.save.submit')}
        </button>
      </div>
    </div>
  )
}
