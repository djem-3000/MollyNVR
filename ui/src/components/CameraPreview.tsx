import { useEffect, useRef, useState } from 'react'
import { t } from '../i18n'
import { connectCamera, type StreamSession } from '../webrtc'

type Props = {
  cameraId: string
  cameraName: string
  profile: 'main' | 'sub'
  profileId?: string | null
}

type PreviewState = 'connecting' | 'live' | 'failed'

export default function CameraPreview({ cameraId, cameraName, profile, profileId }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const [state, setState] = useState<PreviewState>('connecting')
  const [muted, setMuted] = useState(true)
  const [audioAvailable, setAudioAvailable] = useState(false)
  const [retryToken, setRetryToken] = useState(0)

  useEffect(() => {
    const video = videoRef.current
    if (!video) {
      return
    }

    let cancelled = false
    let session: StreamSession | null = null
    let stream: MediaStream | null = null
    let retryTimer: number | null = null
    let retryScheduled = false

    setState('connecting')
    setAudioAvailable(false)

    const updateAudio = () => {
      const mediaStream = video.srcObject as MediaStream | null
      setAudioAvailable((mediaStream?.getAudioTracks().length ?? 0) > 0)
    }

    const handlePlaying = () => {
      setState('live')
      updateAudio()
    }

    const scheduleRetry = () => {
      if (cancelled || retryScheduled) {
        return
      }

      retryScheduled = true
      retryTimer = window.setTimeout(() => {
        if (!cancelled) {
          setRetryToken((value) => value + 1)
        }
      }, 5000)
    }

    video.addEventListener('playing', handlePlaying)

    connectCamera(cameraId, profile, video, profileId ?? undefined)
      .then((created) => {
        if (cancelled) {
          created.close()
          return
        }

        session = created
        stream = video.srcObject as MediaStream | null
        stream?.addEventListener('addtrack', updateAudio)
        updateAudio()

        created.connection.addEventListener('connectionstatechange', () => {
          if (cancelled) {
            return
          }

          const connectionState = created.connection.connectionState
          if (connectionState === 'failed' || connectionState === 'closed') {
            setState('failed')
            created.close()
            scheduleRetry()
          }
        })
      })
      .catch(() => {
        if (!cancelled) {
          setState('failed')
          scheduleRetry()
        }
      })

    return () => {
      cancelled = true
      if (retryTimer !== null) {
        window.clearTimeout(retryTimer)
      }
      video.removeEventListener('playing', handlePlaying)
      stream?.removeEventListener('addtrack', updateAudio)
      session?.close()
    }
  }, [cameraId, profile, profileId, retryToken])

  useEffect(() => {
    if (videoRef.current) {
      videoRef.current.muted = muted
    }
  }, [muted])

  function toggleMute() {
    if (!audioAvailable) {
      return
    }

    if (muted) {
      const video = videoRef.current
      setMuted(false)
      void video?.play().catch(() => setMuted(true))
      return
    }

    setMuted(true)
  }

  return (
    <div className="camera-preview">
      <video ref={videoRef} autoPlay playsInline muted={muted} aria-label={cameraName} />

      {state !== 'live' ? (
        <div className="camera-preview-overlay">
          <span className={`badge ${state === 'failed' ? 'bad' : 'warn'}`}>
            {state === 'failed' ? t('camera.preview.failed') : t('camera.preview.connecting')}
          </span>
          {state === 'failed' ? (
            <button type="button" onClick={() => setRetryToken((value) => value + 1)}>
              {t('camera.preview.retry')}
            </button>
          ) : null}
        </div>
      ) : null}

      <div className="camera-preview-controls">
        <button
          type="button"
          className="badge"
          disabled={!audioAvailable}
          title={audioAvailable ? undefined : t('camera.preview.audioUnavailable')}
          onClick={toggleMute}
        >
          {muted ? t('camera.preview.soundOn') : t('camera.preview.soundOff')}
        </button>
      </div>
    </div>
  )
}
