import './themes/winchester/winchester.scss'

export const WinchesterView = () => {
  return (
    <div className="winchester-container">
      <header className="winchester-header">
        <span className="journal-badge">Hunter's Journal &bull; Men of Letters Archive</span>
        <h1>Winchester Case Log</h1>
        <p>"Saving people, hunting things, the family business."</p>
      </header>

      <div className="winchester-case-board">
        <article className="case-card">
          <div className="case-status">Solved &bull; Salt &amp; Burn</div>
          <h3>Case #104: The Poltergeist in the Server Room</h3>
          <p>
            Anomalous network spikes traced to an ungrounded iron pipe and legacy cron job. Salted the rack, burned the ghost script.
          </p>
          <div className="case-meta">Location: Lebanon, Kansas &bull; Lead: Dean</div>
        </article>

        <article className="case-card">
          <div className="case-status">Open &bull; Investigation</div>
          <h3>Case #105: The Shape-shifter in CI/CD</h3>
          <p>
            Build artifacts changing checksums between runners. Testing silver bullets and verifying git SHA commits.
          </p>
          <div className="case-meta">Location: Bunker Terminal 4 &bull; Lead: Sam</div>
        </article>

        <article className="case-card">
          <div className="case-status">Pending &bull; Surveillance</div>
          <h3>Case #106: Crossroads Contract at Port 4554</h3>
          <p>
            PostgreSQL instance deployed on custom port 4554 with seed data locked in EF Core. No demon deals found, purely clean vibe coding.
          </p>
          <div className="case-meta">Location: Internet-facing Farm &bull; Lead: Elder Vibe Coder</div>
        </article>
      </div>
    </div>
  )
}

export default WinchesterView
