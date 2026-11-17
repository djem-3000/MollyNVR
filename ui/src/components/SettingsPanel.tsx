import type { SystemInfo } from '../api'
import { t } from '../i18n'
import StorageSection from './StorageSection'

type Props = {
  info: SystemInfo | null
}

export default function SettingsPanel({ info }: Props) {
  const go2rtcOk = info?.go2rtc === 'ok'

  return (
    <>
      <section className="page-section">
        <h2>{t('settings.system')}</h2>
        <dl className="settings-list">
          <div>
            <dt>{t('settings.version')}</dt>
            <dd>{info ? `${info.app} ${info.version}` : t('camera.unknown')}</dd>
          </div>
          <div>
            <dt>{t('settings.go2rtc')}</dt>
            <dd className={go2rtcOk ? 'status-ok' : 'error'}>
              {info ? (go2rtcOk ? t('system.go2rtc.ok') : t('system.go2rtc.unavailable')) : t('camera.unknown')}
            </dd>
          </div>
        </dl>
      </section>

      <StorageSection />

      <section className="page-section">
        <h2>{t('settings.about')}</h2>
        <p className="subtitle">{t('settings.about.text')}</p>
      </section>
    </>
  )
}
