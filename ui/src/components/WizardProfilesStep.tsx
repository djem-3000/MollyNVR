import type { CameraInfo, CameraProfile } from '../api'
import { audioLabel, resolutionLabel } from '../cameraProfileFormat'
import { t } from '../i18n'
import { profileRoleLabel, type SnapshotEntry } from '../wizardTypes'
import { BitrateCell, BitrateLegend } from './BitrateCell'

type Props = {
  camera: CameraInfo
  mainProfileId: string
  subProfileId: string | null
  snapshotBusyId: string | null
  snapshotError: string | null
  preview: SnapshotEntry | null
  onSubProfileChange: (profileId: string) => void
  onSnapshot: (profile: CameraProfile) => void
  onBack: () => void
  onNext: () => void
}

export default function WizardProfilesStep({
  camera,
  mainProfileId,
  subProfileId,
  snapshotBusyId,
  snapshotError,
  preview,
  onSubProfileChange,
  onSnapshot,
  onBack,
  onNext
}: Props) {
  const hasProfiles = camera.profiles.length > 0

  return (
    <section className="page-section">
      <h2 tabIndex={-1}>{t('wizard.profiles.title')}</h2>
      <p className="subtitle">{t('wizard.profiles.hint')}</p>

      {hasProfiles ? (
        <div className="streams-panel">
          <table className="camera-profiles wizard-table" aria-label={t('camera.profiles')}>
            <thead>
              <tr>
                <th className="profile-select-column" aria-label={t('camera.substream')} />
                <th scope="col">{t('camera.profile.kind')}</th>
                <th scope="col">{t('camera.profile.codec')}</th>
                <th scope="col">{t('camera.profile.resolution')}</th>
                <th scope="col">{t('camera.profile.fps')}</th>
                <th scope="col">{t('camera.profile.bitrate')}</th>
                <th scope="col">{t('camera.profile.audio')}</th>
                <th scope="col" aria-label={t('camera.snapshot')} />
              </tr>
            </thead>
            <tbody>
              {camera.profiles.map((profile) => {
                const isMain = profile.id === mainProfileId
                const selected = isMain || profile.id === subProfileId
                const busy = snapshotBusyId === profile.id
                const roleLabel = profileRoleLabel(profile, camera.profiles, mainProfileId)

                return (
                  <tr key={profile.id} className={selected ? 'profile-selected' : undefined}>
                    <td className="profile-select-cell">
                      <input
                        type="radio"
                        name={isMain ? 'wizard-main-profile' : 'wizard-sub-profile'}
                        checked={selected}
                        disabled={isMain}
                        aria-label={
                          isMain
                            ? t('wizard.profiles.mainAuto')
                            : t('wizard.profiles.selectSub', { profile: roleLabel })
                        }
                        onChange={() => onSubProfileChange(profile.id)}
                      />
                    </td>
                    <td>{roleLabel}</td>
                    <td>{profile.videoCodec}</td>
                    <td>{resolutionLabel(profile)}</td>
                    <td>{profile.frameRate}</td>
                    <BitrateCell profile={profile} />
                    <td>{audioLabel(profile)}</td>
                    <td className="camera-actions">
                      <button
                        type="button"
                        className="snapshot-button"
                        disabled={snapshotBusyId !== null}
                        aria-busy={busy}
                        aria-label={t('wizard.profiles.snapshot', { profile: roleLabel })}
                        onClick={() => onSnapshot(profile)}
                      >
                        <span>{busy ? t('camera.snapshot.busy') : t('camera.snapshot')}</span>
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>

          <BitrateLegend />
        </div>
      ) : (
        <p className="wizard-note error" role="alert">
          {t('wizard.profiles.empty')}
        </p>
      )}

      {snapshotError ? (
        <p className="wizard-note error" role="status" aria-live="polite">
          {snapshotError}
        </p>
      ) : null}

      {preview ? (
        <figure className="camera-snapshot">
          <img src={preview.url} alt={preview.label} />
          <figcaption>{preview.label}</figcaption>
        </figure>
      ) : null}

      <div className="wizard-actions spread">
        <button type="button" className="secondary" onClick={onBack}>
          {t('wizard.back')}
        </button>
        <button type="button" disabled={!hasProfiles} onClick={onNext}>
          {t('wizard.next')}
        </button>
      </div>
    </section>
  )
}
