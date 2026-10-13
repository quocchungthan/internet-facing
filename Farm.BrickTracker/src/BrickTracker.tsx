import { useEffect, useState, useMemo, type CSSProperties } from 'react'
import './brick.scss'

export interface PlatformBookmark {
  id: number
  platform: string
  url: string
  platformId: string
  description: string
  color: string
  displayOrder: number
}

export interface ClickRow {
  platform: string
  day: string
  count: number
}

export interface LocalhostTool {
  id: number
  name: string
  description: string
  category: 'SearchCollect' | 'ConvertTransformCompose' | 'ObserveWatching' | 'CalculatingAnalysing' | string
  directUrl: string
  repositoryUrl: string
  issueUrl?: string | null
  displayOrder: number
}

const CATEGORY_METADATA: Record<
  string,
  { label: string; description: string; badgeClass: string }
> = {
  SearchCollect: {
    label: 'Search & Collect',
    description: 'Context discovery, web extractors, repository scanners & knowledge collectors',
    badgeClass: 'cat-SearchCollect',
  },
  ConvertTransformCompose: {
    label: 'Convert, Transform & Compose',
    description: 'Document translators, syntax converters, prompt composers & AST transformers',
    badgeClass: 'cat-ConvertTransformCompose',
  },
  ObserveWatching: {
    label: 'Observe & Watch',
    description: 'Live telemetry, log streamers, system watchers & health monitors',
    badgeClass: 'cat-ObserveWatching',
  },
  CalculatingAnalysing: {
    label: 'Calculate & Analyze',
    description: 'Metrics aggregators, query analyzers, habit engines & performance scorers',
    badgeClass: 'cat-CalculatingAnalysing',
  },
}

const ORDERED_CATEGORIES = [
  'SearchCollect',
  'ConvertTransformCompose',
  'ObserveWatching',
  'CalculatingAnalysing',
]

const dayKey = (d: Date) => d.toISOString().slice(0, 10)

function buildDailyCounts(rows: ClickRow[], daysCount: number, platform: string) {
  const byDay = new Map<string, number>()
  for (const r of rows) {
    if (platform !== 'all' && r.platform.toLowerCase() !== platform) continue
    byDay.set(r.day, (byDay.get(r.day) ?? 0) + r.count)
  }
  const today = new Date()
  const base = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate())
  const days: { date: string; count: number }[] = []
  for (let i = daysCount - 1; i >= 0; i--) {
    const date = dayKey(new Date(base - i * 86400000))
    days.push({ date, count: byDay.get(date) ?? 0 })
  }
  return days
}

const levelOf = (count: number) => (count === 0 ? 0 : count <= 1 ? 1 : count <= 3 ? 2 : count <= 6 ? 3 : 4)

// SVG Icons
function BrickIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="2" y="3" width="20" height="18" rx="2" />
      <path d="M2 9h20" />
      <path d="M2 15h20" />
      <path d="M8 3v6" />
      <path d="M16 3v6" />
      <path d="M12 9v6" />
      <path d="M6 15v6" />
      <path d="M18 15v6" />
    </svg>
  )
}

function ToolsIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
    </svg>
  )
}

function ExternalIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
      <polyline points="15 3 21 3 21 9" />
      <line x1="10" y1="14" x2="21" y2="3" />
    </svg>
  )
}

function GitRepoIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <line x1="6" y1="3" x2="6" y2="15" />
      <circle cx="18" cy="6" r="3" />
      <circle cx="6" cy="18" r="3" />
      <path d="M18 9a9 9 0 0 1-9 9" />
    </svg>
  )
}

function IssueIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  )
}

