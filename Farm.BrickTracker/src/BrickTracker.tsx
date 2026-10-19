import { useState, type CSSProperties, type ReactNode } from 'react'
import { CategoryIcon, ExternalIcon, FlameIcon, GitRepoIcon, IssueIcon, RefreshIcon } from './icons'
import { JsonView } from './JsonView'
import { formatPeriod, levelOf, plural, useSiteContent, type ApiProbe, type ToolStatus } from './siteContent'
import './brick.scss'

export type { ClickRow } from './activity'

const STATUS_LABEL: Record<ToolStatus | 'unknown', string> = {
  checking: 'Checking',
  online: 'Online',
  offline: 'Offline',
  unknown: 'Not checked',
}

const todayLabel = () => new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'short', day: 'numeric' })

// Bricks cycle through a fixed palette so every block reads as a different piece.
const hue = (index: number) => ({ '--piece': `var(--piece-${index % 5})` }) as CSSProperties

function Studs({ count = 4 }: { count?: number }) {
  return (
    <span className="bt-studs" aria-hidden="true">
      {Array.from({ length: count }, (_, i) => <i key={i} />)}
    </span>
  )
}

function Plate({ id, title, hint, action, children, wide }: { id: string; title: string; hint?: string; action?: ReactNode; children: ReactNode; wide?: boolean }) {
  return (
    <section id={`bt-${id}`} className={`bt-plate${wide ? ' bt-plate-wide' : ''}`} aria-labelledby={`bt-${id}-title`}>
      <header className="bt-plate-head">
        <div>
          <h3 id={`bt-${id}-title`}>{title}</h3>
          {hint && <p>{hint}</p>}
        </div>
        {action}
      </header>
      {children}
    </section>
  )
}

function Ping({ probe }: { probe: ApiProbe }) {
  const ok = probe.status !== null && probe.status >= 200 && probe.status < 300
  const state = probe.loading ? 'checking' : ok ? 'online' : probe.status === null ? 'unknown' : 'offline'
  return (
    <article className="bt-ping" data-status={state}>
      <header>
        <span className="bt-led" aria-hidden="true" />
        <code>GET {probe.endpoint}</code>
        <button type="button" className="bt-icon-btn" onClick={probe.refresh} disabled={probe.loading} aria-label={`Send GET request to ${probe.endpoint}`} title="Ping again">
          <RefreshIcon />
        </button>
      </header>
      <dl>
        <div><dt>Status</dt><dd>{probe.statusText || 'Waiting'}</dd></div>
        {probe.totalRequests !== null && <div><dt>Requests</dt><dd>{probe.totalRequests}</dd></div>}
        {probe.uniqueClients !== null && <div><dt>Clients</dt><dd>{probe.uniqueClients}</dd></div>}
        {probe.rateLimitRemaining !== null && <div><dt>Rate limit</dt><dd>{probe.rateLimitRemaining}/{probe.rateLimitLimit ?? 60}</dd></div>}
      </dl>
      <details>
        <summary>{probe.title} response</summary>
        <pre tabIndex={0}>{probe.rawJson ? <JsonView text={probe.rawJson} /> : '// No response yet'}</pre>
      </details>
      {probe.lastUpdated && <small>Updated {probe.lastUpdated}</small>}
    </article>
  )
}

