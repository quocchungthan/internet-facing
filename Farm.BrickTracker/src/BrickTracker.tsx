import { useCallback, useEffect, useMemo, useState, type CSSProperties } from 'react'
import { activeDays, currentStreak, doneToday, levelOf, longestStreak, lastDays, type ClickRow } from './activity'
import { CategoryIcon, ExternalIcon, FlameIcon, GitRepoIcon, IssueIcon, RefreshIcon } from './icons'
import './brick.scss'

export type { ClickRow } from './activity'

export interface PlatformBookmark {
  id: number
  platform: string
  url: string
  platformId: string
  description: string
  color: string
  displayOrder: number
}

export interface LocalhostTool {
  id: number
  name: string
  description: string
  category: 'SearchCollect' | 'ConvertTransformCompose' | 'ObserveWatching' | 'CalculatingAnalysing' | 'WriteExecute' | string
  directUrl: string
  repositoryUrl: string
  issueUrl?: string | null
  displayOrder: number
}

type ToolStatus = 'checking' | 'online' | 'offline'

const CATEGORIES: { key: string; label: string; description: string }[] = [
  { key: 'SearchCollect', label: 'Search & Collect', description: 'Discovery, extractors, scanners' },
  { key: 'ConvertTransformCompose', label: 'Convert & Compose', description: 'Translators, converters, composers' },
  { key: 'ObserveWatching', label: 'Observe & Watch', description: 'Telemetry, logs, health monitors' },
  { key: 'CalculatingAnalysing', label: 'Calculate & Analyze', description: 'Metrics, analyzers, scorers' },
    { key: 'WriteExecute', label: 'Write & Execute', description: 'Create or update documents and other content' },
  ]

const HISTORY_WEEKS = 14
const STATUS_LABEL: Record<ToolStatus | 'unknown', string> = {
  checking: 'Checking',
  online: 'Online',
  offline: 'Offline',
  unknown: 'Not checked',
}

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

const loadJson = <T,>(url: string) =>
  fetch(url).then((res) => (res.ok ? (res.json() as Promise<T>) : ([] as unknown as T)))

const todayLabel = () =>
  new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'short', day: 'numeric' })

