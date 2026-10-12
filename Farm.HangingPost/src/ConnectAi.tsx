import { useCallback, useEffect, useRef, useState } from 'react'
import { signInUrl, type Session } from './session'

async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch {
    // Fallback for non-secure contexts or denied clipboard permission.
    const area = document.createElement('textarea')
    area.value = text
    area.style.position = 'fixed'
    area.style.opacity = '0'
    document.body.appendChild(area)
    area.select()
    const ok = document.execCommand('copy')
    area.remove()
    return ok
  }
}

function CodeBlock({ text }: { text: string }) {
  const [toast, setToast] = useState<string | null>(null)
  const timer = useRef<number>(0)

  useEffect(() => () => window.clearTimeout(timer.current), [])

  const copy = async () => {
    setToast((await copyText(text)) ? 'Copied to clipboard' : 'Could not copy')
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => setToast(null), 2000)
  }

  return (
    <div className="copy-block">
      <code className="connect-code">{text}</code>
      <button type="button" className="copy-btn" onClick={copy} aria-label="Copy to clipboard" title="Copy to clipboard">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <rect x="9" y="9" width="13" height="13" rx="2" />
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
        </svg>
      </button>
      {toast && (
        <div className="toast" role="status">
          {toast}
        </div>
      )}
    </div>
  )
}

interface ConnectAiProps {
  session: Session
  loading: boolean
}

interface AccessKeyInfo {
  id: string
  name: string
  preview: string
  createdAt: string
  lastUsedAt: string | null
}

interface UsageDay {
  date: string
  readBytes: number
  writeBytes: number
}

