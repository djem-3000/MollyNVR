import type { ReactNode } from 'react'

type Props = {
  title: string
  actions?: ReactNode
  className?: string
  bodyClassName?: string
  children: ReactNode
}

export default function PageLayout({
  title,
  actions,
  className,
  bodyClassName,
  children
}: Props) {
  const shellClassName = ['card', 'page-shell', className].filter(Boolean).join(' ')
  const bodyClassNames = ['page-body', bodyClassName].filter(Boolean).join(' ')

  return (
    <section className={shellClassName}>
      <header className="page-toolbar">
        <h1>{title}</h1>
        {actions ? <div className="page-actions">{actions}</div> : null}
      </header>
      <div className={bodyClassNames}>{children}</div>
    </section>
  )
}
