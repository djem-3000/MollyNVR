import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  getArchiveCalendar,
  getArchiveTimeline,
  listArchiveCameras,
  type ArchiveCalendar,
  type ArchiveCamera,
  type ArchiveEventType,
  type ArchiveTimeline
} from '../../api'
import { t } from '../../i18n'
import { archiveCameraPath, archivePath, navigate } from '../../router'
import EventIcon from '../EventIcon'
import ArchiveControls from './ArchiveControls'
import ArchiveGrid from './ArchiveGrid'
import ArchivePlayer from './ArchivePlayer'
import ArchiveTimelineView, { type TimelineLane } from './ArchiveTimeline'
import {
  buildRanges,
  findNextStart,
  findPreviousStart,
  findStartRight,
  localDateKey
} from './archiveTime'
import { useArchivePlayback } from './useArchivePlayback'

type Props = {
  cameraId: string | null
}

const EMPTY_SLOTS: (string | null)[] = [null, null, null, null]
const EMPTY_TIMELINES: (ArchiveTimeline | null)[] = [null, null, null, null]

function currentMonth(): { year: number; month: number } {
  const now = new Date()
  return { year: now.getFullYear(), month: now.getMonth() + 1 }
}

function ScheduleIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="8" />
      <path d="M12 7.5V12l3 2" />
    </svg>
  )
}