function CategoryIcon({ category }: { category: string }) {
  if (category === 'SearchCollect') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <circle cx="11" cy="11" r="8" />
        <line x1="21" y1="21" x2="16.65" y2="16.65" />
      </svg>
    )
  }
  if (category === 'ConvertTransformCompose') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <polyline points="16 3 21 3 21 8" />
        <line x1="4" y1="20" x2="21" y2="3" />
        <polyline points="21 16 21 21 16 21" />
        <line x1="15" y1="15" x2="21" y2="21" />
        <line x1="4" y1="4" x2="9" y2="9" />
      </svg>
    )
  }
  if (category === 'ObserveWatching') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
        <circle cx="12" cy="12" r="3" />
      </svg>
    )
  }
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="4" y="2" width="16" height="20" rx="2" />
      <line x1="8" y1="6" x2="16" y2="6" />
      <line x1="16" y1="14" x2="16" y2="18" />
      <path d="M16 10h.01" />
      <path d="M12 10h.01" />
      <path d="M8 10h.01" />
      <path d="M12 14h.01" />
      <path d="M8 14h.01" />
      <path d="M12 18h.01" />
      <path d="M8 18h.01" />
    </svg>
  )
}

function RefreshIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <polyline points="23 4 23 10 17 10" />
      <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" />
    </svg>
  )
}

