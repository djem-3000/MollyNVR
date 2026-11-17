import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useState } from 'react'
import {
  getCameraRecording,
  saveCameraRecording,
  type CameraEventType,
  type CameraRecording,
  type SavedCamera
} from '../api'
import { profileLabel } from '../cameraProfileFormat'
import { formatBytes } from '../format'
import { t } from '../i18n'
import EventIcon from './EventIcon'
import ScheduleGrid from './ScheduleGrid'

export type RecordingHandle = {
  save: () => Promise<void>
}

type Props = {
  camera: SavedCamera
}

const ROLL_VALUES = [0, 15, 30, 60]

const RecordingSection = forwardRef<RecordingHandle, Props>(function RecordingSection({ camera }, ref) {
  const [view, setView] = useState<CameraRecording | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [enabled, setEnabled] = useState(false)
  const [profileId, setProfileId] = useState(camera.profiles[0]?.id ?? '')
  const [continuousEnabled, setContinuousEnabled] = useState(false)
  const [continuousSchedule, setContinuousSchedule] = useState('')
  const [eventEnabled, setEventEnabled] = useState(false)
  const [eventSchedule, setEventSchedule] = useState('')
  const [eventTypes, setEventTypes] = useState<string[]>([])
  const [preRoll, setPreRoll] = useState(0)
  const [postRoll, setPostRoll] = useState(0)

  useEffect(() => {
    let cancelled = false

    setLoading(true)
    setError(null)

    getCameraRecording(camera.id)
      .then((value) => {
        if (cancelled) {
          return
        }

        setView(value)
        setEnabled(value.enabled)
        setProfileId(value.profileId || camera.profiles[0]?.id || '')
        setContinuousEnabled(value.continuousEnabled)
        setContinuousSchedule(value.continuousSchedule)
        setEventEnabled(value.eventEnabled)
        setEventSchedule(value.eventSchedule)
        setEventTypes(value.eventTypes)
        setPreRoll(value.preRollSeconds)
        setPostRoll(value.postRollSeconds)
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : t('recording.loadError'))
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [camera.id, camera.profiles])

  const supported = useMemo<CameraEventType[]>(() => view?.supportedEventTypes ?? [], [view])
  const eventsSupported = view?.eventsSupported === true
  const selectedProfile = camera.profiles.find((profile) => profile.id === profileId) ?? camera.profiles[0]

  const dailyBytes = selectedProfile
    ? (Math.max(0, selectedProfile.bitrateKbps + (selectedProfile.audioBitrateKbps ?? 0)) * 1000 / 8) * 86400
    : 0

  const save = useCallback(async () => {
    if (!view) {
      return
    }

    const updated = await saveCameraRecording(camera.id, {
      enabled,
      profileId,
      continuousEnabled,
      continuousSchedule,
      eventEnabled,
      eventSchedule,
      eventTypes,
      preRollSeconds: preRoll,
      postRollSeconds: postRoll
    })

    setView(updated)
  }, [
    camera.id,
    view,
    enabled,
    profileId,
    continuousEnabled,
    continuousSchedule,
    eventEnabled,
    eventSchedule,
    eventTypes,
    preRoll,
    postRoll
  ])

  useImperativeHandle(ref, () => ({ save }), [save])

  function toggleEventType(key: string) {
    setEventTypes((current) =>
      current.includes(key) ? current.filter((item) => item !== key) : [...current, key]
    )
  }

  return (
    <section className="wizard-streams">
      <div className="streams-panel recording-panel">
        <div className="section-heading">
          <h2>{t('recording.title')}</h2>
        </div>

        {loading ? <p className="subtitle">{t('recording.loading')}</p> : null}

        {!loading ? (
          <>
            <div className="switch-row">
              <label className="switch">
                <input
                  type="checkbox"
                  checked={enabled}
                  onChange={(event) => setEnabled(event.target.checked)}
                />
                <span className="switch-track" aria-hidden="true">
                  <span className="switch-thumb" />
                </span>
              </label>
              <span>{t('recording.enabled')}</span>
            </div>

            {enabled ? (
              <div className="recording-body">
                <div className="recording-stream">
                  <label className="field">
                    {t('recording.stream')}
                    <select value={profileId} onChange={(event) => setProfileId(event.target.value)}>
                      {camera.profiles.map((profile) => (
                        <option key={profile.id} value={profile.id}>
                          {profileLabel(profile)} · {profile.videoCodec} · {profile.width}×{profile.height}
                        </option>
                      ))}
                    </select>
                  </label>
                  {selectedProfile ? (
                    <span className="subtitle recording-space">
                      {t('recording.space', { size: formatBytes(dailyBytes) })}
                    </span>
                  ) : null}
                </div>

                <div className="recording-block">
                  <div className="switch-row">
                    <label className="switch">
                      <input
                        type="checkbox"
                        checked={continuousEnabled}
                        onChange={(event) => setContinuousEnabled(event.target.checked)}
                      />
                      <span className="switch-track" aria-hidden="true">
                        <span className="switch-thumb" />
                      </span>
                    </label>
                    <div>
                      <strong>{t('recording.continuous')}</strong>
                      <p className="subtitle">{t('recording.continuous.hint')}</p>
                    </div>
                  </div>

                  <ScheduleGrid
                    value={continuousSchedule}
                    disabled={!continuousEnabled}
                    onChange={setContinuousSchedule}
                  />
                </div>

                <div className="recording-block">
                  <div className="switch-row">
                    <label className="switch">
                      <input
                        type="checkbox"
                        checked={eventEnabled}
                        disabled={!eventsSupported}
                        onChange={(event) => setEventEnabled(event.target.checked)}
                      />
                      <span className="switch-track" aria-hidden="true">
                        <span className="switch-thumb" />
                      </span>
                    </label>
                    <div>
                      <strong>{t('recording.event')}</strong>
                      <p className="subtitle">{t('recording.event.hint')}</p>
                    </div>
                  </div>

                  {!eventsSupported ? (
                    <p className="wizard-note warn">{t('recording.event.unsupported')}</p>
                  ) : (
                    <>
                      <div className="recording-types">
                        <span className="field-label">{t('recording.event.types')}</span>
                        <div className="recording-type-list">
                          {supported.map((type) => (
                            <label key={type.key} className="recording-type">
                              <input
                                type="checkbox"
                                checked={eventTypes.includes(type.key)}
                                onChange={() => toggleEventType(type.key)}
                              />
                              <EventIcon type={type.key} />
                              <span>{type.label}</span>
                            </label>
                          ))}
                        </div>
                      </div>

                      <ScheduleGrid
                        value={eventSchedule}
                        disabled={!eventEnabled}
                        onChange={setEventSchedule}
                      />

                      <div className="recording-roll">
                        <label className="field">
                          {t('recording.preRoll')}
                          <select
                            value={preRoll}
                            disabled={!eventEnabled}
                            onChange={(event) => setPreRoll(Number(event.target.value))}
                          >
                            {ROLL_VALUES.map((value) => (
                              <option key={value} value={value}>
                                {t('recording.seconds', { seconds: String(value) })}
                              </option>
                            ))}
                          </select>
                        </label>

                        <label className="field">
                          {t('recording.postRoll')}
                          <select
                            value={postRoll}
                            disabled={!eventEnabled}
                            onChange={(event) => setPostRoll(Number(event.target.value))}
                          >
                            {ROLL_VALUES.map((value) => (
                              <option key={value} value={value}>
                                {t('recording.seconds', { seconds: String(value) })}
                              </option>
                            ))}
                          </select>
                        </label>
                      </div>
                    </>
                  )}
                </div>
              </div>
            ) : null}

            {view?.lastError ? (
              <p className="wizard-note error">{t('recording.status.error', { error: view.lastError })}</p>
            ) : null}

            {error ? <p className="error">{error}</p> : null}
          </>
        ) : null}
      </div>
    </section>
  )
})

export default RecordingSection
