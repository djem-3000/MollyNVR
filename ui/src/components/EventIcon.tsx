type Props = {
  type: string
}

/** Векторные иконки типов обнаружения (24×24, stroke = currentColor). */
export default function EventIcon({ type }: Props) {
  return (
    <svg className="event-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      {icon(type)}
    </svg>
  )
}

function icon(type: string) {
  switch (type) {
    case 'motion':
      return (
        <>
          <circle cx="12" cy="12" r="1.6" />
          <path d="M8.5 8.5a5 5 0 0 1 7 0" />
          <path d="M5.5 5.5a9 9 0 0 1 13 0" />
          <path d="M8.5 15.5a5 5 0 0 0 7 0" />
          <path d="M5.5 18.5a9 9 0 0 0 13 0" />
        </>
      )

    case 'linecrossing':
      return (
        <>
          <path d="M3 17 21 7" strokeDasharray="3 3" />
          <path d="M14 5h5v5" />
          <path d="M19 5l-6 6" />
        </>
      )

    case 'intrusion':
      return (
        <>
          <path d="M12 3l7 3v5c0 4.2-3 7.2-7 9-4-1.8-7-4.8-7-9V6z" />
          <path d="M12 8v4" />
          <path d="M12 15.5v.5" />
        </>
      )

    case 'regionentrance':
      return (
        <>
          <rect x="4" y="4" width="8" height="16" rx="1" />
          <path d="M14 12h7" />
          <path d="M18 8l3 4-3 4" />
        </>
      )

    case 'regionexiting':
      return (
        <>
          <rect x="4" y="4" width="8" height="16" rx="1" />
          <path d="M21 12h-7" />
          <path d="M17 8l-3 4 3 4" />
        </>
      )

    case 'loitering':
      return (
        <>
          <circle cx="12" cy="12" r="8" />
          <path d="M12 7.5V12l3 2" />
        </>
      )

    case 'face':
      return (
        <>
          <circle cx="12" cy="12" r="8" />
          <path d="M9 10v.5" />
          <path d="M15 10v.5" />
          <path d="M9 15c1.8 1.5 4.2 1.5 6 0" />
        </>
      )

    case 'person':
      return (
        <>
          <circle cx="12" cy="8" r="3" />
          <path d="M6 20a6 6 0 0 1 12 0" />
        </>
      )

    case 'vehicle':
      return (
        <>
          <path d="M4 15l1.6-4.5A2 2 0 0 1 7.5 9h9a2 2 0 0 1 1.9 1.5L20 15" />
          <rect x="3" y="15" width="18" height="4" rx="1" />
          <circle cx="7" cy="19" r="1" />
          <circle cx="17" cy="19" r="1" />
        </>
      )

    case 'tamper':
      return (
        <>
          <path d="M3 8h10v7H3z" />
          <path d="M13 11l7-3v8l-7-3" />
          <path d="M4 20L20 4" />
        </>
      )

    case 'animal':
      return (
        <>
          <circle cx="7" cy="9.5" r="1.5" />
          <circle cx="12" cy="7.5" r="1.5" />
          <circle cx="17" cy="9.5" r="1.5" />
          <path d="M7.8 14.6c0-2.3 1.9-3.8 4.2-3.8s4.2 1.5 4.2 3.8c0 1.9-1.6 3.2-3.4 3.2-.8 0-1 .5-1.6.5s-.8-.5-1.6-.5c-1.8 0-3.4-1.3-3.4-3.2z" />
        </>
      )

    case 'sound':
      return (
        <>
          <path d="M5 10v4h3l4 3V7L8 10z" />
          <path d="M16 9a4 4 0 0 1 0 6" />
        </>
      )

    default:
      return (
        <>
          <circle cx="12" cy="12" r="7" />
          <circle cx="12" cy="12" r="1.5" />
        </>
      )
  }
}