const CHART_WIDTH = 720
const CHART_HEIGHT = 260
const CHART_PADDING = { top: 16, right: 16, bottom: 40, left: 68 }

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB', 'TB']
  let value = bytes / 1024
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${value.toFixed(1)} ${units[unit]}`
}

function UsageHistory() {
  const [days, setDays] = useState(30)
  const [usage, setUsage] = useState<UsageDay[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    fetch(`/api/memory/usage?days=${days}`, { credentials: 'same-origin', signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error('Could not load bandwidth history.')
        setUsage((await response.json()) as UsageDay[])
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Could not load bandwidth history.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [days])

  const today = new Date().toISOString().slice(0, 10)
  const totals = usage.reduce(
    (sum, day) => ({ readBytes: sum.readBytes + day.readBytes, writeBytes: sum.writeBytes + day.writeBytes }),
    { readBytes: 0, writeBytes: 0 },
  )
  const byDate = new Map(usage.map((day) => [day.date, day]))
  const points = Array.from({ length: days }, (_, index) => {
    const date = new Date()
    date.setUTCDate(date.getUTCDate() - (days - 1 - index))
    const key = date.toISOString().slice(0, 10)
    return byDate.get(key) ?? { date: key, readBytes: 0, writeBytes: 0 }
  })
  const plotWidth = CHART_WIDTH - CHART_PADDING.left - CHART_PADDING.right
  const plotHeight = CHART_HEIGHT - CHART_PADDING.top - CHART_PADDING.bottom
  const maxBytes = Math.max(1, ...points.map((point) => Math.max(point.readBytes, point.writeBytes)))
  const tickSize = 10 ** Math.floor(Math.log10(maxBytes))
  const axisMax = Math.ceil(maxBytes / tickSize) * tickSize
  const x = (index: number) => CHART_PADDING.left + (points.length <= 1 ? plotWidth / 2 : (index / (points.length - 1)) * plotWidth)
  const y = (value: number) => CHART_PADDING.top + plotHeight - (value / axisMax) * plotHeight
  const pathFor = (field: 'readBytes' | 'writeBytes') =>
    points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${x(index)} ${y(point[field])}`).join(' ')
  const dateLabels = [0, Math.floor((points.length - 1) / 2), points.length - 1]
    .filter((index, position, values) => values.indexOf(index) === position)
  const chartDescription = `${days}-day total: ${formatBytes(totals.readBytes)} read from the server and ${formatBytes(totals.writeBytes)} written to the server.`

  return (
    <section className="connect-card usage-card" aria-labelledby="connect-usage">
      <div className="usage-heading">
        <div>
          <h2 id="connect-usage">MCP bandwidth history</h2>
          <p className="connect-hint">Daily message-body traffic. Only byte totals are stored—never prompts, responses, or tool details.</p>
        </div>
        <label className="usage-range">
          <span>Time range</span>
          <select
            value={days}
            onChange={(event) => {
              setLoading(true)
              setError(null)
              setDays(Number(event.target.value))
            }}
          >
            <option value={7}>7 days</option>
            <option value={30}>30 days</option>
            <option value={90}>90 days</option>
          </select>
        </label>
      </div>
      <div className="usage-totals" aria-label="Traffic totals">
        <p><span className="usage-dot usage-read-dot" aria-hidden="true" />Read <strong>{formatBytes(totals.readBytes)}</strong></p>
        <p><span className="usage-dot usage-write-dot" aria-hidden="true" />Write <strong>{formatBytes(totals.writeBytes)}</strong></p>
      </div>
      {loading ? (
        <p className="connect-hint" role="status">Loading bandwidth history…</p>
      ) : error ? (
        <p className="usage-error" role="alert">{error}</p>
      ) : (
        <div className="usage-chart-wrap">
          <svg
            className="usage-chart"
            viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
            role="img"
            aria-label={chartDescription}
          >
            <title>MCP read and write bandwidth by day</title>
            <desc>{chartDescription} Read is the solid line; write is dashed. The default range is 30 days.</desc>
            {[0, 1, 2, 3].map((step) => {
              const value = (axisMax / 3) * step
              const lineY = y(value)
              return (
                <g key={step}>
                  <line className="usage-gridline" x1={CHART_PADDING.left} x2={CHART_WIDTH - CHART_PADDING.right} y1={lineY} y2={lineY} />
                  <text className="usage-axis-label" x={CHART_PADDING.left - 8} y={lineY + 4} textAnchor="end">{formatBytes(value)}</text>
                </g>
              )
            })}
            <path className="usage-line usage-read-line" d={pathFor('readBytes')} />
            <path className="usage-line usage-write-line" d={pathFor('writeBytes')} />
            {dateLabels.map((index) => (
              <text key={index} className="usage-axis-label" x={x(index)} y={CHART_HEIGHT - 12} textAnchor={index === 0 ? 'start' : index === points.length - 1 ? 'end' : 'middle'}>
                {new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' }).format(new Date(`${points[index].date}T12:00:00Z`))}
              </text>
            ))}
          </svg>
          <div className="usage-legend" aria-hidden="true">
            <span><i className="usage-legend-line usage-read-swatch" />Read (server to you)</span>
            <span><i className="usage-legend-line usage-write-swatch" />Write (you to server)</span>
          </div>
          <p className="usage-range-note">{today && `Through ${new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' }).format(new Date(`${today}T12:00:00Z`))}.`} HTTP headers are not included.</p>
        </div>
      )}
    </section>
  )
}

