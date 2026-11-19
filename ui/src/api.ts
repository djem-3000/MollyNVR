export type SystemInfo = {
  app: string
  version: string
  dotnet: string
  go2rtc: string
}

async function readError(response: Response): Promise<string> {
  try {
    const data = (await response.json()) as { error?: string; detail?: string; title?: string }
    return data.error ?? data.detail ?? data.title ?? `HTTP ${response.status}`
  } catch {
    return `HTTP ${response.status}`
  }
}

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  const response = await fetch(input, init)
  if (!response.ok) {
    throw new Error(await readError(response))
  }

  return (await response.json()) as T
}

export function getSystemInfo(): Promise<SystemInfo> {
  return request<SystemInfo>('/api/system/info')
}

export type CameraProfile = {
  kind: 'main' | 'sub'
  id: string
  name: string
  videoCodec: string
  width: number
  height: number
  frameRate: number
  bitrateKbps: number
  audioEnabled: boolean
  audioCodec: string | null
  audioSampleRate: number | null
  audioBitrateKbps: number | null
  rtspUrl: string
  streamId?: string | null
  videoPlayback: 'passthrough' | 'transcode-available'
  videoTargetCodec: string | null
  audioPlayback: 'disabled' | 'passthrough' | 'transcoded' | 'unsupported'
  audioTargetCodec: string | null
}

export type CameraProtocol = {
  name: string | null
  port: number
  role: 'video' | 'control' | null
}

export type CameraInfo = {
  vendorKey: string
  vendor: string
  model: string
  firmware: string
  serialNumber: string | null
  deviceName: string | null
  profiles: CameraProfile[]
  protocols: CameraProtocol[]
}

export type CameraRequest = {
  host: string
  userName: string
  password: string
  vendor?: string
  profile?: 'main' | 'sub'
  profileId?: string
}

export type SavedCameraProfile = CameraProfile & {
  streamId: string | null
}

export type SavedCamera = {
  id: string
  name: string
  host: string
  rtspPort: number
  vendorKey: string
  vendor: string
  model: string
  firmware: string
  serialNumber: string | null
  isActive: boolean
  profiles: SavedCameraProfile[]
  subProfileId: string
  createdAt: string
  updatedAt: string
}

export type CameraRegistrationRequest = {
  name?: string
  host: string
  rtspPort?: number
  userName?: string
  password?: string
  vendor?: string
  subProfileId?: string
  mainProfileId?: string
}

export type CameraSettingsRequest = {
  name?: string
  subProfileId?: string | null
}

export type CameraNetworkInfo = {
  dhcpEnabled: boolean | null
}

export type DiscoveredCamera = {
  host: string
  mac: string | null
  vendorKey: string | null
  vendor: string | null
  model: string | null
  openPorts: number[]
}

export type DiscoveryResult = {
  range?: string
  devices: DiscoveredCamera[]
}

export type DiscoverySettings = {
  defaultRange: string
}

function jsonRequest(body: unknown, method = 'POST'): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  }
}

export async function probeCamera(request: CameraRequest): Promise<CameraInfo> {
  const response = await fetch('/api/cameras/probe', jsonRequest(request))
  if (!response.ok) {
    throw new Error(await readError(response))
  }

  return (await response.json()) as CameraInfo
}

export async function cameraSnapshot(request: CameraRequest): Promise<Blob> {
  const response = await fetch('/api/cameras/snapshot', jsonRequest(request))
  if (!response.ok) {
    throw new Error(await readError(response))
  }

  return response.blob()
}

export function listCameras(): Promise<SavedCamera[]> {
  return request<SavedCamera[]>('/api/cameras')
}

export function addCamera(camera: CameraRegistrationRequest): Promise<SavedCamera> {
  return request<SavedCamera>('/api/cameras', jsonRequest(camera))
}

export function updateCamera(id: string, camera: CameraRegistrationRequest): Promise<SavedCamera> {
  return request<SavedCamera>(`/api/cameras/${encodeURIComponent(id)}`, jsonRequest(camera, 'PUT'))
}

export function updateCameraSettings(
  id: string,
  settings: CameraSettingsRequest
): Promise<SavedCamera> {
  return request<SavedCamera>(
    `/api/cameras/${encodeURIComponent(id)}/settings`,
    jsonRequest(settings, 'PUT')
  )
}

export function getCameraNetworkInfo(id: string): Promise<CameraNetworkInfo> {
  return request<CameraNetworkInfo>(`/api/cameras/${encodeURIComponent(id)}/network`)
}

export function setCameraActive(id: string, active: boolean): Promise<SavedCamera> {
  return request<SavedCamera>(`/api/cameras/${encodeURIComponent(id)}/active`, jsonRequest({ active }, 'PUT'))
}

export async function deleteCamera(id: string): Promise<void> {
  const response = await fetch(`/api/cameras/${encodeURIComponent(id)}`, { method: 'DELETE' })
  if (!response.ok) {
    throw new Error(await readError(response))
  }
}

export function getDiscoverySettings(): Promise<DiscoverySettings> {
  return request<DiscoverySettings>('/api/discovery/settings')
}

export function scanNetwork(range: string): Promise<DiscoveryResult> {
  return request<DiscoveryResult>('/api/discovery/scan', jsonRequest({ range }))
}

export function scanMulticast(): Promise<DiscoveryResult> {
  return request<DiscoveryResult>('/api/discovery/multicast', jsonRequest({}))
}

export function scanAddress(host: string, vendor?: string): Promise<DiscoveryResult> {
  return request<DiscoveryResult>('/api/discovery/ip', jsonRequest({ host, vendor }))
}

