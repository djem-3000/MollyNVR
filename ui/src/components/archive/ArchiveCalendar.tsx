import type { ArchiveDay } from '../../api'
import { t } from '../../i18n'
import { localDateKey, pad2 } from './archiveTime'

type Props = {
  year: number
  month: number
  days: ArchiveDay[]
  selectedDate: string
  onSelect: (date: string) => void
  onMonthChange: (year: number, month: number) => void
}

const WEEKDAYS = [
  t('schedule.day.mon'),
  t('schedule.day.tue'),
  t('schedule.day.wed'),
  t('schedule.day.thu'),
  t('schedule.day.fri'),
  t('schedule.day.sat'),
  t('schedule.day.sun')
]

export default function ArchiveCalendar({
  year,
  month,
  days,
  selectedDate,
  onSelect,
  onMonthChange
}: Props) {
  const first = new Date(year, month - 1, 1)
  const offset = (first.getDay() + 6) % 7
  const total = new Date(year, month, 0).getDate()
  const cells: Array<number | null> = [
    ...Array.from({ length: offset }, () => null),
    ...Array.from({ length: total }, (_, index) => index + 1)
  ]

  while (cells.length % 7 !== 0) {
    cells.push(null)
  }

  const recordings = new Map(days.map((day) => [day.date, day]))
  const today = localDateKey(new Date())
  const headingRaw = first.toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' })
  const heading = headingRaw.charAt(0).toUpperCase() + headingRaw.slice(1)

  function shiftMonth(delta: number) {
    const next = new Date(year, month - 1 + delta, 1)
    onMonthChange(next.getFullYear(), next.getMonth() + 1)
  }

  return (
    <section className="archive-calendar">
      <header className="archive-calendar-header">
        <button
          type="button"
          className="archive-icon-button"
          aria-label={t('archive.calendar.prev')}
          title={t('archive.calendar.prev')}
          onClick={() => shiftMonth(-1)}
        >
          ‹
        </button>
        <span className="archive-calendar-title">{heading}</span>
        <button
          type="button"
          className="archive-icon-button"
          aria-label={t('archive.calendar.next')}
          title={t('archive.calendar.next')}
          onClick={() => shiftMonth(1)}
        >
          ›
        </button>
      </header>

      <div className="archive-calendar-grid">
        {WEEKDAYS.map((weekday) => (
          <span key={weekday} className="archive-calendar-weekday">
            {weekday}
          </span>
        ))}

        {cells.map((day, index) => {
          if (day === null) {
            return <span key={`empty-${index}`} className="archive-calendar-empty" />
          }

          const key = `${year}-${pad2(month)}-${pad2(day)}`
          const recorded = recordings.get(key)
          const classes = ['archive-calendar-day']

          if (key === selectedDate) {
            classes.push('selected')
          }

          if (key === today) {
            classes.push('today')
          }

          if (recorded) {
            classes.push('has-recordings')
          }

          return (
            <button
              key={key}
              type="button"
              className={classes.join(' ')}
              title={recorded ? t('archive.calendar.count', { count: recorded.segmentCount }) : undefined}
              onClick={() => onSelect(key)}
            >
              {day}
              {recorded ? <span className="archive-calendar-dot" aria-hidden="true" /> : null}
            </button>
          )
        })}
      </div>

      <button type="button" className="archive-calendar-today" onClick={() => onSelect(today)}>
        {t('archive.calendar.today')}
      </button>
    </section>
  )
}
