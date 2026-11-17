import { t } from '../i18n'
import { moduleLabel, type ConnectionState } from '../wizardTypes'

type Props = {
  connection: ConnectionState
  vendorOptions: string[]
  onFieldChange: (field: 'host' | 'userName' | 'password' | 'vendor', value: string) => void
  onProbe: () => void
  onNext: () => void
}

export default function WizardConnectStep({
  connection,
  vendorOptions,
  onFieldChange,
  onProbe,
  onNext
}: Props) {
  return (
    <section className="page-section">
      <h2 tabIndex={-1}>{t('wizard.connect.title')}</h2>

      <form
        className="camera-form"
        onSubmit={(event) => {
          event.preventDefault()
          onProbe()
        }}
      >
        <fieldset className="wizard-fieldset" disabled={connection.busy}>
          <label>
            {t('camera.host')}
            <input
              value={connection.host}
              onChange={(event) => onFieldChange('host', event.target.value)}
              placeholder={t('camera.host.placeholder')}
              autoComplete="off"
              required
            />
          </label>

          <label>
            {t('camera.user')}
            <input
              value={connection.userName}
              onChange={(event) => onFieldChange('userName', event.target.value)}
              autoComplete="off"
              required
            />
          </label>

          <label>
            {t('camera.password')}
            <input
              type="password"
              value={connection.password}
              onChange={(event) => onFieldChange('password', event.target.value)}
              autoComplete="new-password"
              required
            />
          </label>

          <label>
            {t('camera.vendor')}
            <select
              value={connection.vendor}
              onChange={(event) => onFieldChange('vendor', event.target.value)}
            >
              {vendorOptions.map((vendor) => (
                <option key={vendor} value={vendor}>
                  {moduleLabel(vendor)}
                </option>
              ))}
            </select>
          </label>
        </fieldset>

        <button
          type="submit"
          className={connection.camera ? 'secondary' : undefined}
          disabled={connection.busy}
        >
          {connection.busy ? t('camera.probe.busy') : t('camera.probe')}
        </button>
      </form>

      {connection.changed && !connection.camera ? (
        <p className="wizard-note">{t('wizard.connect.changed')}</p>
      ) : null}

      {connection.error ? (
        <div className="wizard-note error" role="alert">
          <strong>{t('wizard.connect.errorTitle')}</strong>
          <span className="detail">{connection.error}</span>
          <span className="subtitle">{t('wizard.connect.errorHint')}</span>
        </div>
      ) : null}

      {connection.camera ? (
        <div className="camera-result">
          <h3>{t('wizard.connect.success')}</h3>
          <dl className="camera-device">
            <div>
              <dt>{t('camera.device')}</dt>
              <dd>{connection.camera.deviceName ?? connection.camera.vendor ?? t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('camera.model')}</dt>
              <dd>{connection.camera.model || t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('camera.firmware')}</dt>
              <dd>{connection.camera.firmware || t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('camera.serial')}</dt>
              <dd>{connection.camera.serialNumber ?? t('camera.unknown')}</dd>
            </div>
            <div>
              <dt>{t('wizard.connect.ip')}</dt>
              <dd>{connection.host.trim()}</dd>
            </div>
          </dl>

          <div className="wizard-actions">
            <button type="button" onClick={onNext}>
              {t('wizard.next')}
            </button>
          </div>
        </div>
      ) : null}
    </section>
  )
}
