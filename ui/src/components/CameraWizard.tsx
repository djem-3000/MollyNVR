import { useEffect, useMemo, useRef, useState } from 'react'
import {
  addCamera,
  cameraSnapshot,
  getCameraModules,
  getDiscoverySettings,
  probeCamera,
  scanAddress,
  scanMulticast,
  scanNetwork,
  type CameraProfile
} from '../api'
import { t } from '../i18n'
import {
  pickDefaultSubProfile,
  pickMainProfile,
  profileRoleLabel,
  type ConnectionState,
  type SearchMode,
  type SearchState,
  type SnapshotEntry,
  type WizardStep
} from '../wizardTypes'
import PageLayout from './PageLayout'
import WizardConnectStep from './WizardConnectStep'
import WizardProfilesStep from './WizardProfilesStep'
import WizardSaveStep from './WizardSaveStep'
import WizardSearchStep from './WizardSearchStep'
import WizardSteps from './WizardSteps'

type Props = {
  savedHosts: string[]
  onSaved: () => Promise<void> | void
  onBackToCameras: () => void
}

const FALLBACK_MODULES = ['onvif', 'hikvision']

function createSearchState(): SearchState {
  return {
    mode: 'unicast',
    range: '',
    ipHost: '',
    ipVendor: 'onvif',
    devices: null,
    searchedMode: null,
    busy: false,
    error: null
  }
}

function createConnectionState(): ConnectionState {
  return {
    host: '',
    userName: '',
    password: '',
    vendor: 'onvif',
    busy: false,
    camera: null,
    error: null,
    changed: false
  }
}

