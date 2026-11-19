import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import CameraList from './components/CameraList'
import CameraSettingsPanel from './components/CameraSettingsPanel'
import CameraWizard from './components/CameraWizard'
import LiveGrid from './components/LiveGrid'
import PageLayout from './components/PageLayout'
import SettingsPanel from './components/SettingsPanel'
const ArchivePage = lazy(() => import('./components/archive/ArchivePage'))
import { getSystemInfo, listCameras, type SavedCamera, type SystemInfo } from './api'
import { t } from './i18n'
import {
  archiveCameraPath,
  archivePath,
  cameraAddPath,
  camerasPath,
  cameraSettingsPath,
  navigate,
  navigateBack,
  useRoute
} from './router'
import { useIsMobile } from './useMediaQuery'
import { useLocale } from './useLocale'

function LiveIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="4" width="18" height="13" rx="2" />
      <path d="M8 21h8" />
    </svg>
  )
}

function CamerasIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="7" width="12" height="10" rx="2" />
      <path d="M15 10.5l6-3v9l-6-3z" />
    </svg>
  )
}

function ArchiveIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M19.5617 7C19.7904 5.69523 18.7863 4.5 17.4617 4.5H6.53788C5.21323 4.5 4.20922 5.69523 4.43784 7" />
      <path d="M17.4999 4.5C17.5283 4.24092 17.5425 4.11135 17.5427 4.00435C17.545 2.98072 16.7739 2.12064 15.7561 2.01142C15.6497 2 15.5194 2 15.2588 2H8.74099C8.48035 2 8.35002 2 8.24362 2.01142C7.22584 2.12064 6.45481 2.98072 6.45704 4.00434C6.45727 4.11135 6.47146 4.2409 6.49983 4.5" />
      <path d="M21.1935 16.793C20.8437 19.2739 20.6689 20.5143 19.7717 21.2572C18.8745 22 17.5512 22 14.9046 22H9.09536C6.44881 22 5.12553 22 4.22834 21.2572C3.33115 20.5143 3.15626 19.2739 2.80648 16.793L2.38351 13.793C1.93748 10.6294 1.71447 9.04765 2.66232 8.02383C3.61017 7 5.29758 7 8.67239 7H15.3276C18.7024 7 20.3898 7 21.3377 8.02383C22.2855 9.0477 22.0625 10.6294 21.6165 13.793L21.1935 16.793Z" />
      <path d="M14.5812 13.6159C15.1396 13.9621 15.1396 14.8582 14.5812 15.2044L11.2096 17.2945C10.6669 17.6309 10 17.1931 10 16.5003L10 12.32C10 11.6273 10.6669 11.1894 11.2096 11.5258L14.5812 13.6159Z" />
    </svg>
  )
}

function SettingsIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 6h16M4 12h16M4 18h16" />
      <circle cx="9" cy="6" r="2" />
      <circle cx="15" cy="12" r="2" />
      <circle cx="7" cy="18" r="2" />
    </svg>
  )
}

function PlusIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 5v14M5 12h14" />
    </svg>
  )
}

function MenuIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  )
}

