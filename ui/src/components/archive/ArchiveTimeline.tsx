import { useEffect, useRef, useState } from 'react'
import type { ArchiveEventSlice, ArchiveInterval } from '../../api'
import { DAY_MS, formatClock, formatDuration, startOfDay } from './archiveTime'

export type TimelineLane = {
  cameraId: string
  name: string
  intervals: ArchiveInterval[]
  events: ArchiveEventSlice[]
}

type Props = {
  date: string
  intervals: ArchiveInterval[]
  events: ArchiveEventSlice[]
  activeTypes: Set<string>
  scheduleEnabled: boolean
  playheadTime: Date | null
  follow: boolean
  onSeek: (wall: Date) => void
  lanes?: TimelineLane[]
  activeLaneId?: string | null
  onSelectLane?: (cameraId: string) => void
}

const MIN_ZOOM = 1
const MAX_ZOOM = 12

function pickStep(steps: number[], pxPerHour: number, minPx: number): number {
  return steps.find((step) => (pxPerHour * step) / 60 >= minPx) ?? steps[steps.length - 1]
}

export default function ArchiveTimeline({
  date,
  intervals,
  events,
  activeTypes,
  scheduleEnabled,
  playheadTime,
  follow,
  onSeek,
  lanes,
  activeLaneId,
  onSelectLane
}: Props) {
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const trackRef = useRef<HTMLDivElement | null>(null)
  const dragRef = useRef<{ pointerId: number; startX: number; scroll: number; moved: boolean } | null>(
    null
  )
  const pointersRef = useRef<Map<number, { x: number; y: number }>>(new Map())
  const pinchRef = useRef<{ startDistance: number; startZoom: number } | null>(null)
  const suppressSeekRef = useRef(false)
  const zoomAnchorRef = useRef<{ ratio: number; cursorX: number } | null>(null)
  const [viewportWidth, setViewportWidth] = useState(0)
  const [zoom, setZoom] = useState(1)

  const dayStartMs = startOfDay(date).getTime()

  useEffect(() => {
    const element = scrollRef.current

    if (!element) {
      return
    }

    const update = (width: number) => {
      if (width > 0) {
        setViewportWidth(width)
      }
    }

    update(element.clientWidth)

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0]

      if (entry) {
        update(entry.contentRect.width)
      }
    })

    observer.observe(element)

    const onWindowResize = () => update(element.clientWidth)
    window.addEventListener('resize', onWindowResize)

    return () => {
      observer.disconnect()
      window.removeEventListener('resize', onWindowResize)
    }
  }, [])

  useEffect(() => {
    const element = scrollRef.current

    if (!element) {
      return
    }

    const handleWheel = (event: WheelEvent) => {
      if (Math.abs(event.deltaY) < 1) {
        return
      }

      event.preventDefault()
      const rect = element.getBoundingClientRect()
      const cursorX = event.clientX - rect.left
      const ratio = (element.scrollLeft + cursorX) / Math.max(1, element.scrollWidth)

      zoomAnchorRef.current = { ratio, cursorX }
      setZoom((current) => {
        const factor = event.deltaY < 0 ? 1.25 : 0.8
        return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, current * factor))
      })
    }

    element.addEventListener('wheel', handleWheel, { passive: false })
    return () => element.removeEventListener('wheel', handleWheel)
  }, [])

  useEffect(() => {
    const element = scrollRef.current
    const anchor = zoomAnchorRef.current

    if (!element || !anchor) {
      return
    }

    zoomAnchorRef.current = null
    element.scrollLeft = Math.max(0, anchor.ratio * element.scrollWidth - anchor.cursorX)
  }, [zoom])

  useEffect(() => {
    if (!follow || !playheadTime) {
      return
    }

    const element = scrollRef.current
    const track = trackRef.current

    if (!element || !track) {
      return
    }

    const x = track.offsetLeft + ((playheadTime.getTime() - dayStartMs) / DAY_MS) * track.offsetWidth
    const margin = 60

    if (x < element.scrollLeft + margin || x > element.scrollLeft + element.clientWidth - margin) {
      element.scrollLeft = Math.max(0, x - element.clientWidth / 2)
    }
  }, [playheadTime, follow, dayStartMs])

  function barStyle(startIso: string, endIso: string) {
    const startMs = Math.max(dayStartMs, Date.parse(startIso))
    const endMs = Math.min(dayStartMs + DAY_MS, Date.parse(endIso))
    const left = ((startMs - dayStartMs) / DAY_MS) * 100
    const width = Math.max(0.08, ((endMs - startMs) / DAY_MS) * 100)

    return { left: `${left}%`, width: `${width}%` }
  }

  function barTitle(startIso: string, endIso: string): string {
    const startMs = Date.parse(startIso)
    const endMs = Date.parse(endIso)
    return `${formatClock(new Date(startMs))}–${formatClock(new Date(endMs))} · ${formatDuration(
      (endMs - startMs) / 1000
    )}`
  }

  function renderBars(laneIntervals: ArchiveInterval[], laneEvents: ArchiveEventSlice[]) {
    return (
      <>
        {scheduleEnabled
          ? laneIntervals.map((interval) => (
              <span
                key={`schedule-${interval.start}`}
                className="archive-bar schedule"
                style={barStyle(interval.start, interval.end)}
                title={barTitle(interval.start, interval.end)}
              />
            ))
          : null}

        {laneEvents
          .filter((event) => activeTypes.has(event.type))
          .map((event, index) => (
            <span
              key={`event-${event.type}-${event.start}-${index}`}
              className="archive-bar event"
              style={barStyle(event.start, event.end)}
              title={`${event.type}: ${barTitle(event.start, event.end)}`}
            />
          ))}
      </>
    )
  }

  const pxPerHour = viewportWidth > 0 ? (viewportWidth * zoom) / 24 : 0
  const majorStep = pickStep([60, 120, 180, 360, 720], pxPerHour, 36)

  const majorTicks: number[] = []
  const minorTicks: number[] = []

  if (pxPerHour > 0) {
    for (let minute = 0; minute <= 24 * 60; minute += majorStep) {
      majorTicks.push(minute)
    }

    // Between the hour marks there are only three ticks: 15, 30 and 45 minutes.
    for (let hour = 0; hour < 24; hour++) {
      minorTicks.push(hour * 60 + 15, hour * 60 + 30, hour * 60 + 45)
    }
  }

  const playheadLeft = playheadTime
    ? Math.min(100, Math.max(0, ((playheadTime.getTime() - dayStartMs) / DAY_MS) * 100))
    : null

  function handlePointerDown(event: React.PointerEvent<HTMLDivElement>) {
    const element = scrollRef.current

    if (!element) {
      return
    }

    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })

    if (pointersRef.current.size === 2) {
      const points = [...pointersRef.current.values()]
      const distance = Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y)

      pinchRef.current = {
        startDistance: Math.max(1, distance),
        startZoom: zoom
      }
      dragRef.current = null
      suppressSeekRef.current = true
      return
    }

    dragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      scroll: element.scrollLeft,
      moved: false
    }

    try {
      event.currentTarget.setPointerCapture(event.pointerId)
    } catch {
      // synthetic events and some browsers do not support capture
    }
  }

  function handlePointerMove(event: React.PointerEvent<HTMLDivElement>) {
    if (pointersRef.current.has(event.pointerId)) {
      pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    }

    const pinch = pinchRef.current

    if (pinch && pointersRef.current.size >= 2) {
      const points = [...pointersRef.current.values()]
      const element = scrollRef.current

      if (points.length >= 2 && element) {
        event.preventDefault()

        const distance = Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y)
        const factor = distance / pinch.startDistance
        const nextZoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, pinch.startZoom * factor))

        if (nextZoom !== zoom) {
          const rect = element.getBoundingClientRect()
          const cursorX = (points[0].x + points[1].x) / 2 - rect.left
          const ratio = (element.scrollLeft + cursorX) / Math.max(1, element.scrollWidth)

          zoomAnchorRef.current = { ratio, cursorX }
          setZoom(nextZoom)
        }
      }

      return
    }

    const drag = dragRef.current
    const element = scrollRef.current

    if (!drag || !element) {
      return
    }

    const dx = event.clientX - drag.startX

    if (Math.abs(dx) > 4) {
      drag.moved = true
    }

    if (drag.moved) {
      element.scrollLeft = drag.scroll - dx
    }
  }

  function handlePointerUp(event: React.PointerEvent<HTMLDivElement>) {
    pointersRef.current.delete(event.pointerId)

    if (pointersRef.current.size < 2) {
      pinchRef.current = null
    }

    if (suppressSeekRef.current) {
      dragRef.current = null

      if (pointersRef.current.size === 0) {
        suppressSeekRef.current = false
      }

      return
    }

    const drag = dragRef.current
    dragRef.current = null

    if (!drag || drag.moved) {
      return
    }

    const content = trackRef.current

    if (!content) {
      return
    }

    const rect = content.getBoundingClientRect()
    const ratio = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width))
    onSeek(new Date(dayStartMs + ratio * DAY_MS))
  }

  const showLanes = lanes !== undefined && lanes.length > 0

  return (
    <section className="archive-timeline-panel">
      <div
        className="archive-timeline-scroll"
        ref={scrollRef}
        style={{ overflowX: zoom > MIN_ZOOM ? 'auto' : 'hidden' }}
      >
        <div
          className="archive-timeline-content"
          style={{ width: `${zoom * 100}%` }}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerCancel={(event) => {
            pointersRef.current.delete(event.pointerId)
            dragRef.current = null

            if (pointersRef.current.size < 2) {
              pinchRef.current = null
            }

            suppressSeekRef.current = false
          }}
        >
          <div className="archive-timeline-ruler">
            {minorTicks.map((minute) => (
              <span
                key={`minor-${minute}`}
                className={
                  minute % 60 === 30
                    ? 'archive-timeline-tick minor half'
                    : 'archive-timeline-tick minor'
                }
                style={{ left: `${(minute / (24 * 60)) * 100}%` }}
              />
            ))}

            {majorTicks.map((minute) => {
              const label = `${Math.floor(minute / 60)
                .toString()
                .padStart(2, '0')}:${(minute % 60).toString().padStart(2, '0')}`
              const edge = minute === 0 ? ' first' : minute === 24 * 60 ? ' last' : ''

              return (
                <span
                  key={`major-${minute}`}
                  className={`archive-timeline-tick major${edge}`}
                  style={{ left: `${(minute / (24 * 60)) * 100}%` }}
                  title={label}
                >
                  {label}
                </span>
              )
            })}

            {playheadLeft !== null && playheadTime ? (
              <span
                className={`archive-playhead-time${
                  playheadLeft < 6 ? ' first' : playheadLeft > 94 ? ' last' : ''
                }`}
                style={{ left: `${playheadLeft}%` }}
              >
                {formatClock(playheadTime)}
              </span>
            ) : null}
          </div>

          <div className="archive-timeline-tracks" ref={trackRef}>
            {showLanes ? (
              lanes.map((lane) => (
                <div
                  key={lane.cameraId}
                  className={
                    lane.cameraId === activeLaneId
                      ? 'archive-timeline-lane active'
                      : 'archive-timeline-lane'
                  }
                  title={lane.name}
                  onPointerDown={() => onSelectLane?.(lane.cameraId)}
                >
                  {renderBars(lane.intervals, lane.events)}
                </div>
              ))
            ) : (
              <div className="archive-timeline-lane active">
                {renderBars(intervals, events)}
              </div>
            )}

            {playheadLeft !== null ? (
              <span className="archive-playhead" style={{ left: `${playheadLeft}%` }} />
            ) : null}
          </div>
        </div>
      </div>
    </section>
  )
}
