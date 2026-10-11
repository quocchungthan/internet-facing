import { useEffect, useId, useRef, useState } from 'react'

interface Service {
  label: string
  description: string
  url: string
}

const services: Service[] = [
  { label: 'Mail', description: 'mail.eldervibe.dev', url: 'https://mail.eldervibe.dev' },
  { label: 'Note', description: 'note.eldervibe.dev', url: 'https://note.eldervibe.dev' },
  { label: 'Storage', description: 'storage.eldervibe.dev', url: 'https://storage.eldervibe.dev' },
]

export function ServicesMenu() {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const listId = useId()

  useEffect(() => {
    if (!open) return
    const close = (event: MouseEvent | KeyboardEvent) => {
      if (event instanceof KeyboardEvent) {
        if (event.key === 'Escape') setOpen(false)
      } else if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [open])

  return (
    <div className="services" ref={rootRef}>
      <button
        type="button"
        className="services-toggle"
        aria-expanded={open}
        aria-controls={listId}
        aria-label="Elder Vibe services"
        onClick={() => setOpen((value) => !value)}
      >
        Services <span className={`services-arrow${open ? ' services-arrow-open' : ''}`} aria-hidden="true">▾</span>
      </button>
      {open && (
        <ul id={listId} className="services-list">
          {services.map((service) => (
            <li key={service.url}>
              <a href={service.url} target="_blank" rel="noopener noreferrer">
                <span className="services-name">{service.label}</span>
                <span className="services-host">{service.description}</span>
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
