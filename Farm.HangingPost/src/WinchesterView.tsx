import './themes/winchester/winchester.scss'

type CaseState = 'solved' | 'open' | 'pending'

const cases: { id: number; state: CaseState; status: string; title: string; summary: string; meta: string }[] = [
  {
    id: 104,
    state: 'solved',
    status: 'Solved · Salt & Burn',
    title: 'The Poltergeist in the Server Room',
    summary:
      'Anomalous network spikes traced to an ungrounded iron pipe and a legacy cron job. Salted the rack, burned the ghost script.',
    meta: 'Lebanon, Kansas · Lead: Dean',
  },
  {
    id: 105,
    state: 'open',
    status: 'Open · Investigation',
    title: 'The Shape-shifter in CI/CD',
    summary:
      'Build artifacts changing checksums between runners. Testing silver bullets and verifying git SHA commits.',
    meta: 'Bunker Terminal 4 · Lead: Sam',
  },
  {
    id: 106,
    state: 'pending',
    status: 'Pending · Surveillance',
    title: 'Crossroads Contract at Port 4554',
    summary:
      'PostgreSQL instance deployed on custom port 4554 with seed data locked in EF Core. No demon deals found, purely clean vibe coding.',
    meta: 'Internet-facing Farm · Lead: Elder Vibe Coder',
  },
]

function Seal() {
  return (
    <svg className="winchester-seal" viewBox="0 0 200 200" fill="none" stroke="currentColor" strokeWidth="3" aria-hidden="true">
      <circle cx="100" cy="100" r="94" />
      <circle cx="100" cy="100" r="80" strokeWidth="1.5" />
      <polygon points="100,22 144,157 30,74 170,74 56,157" strokeLinejoin="round" />
    </svg>
  )
}

export const WinchesterView = () => (
  <div className="winchester">
    <header className="winchester-header">
      <Seal />
      <div>
        <span className="journal-badge">Hunter&apos;s Journal &middot; Men of Letters Archive</span>
        <h1>Saving people, hunting things</h1>
        <p>The family business: open case files, lore and leads.</p>
      </div>
    </header>

    <div className="winchester-board">
      {cases.map((c) => (
        <article key={c.id} className="case-file">
          <span className="case-status" data-state={c.state}>
            {c.status}
          </span>
          <h2>
            Case #{c.id}: {c.title}
          </h2>
          <p>{c.summary}</p>
          <footer>{c.meta}</footer>
        </article>
      ))}
    </div>
  </div>
)

export default WinchesterView
