import { useEffect, useState, type ReactNode } from 'react'
import { UsageHistory } from './ConnectAi'
import { bytes, count, dateLabel, duration, money, summarize, type AudienceData, type DashboardData } from './dashboard'
import { signInUrl, type Session } from './session'
import './Dashboard.css'

function Metric({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <article className="analytics-metric"><h3>{label}</h3><strong>{value}</strong><p>{detail}</p></article>
}

function Table({ children, label }: { children: ReactNode; label: string }) {
  return <div className="analytics-table-wrap" role="region" aria-label={label} tabIndex={0}><table>{children}</table></div>
}

function Insights({ data }: { data: DashboardData }) {
  const totals = summarize(data.tasks)
  const daily = Array.from({ length: data.days }, (_, index) => {
    const date = new Date(`${data.from}T12:00:00Z`)
    date.setUTCDate(date.getUTCDate() + index)
    const key = date.toISOString().slice(0, 10)
    const tasks = data.tasks.filter(task => task.date === key)
    return { date: key, ...summarize(tasks) }
  })
  const maxCost = Math.max(0.01, ...daily.map(day => day.baseline))
  const audience = data.isOwner ? data.audience : null

  return (
    <>
      <aside className="analytics-notice" aria-label="Demo data disclosure">
        <strong>Demo data - not your actual usage or bill.</strong>
        <p>Cost, tokens, task times and task traffic below are fictional examples.
          Live MCP bandwidth is shown separately. Demo savings compare a fictional reference cost with AI cost plus a sample Eldervibe fee.</p>
      </aside>

      <div className="analytics-metrics">
        <Metric label="AI spend" value={money(totals.cost)} detail={`${money(totals.fees)} additional demo Eldervibe fees`} />
        <Metric label="Estimated net savings" value={money(totals.savings)} detail={`${money(totals.baseline)} fictional baseline minus AI spend and fees`} />
        <Metric label="Input / output tokens" value={`${count(totals.inputTokens)} / ${count(totals.outputTokens)}`} detail="Model tokens, not network bytes" />
        <Metric label="Average time per task" value={duration(totals.averageSeconds)} detail={`${duration(totals.totalSeconds)} total elapsed task time; not human time saved`} />
        <Metric label="Input / output traffic" value={`${bytes(totals.inputBytes)} / ${bytes(totals.outputBytes)}`} detail="Demo task payload sizes; not connection speed" />
        <Metric label="Completed tasks" value={count(data.tasks.length)} detail={`${money(totals.costPerTask)} per task including demo fees`} />
      </div>

      <section className="analytics-section" aria-labelledby="spend-title">
        <div className="analytics-heading">
          <div><h2 id="spend-title">Spend over time</h2><p>Demo USD, {dateLabel(data.from)} - {dateLabel(data.through)} (UTC)</p></div>
          <span className="analytics-badge">Demo</span>
        </div>
        <p className="analytics-legend">Solid: AI spend + fees. Outline: fictional reference cost.</p>
        <div className="analytics-bars" role="img" aria-label={`Demo daily cost comparison. Total AI spend and fees ${money(totals.cost + totals.fees)}; reference ${money(totals.baseline)}. Exact values are in the table below.`}>
          {daily.map(day => (
            <div key={day.date} className="analytics-bar-column">
              <div className="analytics-baseline" style={{ height: `${day.baseline / maxCost * 100}%` }}>
                <div className="analytics-spend" style={{ height: `${day.baseline ? (day.cost + day.fees) / day.baseline * 100 : 0}%` }} />
              </div>
            </div>
          ))}
        </div>
        <div className="analytics-chart-dates"><span>{dateLabel(data.from)}</span><span>{dateLabel(data.through)}</span></div>
        <details>
          <summary>Daily comparison data</summary>
          <Table label="Daily demo spending">
            <caption>Daily demo spending in USD</caption>
            <thead><tr><th scope="col">Date (UTC)</th><th scope="col">AI spend + fees</th><th scope="col">Reference cost</th><th scope="col">Net savings</th></tr></thead>
            <tbody>{daily.map(day => <tr key={day.date}><th scope="row">{day.date}</th><td>{money(day.cost + day.fees)}</td><td>{money(day.baseline)}</td><td>{money(day.savings)}</td></tr>)}</tbody>
          </Table>
        </details>
      </section>

      <section className="analytics-section" aria-labelledby="tasks-title">
        <div className="analytics-heading"><div><h2 id="tasks-title">Per-task breakdown</h2><p>Latest 10 demo tasks in the selected period. Summary cards include all {data.tasks.length} tasks.</p></div><span className="analytics-badge">Demo</span></div>
        <Table label="Demo task usage">
          <caption>Demo task usage - input / output values are shown separately</caption>
          <thead><tr><th scope="col">Task / model</th><th scope="col">Date (UTC)</th><th scope="col">Cost + fee</th><th scope="col">Tokens in / out</th><th scope="col">Elapsed time</th><th scope="col">Bytes in / out</th></tr></thead>
          <tbody>{data.tasks.slice(0, 10).map(task => (
            <tr key={task.id}>
              <th scope="row">{task.name}<small>{task.model}</small></th><td>{dateLabel(task.date)}</td>
              <td>{money(task.costUsd + task.feeUsd)}</td><td>{count(task.inputTokens)} / {count(task.outputTokens)}</td>
              <td>{duration(task.durationSeconds)}</td><td>{bytes(task.inputBytes)} / {bytes(task.outputBytes)}</td>
            </tr>
          ))}</tbody>
        </Table>
      </section>

      {audience && <Audience key={audience.url} url={audience.url} />}
    </>
  )
}

function Audience({ url }: { url: string | null }) {
  const [data, setData] = useState<AudienceData | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (!url) return
    const controller = new AbortController()
    fetch(url, { credentials: 'include', signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(response.status === 401
          ? 'Sign in through Fences again to view audience insights.'
          : 'Fences audience data is unavailable. Check that the identity service has been updated.')
        const result: AudienceData = await response.json()
        if (!controller.signal.aborted) setData(result)
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Could not load Fences audience data.')
      })
    return () => controller.abort()
  }, [url])
  return (
    <section className="analytics-section" aria-labelledby="audience-title">
      <div className="analytics-heading"><div><h2 id="audience-title">Audience insights</h2><p>Real application audience recorded by Fences. Owner-only visibility.</p></div><span className="analytics-badge">Fences</span></div>
      {!url ? <p role="alert">Fences sign-in is not configured.</p> : error ? <p role="alert">{error}</p> : !data ? <p role="status">Loading Fences audience...</p> : (
        <>
          <p>{data.coverage}</p>
          <div className="analytics-metrics">
            <Metric label="Observed users" value={count(data.totalUsers)} detail="Unique users of this app since tracking began" />
            <Metric label="New users" value={count(data.newUsers)} detail="First observed in the selected period" />
            <Metric label="Returning users" value={count(data.returningUsers)} detail="Observed before and during the selected period" />
          </div>
          {data.totalUsers === 0 && <p>No users recorded yet. Existing sessions are recorded on their next app authorization through Fences; older history is not backfilled.</p>}
          <Table label="User growth">
            <caption>Users first observed by day (UTC)</caption>
            <thead><tr><th scope="col">Date</th><th scope="col">New users</th></tr></thead>
            <tbody>{data.growth.map(day => <tr key={day.date}><th scope="row">{day.date}</th><td>{day.users}</td></tr>)}</tbody>
          </Table>
          <Table label="App users">
            <caption>Users observed by Fences for this application</caption>
            <thead><tr><th scope="col">User</th><th scope="col">First observed (UTC)</th><th scope="col">Last observed (UTC)</th><th scope="col">Observed days</th></tr></thead>
            <tbody>{data.users.map(user => <tr key={user.subject}><th scope="row">{user.username}</th><td>{user.firstSeen}</td><td>{user.lastSeen}</td><td>{user.observedDays}</td></tr>)}</tbody>
          </Table>
        </>
      )}
    </section>
  )
}

