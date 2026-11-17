import { useEffect, useMemo, useState } from 'react'
import { t } from '../i18n'

type Props = {
  value: string
  disabled?: boolean
  onChange: (value: string) => void
}

const Days = 7
const Slots = 24

const DAY_KEYS = [
  'schedule.day.mon',
  'schedule.day.tue',
  'schedule.day.wed',
  'schedule.day.thu',
  'schedule.day.fri',
  'schedule.day.sat',
  'schedule.day.sun'
]

function parse(value: string): boolean[] {
  const bits = new Array<boolean>(Days * Slots).fill(false)

  for (let index = 0; index < bits.length && index < value.length; index++) {
    bits[index] = value[index] === '1'
  }

  return bits
}

function serialize(bits: boolean[]): string {
  return bits.map((bit) => (bit ? '1' : '0')).join('')
}

function timeLabel(slot: number): string {
  return `${String(slot).padStart(2, '0')}:00`
}

export default function ScheduleGrid({ value, disabled, onChange }: Props) {
  const bits = useMemo(() => parse(value), [value])
  const [painting, setPainting] = useState<boolean | null>(null)

  useEffect(() => {
    const stop = () => setPainting(null)
    window.addEventListener('pointerup', stop)
    window.addEventListener('pointercancel', stop)

    return () => {
      window.removeEventListener('pointerup', stop)
      window.removeEventListener('pointercancel', stop)
    }
  }, [])

  function apply(day: number, slot: number, bit: boolean) {
    const index = day * Slots + slot
    if (bits[index] === bit) {
      return
    }

    const next = bits.slice()
    next[index] = bit
    onChange(serialize(next))
  }

  function handleDown(day: number, slot: number) {
    if (disabled) {
      return
    }

    const bit = !bits[day * Slots + slot]
    setPainting(bit)
    apply(day, slot, bit)
  }

  function handleEnter(day: number, slot: number) {
    if (!disabled && painting !== null) {
      apply(day, slot, painting)
    }
  }

  return (
    <div className={`schedule ${disabled ? 'disabled' : ''}`}>
      <div className="schedule-actions">
        <button
          type="button"
          className="secondary"
          disabled={disabled}
          onClick={() => onChange(serialize(new Array<boolean>(Days * Slots).fill(true)))}
        >
          {t('schedule.all')}
        </button>
        <button
          type="button"
          className="secondary"
          disabled={disabled}
          onClick={() => onChange(serialize(new Array<boolean>(Days * Slots).fill(false)))}
        >
          {t('schedule.none')}
        </button>
      </div>

      <div className="schedule-scroll">
        <table className="schedule-grid">
          <thead>
            <tr>
              <th scope="col" aria-label={t('schedule.time')} />
              {Array.from({ length: Slots }, (_, slot) => (
                <th key={slot} scope="col">
                  {String(slot).padStart(2, '0')}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: Days }, (_, day) => (
              <tr key={day}>
                <th scope="row">{t(DAY_KEYS[day])}</th>
                {Array.from({ length: Slots }, (_, slot) => {
                  const active = bits[day * Slots + slot]

                  return (
                    <td key={slot}>
                      <button
                        type="button"
                        className={`schedule-cell ${active ? 'active' : ''}`}
                        disabled={disabled}
                        aria-pressed={active}
                        aria-label={`${t(DAY_KEYS[day])} ${timeLabel(slot)}`}
                        onPointerDown={() => handleDown(day, slot)}
                        onPointerEnter={() => handleEnter(day, slot)}
                      />
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
