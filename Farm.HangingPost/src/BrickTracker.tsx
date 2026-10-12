import { useEffect, useState, useMemo } from 'react'
import './themes/brick/brick.scss'

interface PlatformBookmark {
  id: number
  platform: string
  url: string
  platformId: string
  description: string
  color: string
  displayOrder: number
}

interface ClickRow {
  platform: string
  day: string
  count: number
}

const dayKey = (d: Date) => d.toISOString().slice(0, 10)

// Daily click counts for the last `daysCount` UTC days (oldest first).
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

export const BrickTracker = () => {
  const [platforms, setPlatforms] = useState<PlatformBookmark[]>([])
  const [selectedPlatform, setSelectedPlatform] = useState<string>('all')
  const [clicks, setClicks] = useState<ClickRow[]>([])
  const [loading, setLoading] = useState(true)

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

  useEffect(() => {
    Promise.all([
      fetch('/api/feeds').then((res) => res.json() as Promise<PlatformBookmark[]>),
      fetch('/api/feeds/clicks').then((res) => (res.ok ? (res.json() as Promise<ClickRow[]>) : [])),
    ])
      .then(([feeds, rows]) => {
        setPlatforms(feeds)
        setClicks(rows)
        setLoading(false)
      })
      .catch((err) => {
        console.error('Failed to load feeds for brick tracker:', err)
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

  return (
    <div className="brick-container">
      <header className="brick-header">
        <div>
          <div className="brick-title-group">
            <span className="brick-icon">🧱</span>
            <h2>Platform Habit Tracker &amp; Feeds</h2>
          </div>
          <p className="brick-subtitle">
            Link clicks and gig feeds across active platforms.
          </p>
        </div>
      </header>

      {/* Platform Filter Buttons */}
      <div className="brick-platform-selector">
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
      </div>

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
              Active weeks: <strong>{weeks}</strong>
            </span>
          </div>
        </div>

        <div className="tracker-grid-container">
          <div className="tracker-grid" role="grid">
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
                  title={`${date}: ${count} link clicks (${selectedPlatform})`}
                  role="gridcell"
                />
              )
            })}
          </div>
        </div>

        <div className="tracker-footer">
          <span>{loading ? 'Fetching platform feeds...' : `${platforms.length} active platforms monitored`}</span>
          <div className="legend-group">
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
      <section className="brick-feeds-section">
        <h3>Configured Platform Feeds &amp; Bookmarks</h3>
        <div className="brick-feeds-list">
          {platforms.map((p) => (
            <div
              key={p.id}
              className="brick-feed-row"
              style={{ '--card-color': p.color } as React.CSSProperties}
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
                  Visit {p.platform} &rarr;
                </a>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}

export default BrickTracker