export const BrickTracker = ({ analytics }: { analytics?: ReactNode }) => {
  const site = useSiteContent()
  const { activity } = site
  const [openProject, setOpenProject] = useState<string | null>(null)

  return (
    <div className="bt" aria-busy={site.loading}>
      <section className="bt-hero" aria-labelledby="bt-hero-title">
        <div className="bt-hero-main">
          <p className="bt-kicker">{todayLabel()} · Elder Vibe Coder</p>
          <h2 id="bt-hero-title">{activity.todayDone ? 'Nice, you looked for work today.' : 'Did I look for work today?'}</h2>
          <p className="bt-lead">If you are seeking for a software developer to talk to, it&apos;s me here.</p>
          <p className="bt-streak">
            <span className="bt-streak-num">{activity.streak}</span>
            <span className="bt-streak-label"><FlameIcon />{activity.streak === 1 ? 'day in a row' : 'days in a row'}</span>
          </p>
        </div>
        <div className="bt-hero-side">
          <div className="bt-dots" role="img" aria-label={`Last 15 days: ${plural(activity.recent.filter((d) => d.count > 0).length, 'day')} with activity`}>
            {activity.recent.map((d) => <span key={d.date} className="bt-dot-cell" data-level={levelOf(d.count)} title={`${d.date}: ${plural(d.count, 'brick')}`} />)}
          </div>
          <dl className="bt-facts">
            <div><dt>Active days</dt><dd>{activity.activeDays}</dd></div>
            <div><dt>Best streak</dt><dd>{activity.bestStreak}</dd></div>
            <div><dt>Projects</dt><dd>{site.projects.length}</dd></div>
          </dl>
        </div>
      </section>

      {site.portfolioFailed && <p className="bt-alert" role="alert">Portfolio is unavailable right now.</p>}

      <div className="bt-board">
        <Plate id="experience" title="Build steps" hint="Experience, one brick at a time." wide>
          {!site.loading && site.experiences.length === 0 && <p className="bt-empty">No experience listed yet.</p>}
          <ol className="bt-steps">
            {site.experiences.map((item, index) => (
              <li key={item.id} className="bt-step" style={hue(index)}>
                <span className="bt-step-no" aria-label={`Step ${index + 1}`}>{index + 1}</span>
                <div className="bt-brick">
                  <Studs />
                  <h4>{item.jobTitle}</h4>
                  <p className="bt-brick-meta">{item.company} · {formatPeriod(item.startDate, item.endDate, item.isCurrent)}</p>
                  {item.description && <p className="bt-brick-text">{item.description}</p>}
                </div>
              </li>
            ))}
          </ol>
        </Plate>

        <Plate id="projects" title="Projects" hint="Featured builds are the wide ones." wide>
          {!site.loading && site.projects.length === 0 && <p className="bt-empty">No projects listed yet.</p>}
          <div className="bt-projects">
            {site.projects.map((project, index) => {
              const association = project.association
              const key = `${association?.profileId ?? 'p'}:${project.id}`
              const period = formatPeriod(association?.startDate, association?.endDate, false)
              const detail = [association?.role, association?.organization].filter(Boolean).join(' · ')
              const expanded = openProject === key
              return (
                <article key={key} className={`bt-project${association?.isFeatured ? ' is-featured' : ''}`} style={hue(index)}>
                  <Studs count={association?.isFeatured ? 6 : 3} />
                  {association?.isFeatured && <span className="bt-tag">★ Featured</span>}
                  <h4>{project.title}</h4>
                  {period && <p className="bt-brick-meta">{period}</p>}
                  {detail && <p className="bt-brick-meta">{detail}</p>}
                  <p>{project.summary}</p>
                  {project.description && (
                    <>
                      <p className={`bt-brick-text${expanded ? '' : ' is-clamped'}`}>{project.description}</p>
                      <button type="button" className="bt-link-btn" aria-expanded={expanded} onClick={() => setOpenProject(expanded ? null : key)}>
                        {expanded ? 'Show less' : 'Read more'}
                      </button>
                    </>
                  )}
                  {(project.sourceUrl || project.demoUrl) && (
                    <footer>
                      {project.demoUrl && <a className="bt-btn bt-btn-dark" href={project.demoUrl} target="_blank" rel="noreferrer noopener">Demo<ExternalIcon /></a>}
                      {project.sourceUrl && <a className="bt-btn" href={project.sourceUrl} target="_blank" rel="noreferrer noopener">Source<GitRepoIcon /></a>}
                    </footer>
                  )}
                </article>
              )
            })}
          </div>
        </Plate>

        <Plate id="skills" title="Brick bins" hint="Skills sorted by category." wide>
          <div className="bt-bins">
            {site.skillGroups.map(([category, items], index) => (
              <article key={category} className="bt-bin" style={hue(index)}>
                <h4>{category}</h4>
                <ul>{items.map((skill) => <li key={skill.id}><i aria-hidden="true" />{skill.name}</li>)}</ul>
              </article>
            ))}
          </div>
          {!site.loading && site.skillGroups.length === 0 && <p className="bt-empty">No skills listed yet.</p>}
        </Plate>

        <Plate id="sources" title="Where do I usually find work" hint="Opening a source lays today's brick." wide>
          {!site.loading && site.habits.length === 0 && <p className="bt-empty">No sources configured.</p>}
          <div className="bt-habit-grid">
            {site.habits.map(({ platform, days, streak }) => (
              <article key={platform.id} className="bt-habit" style={{ '--habit': platform.color } as CSSProperties}>
                <header className="bt-habit-head">
                  <span className="bt-habit-tile" aria-hidden="true">{platform.platform.slice(0, 1).toUpperCase()}</span>
                  <div className="bt-habit-title"><h4>{platform.platform}</h4><p>{platform.description}</p></div>
                  <a className="bt-icon-btn bt-open" href={platform.url} target="_blank" rel="noreferrer noopener" aria-label={`Open ${platform.platform}`} onClick={() => site.registerVisit(platform)}>
                    <ExternalIcon />
                  </a>
                </header>
                <div className="bt-habit-grid-panel" aria-hidden="true">
                  <div className="bt-squares" style={{ '--cols': 14 } as CSSProperties}>
                    {days.map((d) => <span key={d.date} className="bt-square" data-level={levelOf(d.count)} title={`${d.date}: ${plural(d.count, 'brick')}`} />)}
                  </div>
                </div>
                <p className="bt-habit-streak">{plural(streak, 'day')} streak</p>
              </article>
            ))}
          </div>
        </Plate>

        <Plate
          id="tools"
          title="My local tools"
          hint="Services running on this machine."
          action={site.toolCount > 0 && <button type="button" className="bt-btn" onClick={site.checkAllTools}><RefreshIcon />Check all</button>}
          wide
        >
          {!site.loading && site.toolCount === 0 && <p className="bt-empty">No tools registered yet.</p>}
          <div className="bt-groups">
            {site.toolGroups.map((group) => (
              <div key={group.key} className="bt-group" data-category={group.key}>
                <h4 className="bt-group-title">
                  <span className="bt-group-badge"><CategoryIcon category={group.key} />{group.label}</span>
                  <span className="bt-group-desc">{group.description}</span>
                </h4>
                <ul className="bt-tool-grid">
                  {group.items.map((tool) => {
                    const status = site.toolStatuses[tool.id]
                    return (
                      <li key={tool.id} className="bt-tool" data-status={status ?? 'unknown'}>
                        <span className="bt-status" role="status" aria-label={STATUS_LABEL[status ?? 'unknown']} title={STATUS_LABEL[status ?? 'unknown']}><span className="bt-status-dot" /></span>
                        <span className="bt-tool-tile" aria-hidden="true"><CategoryIcon category={group.key} /></span>
                        <div className="bt-tool-body"><h5>{tool.name}</h5><p>{tool.description}</p></div>
                        <div className="bt-tool-actions">
                          <a className="bt-btn bt-btn-dark" href={tool.directUrl} target="_blank" rel="noreferrer noopener">Launch<ExternalIcon /></a>
                          <button type="button" className="bt-icon-btn" onClick={() => site.checkTool(tool)} disabled={status === 'checking'} aria-label={`Check whether ${tool.name} is running`} title="Check status"><RefreshIcon /></button>
                          {tool.repositoryUrl && <a className="bt-icon-btn" href={tool.repositoryUrl} target="_blank" rel="noreferrer noopener" aria-label={`${tool.name} repository`} title="Repository"><GitRepoIcon /></a>}
                          {tool.issueUrl && <a className="bt-icon-btn" href={tool.issueUrl} target="_blank" rel="noreferrer noopener" aria-label={`${tool.name} issues`} title="Issues"><IssueIcon /></a>}
                        </div>
                      </li>
                    )
                  })}
                </ul>
              </div>
            ))}
          </div>
        </Plate>

        <Plate id="pings" title="Pings" hint="Live checks against the Farm API.">
          <div className="bt-pings">{site.probes.map((probe) => <Ping key={probe.endpoint} probe={probe} />)}</div>
        </Plate>

        <Plate id="cases" title="Missions" hint="Case files from the family business.">
          <ul className="bt-missions">
            {site.cases.map((item, index) => (
              <li key={item.id} className="bt-mission" data-state={item.state} style={hue(index + 1)}>
                <span className="bt-sticker">{item.status}</span>
                <h4>#{item.id} · {item.title}</h4>
                <p>{item.summary}</p>
                <small>{item.meta}</small>
              </li>
            ))}
          </ul>
        </Plate>

        {analytics && <Plate id="usage" title="Usage & savings" wide>{analytics}</Plate>}
      </div>
    </div>
  )
}

export default BrickTracker
