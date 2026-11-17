export type CameraLayout =
  | '1'
  | '2'
  | '4'
  | '4big'
  | '6x2'
  | '6big'
  | '6'
  | '9'
  | '8'

export type LivePreferences = {
  layout: CameraLayout
  layouts: Record<CameraLayout, (string | null)[]>
  focusedTile: number | null
  stretch: boolean
  muted: boolean
  /** Порядок камер в мобильном списке (только в браузере). */
  mobileOrder: string[]
}

export const LIVE_LAYOUTS: CameraLayout[] = [
  '1',
  '2',
  '4',
  '4big',
  '6x2',
  '6big',
  '6',
  '8',
  '9'
]

export const LAYOUT_GRID: Record<CameraLayout, { columns: number; rows: number }> = {
  '1': { columns: 1, rows: 1 },
  '2': { columns: 2, rows: 1 },
  '4': { columns: 2, rows: 2 },
  '4big': { columns: 4, rows: 3 },
  '6x2': { columns: 3, rows: 2 },
  '6big': { columns: 4, rows: 3 },
  '6': { columns: 4, rows: 3 },
  '9': { columns: 4, rows: 3 },
  '8': { columns: 5, rows: 4 }
}

export const LAYOUT_TILE_COUNTS: Record<CameraLayout, number> = {
  '1': 1,
  '2': 2,
  '4': 4,
  '4big': 4,
  '6x2': 6,
  '6big': 6,
  '6': 9,
  '9': 12,
  '8': 12
}

export const LAYOUT_TILE_AREAS: Partial<Record<CameraLayout, string[]>> = {
  '4big': [
    '1 / 1 / 4 / 4',
    '1 / 4 / 2 / 5',
    '2 / 4 / 3 / 5',
    '3 / 4 / 4 / 5'
  ],
  '6big': [
    '1 / 1 / 3 / 3',
    '1 / 3 / 3 / 5',
    '3 / 1 / 4 / 2',
    '3 / 2 / 4 / 3',
    '3 / 3 / 4 / 4',
    '3 / 4 / 4 / 5'
  ],
  '6': [
    '1 / 1 / 3 / 3',
    '1 / 3 / 2 / 4',
    '2 / 3 / 3 / 4',
    '1 / 4 / 2 / 5',
    '2 / 4 / 3 / 5',
    '3 / 1 / 4 / 2',
    '3 / 2 / 4 / 3',
    '3 / 3 / 4 / 4',
    '3 / 4 / 4 / 5'
  ],
  '8': [
    '1 / 1 / 4 / 4',
    '1 / 4 / 2 / 5',
    '2 / 4 / 3 / 5',
    '3 / 4 / 4 / 5',
    '1 / 5 / 2 / 6',
    '2 / 5 / 3 / 6',
    '3 / 5 / 4 / 6',
    '4 / 5 / 5 / 6',
    '4 / 1 / 5 / 2',
    '4 / 2 / 5 / 3',
    '4 / 3 / 5 / 4',
    '4 / 4 / 5 / 5'
  ]
}

const STORAGE_KEY = 'mollynvr.live-grid.v1'
const DEFAULT_LAYOUT: CameraLayout = '4'

function emptySlots(count: number): (string | null)[] {
  return Array.from({ length: count }, () => null)
}

function createEmptyLayouts(): Record<CameraLayout, (string | null)[]> {
  return {
    '1': emptySlots(LAYOUT_TILE_COUNTS['1']),
    '2': emptySlots(LAYOUT_TILE_COUNTS['2']),
    '4': emptySlots(LAYOUT_TILE_COUNTS['4']),
    '4big': emptySlots(LAYOUT_TILE_COUNTS['4big']),
    '6x2': emptySlots(LAYOUT_TILE_COUNTS['6x2']),
    '6big': emptySlots(LAYOUT_TILE_COUNTS['6big']),
    '6': emptySlots(LAYOUT_TILE_COUNTS['6']),
    '9': emptySlots(LAYOUT_TILE_COUNTS['9']),
    '8': emptySlots(LAYOUT_TILE_COUNTS['8'])
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isCameraLayout(value: unknown): value is CameraLayout {
  return typeof value === 'string' && LIVE_LAYOUTS.includes(value as CameraLayout)
}

function normalizeSlots(value: unknown, count: number): (string | null)[] {
  if (!Array.isArray(value)) {
    return emptySlots(count)
  }

  return Array.from({ length: count }, (_, index) => {
    const cameraId = value[index]
    return typeof cameraId === 'string' && cameraId.length > 0 ? cameraId : null
  })
}

function normalizeFocusedTile(value: unknown, layout: CameraLayout): number | null {
  return typeof value === 'number' &&
    Number.isInteger(value) &&
    value >= 0 &&
    value < LAYOUT_TILE_COUNTS[layout]
    ? value
    : null
}

export function createDefaultPreferences(): LivePreferences {
  return {
    layout: DEFAULT_LAYOUT,
    layouts: createEmptyLayouts(),
    focusedTile: null,
    stretch: false,
    muted: false,
    mobileOrder: []
  }
}

/** Порядок камер: сначала сохранённый, затем новые в исходном порядке. */
export function orderCameras<T extends { id: string }>(cameras: T[], order: string[]): T[] {
  if (order.length === 0) {
    return cameras
  }

  const byId = new Map(cameras.map((camera) => [camera.id, camera]))
  const ordered: T[] = []

  for (const cameraId of order) {
    const camera = byId.get(cameraId)

    if (camera) {
      ordered.push(camera)
      byId.delete(cameraId)
    }
  }

  for (const camera of cameras) {
    if (byId.has(camera.id)) {
      ordered.push(camera)
    }
  }

  return ordered
}

export function tileCount(layout: CameraLayout): number {
  return LAYOUT_TILE_COUNTS[layout]
}

export function loadLivePreferences(): LivePreferences {
  let raw: string | null

  try {
    raw = window.localStorage.getItem(STORAGE_KEY)
  } catch {
    return createDefaultPreferences()
  }

  if (!raw) {
    return createDefaultPreferences()
  }

  let parsed: unknown
  try {
    parsed = JSON.parse(raw)
  } catch {
    return createDefaultPreferences()
  }

  if (!isRecord(parsed)) {
    return createDefaultPreferences()
  }

  const layout = isCameraLayout(parsed.layout) ? parsed.layout : DEFAULT_LAYOUT
  const layouts = createEmptyLayouts()
  const storedLayouts = isRecord(parsed.layouts) ? parsed.layouts : null

  if (storedLayouts) {
    for (const key of LIVE_LAYOUTS) {
      layouts[key] = normalizeSlots(storedLayouts[key], LAYOUT_TILE_COUNTS[key])
    }
  }

  return {
    layout,
    layouts,
    focusedTile: normalizeFocusedTile(parsed.focusedTile, layout),
    stretch: parsed.stretch === true,
    muted: parsed.muted === true,
    mobileOrder: Array.isArray(parsed.mobileOrder)
      ? parsed.mobileOrder.filter(
          (value): value is string => typeof value === 'string' && value.length > 0
        )
      : []
  }
}

export function saveLivePreferences(preferences: LivePreferences): void {
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences))
}