export const BrickTracker = () => {
  const [platforms, setPlatforms] = useState<PlatformBookmark[]>([])
  const [selectedPlatform, setSelectedPlatform] = useState<string>('all')
  const [clicks, setClicks] = useState<ClickRow[]>([])
  const [tools, setTools] = useState<LocalhostTool[]>([])
  const [loading, setLoading] = useState(true)
  const [toolStatuses, setToolStatuses] = useState<Record<number, 'checking' | 'online' | 'offline'>>({})

  const registerClick = (p: PlatformBookmark) => {
    const day = dayKey(new Date())
    setClicks((prev) => {
      const hit = prev.find((r) => r.platform === p.platform && r.day === day)
      return hit
        ? prev.map((r) => (r === hit ? { ...r, count: r.count + 1 } : r))
        : [...prev, { platform: p.platform, day, count: 1 }]
    })
    fetch(`/api/feeds/${encodeURIComponent(p.platform)}/click`, { method: 'POST', keepalive: true }).catch(() => {})
  }

  const checkToolHealth = async (tool: LocalhostTool) => {
    setToolStatuses((prev) => ({ ...prev, [tool.id]: 'checking' }))
    try {
      // Use no-cors mode to safely ping localhost origins without triggering CORS blocking
      const controller = new AbortController()
      const timeoutId = setTimeout(() => controller.abort(), 2500)
      await fetch(tool.directUrl, { method: 'HEAD', mode: 'no-cors', signal: controller.signal })
      clearTimeout(timeoutId)
      setToolStatuses((prev) => ({ ...prev, [tool.id]: 'online' }))
    } catch {
      setToolStatuses((prev) => ({ ...prev, [tool.id]: 'offline' }))
    }
  }

  useEffect(() => {
    Promise.all([
      fetch('/api/feeds').then((res) => (res.ok ? (res.json() as Promise<PlatformBookmark[]>) : [])),
      fetch('/api/feeds/clicks').then((res) => (res.ok ? (res.json() as Promise<ClickRow[]>) : [])),
      fetch('/api/tools').then((res) => (res.ok ? (res.json() as Promise<LocalhostTool[]>) : [])),
    ])
      .then(([feeds, rows, toolList]) => {
        setPlatforms(feeds)
        setClicks(rows)
        setTools(toolList)
        setLoading(false)
      })
      .catch((err) => {
        console.error('Failed to load data for brick tracker:', err)
        setLoading(false)
      })
  }, [])

  const currentThemeColor = useMemo(() => {
    if (selectedPlatform === 'all') return '#b24c29'
    const found = platforms.find((p) => p.platform.toLowerCase() === selectedPlatform.toLowerCase())
    return found ? found.color : '#b24c29'
  }, [platforms, selectedPlatform])

  const weeks = 24
  const daysCount = weeks * 7
  const days = useMemo(() => buildDailyCounts(clicks, daysCount, selectedPlatform), [clicks, daysCount, selectedPlatform])
  const totalContributions = useMemo(() => days.reduce((a, b) => a + b.count, 0), [days])

  // Group tools by category
  const groupedTools = useMemo(() => {
    const groups: Record<string, LocalhostTool[]> = {}
    for (const cat of ORDERED_CATEGORIES) {
      groups[cat] = []
    }
    for (const tool of tools) {
      if (!groups[tool.category]) groups[tool.category] = []
      groups[tool.category].push(tool)
    }
    return groups
  }, [tools])

  return (
    <div className="brick-container">
      {/* Header */}
      <header className="brick-header">
        <div>
          <div className="brick-title-group">
            <span className="brick-icon-wrapper">
              <BrickIcon />
            </span>
            <h2>Platform Habit Tracker &amp; Feeds</h2>
          </div>
          <p className="brick-subtitle">
            Link clicks, activity habits, and local developer tool orchestration.
          </p>
        </div>
      </header>

      {/* Platform Filter Buttons */}
      <nav className="brick-platform-selector" aria-label="Filter activity by platform">
        <button
          type="button"
          className={`brick-platform-btn ${selectedPlatform === 'all' ? 'active' : ''}`}
          style={selectedPlatform === 'all' ? { backgroundColor: '#b24c29', borderColor: '#b24c29' } : {}}
          onClick={() => setSelectedPlatform('all')}
        >
          <span className="platform-dot" style={{ backgroundColor: '#b24c29' }} />
          All Platforms
        </button>

        {platforms.map((p) => {
          const isActive = selectedPlatform.toLowerCase() === p.platform.toLowerCase()
          return (
            <button
              key={p.id}
              type="button"
              className={`brick-platform-btn ${isActive ? 'active' : ''}`}
              style={isActive ? { backgroundColor: p.color, borderColor: p.color } : {}}
              onClick={() => setSelectedPlatform(p.platform.toLowerCase())}
            >
              <span className="platform-dot" style={{ backgroundColor: p.color }} />
              {p.platform}
            </button>
          )
        })}
      </nav>

      {/* Habit Tracker Matrix */}
      <section className="brick-tracker-section" aria-label="Habit contribution graph">
        <div className="tracker-header">
          <h3>
            {selectedPlatform === 'all'
              ? 'Aggregated Activity'
              : `${platforms.find((p) => p.platform.toLowerCase() === selectedPlatform)?.platform || selectedPlatform} Contributions`}
          </h3>
          <div className="tracker-stats">
            <span>
              Total clicks: <strong>{totalContributions}</strong>
            </span>
            <span>
              Active window: <strong>{weeks} weeks</strong>
            </span>
          </div>
        </div>

        <div className="tracker-grid-container">
          <div className="tracker-grid" role="grid" aria-label="Contribution squares">
            {days.map(({ date, count }, idx) => {
              const level = levelOf(count)
              const bg =
                level === 0
                  ? 'var(--brick-empty, #ebedf0)'
                  : currentThemeColor + (level === 1 ? '44' : level === 2 ? '77' : level === 3 ? 'bb' : 'ff')

              return (
                <div
                  key={idx}
                  className="tracker-brick"
                  style={{ backgroundColor: bg }}
                  title={`${date}: ${count} link click(s) (${selectedPlatform})`}
                  role="gridcell"
                  tabIndex={0}
                  aria-label={`${date}: ${count} clicks`}
                />
              )
            })}
          </div>
        </div>

        <div className="tracker-footer">
          <span>{loading ? 'Fetching platform feeds...' : `${platforms.length} active platforms monitored`}</span>
          <div className="legend-group" aria-hidden="true">
            <span>Less</span>
            <span className="legend-brick" style={{ backgroundColor: 'var(--brick-empty, #ebedf0)' }} />
            <span className="legend-brick" style={{ backgroundColor: currentThemeColor + '44' }} />
            <span className="legend-brick" style={{ backgroundColor: currentThemeColor + '77' }} />
            <span className="legend-brick" style={{ backgroundColor: currentThemeColor + 'bb' }} />
            <span className="legend-brick" style={{ backgroundColor: currentThemeColor + 'ff' }} />
            <span>More</span>
          </div>
        </div>
      </section>

      {/* Feeds Stream List */}
      <section className="brick-feeds-section" aria-label="Configured platform bookmarks">
        <div className="section-header-row">
          <h3>Configured Platform Feeds &amp; Bookmarks</h3>
        </div>
        <div className="brick-feeds-list">
          {platforms.map((p) => (
            <div
              key={p.id}
              className="brick-feed-row"
              style={{ '--card-color': p.color } as CSSProperties}
            >
              <div className="feed-left">
                <span className="feed-dot" style={{ backgroundColor: p.color }} />
                <span className="feed-platform">{p.platform}</span>
                <span className="feed-tag">@{p.platformId}</span>
              </div>
              <p className="feed-desc">{p.description}</p>
              <div>
                <a
                  href={p.url}
                  target="_blank"
                  rel="noreferrer noopener"
                  className="feed-link"
                  onClick={() => registerClick(p)}
                >
                  <span>Visit {p.platform}</span>
                  <ExternalIcon />
                </a>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Localhost Tool Directory (EMFc functional blocks) */}
      <section className="localhost-tools-section" aria-label="Localhost Developer Tools">
        <header className="tools-header">
          <div className="tools-title-row">
            <span className="tools-icon-wrapper">
              <ToolsIcon />
            </span>
            <h3>Localhost Personal Tools &amp; Workers</h3>
          </div>
          <p className="tools-subtitle">
            Encapsulated functional microservices running on your local machine, partitioned into EMFc contract categories. Click to launch directly or inspect repository issues.
          </p>
        </header>

        {ORDERED_CATEGORIES.map((catKey) => {
          const meta = CATEGORY_METADATA[catKey] || {
            label: catKey,
            description: '',
            badgeClass: 'cat-SearchCollect',
          }
          const items = groupedTools[catKey] || []
          if (items.length === 0) return null

          return (
            <div key={catKey} className="tools-category-group">
              <div className="category-title-bar">
                <span className={`category-badge ${meta.badgeClass}`}>
                  <CategoryIcon category={catKey} />
                  <span>{meta.label}</span>
                </span>
                <span className="category-desc">{meta.description}</span>
              </div>

              <div className="tools-grid">
                {items.map((tool) => {
                  const status = toolStatuses[tool.id]
                  return (
                    <article key={tool.id} className="tool-card">
                      <div className="card-top">
                        <div className="tool-name-row">
                          <h4>{tool.name}</h4>
                          {status && (
                            <span className={`status-pill ${status}`}>
                              <span className="status-dot" />
                              {status === 'online'
                                ? 'Online'
                                : status === 'offline'
                                ? 'Offline'
                                : 'Checking...'}
                            </span>
                          )}
                        </div>
                        <p className="tool-desc">{tool.description}</p>
                      </div>

                      <div className="card-actions">
                        <a
                          href={tool.directUrl}
                          target="_blank"
                          rel="noreferrer noopener"
                          className="btn-direct"
                          title={`Launch ${tool.name} at ${tool.directUrl}`}
                        >
                          <span>Launch Tool</span>
                          <ExternalIcon />
                        </a>

                        {tool.repositoryUrl && (
                          <a
                            href={tool.repositoryUrl}
                            target="_blank"
                            rel="noreferrer noopener"
                            className="btn-secondary"
                            title="View Git Repository"
                          >
                            <GitRepoIcon />
                            <span>Repo</span>
                          </a>
                        )}

                        {tool.issueUrl && (
                          <a
                            href={tool.issueUrl}
                            target="_blank"
                            rel="noreferrer noopener"
                            className="btn-secondary"
                            title="View Tracker / Backlog Issues"
                          >
                            <IssueIcon />
                            <span>Issues</span>
                          </a>
                        )}

                        <button
                          type="button"
                          className="btn-ping"
                          onClick={() => checkToolHealth(tool)}
                          title="Ping localhost service to check if running"
                          aria-label={`Ping ${tool.name}`}
                        >
                          <RefreshIcon />
                          <span>Ping</span>
                        </button>
                      </div>
                    </article>
                  )
                })}
              </div>
            </div>
          )
        })}
      </section>
    </div>
  )
}

export default BrickTracker