function Analytics({ session }: { session: Session }) {
  const [days, setDays] = useState(30)
  const [state, setState] = useState<{ data: DashboardData | null; error: string | null; loading: boolean }>({ data: null, error: null, loading: true })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    fetch(`/api/dashboard?days=${days}`, { credentials: 'same-origin', signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error(response.status === 401 ? 'Your session expired. Sign in again to load your dashboard.' : 'Could not load dashboard analytics.')
        const data: DashboardData = await response.json()
        if (!controller.signal.aborted) setState({ data, error: null, loading: false })
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setState({ data: null, error: error instanceof Error ? error.message : 'Could not load dashboard analytics.', loading: false })
      })
    return () => controller.abort()
  }, [days, attempt, session.subject])

  return (
    <>
      <div className="analytics-heading">
        <div><p className="analytics-eyebrow">Your workspace</p><h1>Usage &amp; savings</h1><p>Understand what each task uses, costs and takes to finish.</p></div>
        <label className="analytics-range">Time range
          <select value={days} onChange={event => {
            setState({ data: null, error: null, loading: true })
            setDays(Number(event.target.value))
          }}><option value={7}>7 days</option><option value={30}>30 days</option><option value={90}>90 days</option></select>
        </label>
      </div>
      {state.loading && <p role="status">Loading dashboard analytics...</p>}
      {state.error && <div role="alert"><p>{state.error}</p><button type="button" onClick={() => {
        setState({ data: null, error: null, loading: true })
        setAttempt(value => value + 1)
                }}>Retry analytics</button> <a href={signInUrl('/')}>Sign in again</a></div>}
      {state.data && <Insights data={state.data} />}
      <section aria-labelledby="live-bandwidth-title" className="analytics-live">
        <div className="analytics-heading"><div><h2 id="live-bandwidth-title">Live MCP bandwidth</h2><p>Measured traffic for your connected memory service only; not all AI usage.</p></div><span className="analytics-badge">Live source</span></div>
        <UsageHistory />
      </section>
    </>
  )
}

export function UsagePanel({ session, loading }: { session: Session; loading: boolean }) {
  if (loading || !session.isAuthenticated) return null
  return (
    <div className="analytics-dashboard">
      <Analytics key={session.subject} session={session} />
    </div>
  )
}
