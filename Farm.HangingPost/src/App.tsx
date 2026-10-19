import { Component, useEffect, useState, type ErrorInfo, type ReactNode } from 'react'
import { MicrosoftTab } from '@farm/microsoft'
import { BrickTracker } from '@farm/brick-tracker'
import { UsagePanel } from './UsagePanel'
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

interface ThemeOption {
  label: string
  theme: 'microsoft' | 'brick' | 'winchester'
}

const themes: ThemeOption[] = [
  { label: 'Microsoft', theme: 'microsoft' },
  { label: 'Brick', theme: 'brick' },
  { label: 'Winchester', theme: 'winchester' },
]

// Same content in three projects, each with its own layout.
function PortfolioView({ theme, analytics }: { theme: ThemeOption['theme']; analytics?: ReactNode }) {
  if (theme === 'brick') return <BrickTracker analytics={analytics} />
  if (theme === 'winchester') return <WinchesterView analytics={analytics} />
  return <MicrosoftTab analytics={analytics} />
}

function getPathname(): string {
  return window.location.pathname.toLowerCase() === '/connect' ? '/connect' : '/'
}

// Old tab URLs now just open the portfolio in the matching style.
function themeFromPath(): ThemeOption['theme'] | null {
  const path = window.location.pathname.toLowerCase()
  if (path === '/winchester') return 'winchester'
  if (path === '/tracker' || path === '/brick' || path === '/bricks') return 'brick'
  return null
}

const LAST_THEME_KEY = 'farm.lastTheme'

function loadLastTheme(): ThemeOption['theme'] {
  const fromPath = themeFromPath()
  if (fromPath) return fromPath
  try {
    const saved = window.localStorage.getItem(LAST_THEME_KEY)
    return themes.find((option) => option.theme === saved)?.theme ?? 'microsoft'
  } catch {
    return 'microsoft'
  }
}

function saveLastTheme(theme: ThemeOption['theme']) {
  try {
    window.localStorage.setItem(LAST_THEME_KEY, theme)
  } catch {
    // Storage can be blocked; the theme simply falls back to the default.
  }
}

export function App() {
  const [pathname, setPathname] = useState(getPathname)
  const [theme, setTheme] = useState(loadLastTheme)
  const { status, session } = useSession()

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    saveLastTheme(theme)
  }, [theme])

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
        <div className="tab-list theme-switcher" role="group" aria-label="Portfolio display style">
          {themes.map((option) => {
            const isActive = theme === option.theme
            return (
              <button
                key={option.theme}
                type="button"
                aria-pressed={isActive}
                className={`tab${isActive ? ' tabActive' : ''}`}
                onClick={() => setTheme(option.theme)}
              >
                {option.label}
              </button>
            )
          })}
        </div>
        <AccountMenu session={session} loading={status === 'loading'} pathname={pathname} onNavigate={navigate} />
      </header>

      <section className="tab-content" aria-live="polite">
        <ErrorBoundary>
          {pathname === '/' && <PortfolioView theme={theme} analytics={status !== 'loading' && session.isAuthenticated ? <UsagePanel session={session} loading={false} /> : undefined} />}
          {pathname === '/connect' && <ConnectAi session={session} loading={status === 'loading'} />}
        </ErrorBoundary>
      </section>

      <ServicesMenu />
    </main>
  )
}

export default App