function defaultCameraName(camera: { deviceName: string | null; model: string; vendor: string }, host: string): string {
  return camera.deviceName?.trim() || camera.model?.trim() || `${camera.vendor || 'Camera'} ${host}`.trim()
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

export default function CameraWizard({ savedHosts, onSaved, onBackToCameras }: Props) {
  const [step, setStep] = useState<WizardStep>(1)
  const [modules, setModules] = useState<string[]>(FALLBACK_MODULES)
  const [search, setSearch] = useState<SearchState>(createSearchState)
  const [connection, setConnection] = useState<ConnectionState>(createConnectionState)
  const [selectedSubProfileId, setSelectedSubProfileId] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [nameEdited, setNameEdited] = useState(false)
  const [snapshots, setSnapshots] = useState<Record<string, SnapshotEntry>>({})
  const [snapshotBusyId, setSnapshotBusyId] = useState<string | null>(null)
  const [snapshotError, setSnapshotError] = useState<string | null>(null)
  const [previewId, setPreviewId] = useState<string | null>(null)
  const [saveBusy, setSaveBusy] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const snapshotUrls = useRef<Record<string, string>>({})

  const vendorOptions = useMemo(
    () => Array.from(new Set(['onvif', ...modules])),
    [modules]
  )

  useEffect(() => {
    let cancelled = false

    getDiscoverySettings()
      .then((settings) => {
        if (!cancelled) {
          setSearch((current) => ({ ...current, range: settings.defaultRange }))
        }
      })
      .catch(() => {
        // The range can also be entered manually.
      })

    getCameraModules()
      .then((keys) => {
        if (!cancelled && keys.length > 0) {
          setModules(keys)
          setSearch((current) => ({
            ...current,
            ipVendor: keys.includes('onvif') ? 'onvif' : keys[0]
          }))
        }
      })
      .catch(() => {
        // Keep the built-in module list.
      })

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    return () => {
      Object.values(snapshotUrls.current).forEach((url) => URL.revokeObjectURL(url))
      snapshotUrls.current = {}
    }
  }, [])

  const profiles = connection.camera?.profiles ?? []
  const mainProfile = pickMainProfile(profiles)
  const mainProfileId = mainProfile?.id ?? ''
  const subProfiles = profiles.filter((profile) => profile.id !== mainProfileId)
  const defaultSubProfile = pickDefaultSubProfile(profiles, mainProfileId)
  const activeSubProfileId =
    selectedSubProfileId && subProfiles.some((profile) => profile.id === selectedSubProfileId)
      ? selectedSubProfileId
      : defaultSubProfile?.id ?? null
  const selectedSubProfile =
    subProfiles.find((profile) => profile.id === activeSubProfileId) ?? null
  const selectedProfiles = [mainProfile, selectedSubProfile].filter(
    (profile): profile is CameraProfile => profile !== null
  )
  const preview = previewId ? snapshots[previewId] ?? null : null
  const mainSnapshot = mainProfile ? snapshots[mainProfile.id] ?? null : null
  const nameValid = name.trim() !== ''

  function revokeSnapshots() {
    Object.values(snapshotUrls.current).forEach((url) => URL.revokeObjectURL(url))
    snapshotUrls.current = {}
    setSnapshots({})
    setSnapshotBusyId(null)
    setSnapshotError(null)
    setPreviewId(null)
  }

  function resetCameraSession() {
    revokeSnapshots()
    setSelectedSubProfileId(null)
    setName('')
    setNameEdited(false)
    setSaveError(null)
  }

  function handleModeChange(mode: SearchMode) {
    setSearch((current) => ({ ...current, mode, error: null }))
  }

  async function handleSearch() {
    const mode = search.mode

    if (mode === 'unicast' && search.range.trim() === '') {
      setSearch((current) => ({ ...current, error: t('wizard.search.rangeInvalid') }))
      return
    }

    if (mode === 'ip' && search.ipHost.trim() === '') {
      setSearch((current) => ({ ...current, error: t('wizard.search.ipInvalid') }))
      return
    }

    setSearch((current) => ({
      ...current,
      busy: true,
      error: null,
      devices: null,
      searchedMode: null
    }))

    try {
      const result =
        mode === 'multicast'
          ? await scanMulticast()
          : mode === 'unicast'
            ? await scanNetwork(search.range.trim())
            : await scanAddress(search.ipHost.trim(), search.ipVendor || undefined)

      setSearch((current) => ({
        ...current,
        busy: false,
        devices: result.devices,
        searchedMode: mode
      }))
    } catch (searchError) {
      setSearch((current) => ({
        ...current,
        busy: false,
        devices: null,
        searchedMode: mode,
        error: errorMessage(searchError)
      }))
    }
  }

  function handleConnect(host: string, vendorKey: string | null) {
    const sameCamera = connection.camera !== null && connection.host.trim() === host

    if (sameCamera) {
      setConnection((current) => ({
        ...current,
        vendor: vendorKey ?? current.vendor,
        error: null
      }))
      setStep(2)
      return
    }

    resetCameraSession()
    setConnection({
      ...createConnectionState(),
      host,
      vendor: vendorKey ?? 'onvif'
    })
    setStep(2)
  }

  function handleConnectionField(
    field: 'host' | 'userName' | 'password' | 'vendor',
    value: string
  ) {
    if (connection.camera) {
      revokeSnapshots()
    }

    setConnection((current) => ({
      ...current,
      [field]: value,
      camera: null,
      changed: current.changed || current.camera !== null,
      error: null
    }))
  }

  async function handleProbe() {
    setConnection((current) => ({ ...current, busy: true, error: null }))

    try {
      const camera = await probeCamera({
        host: connection.host.trim(),
        userName: connection.userName.trim(),
        password: connection.password,
        vendor: connection.vendor || undefined
      })

      setConnection((current) => ({ ...current, busy: false, camera, changed: false }))
      revokeSnapshots()
      setSelectedSubProfileId(null)
      setName((current) =>
        nameEdited && current.trim() !== ''
          ? current
          : defaultCameraName(camera, connection.host.trim())
      )
    } catch (probeError) {
      setConnection((current) => ({
        ...current,
        busy: false,
        camera: null,
        error: errorMessage(probeError)
      }))
    }
  }

  async function captureSnapshot(profile: CameraProfile) {
    setSnapshotBusyId(profile.id)
    setSnapshotError(null)

    try {
      const blob = await cameraSnapshot({
        host: connection.host.trim(),
        userName: connection.userName.trim(),
        password: connection.password,
        vendor: connection.vendor || undefined,
        profile: profile.kind,
        profileId: profile.id
      })
      const url = URL.createObjectURL(blob)
      const previous = snapshotUrls.current[profile.id]

      if (previous) {
        URL.revokeObjectURL(previous)
      }

      snapshotUrls.current[profile.id] = url
      setSnapshots((current) => ({
        ...current,
        [profile.id]: {
          url,
          profileId: profile.id,
          label: profileRoleLabel(profile, profiles, mainProfileId)
        }
      }))
      setPreviewId(profile.id)
    } catch (snapshotFailure) {
      setSnapshotError(errorMessage(snapshotFailure) || t('wizard.profiles.snapshotError'))
    } finally {
      setSnapshotBusyId(null)
    }
  }

  function handleNextToProfiles() {
    if (!connection.camera) {
      setConnection((current) => ({ ...current, error: t('wizard.probeRequired') }))
      return
    }

    setStep(3)
  }

  function handleNextToSave() {
    if (!mainProfile) {
      return
    }

    setStep(4)

    if (!snapshots[mainProfile.id] && snapshotBusyId === null) {
      void captureSnapshot(mainProfile)
    }
  }

  function handleNameChange(value: string) {
    setName(value)
    setNameEdited(true)
  }

  async function handleSave() {
    if (!connection.camera || !mainProfile || !nameValid) {
      return
    }

    setSaveBusy(true)
    setSaveError(null)

    try {
      await addCamera({
        name: name.trim(),
        host: connection.host.trim(),
        userName: connection.userName.trim(),
        password: connection.password,
        vendor: connection.vendor || connection.camera.vendorKey,
        mainProfileId: mainProfile.id,
        subProfileId: activeSubProfileId ?? undefined
      })
      await onSaved()
      onBackToCameras()
    } catch (saveFailure) {
      setSaveError(errorMessage(saveFailure))
    } finally {
      setSaveBusy(false)
    }
  }

  function handleNavigate(target: WizardStep) {
    if (target < step) {
      setStep(target)
    }
  }

  return (
    <PageLayout title={t('wizard.title')}>
      <WizardSteps current={step} onNavigate={handleNavigate} />

      {step === 1 ? (
        <WizardSearchStep
          savedHosts={savedHosts}
          search={search}
          vendorOptions={vendorOptions}
          onModeChange={handleModeChange}
          onRangeChange={(range) => setSearch((current) => ({ ...current, range }))}
          onIpHostChange={(ipHost) => setSearch((current) => ({ ...current, ipHost }))}
          onIpVendorChange={(ipVendor) => setSearch((current) => ({ ...current, ipVendor }))}
          onSearch={() => void handleSearch()}
          onConnect={handleConnect}
          onSwitchToUnicast={() => handleModeChange('unicast')}
        />
      ) : null}

      {step === 2 ? (
        <WizardConnectStep
          connection={connection}
          vendorOptions={vendorOptions}
          onFieldChange={handleConnectionField}
          onProbe={() => void handleProbe()}
          onNext={handleNextToProfiles}
        />
      ) : null}

      {step === 3 && connection.camera ? (
        <WizardProfilesStep
          camera={connection.camera}
          mainProfileId={mainProfileId}
          subProfileId={activeSubProfileId}
          snapshotBusyId={snapshotBusyId}
          snapshotError={snapshotError}
          preview={preview}
          onSubProfileChange={setSelectedSubProfileId}
          onSnapshot={(profile) => void captureSnapshot(profile)}
          onBack={() => setStep(2)}
          onNext={handleNextToSave}
        />
      ) : null}

      {step === 4 && connection.camera && mainProfile ? (
        <WizardSaveStep
          camera={connection.camera}
          host={connection.host.trim()}
          name={name}
          nameValid={nameValid}
          mainProfileId={mainProfileId}
          profiles={selectedProfiles}
          snapshot={mainSnapshot}
          snapshotBusy={snapshotBusyId === mainProfile.id}
          snapshotError={snapshotError}
          saveBusy={saveBusy}
          error={saveError}
          onNameChange={handleNameChange}
          onRetrySnapshot={() => void captureSnapshot(mainProfile)}
          onBack={() => setStep(3)}
          onSave={() => void handleSave()}
        />
      ) : null}

      {step === 3 && !connection.camera ? (
        <p className="wizard-note error" role="alert">
          {t('wizard.probeRequired')}
        </p>
      ) : null}

      {step === 4 && !mainProfile ? (
        <p className="wizard-note error" role="alert">
          {t('wizard.profiles.empty')}
        </p>
      ) : null}
    </PageLayout>
  )
}
