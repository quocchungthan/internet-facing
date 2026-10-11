import { signInUrl, type Session } from './session'

interface ConnectAiProps {
  session: Session
  loading: boolean
}

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
            <a href={signInUrl('/connect')}>Sign in</a> to manage the AI clients connected to your account.
          </p>
        ) : (
          <p>Sign-in is not available on this server yet.</p>
        )}
      </section>

      <section className="connect-card" aria-labelledby="connect-clients">
        <h2 id="connect-clients">Connected clients</h2>
        <p className="connect-hint">Granted clients will be listed here so you can revoke them. Coming soon.</p>
      </section>
    </article>
  )
}
