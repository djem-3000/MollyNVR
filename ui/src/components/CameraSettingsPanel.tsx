import { useEffect, useMemo, useRef, useState } from 'react'
import {
  getCameraNetworkInfo,
  updateCameraSettings,
  type SavedCamera,
  type SavedCameraProfile
} from '../api'
import {
  audioCodecAssessment,
  codecToneClass,
  videoCodecAssessment
} from '../codecSupport'
import { t } from '../i18n'
import { audioLabel, profileLabel, resolutionLabel } from '../cameraProfileFormat'
import { BitrateCell, BitrateLegend } from './BitrateCell'
import CameraPreview from './CameraPreview'
import RecordingSection, { type RecordingHandle } from './RecordingSection'

type Props = {
  camera: SavedCamera
  onSaved: (camera: SavedCamera) => Promise<void> | void
  onBack: () => void
}

type PreviewProfile = {
  kind: 'main' | 'sub'
  profileId: string | null
}

type Toast = {
  id: number
  message: string
}

function PlayIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M8 5v14l11-7z" />
    </svg>
  )
}

function previewFor(profile: SavedCameraProfile): PreviewProfile {
  return {
    kind: profile.kind,
    profileId: profile.kind === 'sub' ? profile.id : null
  }
}

type StreamSwitchProps = {
  kind: 'main' | 'sub'
  checked: boolean
  groupName: string
  label: string
  onChange: () => void
}

function StreamSwitch({ kind, checked, groupName, label, onChange }: StreamSwitchProps) {
  const isMain = kind === 'main'

  return (
    <label
      className={`stream-toggle ${isMain ? 'disabled' : ''}`}
      title={isMain ? t('cameraSettings.mainStreamAlwaysUsed') : t('cameraSettings.useStream')}
    >
      <input
        type={isMain ? 'checkbox' : 'radio'}
        name={isMain ? undefined : groupName}
        checked={checked}
        disabled={isMain}
        readOnly={isMain}
        onChange={isMain ? undefined : onChange}
        aria-label={
          isMain
            ? t('cameraSettings.mainStreamAlwaysUsed')
            : `${t('cameraSettings.useStream')}: ${label}`
        }
      />
      <span className="stream-toggle-track" aria-hidden="true">
        <span className="stream-toggle-thumb" />
      </span>
    </label>
  )
}