export default function App() {
  useLocale()
  const route = useRoute()
  const [info, setInfo] = useState<SystemInfo | null>(null)
  const [cameras, setCameras] = useState<SavedCamera[]>([])
  const [camerasLoaded, setCamerasLoaded] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const archiveDragAt = useRef(0)
  const isMobile = useIsMobile()
  const [menuOpen, setMenuOpen] = useState(false)
  const [archiveExpanded, setArchiveExpanded] = useState(false)

  const closeMenu = useCallback(() => setMenuOpen(false), [])

  const refresh = useCallback(async () => {
    try {
      const [system, cameraList] = await Promise.all([getSystemInfo(), listCameras()])
      setInfo(system)
      setCameras(cameraList)
      setError(null)
    } catch (refreshError) {
      setError(refreshError instanceof Error ? refreshError.message : String(refreshError))
    } finally {
      setCamerasLoaded(true)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  useEffect(() => {
    const scrollPage =
      route.name === 'settings' ||
      route.name === 'cameras' ||
      route.name === 'camera-settings' ||
      route.name === 'camera-add'

    document.body.classList.toggle('page-scroll', scrollPage)

    return () => document.body.classList.remove('page-scroll')
  }, [route.name])

  const cameraSettings =
    route.name === 'camera-settings'
      ? cameras.find((camera) => camera.id === route.cameraId) ?? null
      : null

  const activePage =
    route.name === 'camera-settings' || route.name === 'camera-add' ? 'cameras' : route.name

  const activeCameras = cameras.filter((camera) => camera.isActive)

  return (
    <div className="app-shell">
      <header className="mobile-bar">
        <span className="mobile-brand">
          <span className="brand-avatar">
            <img src="/logo.png" alt="" />
          </span>
          MollyNVR
        </span>
        <button
          type="button"
          className="menu-toggle"
          aria-label={t('menu.open')}
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((open) => !open)}
        >
          <MenuIcon />
        </button>
      </header>

      {isMobile && menuOpen ? (
        <button type="button" className="sidebar-backdrop" aria-label={t('menu.close')} onClick={closeMenu} />
      ) : null}

      <aside className={`sidebar${isMobile ? ' mobile' : ''}${menuOpen ? ' open' : ''}`}>
        <div className="brand">
          <span className="brand-avatar">
            <img src="/logo.png" alt="" />
          </span>
          MollyNVR
        </div>

        <nav>
          <button
            type="button"
            className={activePage === 'live' ? 'active' : ''}
            aria-current={route.name === 'live' ? 'page' : undefined}
            onClick={() => {
              navigate('/live')
              closeMenu()
            }}
          >
            <LiveIcon />
            {t('nav.live')}
          </button>
          <button
            type="button"
            className={activePage === 'cameras' ? 'active' : ''}
            aria-current={route.name === 'cameras' || route.name === 'camera-add' ? 'page' : undefined}
            onClick={() => {
              navigate(camerasPath)
              closeMenu()
            }}
          >
            <CamerasIcon />
            {t('nav.cameras')}
          </button>
          <button
            type="button"
            className={activePage === 'archive' ? 'active' : ''}
            aria-current={route.name === 'archive' ? 'page' : undefined}
            onClick={() => {
              if (isMobile) {
                if (archiveExpanded) {
                  setArchiveExpanded(false)
                  return
                }

                setArchiveExpanded(true)

                if (route.name !== 'archive') {
                  navigate(archivePath)
                }

                return
              }

              navigate(archivePath)
            }}
          >
            <ArchiveIcon />
            {t('nav.archive')}
            {isMobile ? (
              <span className="nav-chevron" aria-hidden="true">
                {archiveExpanded ? '▾' : '▸'}
              </span>
            ) : null}
          </button>

          {route.name === 'archive' && (!isMobile || archiveExpanded) ? (
            <ul className="sidebar-cameras" aria-label={t('archive.cameras')}>
              {activeCameras.map((camera) => (
                <li key={camera.id}>
                  <button
                    type="button"
                    className={route.cameraId === camera.id ? 'active' : ''}
                    title={camera.name}
                    draggable
                    onDragStart={(event) => {
                      event.dataTransfer.effectAllowed = 'copy'
                      event.dataTransfer.setData('application/x-molly-camera', camera.id)
                      event.dataTransfer.setData('text/plain', camera.id)
                      archiveDragAt.current = Date.now()
                    }}
                    onClick={() => {
                      // after dragging, a click must not open single mode
                      if (Date.now() - archiveDragAt.current < 800) {
                        return
                      }

                      navigate(archiveCameraPath(camera.id))
                      closeMenu()
                    }}
                  >
                    <span className="sidebar-camera-dot" aria-hidden="true" />
                    <span className="sidebar-camera-name">{camera.name}</span>
                  </button>
                </li>
              ))}
              {camerasLoaded && activeCameras.length === 0 ? (
                <li className="sidebar-cameras-empty">{t('archive.cameras.empty')}</li>
              ) : null}
            </ul>
          ) : null}

          <button
            type="button"
            className={activePage === 'settings' ? 'active' : ''}
            aria-current={route.name === 'settings' ? 'page' : undefined}
            onClick={() => {
              navigate('/settings')
              closeMenu()
            }}
          >
            <SettingsIcon />
            {t('nav.settings')}
          </button>
        </nav>

        <div className="sidebar-version" title={t('settings.version')}>
          v{info?.version ?? '0.0.1-dev'}
        </div>
      </aside>

      <main className="content">
        {error ? <p className="error">{`${t('api.unavailable')}: ${error}`}</p> : null}

        {route.name === 'live' ? (
          <LiveGrid
            cameras={cameras}
            onOpenCameraSettings={(camera) => navigate(cameraSettingsPath(camera.id))}
            onOpenArchive={(camera) => navigate(archiveCameraPath(camera.id))}
          />
        ) : null}

        {route.name === 'archive' ? (
          <Suspense fallback={<p className="subtitle">{t('archive.loading')}</p>}>
            <ArchivePage cameraId={route.cameraId} />
          </Suspense>
        ) : null}

        {route.name === 'camera-settings' ? (
          <PageLayout title={t('cameraSettings.title')}>
            {cameraSettings ? (
              <CameraSettingsPanel
                key={cameraSettings.id}
                camera={cameraSettings}
                onSaved={(updated) =>
                  setCameras((current) =>
                    current.map((camera) => (camera.id === updated.id ? updated : camera))
                  )
                }
                onBack={() => navigateBack(camerasPath)}
              />
            ) : (
              <p className="subtitle">
                {camerasLoaded ? t('cameraSettings.notFound') : t('grid.loading')}
              </p>
            )}
          </PageLayout>
        ) : null}

        {route.name === 'cameras' ? (
          <PageLayout
            title={t('nav.cameras')}
            actions={
              <button type="button" className="add-camera-button" onClick={() => navigate(cameraAddPath)}>
                <PlusIcon />
                {t('cameras.add')}
              </button>
            }
          >
            <CameraList
              cameras={cameras}
              onChanged={() => void refresh()}
              onEdit={(camera) => navigate(cameraSettingsPath(camera.id))}
            />
          </PageLayout>
        ) : null}

        {route.name === 'camera-add' ? (
          <CameraWizard
            savedHosts={cameras.map((camera) => camera.host)}
            onSaved={() => refresh()}
            onBackToCameras={() => navigate(camerasPath)}
          />
        ) : null}

        {route.name === 'settings' ? (
          <PageLayout title={t('nav.settings')}>
            <SettingsPanel info={info} />
          </PageLayout>
        ) : null}
      </main>
    </div>
  )
}
