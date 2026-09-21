import { useEffect, useState } from 'react'
import './App.css'

const tabs = [
  { label: 'Elder Vibe Coder', path: '/' },
  { label: 'Winchester', path: '/winchester' },
]

function getPathname() {
  return window.location.pathname === '/winchester' ? '/winchester' : '/'
}

function App() {
  const [pathname, setPathname] = useState(getPathname)

  useEffect(() => {
    const handlePopState = () => setPathname(getPathname())

    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  const navigate = (path: string) => {
    if (path === pathname) {
      return
    }

    window.history.pushState(null, '', path)
    setPathname(path)
  }

  return (
    <main className="app-shell">
      <nav className="tab-list" aria-label="Primary navigation">
        {tabs.map((tab) => (
          <a
            aria-current={pathname === tab.path ? 'page' : undefined}
            className={`tab${pathname === tab.path ? ' tabActive' : ''}`}
            href={tab.path}
            key={tab.path}
            onClick={(event) => {
              event.preventDefault()
              navigate(tab.path)
            }}
          >
            {tab.label}
          </a>
        ))}
      </nav>
      <section className="content" aria-labelledby="page-title">
        <p className="eyebrow">Farm</p>
        <h1 id="page-title">
          {pathname === '/winchester' ? 'Winchester' : 'Elder Vibe Coder'}
        </h1>
        <p className="intro">
          {pathname === '/winchester'
            ? 'A focused space for Winchester.'
            : 'A practical home for thoughtful software work.'}
        </p>
      </section>
    </main>
  )
}

export default App
