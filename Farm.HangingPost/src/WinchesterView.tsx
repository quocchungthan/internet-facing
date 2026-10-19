import type { ReactNode } from 'react'
import { JsonView } from './JsonView'
import { formatPeriod, levelOf, plural, useSiteContent, type ApiProbe, type CaseFile, type DayCell, type DisplayProject } from './siteContent'
import './themes/winchester/winchester.scss'

const CHAPTERS = [
  { id: 'journal', title: 'Journal entries', note: 'Experience' },
  { id: 'files', title: 'Case files', note: 'Projects' },
  { id: 'lore', title: 'Lore', note: 'Skills' },
  { id: 'armory', title: 'The armory', note: 'Local tools' },
  { id: 'informants', title: 'Informants', note: 'Where work comes from' },
  { id: 'radio', title: 'Radio transmissions', note: 'API console' },
  { id: 'board', title: 'The board', note: 'Open cases' },
]

const ROMAN = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII']

function Seal() {
  return (
    <svg className="winchester-seal" viewBox="0 0 200 200" fill="none" stroke="currentColor" strokeWidth="3" aria-hidden="true">
      <circle cx="100" cy="100" r="94" />
      <circle cx="100" cy="100" r="80" strokeWidth="1.5" />
      <polygon points="100,22 144,157 30,74 170,74 56,157" strokeLinejoin="round" />
    </svg>
  )
}

function Chapter({ index, id, title, children }: { index: number; id: string; title: string; children: ReactNode }) {
  return (
    <section id={`wn-${id}`} className="wn-chapter" aria-labelledby={`wn-${id}-title`}>
      <header>
        <span className="wn-numeral" aria-hidden="true">{ROMAN[index]}</span>
        <h2 id={`wn-${id}-title`}>{title}</h2>
      </header>
      {children}
    </section>
  )
}

function Tally({ days, label }: { days: DayCell[]; label: string }) {
  return (
    <span className="wn-tally" role="img" aria-label={label}>
      {days.map((day) => <i key={day.date} data-level={levelOf(day.count)} title={`${day.date}: ${plural(day.count, 'visit')}`} />)}
    </span>
  )
}

function CaseFolder({ project }: { project: DisplayProject }) {
  const association = project.association
  const opened = association?.startDate?.slice(0, 7)
  const closed = association?.endDate?.slice(0, 7)
  const detail = [association?.role, association?.organization].filter(Boolean).join(' · ')
  return (
    <article className="case-file" data-featured={association?.isFeatured ? 'true' : undefined}>
      {association?.isFeatured && <span className="case-status" data-state="featured">Priority case</span>}
      <h3>{project.title}</h3>
      {(opened || detail) && (
        <dl className="wn-docket">
          {opened && <div><dt>Opened</dt><dd>{opened}</dd></div>}
          {opened && <div><dt>Status</dt><dd>{closed ? `Closed ${closed}` : 'Still hunting'}</dd></div>}
          {detail && <div><dt>Assigned</dt><dd>{detail}</dd></div>}
        </dl>
      )}
      <p>{project.summary}</p>
      {project.description && <p className="wn-notes">{project.description}</p>}
      {(project.sourceUrl || project.demoUrl) && (
        <footer>
          Evidence:
          {project.sourceUrl && <a href={project.sourceUrl} target="_blank" rel="noopener noreferrer">Source</a>}
          {project.demoUrl && <a href={project.demoUrl} target="_blank" rel="noopener noreferrer">Demo</a>}
        </footer>
      )}
    </article>
  )
}

function Transmission({ probe }: { probe: ApiProbe }) {
  return (
    <article className="wn-transmission">
      <header>
        <span><strong>{probe.title}</strong> · <code>{probe.endpoint}</code></span>
        <button type="button" onClick={probe.refresh} disabled={probe.loading} aria-label={`Send GET request to ${probe.endpoint}`}>
          {probe.loading ? 'Listening…' : 'Radio check'}
        </button>
      </header>
      <p className="wn-signal">
        {probe.statusText || 'Static…'}
        {probe.totalRequests !== null && ` · ${probe.totalRequests} requests`}
        {probe.uniqueClients !== null && ` · ${probe.uniqueClients} clients`}
        {probe.rateLimitRemaining !== null && ` · ${probe.rateLimitRemaining}/${probe.rateLimitLimit ?? 60} bars`}
      </p>
      <details>
        <summary>Read the transcript</summary>
        <pre tabIndex={0}>{probe.rawJson ? <JsonView text={probe.rawJson} /> : '-- no signal --'}</pre>
      </details>
      {probe.lastUpdated && <small>Logged {probe.lastUpdated}</small>}
    </article>
  )
}

function BoardCase({ item }: { item: CaseFile }) {
  return (
    <article className="case-file">
      <span className="case-status" data-state={item.state}>{item.status}</span>
      <h3>Case #{item.id}: {item.title}</h3>
      <p>{item.summary}</p>
      <footer>{item.meta}</footer>
    </article>
  )
}