function AccessKeys({ mcpUrl }: { mcpUrl: string }) {
  const [keys, setKeys] = useState<AccessKeyInfo[]>([])
  const [name, setName] = useState('Copilot CLI')
  const [secret, setSecret] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    fetch('/api/memory/keys', { credentials: 'same-origin' })
      .then((r) => (r.ok ? (r.json() as Promise<AccessKeyInfo[]>) : []))
      .then(setKeys)
      .catch(() => setKeys([]))
  }, [])

  useEffect(load, [load])

  const create = async () => {
    setError(null)
    const response = await fetch('/api/memory/keys', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name }),
    })
    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { error?: string } | null
      setError(body?.error ?? 'Could not create the key.')
      return
    }
    const created = (await response.json()) as { secret: string }
    setSecret(created.secret)
    load()
  }

  const revoke = async (id: string) => {
    await fetch(`/api/memory/keys/${id}`, { method: 'DELETE', credentials: 'same-origin' })
    load()
  }

  const config = JSON.stringify(
    { mcpServers: { eldervibe: { type: 'http', url: mcpUrl, headers: { Authorization: `Bearer ${secret ?? '<your key>'}` }, tools: ['*'] } } },
    null,
    2,
  )

  return (
    <>
      <p className="connect-hint">
        For clients that cannot open a browser sign-in (Copilot CLI over SSH, scripts, containers). The key acts as you, so
        keep it private; revoke it here at any time.
      </p>
      <p>
        <input value={name} onChange={(e) => setName(e.target.value)} maxLength={100} aria-label="Key name" />{' '}
        <button type="button" onClick={create}>
          Create key
        </button>
      </p>
      {error && <p role="alert">{error}</p>}
      {secret && (
        <>
          <p className="connect-hint">Copy this now; it is shown only once. Put it in <code>~/.copilot/mcp-config.json</code>:</p>
          <CodeBlock text={config} />
        </>
      )}
      <ul>
        {keys.map((k) => (
          <li key={k.id}>
            {k.name} <code>{k.preview}</code> · created {new Date(k.createdAt).toLocaleDateString()} ·{' '}
            {k.lastUsedAt ? `last used ${new Date(k.lastUsedAt).toLocaleDateString()}` : 'never used'}{' '}
            <button type="button" onClick={() => revoke(k.id)}>
              Revoke
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

const AGENT_SNIPPET = `Use the "eldervibe" MCP server as my shared memory.
- At the start of every session call get_briefing before other work.
- Before decisions on conventions, tooling or preferences, call search first.
- When I state a durable preference, rule or decision, or you learn a reusable project fact, call save_memory immediately (update_memory if one exists) and tell me in one line. Never save secrets.`

/**
 * Landing place for connecting an AI client (Copilot, Claude, ...) to the user's memory.
 * The browser login itself happens in the AI client: it calls the MCP endpoint, gets a 401, and
 * opens Fences. This page shows the endpoint to add and, later, the clients the user has granted.
 */
export function ConnectAi({ session, loading }: ConnectAiProps) {
  const mcpUrl = `${window.location.origin}/mcp`

  return (
    <article className="connect">
      <h1>Connect your AI</h1>
      <p className="connect-lead">
        Add Elder Vibe as an MCP server in your AI assistant. It opens a browser to sign in, then your assistant gets the
        same setup and decisions on every model and machine.
      </p>

      <section className="connect-card" aria-labelledby="connect-endpoint">
        <h2 id="connect-endpoint">MCP endpoint</h2>
        <CodeBlock text={mcpUrl} />
        <p className="connect-hint">Transport: Streamable HTTP. Authentication: browser sign-in (OAuth, PKCE).</p>
      </section>

      {session.isAuthenticated && <UsageHistory />}

      <section className="connect-card" aria-labelledby="connect-cli">
        <h2 id="connect-cli">Connected clients: access keys (Copilot CLI and headless clients)</h2>
        {session.isAuthenticated ? (
          <AccessKeys mcpUrl={mcpUrl} />
        ) : (
          <p className="connect-hint">Sign in below to create an access key.</p>
        )}
      </section>

      <section className="connect-card" aria-labelledby="connect-guidance">
        <h2 id="connect-guidance">Make your AI use it automatically</h2>
        <p className="connect-hint">
          The server already sends these instructions on connect and in <code>get_briefing</code>. Some clients ignore
          server instructions, so paste this into your global agent instructions too (for example
          <code> ~/.copilot/copilot-instructions.md</code> or <code>CLAUDE.md</code>).
        </p>
        <CodeBlock text={AGENT_SNIPPET} />
      </section>

      <section className="connect-card" aria-labelledby="connect-account">
        <h2 id="connect-account">Your account</h2>
        {loading ? (
          <p>Checking your session…</p>
        ) : session.isAuthenticated ? (
          <p>
            Signed in as <strong>{session.name ?? session.username}</strong>. Memories you save through your AI are
            private to this account.
          </p>
        ) : session.signInAvailable ? (
          <p>
            <a href={signInUrl('/connect')}>Sign in</a> so your AI assistants share one private memory. Signing in here only
            identifies you; each assistant still asks for its own approval the first time it connects.
          </p>
        ) : (
          <p>Sign-in is not available on this server yet.</p>
        )}
      </section>
    </article>
  )
}
