import { t } from '../i18n'
import { moduleLabel, type SearchMode, type SearchState } from '../wizardTypes'

type Props = {
  savedHosts: string[]
  search: SearchState
  vendorOptions: string[]
  onModeChange: (mode: SearchMode) => void
  onRangeChange: (value: string) => void
  onIpHostChange: (value: string) => void
  onIpVendorChange: (value: string) => void
  onSearch: () => void
  onConnect: (host: string, vendorKey: string | null) => void
  onSwitchToUnicast: () => void
}

const MODES: { mode: SearchMode; label: string }[] = [
  { mode: 'multicast', label: 'wizard.mode.multicast' },
  { mode: 'unicast', label: 'wizard.mode.unicast' },
  { mode: 'ip', label: 'wizard.mode.ip' }
]

function portBadges(ports: number[]) {
  if (ports.length === 0) {
    return <span className="subtitle">{t('camera.unknown')}</span>
  }

  return (
    <div className="port-badges">
      {ports.map((port) => (
        <span key={port} className="port-badge">
          {port}
        </span>
      ))}
    </div>
  )
}

export default function WizardSearchStep({
  savedHosts,
  search,
  vendorOptions,
  onModeChange,
  onRangeChange,
  onIpHostChange,
  onIpVendorChange,
  onSearch,
  onConnect,
  onSwitchToUnicast
}: Props) {
  const showResults = search.searchedMode === search.mode && search.devices !== null
  const visibleDevices =
    search.devices?.filter((device) => !savedHosts.includes(device.host)) ?? null
  const onlySavedResults =
    search.devices !== null && search.devices.length > 0 && visibleDevices?.length === 0

  return (
    <section className="page-section">
      <h2 tabIndex={-1}>{t('wizard.step.search')}</h2>

      <div className="wizard-mode" role="tablist" aria-label={t('wizard.searchMode')}>
        {MODES.map(({ mode, label }) => (
          <button
            key={mode}
            type="button"
            role="tab"
            aria-selected={search.mode === mode}
            className={search.mode === mode ? 'active' : ''}
            disabled={search.busy}
            onClick={() => onModeChange(mode)}
          >
            {t(label)}
          </button>
        ))}
      </div>

      {search.mode === 'multicast' ? (
        <div className="wizard-search-panel">
          <p className="subtitle">{t('wizard.multicast.hint')}</p>
          <p className="wizard-note warn">{t('wizard.multicast.dockerHint')}</p>
          <button type="button" disabled={search.busy} onClick={onSearch}>
            {search.busy ? t('wizard.search.busy') : t('discovery.scan')}
          </button>
        </div>
      ) : null}

      {search.mode === 'unicast' ? (
        <form
          className="camera-form"
          onSubmit={(event) => {
            event.preventDefault()
            onSearch()
          }}
        >
          <label>
            {t('discovery.range')}
            <input
              value={search.range}
              onChange={(event) => onRangeChange(event.target.value)}
              placeholder="192.168.1.0/24"
              autoComplete="off"
              required
            />
          </label>
          <button type="submit" disabled={search.busy || search.range.trim() === ''}>
            {search.busy ? t('wizard.search.busy') : t('discovery.scan')}
          </button>
        </form>
      ) : null}

      {search.mode === 'unicast' ? (
        <p className="subtitle">{t('discovery.range.hint')}</p>
      ) : null}

      {search.mode === 'ip' ? (
        <>
          <form
            className="camera-form"
            onSubmit={(event) => {
              event.preventDefault()
              onSearch()
            }}
          >
            <label>
              {t('wizard.ip.address')}
              <input
                value={search.ipHost}
                onChange={(event) => onIpHostChange(event.target.value)}
                placeholder="192.168.1.10"
                autoComplete="off"
                required
              />
            </label>
            <label>
              {t('camera.vendor')}
              <select
                value={search.ipVendor}
                onChange={(event) => onIpVendorChange(event.target.value)}
              >
                {vendorOptions.map((vendor) => (
                  <option key={vendor} value={vendor}>
                    {moduleLabel(vendor)}
                  </option>
                ))}
              </select>
            </label>
            <button type="submit" disabled={search.busy || search.ipHost.trim() === ''}>
              {search.busy ? t('wizard.search.busy') : t('discovery.scan')}
            </button>
          </form>
          <p className="subtitle">{t('wizard.ip.hint')}</p>
        </>
      ) : null}

      {search.error ? (
        <p className="error" role="alert">
          {search.error}
        </p>
      ) : null}

      {showResults && visibleDevices && visibleDevices.length === 0 ? (
        <div className="wizard-empty camera-preview-placeholder">
          <span>{t(onlySavedResults ? 'discovery.empty.saved' : 'discovery.empty')}</span>
          {!onlySavedResults && search.mode === 'unicast' ? (
            <span className="subtitle">{t('wizard.unicast.emptyHint')}</span>
          ) : null}
          {!onlySavedResults && search.mode === 'multicast' ? (
            <button type="button" className="secondary" onClick={onSwitchToUnicast}>
              {t('wizard.multicast.emptyAction')}
            </button>
          ) : null}
        </div>
      ) : null}

      {showResults && visibleDevices && visibleDevices.length > 0 ? (
        <div className="wizard-results">
          <h3>{t('wizard.search.results')}</h3>
          <div className="streams-panel">
            <table className="camera-profiles discovery-table wizard-table">
              <thead>
                <tr>
                  <th scope="col">{t('discovery.device')}</th>
                  <th scope="col">{t('discovery.vendor')}</th>
                  <th scope="col">{t('discovery.ports')}</th>
                  <th scope="col" aria-label={t('wizard.search.actions')} />
                </tr>
              </thead>
              <tbody>
                {visibleDevices.map((device) => (
                  <tr key={device.host}>
                    <td>
                      <span className="discovery-host">{device.host}</span>
                      {device.mac ? <span className="subtitle">{device.mac}</span> : null}
                    </td>
                    <td>
                      <span>{device.vendor ?? t('discovery.unknown')}</span>
                      {device.model ? <span className="subtitle">{device.model}</span> : null}
                    </td>
                    <td>
                      {portBadges(device.openPorts)}
                      {search.mode === 'ip' && device.openPorts.length === 0 ? (
                        <span className="subtitle">{t('wizard.ip.ports.empty')}</span>
                      ) : null}
                    </td>
                    <td className="camera-actions">
                      <button
                        type="button"
                        aria-label={t('wizard.search.connect', { host: device.host })}
                        onClick={() => onConnect(device.host, device.vendorKey)}
                      >
                        {t('discovery.connect')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </section>
  )
}
