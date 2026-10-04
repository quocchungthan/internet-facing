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

// Generate deterministic pseudo-activity for demo habit tracker visualization
function generateContributionData(daysCount: number, seedBase: number) {
  const data: number[] = []
  for (let i = 0; i < daysCount; i++) {
    const pseudoRandom = Math.sin((i + 1) * seedBase) * 10000
    const val = Math.floor(Math.abs(pseudoRandom)) % 5 // Levels 0, 1, 2, 3, 4
    data.push(val)
  }
  return data
}

export const BrickTracker = () => {
  const [platforms, setPlatforms] = useState<PlatformBookmark[]>([])
  const [selectedPlatform, setSelectedPlatform] = useState<string>('all')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/feeds')
      .then((res) => res.json())
      .then((data: PlatformBookmark[]) => {
        setPlatforms(data)
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

  const seed = useMemo(() => {
    return selectedPlatform.split('').reduce((acc, c) => acc + c.charCodeAt(0), 17)
  }, [selectedPlatform])

  const weeks = 24
  const daysCount = weeks * 7
  const contributions = useMemo(() => generateContributionData(daysCount, seed), [daysCount, seed])
  const totalContributions = useMemo(() => contributions.reduce((a, b) => a + b, 0), [contributions])

  return (
    <div className="brick-container">
      <header className="brick-header">
        <div>
          <div className="brick-title-group">
            <span className="brick-icon">🧱</span>
            <h2>Platform Habit Tracker &amp; Feeds</h2>
          </div>
          <p className="brick-subtitle">
            Contributions and gig feeds across active platforms.
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
              Total bricks: <strong>{totalContributions}</strong>
            </span>
            <span>
              Active weeks: <strong>{weeks}</strong>
            </span>
          </div>
        </div>

        <div className="tracker-grid-container">
          <div className="tracker-grid" role="grid">
            {contributions.map((level, idx) => {
              const bg =
                level === 0
                  ? 'var(--brick-empty, #ebedf0)'
                  : currentThemeColor + (level === 1 ? '44' : level === 2 ? '77' : level === 3 ? 'bb' : 'ff')

              return (
                <div
                  key={idx}
                  className="tracker-brick"
                  style={{ backgroundColor: bg }}
                  title={`Day ${idx + 1}: ${level * 3} activities (${selectedPlatform})`}
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