export const WinchesterView = ({ analytics }: { analytics?: ReactNode }) => {
  const site = useSiteContent()
  const { activity } = site
  const chapters = analytics ? [...CHAPTERS, { id: 'ledger', title: 'The ledger', note: 'Usage & savings' }] : CHAPTERS

  return (
    <div className="winchester" aria-busy={site.loading}>
      <header className="winchester-header">
        <Seal />
        <div>
          <span className="journal-badge">Hunter&apos;s Journal &middot; Men of Letters Archive</span>
          <h1>Saving people, hunting things</h1>
          <p>The family business: open case files, lore and leads. Kept by Elder Vibe Coder. If you are seeking for a software developer to talk to, it&apos;s me here.</p>
        </div>
      </header>

      <nav className="wn-contents" aria-label="Journal contents">
        <h2>Contents</h2>
        <ol>
          {chapters.map((chapter, index) => (
            <li key={chapter.id}>
              <a
                href={`#wn-${chapter.id}`}
                onClick={(event) => {
                  event.preventDefault()
                  document.getElementById(`wn-${chapter.id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
                }}
              >
                <span>{ROMAN[index]}.</span> {chapter.title}<em>{chapter.note}</em>
              </a>
            </li>
          ))}
        </ol>
        <p className="wn-log">
          {activity.todayDone ? 'On the road today.' : 'No leads chased today.'} {plural(activity.streak, 'day')} in a row · best run {activity.bestStreak} · {plural(activity.activeDays, 'active day')}.
        </p>
      </nav>

      {site.portfolioFailed && <p className="wn-note" role="alert">The archive is unreachable right now.</p>}

      <Chapter index={0} id="journal" title="Journal entries">
        {!site.loading && site.experiences.length === 0 && <p className="wn-note">No entries yet.</p>}
        <ol className="wn-entries">
          {site.experiences.map((item) => (
            <li key={item.id}>
              <time className="wn-date">{formatPeriod(item.startDate, item.endDate, item.isCurrent)}</time>
              <div>
                <h3>{item.jobTitle}</h3>
                <p className="wn-where">{item.company}{item.isCurrent && ' · still on this job'}</p>
                {item.description && <p className="wn-notes">{item.description}</p>}
              </div>
            </li>
          ))}
        </ol>
      </Chapter>

      <Chapter index={1} id="files" title="Case files">
        {!site.loading && site.projects.length === 0 && <p className="wn-note">No case files yet.</p>}
        <div className="winchester-board">
          {site.projects.map((project) => <CaseFolder key={`${project.association?.profileId ?? 'p'}:${project.id}`} project={project} />)}
        </div>
      </Chapter>

      <Chapter index={2} id="lore" title="Lore">
        <dl className="wn-lore">
          {site.skillGroups.map(([category, items]) => (
            <div key={category}>
              <dt>{category}</dt>
              <dd>{items.map((skill) => skill.name).join(' · ')}</dd>
            </div>
          ))}
        </dl>
        {!site.loading && site.skillGroups.length === 0 && <p className="wn-note">The lore is still being written.</p>}
      </Chapter>

      <Chapter index={3} id="armory" title="The armory">
        <div className="wn-toolbar">
          <p className="wn-note">Weapons kept on this machine.</p>
          {site.toolCount > 0 && <button type="button" className="wn-btn" onClick={site.checkAllTools}>Inspect all</button>}
        </div>
        {!site.loading && site.toolCount === 0 && <p className="wn-note">The armory is empty.</p>}
        {site.toolGroups.map((group) => (
          <div key={group.key} className="wn-shelf">
            <h3>{group.label} <small>{group.description}</small></h3>
            <ul>
              {group.items.map((tool) => {
                const status = site.toolStatuses[tool.id]
                const label = status === 'online' ? 'Loaded' : status === 'offline' ? 'Dormant' : status === 'checking' ? 'Inspecting' : 'Unchecked'
                return (
                  <li key={tool.id} data-status={status ?? 'unknown'}>
                    <span className="wn-box" role="status" aria-label={label} title={label}>{status === 'online' ? '✓' : status === 'offline' ? '✗' : ''}</span>
                    <div><strong>{tool.name}</strong><span>{tool.description}</span></div>
                    <span className="wn-links">
                      <a href={tool.directUrl} target="_blank" rel="noreferrer noopener">Launch</a>
                      <button type="button" onClick={() => site.checkTool(tool)} disabled={status === 'checking'} aria-label={`Check whether ${tool.name} is running`}>Inspect</button>
                      {tool.repositoryUrl && <a href={tool.repositoryUrl} target="_blank" rel="noreferrer noopener" aria-label={`${tool.name} repository`}>Repo</a>}
                      {tool.issueUrl && <a href={tool.issueUrl} target="_blank" rel="noreferrer noopener" aria-label={`${tool.name} issues`}>Issues</a>}
                    </span>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </Chapter>

      <Chapter index={4} id="informants" title="Informants">
        <p className="wn-note">Where the jobs come from. Each mark is a day I called on them.</p>
        {!site.loading && site.habits.length === 0 && <p className="wn-note">No informants on file.</p>}
        <ul className="wn-informants">
          {site.habits.map(({ platform, days, visits }) => (
            <li key={platform.id}>
              <div><strong>{platform.platform}</strong><span>{platform.description}</span></div>
              <Tally days={days.slice(-42)} label={`${platform.platform}: ${plural(visits, 'visit')} in the last 14 weeks`} />
              <a href={platform.url} target="_blank" rel="noreferrer noopener" onClick={() => site.registerVisit(platform)} aria-label={`Open ${platform.platform}`}>Call on</a>
            </li>
          ))}
        </ul>
      </Chapter>

      <Chapter index={5} id="radio" title="Radio transmissions">
        <div className="wn-radio">{site.probes.map((probe) => <Transmission key={probe.endpoint} probe={probe} />)}</div>
      </Chapter>

      <Chapter index={6} id="board" title="The board">
        <div className="winchester-board">{site.cases.map((item) => <BoardCase key={item.id} item={item} />)}</div>
      </Chapter>

      {analytics && <Chapter index={7} id="ledger" title="The ledger"><div className="wn-ledger">{analytics}</div></Chapter>}
    </div>
  )
}

export default WinchesterView
