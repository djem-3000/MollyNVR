import type { SystemInfo } from '../api'
import { setLocale, t, type Locale } from '../i18n'
import { useLocale } from '../useLocale'
import StorageSection from './StorageSection'

type Props = {
  info: SystemInfo | null
}

const LANGUAGES: { value: Locale; label: string }[] = [
  { value: 'en', label: 'English' },
  { value: 'ru', label: 'Русский' }
]

export default function SettingsPanel({ info }: Props) {
  const locale = useLocale()
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

      <section className="page-section">
        <h2>{t('settings.language')}</h2>
        <label className="field settings-language">
          <select
            value={locale}
            onChange={(event) => setLocale(event.target.value as Locale)}
          >
            {LANGUAGES.map((language) => (
              <option key={language.value} value={language.value}>
                {language.label}
              </option>
            ))}
          </select>
        </label>
      </section>

      <StorageSection />

      <section className="page-section">
        <h2>{t('settings.about')}</h2>
        <p className="subtitle">{t('settings.about.text')}</p>
      </section>
    </>
  )
}
