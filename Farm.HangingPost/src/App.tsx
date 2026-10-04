import { useEffect, useState } from 'react'
import { MicrosoftTab } from '@farm/microsoft'
import { BrickTracker } from './BrickTracker'
import { WinchesterView } from './WinchesterView'
import './App.css'
import './themes/microsoft/microsoft.scss'
import './themes/brick/brick.scss'
import './themes/winchester/winchester.scss'

interface TabDefinition {
  label: string
  path: string
  themeClass: string
}

const tabs: TabDefinition[] = [
  { label: 'Elder Vibe Coder', path: '/', themeClass: 'tab-microsoft' },
  { label: 'Brick Tracker', path: '/tracker', themeClass: 'tab-brick' },
  { label: 'Winchester', path: '/winchester', themeClass: 'tab-winchester' },
]

function getPathname(): string {
  const path = window.location.pathname.toLowerCase()
  if (path === '/winchester') return '/winchester'
  if (path === '/tracker' || path === '/brick' || path === '/bricks') return '/tracker'
  return '/'
}

export function App() {
  const [pathname, setPathname] = useState(getPathname)

  useEffect(() => {
    const handlePopState = () => setPathname(getPathname())

    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  const navigate = (path: string) => {
    if (path === pathname) return
    window.history.pushState(null, '', path)
    setPathname(path)
  }

  return (
    <main className="app-shell">
      <nav className="tab-list" aria-label="Primary navigation">
        {tabs.map((tab) => {
          const isActive = pathname === tab.path
          return (
            <a
              key={tab.path}
              href={tab.path}
              aria-current={isActive ? 'page' : undefined}
              className={`tab ${tab.themeClass}${isActive ? ' tabActive' : ''}`}
              onClick={(event) => {
                event.preventDefault()
                navigate(tab.path)
              }}
            >
              {tab.label}
            </a>
          )
        })}
      </nav>

      <section className="tab-content" aria-live="polite">
        {pathname === '/' && <MicrosoftTab />}
        {pathname === '/tracker' && <BrickTracker />}
        {pathname === '/winchester' && <WinchesterView />}
      </section>
    </main>
  )
}

export default App
