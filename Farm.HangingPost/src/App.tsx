import { Component, useEffect, useState, type ErrorInfo, type ReactNode } from 'react'
import { MicrosoftTab } from '@farm/microsoft'
import { Dashboard } from './Dashboard'
import { WinchesterView } from './WinchesterView'
import { AccountMenu } from './AccountMenu'
import { ServicesMenu } from './ServicesMenu'
import { ConnectAi } from './ConnectAi'
import { useSession } from './session'
import './App.css'
import './themes/microsoft/microsoft.scss'
import './themes/brick/brick.scss'
import './themes/winchester/winchester.scss'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
  error: Error | null
}

class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('ErrorBoundary caught an error:', error, errorInfo)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{ padding: '2rem', textAlign: 'center', color: '#d83b01' }}>
          <h2>Something went wrong displaying this tab.</h2>
          <pre style={{ textAlign: 'left', background: '#f3f2f1', padding: '1rem', borderRadius: '4px', overflow: 'auto' }}>
            {this.state.error?.message || String(this.state.error)}
          </pre>
          <button
            onClick={() => this.setState({ hasError: false, error: null })}
            style={{ marginTop: '1rem', padding: '0.5rem 1rem', cursor: 'pointer' }}
          >
            Retry
          </button>
        </div>
      )
    }

    return this.props.children
  }
}

interface TabDefinition {
  label: string
  path: string
  theme: 'microsoft' | 'brick' | 'winchester'
}

const tabs: TabDefinition[] = [
  { label: 'Elder Vibe Coder', path: '/', theme: 'microsoft' },
  { label: 'Dashboard', path: '/tracker', theme: 'brick' },
  { label: 'Cases', path: '/winchester', theme: 'winchester' },
]

function getPathname(): string {
  const path = window.location.pathname.toLowerCase()
  if (path === '/connect') return '/connect'
  if (path === '/winchester') return '/winchester'
  if (path === '/tracker' || path === '/brick' || path === '/bricks') return '/tracker'
  return '/'
}

const LAST_THEME_KEY = 'farm.lastTheme'

function loadLastTheme(): TabDefinition['theme'] {
  try {
    const saved = window.localStorage.getItem(LAST_THEME_KEY)
    return tabs.find((tab) => tab.theme === saved)?.theme ?? 'microsoft'
  } catch {
    return 'microsoft'
  }
}

function saveLastTheme(theme: TabDefinition['theme']) {
  try {
    window.localStorage.setItem(LAST_THEME_KEY, theme)
  } catch {
    // Storage can be blocked; the theme simply falls back to the default.
  }
}

export function App() {
  const [pathname, setPathname] = useState(getPathname)
  const { status, session } = useSession()
  const tabTheme = tabs.find((tab) => tab.path === pathname)?.theme
  // Pages that are not tabs (like /connect) keep the theme of the last tab the user opened.
  const theme = tabTheme ?? loadLastTheme()

  // The theme is applied to <html> so the whole page, nav and tab content restyle together.
  useEffect(() => {
    document.documentElement.dataset.theme = theme
    if (tabTheme) saveLastTheme(tabTheme)
  }, [theme, tabTheme])

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
      <header className="app-header">
        <nav className="tab-list" aria-label="Primary navigation">
          {tabs.map((tab) => {
            const isActive = pathname === tab.path
            return (
              <a
                key={tab.path}
                href={tab.path}
                aria-current={isActive ? 'page' : undefined}
                className={`tab${isActive ? ' tabActive' : ''}`}
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
        <AccountMenu session={session} loading={status === 'loading'} pathname={pathname} onNavigate={navigate} />
      </header>

      <section className="tab-content" aria-live="polite">
        <ErrorBoundary>
          {pathname === '/' && <MicrosoftTab />}
          {pathname === '/tracker' && <Dashboard session={session} loading={status === 'loading'} />}
          {pathname === '/winchester' && <WinchesterView />}
          {pathname === '/connect' && <ConnectAi session={session} loading={status === 'loading'} />}
        </ErrorBoundary>
      </section>

      <ServicesMenu />
    </main>
  )
}

export default App
