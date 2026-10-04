import { Component, useEffect, useState, type ErrorInfo, type ReactNode } from 'react'
import { MicrosoftTab } from '@farm/microsoft'
import { BrickTracker } from './BrickTracker'
import { WinchesterView } from './WinchesterView'
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
        <ErrorBoundary>
          {pathname === '/' && <MicrosoftTab />}
          {pathname === '/tracker' && <BrickTracker />}
          {pathname === '/winchester' && <WinchesterView />}
        </ErrorBoundary>
      </section>
    </main>
  )
}

export default App
