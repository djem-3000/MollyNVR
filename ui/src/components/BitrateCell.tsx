import type { CameraProfile } from '../api'
import { bitrateAssessment } from '../cameraProfileFormat'
import { t } from '../i18n'

export function BitrateCell({ profile }: { profile: CameraProfile }) {
  const assessment = bitrateAssessment(profile)

  return (
    <td
      className={assessment ? `bitrate-${assessment.tone}` : undefined}
      title={
        assessment
          ? `${assessment.bitsPerPixel.toLocaleString('ru-RU', {
              minimumFractionDigits: 3,
              maximumFractionDigits: 3
            })} ${t('camera.bitrate.bpp')}`
          : undefined
      }
    >
      {`${profile.bitrateKbps} ${t('camera.bitrate.unit')}`}
    </td>
  )
}

export function BitrateLegend() {
  return (
    <div className="bitrate-legend" aria-label={t('camera.bitrate.legend')}>
      <span>{t('camera.bitrate.legend')}</span>
      <span className="bitrate-good">{t('camera.bitrate.good')}</span>
      <span className="bitrate-warn">{t('camera.bitrate.warn')}</span>
      <span className="bitrate-bad">{t('camera.bitrate.bad')}</span>
    </div>
  )
}
