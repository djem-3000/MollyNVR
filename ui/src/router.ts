import { useEffect, useState } from 'react'

export type Route =
  | { name: 'live' }
  | { name: 'archive'; cameraId: string | null }
  | { name: 'cameras' }
  | { name: 'camera-add' }
  | { name: 'settings' }
  | { name: 'camera-settings'; cameraId: string }

export const camerasPath = '/cameras'
export const cameraAddPath = '/cameras/add'
export const archivePath = '/archive'

export function archiveCameraPath(cameraId: string): string {
  return `${archivePath}?camera=${encodeURIComponent(cameraId)}`
}

export function cameraSettingsPath(cameraId: string): string {
  return `/cameras/${encodeURIComponent(cameraId)}/settings`
}

export function parseRoute(pathname: string, search = ''): Route {
  const path = pathname.replace(/\/+$/, '') || '/'

  if (path === '/' || path === '/live') {
    return { name: 'live' }
  }

  if (path === '/archive') {
    return { name: 'archive', cameraId: readCamera(search) }
  }

  if (path === '/cameras') {
    return { name: 'cameras' }
  }

  if (path === '/cameras/add') {
    return { name: 'camera-add' }
  }

  if (path === '/settings') {
    return { name: 'settings' }
  }

  const cameraSettings = path.match(/^\/cameras\/([^/]+)\/settings$/)
  if (cameraSettings) {
    try {
      return { name: 'camera-settings', cameraId: decodeURIComponent(cameraSettings[1]) }
    } catch {
      return { name: 'live' }
    }
  }

  return { name: 'live' }
}

function readCamera(search: string): string | null {
  const value = new URLSearchParams(search).get('camera')
  return value && value.trim().length > 0 ? value : null
}

export function navigate(path: string): void {
  const current = `${window.location.pathname}${window.location.search}${window.location.hash}`
  if (current === path) {
    return
  }

  window.history.pushState({ from: window.location.pathname }, '', path)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

export function navigateBack(fallback: string): void {
  const state = window.history.state as { from?: unknown } | null
  if (typeof state?.from === 'string' && state.from.startsWith('/')) {
    window.history.back()
    return
  }

  navigate(fallback)
}

export function useRoute(): Route {
  const [location, setLocation] = useState(
    () => `${window.location.pathname}${window.location.search}`
  )

  useEffect(() => {
    const handleLocationChange = () =>
      setLocation(`${window.location.pathname}${window.location.search}`)

    window.addEventListener('popstate', handleLocationChange)
    return () => window.removeEventListener('popstate', handleLocationChange)
  }, [])

  const [pathname, search] = splitLocation(location)
  return parseRoute(pathname, search)
}

function splitLocation(location: string): [string, string] {
  const index = location.indexOf('?')
  return index < 0 ? [location, ''] : [location.slice(0, index), location.slice(index)]
}