export async function getCameraModules(): Promise<string[]> {
  const result = await request<{ modules: string[] }>('/api/cameras/modules')
  return result.modules
}

export async function cameraWhep(
  cameraId: string,
  profile: 'main' | 'sub',
  sdpOffer: string,
  profileId?: string
): Promise<string> {
  const parameters = new URLSearchParams({ profile })
  if (profileId) {
    parameters.set('profileId', profileId)
  }

  const response = await fetch(
    `/api/cameras/${encodeURIComponent(cameraId)}/whep?${parameters.toString()}`,
    {
    method: 'POST',
    headers: { 'Content-Type': 'application/sdp' },
    body: sdpOffer
  })
  if (!response.ok) {
    throw new Error(await readError(response))
  }

  return response.text()
}

export type CameraEventType = {
  key: string
  label: string
}

export type CameraRecording = {
  cameraId: string
  enabled: boolean
  profileId: string
  continuousEnabled: boolean
  continuousSchedule: string
  eventEnabled: boolean
  eventSchedule: string
  eventTypes: string[]
  preRollSeconds: number
  postRollSeconds: number
  supportedEventTypes: CameraEventType[]
  eventsSupported: boolean
  isRecording: boolean
  lastError: string | null
}

export type CameraRecordingRequest = {
  enabled: boolean
  profileId: string
  continuousEnabled: boolean
  continuousSchedule: string
  eventEnabled: boolean
  eventSchedule: string
  eventTypes: string[]
  preRollSeconds: number
  postRollSeconds: number
}

export type StorageSettings = {
  path: string
  quotaPercent: number
  mode: 'Fifo' | 'Watermark'
  highWatermarkPercent: number
  lowWatermarkPercent: number
}

export type StorageStatus = {
  path: string
  totalBytes: number
  freeBytes: number
  quotaBytes: number
  usedBytes: number
  availableBytes: number
  usedPercent: number
}

export type StorageView = {
  settings: StorageSettings
  status: StorageStatus
}

export function getCameraRecording(cameraId: string): Promise<CameraRecording> {
  return request<CameraRecording>(`/api/cameras/${encodeURIComponent(cameraId)}/recording`)
}

export type RecordingLiveStatus = {
  cameraId: string
  startedAt: string | null
  schedule: boolean
  events: string[]
}

export function getRecordingStatus(): Promise<RecordingLiveStatus[]> {
  return request<RecordingLiveStatus[]>('/api/recording/status')
}

export function saveCameraRecording(
  cameraId: string,
  body: CameraRecordingRequest
): Promise<CameraRecording> {
  return request<CameraRecording>(`/api/cameras/${encodeURIComponent(cameraId)}/recording`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
}

export function getStorageSettings(): Promise<StorageView> {
  return request<StorageView>('/api/settings/storage')
}

export function saveStorageSettings(body: {
  path: string
  quotaPercent: number
  mode: 'fifo' | 'watermark'
  highWatermarkPercent: number
  lowWatermarkPercent: number
}): Promise<StorageView> {
  return request<StorageView>('/api/settings/storage', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
}
export type ArchiveCamera = {
  id: string
  name: string
  isActive: boolean
  hasRecordingsToday: boolean
}

export type ArchiveDay = {
  date: string
  segmentCount: number
  sizeBytes: number
}

export type ArchiveCalendar = {
  cameraId: string
  year: number
  month: number
  days: ArchiveDay[]
}

export type ArchiveEventType = {
  key: string
  label: string
}

export type ArchiveInterval = {
  start: string
  end: string
  sizeBytes: number
}

export type ArchiveEventSlice = {
  type: string
  start: string
  end: string
}

export type ArchiveTimeline = {
  cameraId: string
  date: string
  intervals: ArchiveInterval[]
  events: ArchiveEventSlice[]
  eventTypes: ArchiveEventType[]
}

export type ArchiveSegmentSlice = {
  start: string
  end: string
  mediaStart: number
  mediaEnd: number
}

export type ArchivePlayback = {
  sessionId: string
  playlistUrl: string
  windowStart: string
  windowEnd: string
  startOffsetSeconds: number
  durationSeconds: number
  transcoded: boolean
  segments: ArchiveSegmentSlice[]
}

export function listArchiveCameras(): Promise<ArchiveCamera[]> {
  return request<ArchiveCamera[]>('/api/archive/cameras')
}

export function getArchiveCalendar(
  cameraId: string,
  year: number,
  month: number
): Promise<ArchiveCalendar> {
  const query = new URLSearchParams({ year: String(year), month: String(month) })
  return request<ArchiveCalendar>(
    `/api/archive/cameras/${encodeURIComponent(cameraId)}/calendar?${query.toString()}`
  )
}

export function getArchiveTimeline(cameraId: string, date: string): Promise<ArchiveTimeline> {
  const query = new URLSearchParams({ date })
  return request<ArchiveTimeline>(
    `/api/archive/cameras/${encodeURIComponent(cameraId)}/timeline?${query.toString()}`
  )
}

export function startArchivePlayback(
  cameraId: string,
  from: string,
  supportsHevc: boolean
): Promise<ArchivePlayback> {
  return request<ArchivePlayback>('/api/archive/playback', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ cameraId, from, supportsHevc })
  })
}

export async function stopArchivePlayback(sessionId: string): Promise<void> {
  try {
    await fetch(`/api/archive/playback/${encodeURIComponent(sessionId)}`, { method: 'DELETE' })
  } catch {
    // the session may already be stopped
  }
}