export const BrickTracker = () => {
  const [platforms, setPlatforms] = useState<PlatformBookmark[]>([])
  const [clicks, setClicks] = useState<ClickRow[]>([])
  const [tools, setTools] = useState<LocalhostTool[]>([])
  const [loading, setLoading] = useState(true)
  const [failed, setFailed] = useState(false)
  const [toolStatuses, setToolStatuses] = useState<Record<number, ToolStatus>>({})

  useEffect(() => {
    Promise.all([
      loadJson<PlatformBookmark[]>('/api/feeds'),
      loadJson<ClickRow[]>('/api/feeds/clicks'),
      loadJson<LocalhostTool[]>('/api/tools'),
    ])
      .then(([feeds, rows, toolList]) => {
        setPlatforms(feeds)
        setClicks(rows)
        setTools(toolList)
      })
      .catch((err) => {
        console.error('Failed to load data for brick tracker:', err)
        setFailed(true)
      })
      .finally(() => setLoading(false))
  }, [])

  // Opening a source counts as today's brick for it.
  const registerVisit = (p: PlatformBookmark) => {
    const day = new Date().toISOString().slice(0, 10)
    setClicks((prev) => {
      const hit = prev.find((r) => r.platform === p.platform && r.day === day)
      return hit
        ? prev.map((r) => (r === hit ? { ...r, count: r.count + 1 } : r))
        : [...prev, { platform: p.platform, day, count: 1 }]
    })
    fetch(`/api/feeds/${encodeURIComponent(p.platform)}/click`, { method: 'POST', keepalive: true }).catch(() => {})
  }

  const checkToolHealth = useCallback(async (tool: LocalhostTool) => {
    setToolStatuses((prev) => ({ ...prev, [tool.id]: 'checking' }))
    const controller = new AbortController()
    const timeoutId = setTimeout(() => controller.abort(), 2500)
    try {
      // no-cors lets the page ping a localhost origin without being blocked by CORS
      await fetch(tool.directUrl, { method: 'HEAD', mode: 'no-cors', signal: controller.signal })
      setToolStatuses((prev) => ({ ...prev, [tool.id]: 'online' }))
    } catch {
      setToolStatuses((prev) => ({ ...prev, [tool.id]: 'offline' }))
    } finally {
      clearTimeout(timeoutId)
    }
  }, [])

  const historyDays = HISTORY_WEEKS * 7
  const overall = useMemo(() => lastDays(clicks, historyDays), [clicks, historyDays])
  const streak = currentStreak(overall)
  const todayDone = doneToday(overall)
  const recent = overall.slice(-15)

  const habits = useMemo(
    () =>
      platforms.map((p) => {
        const days = lastDays(clicks, historyDays, p.platform)
        return { p, days, streak: currentStreak(days) }
      }),
    [platforms, clicks, historyDays],
  )

  const toolGroups = useMemo(() => {
    const known = new Set(CATEGORIES.map((c) => c.key))
    const groups = CATEGORIES.map((c) => ({ ...c, items: tools.filter((t) => t.category === c.key) }))
    const other = tools.filter((t) => !known.has(t.category))
    if (other.length) groups.push({ key: 'Other', label: 'Other', description: 'Uncategorized tools', items: other })
    return groups.filter((g) => g.items.length > 0)
  }, [tools])

  return (
    <div className="bt" aria-busy={loading}>
      {failed && (
        <p className="bt-alert" role="alert">
          Could not load your dashboard data. Check your connection and reload the page.
        </p>
      )}

      <div className="bt-layout">
      <aside className="bt-rail">
      <section className="bt-hero" aria-labelledby="bt-hero-title">
        <div className="bt-hero-main">
          <p className="bt-kicker">{todayLabel()}</p>
          <h2 id="bt-hero-title">{todayDone ? 'Nice, you looked for work today.' : 'Did I look for work today?'}</h2>
          <p className="bt-streak">
            <span className="bt-streak-num">{streak}</span>
            <span className="bt-streak-label">
              <FlameIcon />
              {streak === 1 ? 'day in a row' : 'days in a row'}
            </span>
          </p>
        </div>

        <div className="bt-hero-side">
          <div
            className="bt-dots"
            role="img"
            aria-label={`Last 15 days: ${plural(recent.filter((d) => d.count > 0).length, 'day')} with activity`}
          >
            {recent.map((d) => (
              <span key={d.date} className="bt-dot-cell" data-level={levelOf(d.count)} title={`${d.date}: ${plural(d.count, 'brick')}`} />
            ))}
          </div>
          <dl className="bt-facts">
            <div>
              <dt>Active days</dt>
              <dd>{activeDays(overall)}</dd>
            </div>
            <div>
              <dt>Best streak</dt>
              <dd>{longestStreak(overall)}</dd>
            </div>
          </dl>
        </div>
      </section>

      <section className="bt-section bt-tools" aria-labelledby="bt-tools-title">
        <div className="bt-section-head bt-section-head-row">
          <div>
            <h3 id="bt-tools-title">My local tools</h3>
            <p>Services running on this machine.</p>
          </div>
          {tools.length > 0 && (
            <button type="button" className="bt-btn" onClick={() => tools.forEach(checkToolHealth)}>
              <RefreshIcon />
              Check all
            </button>
          )}
        </div>

        {!loading && tools.length === 0 && <p className="bt-empty">No tools registered yet.</p>}

        <div className="bt-groups">
        {toolGroups.map((group) => (
          <div key={group.key} className="bt-group" data-category={group.key}>
            <h4 className="bt-group-title">
              <span className="bt-group-badge">
                <CategoryIcon category={group.key} />
                {group.label}
              </span>
              <span className="bt-group-desc">{group.description}</span>
            </h4>

            <ul className="bt-tool-list">
              {group.items.map((tool) => {
                const status = toolStatuses[tool.id]
                return (
                  <li key={tool.id} className="bt-tool" data-status={status ?? 'unknown'}>
                    <span className="bt-tool-tile" aria-hidden="true">
                      <CategoryIcon category={group.key} />
                    </span>
                    <div className="bt-tool-body">
                      <h5>{tool.name}</h5>
                      <p>{tool.description}</p>
                    </div>
                    <span
                      className="bt-status"
                      role="status"
                      aria-label={STATUS_LABEL[status ?? 'unknown']}
                      title={STATUS_LABEL[status ?? 'unknown']}
                    >
                      <span className="bt-status-dot" />
                    </span>
                    <div className="bt-tool-actions">
                      <a className="bt-btn bt-btn-dark" href={tool.directUrl} target="_blank" rel="noreferrer noopener">
                        Launch
                        <ExternalIcon />
                      </a>
                      <button
                        type="button"
                        className="bt-icon-btn"
                        onClick={() => checkToolHealth(tool)}
                        disabled={status === 'checking'}
                        aria-label={`Check whether ${tool.name} is running`}
                        title="Check status"
                      >
                        <RefreshIcon />
                      </button>
                      {tool.repositoryUrl && (
                        <a
                          className="bt-icon-btn"
                          href={tool.repositoryUrl}
                          target="_blank"
                          rel="noreferrer noopener"
                          aria-label={`${tool.name} repository`}
                          title="Repository"
                        >
                          <GitRepoIcon />
                        </a>
                      )}
                      {tool.issueUrl && (
                        <a
                          className="bt-icon-btn"
                          href={tool.issueUrl}
                          target="_blank"
                          rel="noreferrer noopener"
                          aria-label={`${tool.name} issues`}
                          title="Issues"
                        >
                          <IssueIcon />
                        </a>
                      )}
                    </div>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
        </div>
      </section>
      </aside>

      <main className="bt-main">
      <section className="bt-section" aria-labelledby="bt-habits-title">
        <div className="bt-section-head">
          <h3 id="bt-habits-title">Where do I usually find work</h3>
          <p>The places I look for gigs and cases.</p>
        </div>
        {!loading && habits.length === 0 && <p className="bt-empty">No sources configured.</p>}
        <div className="bt-habit-grid">
          {habits.map(({ p, days }) => (
            <article key={p.id} className="bt-habit" style={{ '--habit': p.color } as CSSProperties}>
              <header className="bt-habit-head">
                <span className="bt-habit-tile" aria-hidden="true">
                  {p.platform.slice(0, 1).toUpperCase()}
                </span>
                <div className="bt-habit-title">
                  <h4>{p.platform}</h4>
                  <p>{p.description}</p>
                </div>
                <a
                  className="bt-icon-btn bt-open"
                  href={p.url}
                  target="_blank"
                  rel="noreferrer noopener"
                  aria-label={`Open ${p.platform}`}
                  onClick={() => registerVisit(p)}
                >
                  <ExternalIcon />
                </a>
              </header>

              <div className="bt-habit-grid-panel" aria-hidden="true">
                <div className="bt-squares" style={{ '--cols': HISTORY_WEEKS } as CSSProperties}>
                  {days.map((d) => (
                    <span key={d.date} className="bt-square" data-level={levelOf(d.count)} title={`${d.date}: ${plural(d.count, 'brick')}`} />
                  ))}
                </div>
              </div>
            </article>
          ))}
        </div>
      </section>
      </main>
      </div>
    </div>
  )
}

export default BrickTracker