export default function ArchivePage({ cameraId }: Props) {
  const [cameras, setCameras] = useState<ArchiveCamera[]>([])
  const [camerasLoading, setCamerasLoading] = useState(true)
  const [camerasError, setCamerasError] = useState<string | null>(null)

  const [selectedDate, setSelectedDate] = useState(() => localDateKey(new Date()))
  const [month, setMonth] = useState(currentMonth)
  const [calendar, setCalendar] = useState<ArchiveCalendar | null>(null)
  const [calendarError, setCalendarError] = useState<string | null>(null)

  const [timeline, setTimeline] = useState<ArchiveTimeline | null>(null)
  const [timelineLoading, setTimelineLoading] = useState(false)
  const [timelineError, setTimelineError] = useState<string | null>(null)

  const [scheduleEnabled, setScheduleEnabled] = useState(true)
  const [activeTypes, setActiveTypes] = useState<Set<string>>(new Set())
  const [cursor, setCursor] = useState<Date | null>(null)

  const [multi, setMulti] = useState(false)
  const [slots, setSlots] = useState<(string | null)[]>(EMPTY_SLOTS)
  const [muted, setMuted] = useState(false)
  const [focusedSlot, setFocusedSlot] = useState<number | null>(null)
  const [gridTimelines, setGridTimelines] = useState<(ArchiveTimeline | null)[]>(EMPTY_TIMELINES)
  const [fullscreen, setFullscreen] = useState(false)

  const endedRef = useRef<() => void>(() => {})
  const autoStartRef = useRef<{ cameraId: string | null; at: number }[]>(
    EMPTY_SLOTS.map(() => ({ cameraId: null, at: 0 }))
  )
  const singleController = useArchivePlayback({
    cameraId,
    onEnded: () => endedRef.current()
  })

  const gridOne = useArchivePlayback({ cameraId: multi ? slots[0] : null })
  const gridTwo = useArchivePlayback({ cameraId: multi ? slots[1] : null })
  const gridThree = useArchivePlayback({ cameraId: multi ? slots[2] : null })
  const gridFour = useArchivePlayback({ cameraId: multi ? slots[3] : null })

  const gridControllers = [gridOne, gridTwo, gridThree, gridFour]

  const activeCameraId = multi ? slots.find((slot): slot is string => slot !== null) ?? null : cameraId
  const controllers = multi ? gridControllers : [singleController]
  const wallTime = multi
    ? gridControllers.find((controller) => controller.playing && controller.wallTime !== null)
        ?.wallTime ??
      gridControllers.find((controller) => controller.wallTime !== null)?.wallTime ??
      null
    : singleController.wallTime

  const playing = multi
    ? gridControllers.some((controller) => controller.playing)
    : singleController.playing
  const loading = multi
    ? gridControllers.some((controller) => controller.loading)
    : singleController.loading
  const rate = multi ? gridControllers[0].rate : singleController.rate

  useEffect(() => {
    let cancelled = false
    setCamerasLoading(true)
    setCamerasError(null)

    listArchiveCameras()
      .then((list) => {
        if (!cancelled) {
          setCameras(list)
        }
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setCamerasError(reason instanceof Error ? reason.message : t('archive.cameras.loadError'))
        }
      })
      .finally(() => {
        if (!cancelled) {
          setCamerasLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    if (!cameraId) {
      return
    }

    if (multi) {
      // Clicking a camera in grid mode adds it to the first free tile.
      setSlots((current) => {
        if (current.includes(cameraId)) {
          return current
        }

        const free = current.indexOf(null)

        if (free < 0) {
          return current
        }

        const next = [...current]
        next[free] = cameraId
        return next
      })

      navigate(archivePath)
      return
    }

    setSlots(EMPTY_SLOTS)
  }, [cameraId, multi])

  useEffect(() => {
    if (!activeCameraId) {
      setCalendar(null)
      return
    }

    let cancelled = false
    setCalendarError(null)

    getArchiveCalendar(activeCameraId, month.year, month.month)
      .then((value) => {
        if (!cancelled) {
          setCalendar(value)
        }
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setCalendar(null)
          setCalendarError(reason instanceof Error ? reason.message : t('archive.calendar.loadError'))
        }
      })

    return () => {
      cancelled = true
    }
  }, [activeCameraId, month.year, month.month])

  useEffect(() => {
    singleController.stop()
    gridControllers.forEach((controller) => controller.stop())
    setTimeline(null)
    setTimelineError(null)

    if (multi || !activeCameraId) {
      return
    }

    let cancelled = false
    setTimelineLoading(true)

    getArchiveTimeline(activeCameraId, selectedDate)
      .then((value) => {
        if (cancelled) {
          return
        }

        setTimeline(value)
        setScheduleEnabled(true)
        setActiveTypes(new Set(value.eventTypes.map((type) => type.key)))
        setCursor(
          value.intervals.length > 0
            ? new Date(value.intervals[value.intervals.length - 1].start)
            : null
        )
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setTimelineError(reason instanceof Error ? reason.message : t('archive.timeline.loadError'))
        }
      })
      .finally(() => {
        if (!cancelled) {
          setTimelineLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
    // Controllers are stable by method; a restart is needed only when the camera or date changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeCameraId, selectedDate, multi])

  useEffect(() => {
    if (!multi) {
      setGridTimelines(EMPTY_TIMELINES)
      setFocusedSlot(null)
      return
    }

    let cancelled = false

    void Promise.all(
      slots.map((slotCameraId) =>
        slotCameraId
          ? getArchiveTimeline(slotCameraId, selectedDate).catch(() => null)
          : Promise.resolve(null)
      )
    ).then((list) => {
      if (cancelled) {
        return
      }

      setGridTimelines(list)
      setFocusedSlot((current) => {
        if (current !== null && slots[current]) {
          return current
        }

        const first = slots.findIndex((slot) => slot !== null)
        return first >= 0 ? first : null
      })
    })

    return () => {
      cancelled = true
    }
  }, [multi, slots, selectedDate])

  const eventTypes = useMemo<ArchiveEventType[]>(() => {
    if (!multi) {
      return timeline?.eventTypes ?? []
    }

    const unique = new Map<string, ArchiveEventType>()

    for (const item of gridTimelines) {
      for (const type of item?.eventTypes ?? []) {
        if (!unique.has(type.key)) {
          unique.set(type.key, type)
        }
      }
    }

    return [...unique.values()]
  }, [multi, timeline, gridTimelines])

  const typeSignature = eventTypes.map((type) => type.key).join(',')

  useEffect(() => {
    if (!typeSignature) {
      return
    }

    setActiveTypes((current) => {
      let changed = false
      const next = new Set(current)

      for (const key of typeSignature.split(',')) {
        if (!next.has(key)) {
          next.add(key)
          changed = true
        }
      }

      return changed ? next : current
    })
  }, [typeSignature])

  const focusedTimeline = multi
    ? (focusedSlot !== null ? gridTimelines[focusedSlot] : null) ??
      gridTimelines.find((item): item is ArchiveTimeline => item !== null) ??
      null
    : timeline

  const ranges = useMemo(
    () =>
      focusedTimeline
        ? buildRanges(
            focusedTimeline.intervals,
            focusedTimeline.events,
            activeTypes,
            scheduleEnabled
          )
        : [],
    [focusedTimeline, activeTypes, scheduleEnabled]
  )

  const playheadTime = playing ? wallTime ?? cursor : cursor ?? wallTime
  const currentMs = playheadTime?.getTime() ?? null

  const previous = currentMs === null ? null : findPreviousStart(ranges, currentMs)
  const next =
    currentMs === null ? null : findNextStart(ranges, currentMs)

  const seekAll = useCallback(
    (wall: Date) => {
      setCursor(wall)

      if (!multi) {
        singleController.seek(wall)
        return
      }

      const wallMs = wall.getTime()

      gridControllers.forEach((controller, index) => {
        if (!slots[index]) {
          return
        }

        const cover = gridTimelines[index]?.intervals.some((interval) => {
          const start = Date.parse(interval.start)
          const end = Date.parse(interval.end)
          return wallMs >= start - 1000 && wallMs < end
        })

        if (cover) {
          controller.seek(wall)
        } else {
          controller.stop()
        }
      })
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [multi, slots, gridTimelines, singleController]
  )

  const handleSeek = useCallback(
    (wall: Date) => {
      // Clicking the timeline only moves the playhead and stops playback.
      setCursor(wall)

      if (multi) {
        gridControllers.forEach((controller) => controller.video?.pause())
      } else {
        singleController.video?.pause()
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [multi, slots, singleController]
  )

  useEffect(() => {
    if (!multi || !playing || !wallTime) {
      return
    }

    const wallMs = wallTime.getTime()
    const now = Date.now()

    gridControllers.forEach((controller, index) => {
      const slotCameraId = slots[index]

      if (!slotCameraId || controller.loading) {
        return
      }

      if (controller.playback && !controller.video?.ended) {
        return
      }

      const data = gridTimelines[index]
      const cover = data?.intervals.some((interval) => {
        const start = Date.parse(interval.start)
        const end = Date.parse(interval.end)
        return wallMs >= start - 1000 && wallMs < end
      })

      if (!cover) {
        return
      }

      const previous = autoStartRef.current[index]

      if (previous.cameraId === slotCameraId && now - previous.at < 10000) {
        return
      }

      autoStartRef.current[index] = { cameraId: slotCameraId, at: now }
      controller.playFrom(new Date(wallMs))
    })
    // Controllers and timelines change together with the slots: playback must start when a recording boundary is crossed.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [multi, playing, wallTime, slots, gridTimelines])

  function handlePlayPause() {
    if (playing) {
      if (multi) {
        gridControllers.forEach((controller) => controller.video?.pause())
      } else {
        singleController.video?.pause()
      }

      return
    }

    const cursorMs = cursor?.getTime() ?? null
    const target =
      cursorMs !== null ? findStartRight(ranges, cursorMs) : ranges[0]?.startMs ?? null

    if (target === null) {
      return
    }

    setCursor(new Date(target))
    seekAll(new Date(target))
  }

  endedRef.current = () => {
    const playback = singleController.playback

    if (!playback) {
      return
    }

    const start = findStartRight(ranges, Date.parse(playback.windowEnd) + 500)

    if (start !== null) {
      setCursor(new Date(start))
      singleController.playFrom(new Date(start))
    }
  }

  function handleSelectDate(date: string) {
    setSelectedDate(date)
    const parsed = new Date(`${date}T00:00:00`)
    setMonth({ year: parsed.getFullYear(), month: parsed.getMonth() + 1 })
  }

  function handleMonthChange(year: number, monthValue: number) {
    setMonth({ year, month: monthValue })
  }

  function toggleType(key: string) {
    setActiveTypes((current) => {
      const updated = new Set(current)

      if (updated.has(key)) {
        updated.delete(key)
      } else {
        updated.add(key)
      }

      return updated
    })
  }

  function setRate(value: number) {
    for (const controller of controllers) {
      controller.setRate(value)
    }
  }

  useEffect(() => {
    const handleChange = () => {
      const element = document.fullscreenElement
      setFullscreen(
        element !== null && (element.matches('.archive-player') || element.matches('.archive-grid'))
      )
    }

    document.addEventListener('fullscreenchange', handleChange)
    return () => document.removeEventListener('fullscreenchange', handleChange)
  }, [])

  function toggleFullscreen() {
    if (document.fullscreenElement) {
      void document.exitFullscreen()
      return
    }

    const target = document.querySelector('.archive-grid') ?? document.querySelector('.archive-player')
    void (target as HTMLElement | null)?.requestFullscreen().catch(() => {
      console.warn('Could not enter fullscreen')
    })
  }

  function toggleMulti() {
    if (multi) {
      const first = slots.find((slot): slot is string => slot !== null) ?? null

      if (first) {
        navigate(archiveCameraPath(first))
      }

      setMulti(false)
      setSlots(EMPTY_SLOTS)
      return
    }

    if (cameraId) {
      navigate(archivePath)
    }

    setMulti(true)
    setSlots(EMPTY_SLOTS)
  }

  const selectedCamera = cameras.find((camera) => camera.id === cameraId)

  const lanes: TimelineLane[] = multi
    ? slots.flatMap((slotCameraId, index) => {
        if (!slotCameraId) {
          return []
        }

        const data = gridTimelines[index]

        return [
          {
            cameraId: slotCameraId,
            name: cameras.find((camera) => camera.id === slotCameraId)?.name ?? '',
            intervals: data?.intervals ?? [],
            events: data?.events ?? []
          }
        ]
      })
    : []

  const filters = (
    <div className="archive-filter-icons" role="group" aria-label={t('archive.filters.title')}>
      <button
        type="button"
        className={scheduleEnabled ? 'archive-filter-icon active' : 'archive-filter-icon'}
        title={t('archive.filter.schedule')}
        aria-label={t('archive.filter.schedule')}
        aria-pressed={scheduleEnabled}
        onClick={() => setScheduleEnabled((value) => !value)}
      >
        <ScheduleIcon />
      </button>

      {eventTypes.map((type) => (
        <button
          key={type.key}
          type="button"
          className={
            activeTypes.has(type.key) ? 'archive-filter-icon active' : 'archive-filter-icon'
          }
          title={type.label}
          aria-label={type.label}
          aria-pressed={activeTypes.has(type.key)}
          onClick={() => toggleType(type.key)}
        >
          <EventIcon type={type.key} />
        </button>
      ))}
    </div>
  )

  return (
    <div className="archive-page">
      {camerasError ? <p className="error">{camerasError}</p> : null}

      <div className="archive-main">
        {multi ? (
          <ArchiveGrid
            slots={slots}
            cameras={cameras}
            controllers={gridControllers}
            focusedIndex={focusedSlot ?? -1}
            muted={muted}
            onFocus={setFocusedSlot}
            onSlotsChange={setSlots}
          />
        ) : cameraId ? (
          <ArchivePlayer
            cameraName={selectedCamera?.name ?? t('archive.cameras.none')}
            controller={singleController}
            muted={muted}
          />
        ) : (
          <div className="archive-player archive-player-empty">
            <div className="archive-player-hint">{t('archive.selectCamera')}</div>
          </div>
        )}

        <ArchiveControls
          playing={playing}
          loading={loading}
          rate={rate}
          onRate={setRate}
          canPrev={previous !== null}
          canNext={next !== null}
          onPrev={() => previous !== null && handleSeek(new Date(previous))}
          onNext={() => next !== null && handleSeek(new Date(next))}
          onToggle={handlePlayPause}
          filters={filters}
          multi={multi}
          onToggleMulti={toggleMulti}
          fullscreen={fullscreen}
          onToggleFullscreen={toggleFullscreen}
          muted={muted}
          onToggleMuted={() => setMuted((value) => !value)}
          disabled={activeCameraId === null}
          selectedDate={selectedDate}
          calendarYear={month.year}
          calendarMonth={month.month}
          calendarDays={calendar?.days ?? []}
          onSelectDate={handleSelectDate}
          onMonthChange={handleMonthChange}
        />

        <ArchiveTimelineView
          date={selectedDate}
          intervals={focusedTimeline?.intervals ?? []}
          events={focusedTimeline?.events ?? []}
          activeTypes={activeTypes}
          scheduleEnabled={scheduleEnabled}
          playheadTime={playheadTime}
          follow={playing}
          onSeek={handleSeek}
          lanes={multi ? lanes : undefined}
          activeLaneId={multi && focusedSlot !== null ? slots[focusedSlot] : null}
          onSelectLane={(laneCameraId) => {
            const index = slots.indexOf(laneCameraId)

            if (index >= 0) {
              setFocusedSlot(index)
            }
          }}
        />

        <div className="archive-messages">
          {calendarError ? <p className="error">{calendarError}</p> : null}
          {timelineError ? <p className="error">{timelineError}</p> : null}
          {timelineLoading ? <p className="subtitle">{t('archive.timeline.loading')}</p> : null}
          {camerasLoading ? <p className="subtitle">{t('archive.cameras.loading')}</p> : null}
        </div>
      </div>
    </div>
  )
}
