import { useEffect, useRef, useState, type ReactNode } from 'react'
import type { ArchiveDay } from '../../api'
import { t } from '../../i18n'
import ArchiveCalendarView from './ArchiveCalendar'
import { FullscreenIcon, PauseIcon, PlayIcon } from './ArchivePlayer'

type Props = {
  playing: boolean
  loading: boolean
  rate: number
  onRate: (rate: number) => void
  canPrev: boolean
  canNext: boolean
  onPrev: () => void
  onNext: () => void
  onToggle: () => void
  filters?: ReactNode
  multi: boolean
  onToggleMulti: () => void
  fullscreen: boolean
  onToggleFullscreen: () => void
  muted: boolean
  onToggleMuted: () => void
  disabled?: boolean
  selectedDate: string
  calendarYear: number
  calendarMonth: number
  calendarDays: ArchiveDay[]
  onSelectDate: (date: string) => void
  onMonthChange: (year: number, month: number) => void
}

function formatDateLabel(value: string): string {
  const parsed = new Date(`${value}T00:00:00`)

  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  const pad = (number: number) => number.toString().padStart(2, '0')
  return `${pad(parsed.getDate())}.${pad(parsed.getMonth() + 1)}.${parsed.getFullYear()}`
}

const SPEEDS = [1, 2, 4, 8]

function PrevIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M18 5v14L8 12z" />
      <path d="M6 5v14" />
    </svg>
  )
}

function NextIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M6 5v14l10-7z" />
      <path d="M18 5v14" />
    </svg>
  )
}

function SoundIcon({ muted }: { muted: boolean }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 9h3l4-3v12l-4-3H4z" />
      {muted ? (
        <path d="M15.5 9.5l5 5M20.5 9.5l-5 5" />
      ) : (
        <path d="M15 9a4 4 0 0 1 0 6M17.5 6.5a8 8 0 0 1 0 11" />
      )}
    </svg>
  )
}

function MultiViewIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3.5" y="4.5" width="17" height="15" rx="2" />
      <path d="M12 4.5v15M3.5 12h17" />
    </svg>
  )
}

export default function ArchiveControls({
  playing,
  loading,
  rate,
  onRate,
  canPrev,
  canNext,
  onPrev,
  onNext,
  onToggle,
  filters,
  multi,
  onToggleMulti,
  fullscreen,
  onToggleFullscreen,
  muted,
  onToggleMuted,
  disabled,
  selectedDate,
  calendarYear,
  calendarMonth,
  calendarDays,
  onSelectDate,
  onMonthChange
}: Props) {
  const [calendarOpen, setCalendarOpen] = useState(false)
  const dateRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!calendarOpen) {
      return
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (dateRef.current && !dateRef.current.contains(event.target as Node)) {
        setCalendarOpen(false)
      }
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setCalendarOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [calendarOpen])

  return (
    <div className="archive-controls">
      <div className="archive-controls-group left">
        <div className="archive-date" ref={dateRef}>
          <button
            type="button"
            className="archive-date-button"
            title={t('archive.date.select')}
            aria-label={t('archive.date.select')}
            aria-haspopup="dialog"
            aria-expanded={calendarOpen}
            onClick={() => setCalendarOpen((open) => !open)}
          >
            {formatDateLabel(selectedDate)}
          </button>

          {calendarOpen ? (
            <div className="archive-date-popup" role="dialog" aria-label={t('archive.date.select')}>
              <ArchiveCalendarView
                year={calendarYear}
                month={calendarMonth}
                days={calendarDays}
                selectedDate={selectedDate}
                onSelect={(date) => {
                  onSelectDate(date)
                  setCalendarOpen(false)
                }}
                onMonthChange={onMonthChange}
              />
            </div>
          ) : null}
        </div>

        {filters}
      </div>

      <div className="archive-controls-group center">
        <button
          type="button"
          className="archive-icon-button"
          title={t('archive.prev')}
          aria-label={t('archive.prev')}
          disabled={disabled || !canPrev}
          onClick={onPrev}
        >
          <PrevIcon />
        </button>
        <button
          type="button"
          className="archive-icon-button primary"
          title={playing ? t('archive.pause') : t('archive.play')}
          aria-label={playing ? t('archive.pause') : t('archive.play')}
          disabled={disabled || loading}
          onClick={onToggle}
        >
          {playing ? <PauseIcon /> : <PlayIcon />}
        </button>
        <button
          type="button"
          className="archive-icon-button"
          title={t('archive.next')}
          aria-label={t('archive.next')}
          disabled={disabled || !canNext}
          onClick={onNext}
        >
          <NextIcon />
        </button>

        <div className="archive-speed" role="group" aria-label={t('archive.speed')} title={t('archive.speed')}>
          {SPEEDS.map((value) => (
            <button
              key={value}
              type="button"
              className={rate === value ? 'active' : ''}
              title={`${t('archive.speed')}: ${value}×`}
              aria-label={`${t('archive.speed')}: ${value}×`}
              aria-pressed={rate === value}
              disabled={disabled}
              onClick={() => onRate(value)}
            >
              {value}×
            </button>
          ))}
        </div>
      </div>

      <div className="archive-controls-group right">
        <button
          type="button"
          className={muted ? 'archive-icon-button muted' : 'archive-icon-button'}
          title={muted ? t('grid.unmute') : t('grid.mute')}
          aria-label={muted ? t('grid.unmute') : t('grid.mute')}
          aria-pressed={muted}
          onClick={onToggleMuted}
        >
          <SoundIcon muted={muted} />
        </button>
        <button
          type="button"
          className={multi ? 'archive-icon-button active' : 'archive-icon-button'}
          title={t('archive.grid.title')}
          aria-label={t('archive.grid.title')}
          aria-pressed={multi}
          onClick={onToggleMulti}
        >
          <MultiViewIcon />
        </button>
        <button
          type="button"
          className="archive-icon-button"
          title={fullscreen ? t('archive.fullscreen.exit') : t('archive.fullscreen')}
          aria-label={fullscreen ? t('archive.fullscreen.exit') : t('archive.fullscreen')}
          onClick={onToggleFullscreen}
        >
          <FullscreenIcon />
        </button>
      </div>
    </div>
  )
}
