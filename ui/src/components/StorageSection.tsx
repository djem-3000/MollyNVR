import { useEffect, useState } from 'react'
import { getStorageSettings, saveStorageSettings, type StorageView } from '../api'
import { formatBytes } from '../format'
import { t } from '../i18n'

export default function StorageSection() {
  const [view, setView] = useState<StorageView | null>(null)
  const [path, setPath] = useState('')
  const [quota, setQuota] = useState(90)
  const [mode, setMode] = useState<'fifo' | 'watermark'>('fifo')
  const [high, setHigh] = useState(90)
  const [low, setLow] = useState(80)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    let cancelled = false

    getStorageSettings()
      .then((value) => {
        if (cancelled) {
          return
        }

        setView(value)
        setPath(value.settings.path)
        setQuota(value.settings.quotaPercent)
        setMode(value.settings.mode.toLowerCase() === 'watermark' ? 'watermark' : 'fifo')
        setHigh(value.settings.highWatermarkPercent)
        setLow(value.settings.lowWatermarkPercent)
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : t('storage.loadError'))
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
  }, [])

  async function handleSave() {
    setSaving(true)
    setError(null)
    setSaved(false)

    try {
      const updated = await saveStorageSettings({
        path,
        quotaPercent: quota,
        mode,
        highWatermarkPercent: high,
        lowWatermarkPercent: low
      })

      setView(updated)
      setPath(updated.settings.path)
      setSaved(true)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : t('storage.saveError'))
    } finally {
      setSaving(false)
    }
  }

  const status = view?.status
  const quotaBytes =
    status && Number.isFinite(quota) ? (status.totalBytes * Math.min(99, Math.max(0, quota))) / 100 : 0

  return (
    <section className="page-section">
      <h2>{t('storage.title')}</h2>

      {loading ? <p className="subtitle">{t('storage.loading')}</p> : null}

      {!loading ? (
        <>
          <label className="field">
            {t('storage.path')}
            <input
              value={path}
              onChange={(event) => setPath(event.target.value)}
              placeholder="/recordings"
              autoComplete="off"
            />
          </label>
          <p className="subtitle">{t('storage.path.hint')}</p>

          {status ? (
            <p className="subtitle storage-disk-free">
              {t('storage.diskFree', {
                free: formatBytes(status.freeBytes),
                total: formatBytes(status.totalBytes)
              })}
            </p>
          ) : null}

          <label className="field">
            {t('storage.quota')}
            <span className="storage-quota">
              <input
                type="number"
                min={10}
                max={99}
                value={quota}
                onChange={(event) => setQuota(Number(event.target.value))}
              />
              <span className="storage-quota-size">
                {status ? formatBytes(quotaBytes) : '—'}
              </span>
            </span>
          </label>

          <label className="field storage-mode-field">
            {t('storage.mode')}
            <select value={mode} onChange={(event) => setMode(event.target.value as 'fifo' | 'watermark')}>
              <option value="fifo">{t('storage.mode.fifo')}</option>
              <option value="watermark">{t('storage.mode.watermark')}</option>
            </select>
          </label>

          {mode === 'watermark' ? (
            <div className="storage-row">
              <label className="field">
                {t('storage.high')}
                <input
                  type="number"
                  min={10}
                  max={99}
                  value={high}
                  onChange={(event) => setHigh(Number(event.target.value))}
                />
              </label>

              <label className="field">
                {t('storage.low')}
                <input
                  type="number"
                  min={5}
                  max={98}
                  value={low}
                  onChange={(event) => setLow(Number(event.target.value))}
                />
              </label>
            </div>
          ) : null}

          {status && status.quotaBytes > 0 ? (
            <div className="storage-usage">
              <div className="storage-bar" role="progressbar" aria-valuenow={status.usedPercent} aria-valuemin={0} aria-valuemax={100}>
                <span style={{ width: `${Math.min(100, status.usedPercent)}%` }} />
              </div>
              <div className="subtitle">
                {t('storage.used', {
                  used: formatBytes(status.usedBytes),
                  quota: formatBytes(status.quotaBytes)
                })}
              </div>
            </div>
          ) : null}

          {error ? <p className="error">{error}</p> : null}
          {saved ? <p className="status-ok">{t('storage.saved')}</p> : null}

          <div className="wizard-actions">
            <button type="button" disabled={saving || path.trim() === ''} onClick={() => void handleSave()}>
              {saving ? t('storage.save.busy') : t('storage.save')}
            </button>
          </div>
        </>
      ) : null}
    </section>
  )
}