export default function CameraSettingsPanel({ camera, onSaved, onBack }: Props) {
  const [name, setName] = useState(camera.name)
  const [subProfileId, setsubProfileId] = useState(
    camera.subProfileId
  )
  const [previewProfile, setPreviewProfile] = useState<PreviewProfile>(() => {
    const initialProfile = camera.profiles.find((profile) => profile.kind === 'main') ??
      camera.profiles[0]

    return initialProfile ? previewFor(initialProfile) : { kind: 'main', profileId: null }
  })
  const [dhcpEnabled, setDhcpEnabled] = useState<boolean | null | undefined>(undefined)
  const [saveBusy, setSaveBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState<Toast | null>(null)
  const recordingRef = useRef<RecordingHandle>(null)

  const subProfiles = useMemo(
    () => camera.profiles.filter((profile) => profile.kind === 'sub'),
    [camera.profiles]
  )
  const activeSubProfileId = subProfiles.some((profile) => profile.id === subProfileId)
    ? subProfileId
    : subProfiles[0]?.id || ''
  const nameReady = name.trim() !== ''

  useEffect(() => {
    let cancelled = false

    setDhcpEnabled(undefined)
    void getCameraNetworkInfo(camera.id)
      .then((info) => {
        if (!cancelled) {
          setDhcpEnabled(info.dhcpEnabled)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setDhcpEnabled(null)
        }
      })

    return () => {
      cancelled = true
    }
  }, [camera.id])

  useEffect(() => {
    if (!toast) {
      return
    }

    const timer = window.setTimeout(() => setToast(null), 3000)
    return () => window.clearTimeout(timer)
  }, [toast])

  function showToast(message: string) {
    setToast((current) => ({ id: (current?.id ?? 0) + 1, message }))
  }

  function handleNameChange(value: string) {
    setName(value)
    setError(null)
  }

  function handleSubProfileChange(value: string) {
    setsubProfileId(value)
    setError(null)
  }

  function isPlaying(profile: SavedCameraProfile): boolean {
    return previewProfile.kind === profile.kind &&
      (profile.kind === 'main' || previewProfile.profileId === profile.id)
  }

  async function handleSave() {
    if (!nameReady) {
      setError(t('camera.name.required'))
      return
    }

    setSaveBusy(true)
    setError(null)

    try {
      const updated = await updateCameraSettings(camera.id, {
        name: name.trim(),
        subProfileId: activeSubProfileId || null
      })
      await onSaved(updated)
      await recordingRef.current?.save()
      showToast(t('cameraSettings.saved'))
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : String(saveError))
    } finally {
      setSaveBusy(false)
    }
  }

  const dhcpLabel =
    dhcpEnabled === undefined
      ? t('camera.dhcp.loading')
      : dhcpEnabled === null
        ? t('camera.unknown')
        : dhcpEnabled
          ? t('camera.dhcp.yes')
          : t('camera.dhcp.no')

  return (
    <div className="camera-settings-content">
      {toast ? (
        <div key={toast.id} className="toast toast-success" role="status" aria-live="polite">
          {toast.message}
        </div>
      ) : null}

      <div className="camera-settings-top">
        <section className="camera-settings-preview">
          <h2>{t('cameraSettings.preview')}</h2>
          <CameraPreview
            cameraId={camera.id}
            cameraName={camera.name}
            profile={previewProfile.kind}
            profileId={previewProfile.profileId}
          />
        </section>

        <section className="camera-settings-info">
          <h2>{t('camera.name')}</h2>
          <label className="wizard-name camera-settings-name">
            <input
              value={name}
              onChange={(event) => handleNameChange(event.target.value)}
              placeholder={t('camera.name.placeholder')}
              autoComplete="off"
              required
              aria-label={t('camera.name')}
              aria-required="true"
              aria-invalid={!nameReady}
            />
            {!nameReady ? (
              <span className="subtitle">{t('camera.name.required')}</span>
            ) : null}
          </label>

          <h2>{t('cameraSettings.metadata')}</h2>
          <dl className="camera-device">
            <div>
              <dt>{t('camera.vendor')}</dt>
              <dd>{camera.vendor || t('camera.unknown')}</dd>
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
              <dt>{t('camera.host')}</dt>
              <dd>{camera.host}</dd>
            </div>
            <div>
              <dt>{t('cameraSettings.dhcp')}</dt>
              <dd>{dhcpLabel}</dd>
            </div>
          </dl>
        </section>
      </div>

      <section className="wizard-streams">
        <div className="streams-panel">
          <h2>{t('cameraSettings.streams')}</h2>
          <table className="camera-profiles">
            <thead>
              <tr>
                <th className="profile-select-column" aria-label={t('cameraSettings.useStream')} />
                <th>{t('camera.profile.kind')}</th>
                <th>{t('camera.profile.codec')}</th>
                <th>{t('camera.profile.resolution')}</th>
                <th>{t('camera.profile.fps')}</th>
                <th>{t('camera.profile.bitrate')}</th>
                <th>{t('camera.profile.audio')}</th>
                <th className="profile-play-column" aria-label={t('cameraSettings.play')} />
              </tr>
            </thead>
            <tbody>
              {camera.profiles.map((profile) => {
                const isSelectedSub =
                  profile.kind === 'sub' && profile.id === activeSubProfileId
                const videoCodec = videoCodecAssessment(profile)
                const audioCodec = audioCodecAssessment(profile)
                const playing = isPlaying(profile)

                return (
                  <tr key={profile.id}>
                    <td className="profile-select-cell">
                      <StreamSwitch
                        kind={profile.kind}
                        checked={profile.kind === 'main' || isSelectedSub}
                        groupName={`stream-${camera.id}`}
                        label={profileLabel(profile)}
                        onChange={() => handleSubProfileChange(profile.id)}
                      />
                    </td>
                    <td>{profileLabel(profile)}</td>
                    <td>
                      <span
                        className={codecToneClass(videoCodec.tone)}
                        title={videoCodec.tooltip}
                      >
                        {profile.videoCodec}
                      </span>
                    </td>
                    <td>{resolutionLabel(profile)}</td>
                    <td>{profile.frameRate}</td>
                    <BitrateCell profile={profile} />
                    <td>
                      <span
                        className={codecToneClass(audioCodec.tone)}
                        title={audioCodec.tooltip}
                      >
                        {audioLabel(profile)}
                      </span>
                    </td>
                    <td className="profile-play-cell">
                      <button
                        type="button"
                        className={`profile-play ${playing ? 'active' : ''}`}
                        aria-label={t('cameraSettings.play')}
                        aria-pressed={playing}
                        title={t('cameraSettings.play')}
                        onClick={() => setPreviewProfile(previewFor(profile))}
                      >
                        <PlayIcon />
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>

          <BitrateLegend />
        </div>
      </section>

      <RecordingSection ref={recordingRef} camera={camera} />

      {error ? <p className="error">{error}</p> : null}

      <div className="wizard-actions">
        <button type="button" className="secondary" onClick={onBack}>
          {t('cameraSettings.back')}
        </button>
        <button type="button" disabled={saveBusy || !nameReady} onClick={() => void handleSave()}>
          {saveBusy ? t('cameraSettings.save.busy') : t('cameraSettings.save')}
        </button>
      </div>
    </div>
  )
}
