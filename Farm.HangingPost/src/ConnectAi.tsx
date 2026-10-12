import { useCallback, useEffect, useState } from 'react'
import { signInUrl, type Session } from './session'

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
          <code className="connect-code" style={{ whiteSpace: 'pre-wrap', display: 'block' }}>{config}</code>
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
        <code className="connect-code">{mcpUrl}</code>
        <p className="connect-hint">Transport: Streamable HTTP. Authentication: browser sign-in (OAuth, PKCE).</p>
      </section>

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
        <code className="connect-code" style={{ whiteSpace: 'pre-wrap', display: 'block' }}>{AGENT_SNIPPET}</code>
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
