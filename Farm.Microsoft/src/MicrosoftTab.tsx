import { useEffect, useState, type ReactNode } from 'react'
import './themes/microsoft/microsoft.scss'
import { JsonView } from './JsonView'
import { formatPeriod, levelOf, plural, useSiteContent, type ApiProbe, type DayCell, type DisplayProject, type ToolStatus } from './siteContent'

const SECTIONS = [
  { id: 'api', label: 'API console' },
  { id: 'overview', label: 'Overview' },
  { id: 'experience', label: 'Experience' },
  { id: 'projects', label: 'Projects' },
  { id: 'skills', label: 'Skills' },
  { id: 'tools', label: 'Local tools' },
  { id: 'sources', label: 'Work sources' },
  { id: 'cases', label: 'Work items' },
]

const TOOL_STATUS_LABEL: Record<ToolStatus | 'unknown', string> = {
  checking: 'Checking',
  online: 'Online',
  offline: 'Offline',
  unknown: 'Not checked',
}

function SectionNav({ items }: { items: { id: string; label: string }[] }) {
  const [active, setActive] = useState(items[0].id)

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries.filter((entry) => entry.isIntersecting).sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)[0]
        if (visible) setActive(visible.target.id.replace('ms-', ''))
      },
      { rootMargin: '-15% 0px -70% 0px' }
    )
    items.forEach((item) => {
      const element = document.getElementById(`ms-${item.id}`)
      if (element) observer.observe(element)
    })
    return () => observer.disconnect()
  }, [items])

  return (
    <nav className="ms-nav" aria-label="Page sections">
      {items.map((item) => (
        <a
          key={item.id}
          href={`#ms-${item.id}`}
          aria-current={active === item.id ? 'location' : undefined}
          onClick={(event) => {
            event.preventDefault()
            document.getElementById(`ms-${item.id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
            setActive(item.id)
          }}
        >
          {item.label}
        </a>
      ))}
    </nav>
  )
}

function Panel({ id, title, hint, children }: { id: string; title: string; hint?: string; children: ReactNode }) {
  return (
    <section id={`ms-${id}`} className="ms-panel" aria-labelledby={`ms-${id}-title`}>
      <header className="ms-panel-head">
        <h3 id={`ms-${id}-title`}>{title}</h3>
        {hint && <p>{hint}</p>}
      </header>
      {children}
    </section>
  )
}

function HeatStrip({ days, label }: { days: DayCell[]; label: string }) {
  return (
    <span className="ms-heat" role="img" aria-label={label}>
      {days.map((day) => (
        <i key={day.date} data-level={levelOf(day.count)} title={`${day.date}: ${plural(day.count, 'visit')}`} />
      ))}
    </span>
  )
}

function ProjectCard({ project, pinned }: { project: DisplayProject; pinned?: boolean }) {
  const association = project.association
  const period = formatPeriod(association?.startDate, association?.endDate, false)
  const detail = [association?.role, association?.organization].filter(Boolean).join(' · ')
  return (
    <article className={`ms-card${pinned ? ' is-pinned' : ''}`}>
      <div className="ms-card-top">
        <h4>{project.title}</h4>
        {association?.isFeatured && <span className="ms-chip ms-chip-accent">Featured</span>}
      </div>
      {(period || detail) && (
        <p className="ms-card-meta">
          {period && <span>{period}</span>}
          {detail && <span>{detail}</span>}
        </p>
      )}
      <p>{project.summary}</p>
      {project.description && <p className="ms-card-text">{project.description}</p>}
      {(project.sourceUrl || project.demoUrl) && (
        <footer className="ms-card-actions">
          {project.demoUrl && <a className="ms-btn ms-btn-primary" href={project.demoUrl} target="_blank" rel="noopener noreferrer">Open demo</a>}
          {project.sourceUrl && <a className="ms-btn" href={project.sourceUrl} target="_blank" rel="noopener noreferrer">View source</a>}
        </footer>
      )}
    </article>
  )
}

function ApiBlock({ probe }: { probe: ApiProbe }) {
  const ok = probe.status !== null && probe.status >= 200 && probe.status < 300
  const warn = probe.status === 429 || (probe.rateLimitRemaining !== null && probe.rateLimitRemaining < 5)
  const statusClass = probe.loading ? 'status-loading' : ok ? 'status-success' : 'status-error'
  return (
    <section className="ms-get-block" aria-label={`API block for ${probe.endpoint}`}>
      <div className="ms-api-bar">
        <div className="ms-api-left">
          <span className="ms-method-badge">GET</span>
          <span className="ms-endpoint-path">{probe.endpoint}</span>
          {probe.status !== null && <span className={`ms-status-badge ${statusClass}`}>{probe.statusText}</span>}
          <div className="ms-metrics-group">
            {probe.totalRequests !== null && <span className="ms-metric-pill" title="Total requests served by Farm backend">Reqs: <b>{probe.totalRequests}</b></span>}
            {probe.uniqueClients !== null && <span className="ms-metric-pill" title="Unique clients tracked by Farm backend">Clients: <b>{probe.uniqueClients}</b></span>}
            {probe.rateLimitRemaining !== null && (
              <span className={`ms-metric-pill ${warn ? 'limit-warn' : 'limit-ok'}`} title="Requests remaining in current rate limit window">
                Rate limit: <b>{probe.rateLimitRemaining}/{probe.rateLimitLimit ?? 60} left</b>
                {probe.rateLimitReset !== null && <small>({probe.rateLimitReset}s reset)</small>}
              </span>
            )}
          </div>
        </div>
        <button type="button" className="ms-btn ms-btn-primary" onClick={probe.refresh} disabled={probe.loading} aria-label={`Send GET request to ${probe.endpoint}`}>
          {probe.loading ? 'Fetching...' : 'GET'}
        </button>
      </div>
      <div className="ms-code-block-wrapper">
        <pre tabIndex={0}>
          {probe.loading && !probe.rawJson ? '// Loading response data...' : probe.rawJson ? <JsonView text={probe.rawJson} /> : '// Click GET to invoke request'}
        </pre>
      </div>
      <div className="ms-block-footer">
        <span>{probe.title}</span>
        {probe.lastUpdated && <span>Updated: {probe.lastUpdated}</span>}
      </div>
    </section>
  )
}

export const MicrosoftTab = ({ analytics }: { analytics?: ReactNode }) => {
  const site = useSiteContent()
  const { activity } = site
  const sections = analytics ? [...SECTIONS, { id: 'usage', label: 'Usage & savings' }] : SECTIONS
  const featured = site.projects.filter((project) => project.association?.isFeatured)
  const others = site.projects.filter((project) => !project.association?.isFeatured)

  return (
    <div className="ms-view-container" aria-busy={site.loading}>
      <header className="ms-header">
        <div className="ms-title-group">
          <div className="ms-icon-tile" aria-hidden="true">⊞</div>
          <div>
            <h2>Elder Vibe Coder</h2>
            <p className="ms-subtitle">If you are seeking for a software developer to talk to, it&apos;s me here.</p>
          </div>
        </div>
      </header>

      <div className="ms-shell">
        <SectionNav items={sections} />

        <div className="ms-content">
          <Panel id="api" title="API console" hint="This portfolio is served by a live API. Send a request and inspect the response, rate limits and usage metrics.">
            <div className="ms-api-grid">{site.probes.map((probe) => <ApiBlock key={probe.endpoint} probe={probe} />)}</div>
          </Panel>

          <Panel id="overview" title="Overview" hint="Where things stand today.">
            <div className="ms-kpis">
              <article className="ms-kpi"><span>Day streak</span><strong>{activity.streak}</strong><small>{activity.todayDone ? 'Looked for work today' : 'Nothing logged today yet'}</small></article>
              <article className="ms-kpi"><span>Active days</span><strong>{activity.activeDays}</strong><small>Last 14 weeks</small></article>
              <article className="ms-kpi"><span>Best streak</span><strong>{activity.bestStreak}</strong><small>Consecutive days</small></article>
              <article className="ms-kpi"><span>Projects</span><strong>{site.projects.length}</strong><small>{plural(featured.length, 'featured project')}</small></article>
            </div>
            {site.portfolioFailed && <p className="ms-note" role="alert">Portfolio is unavailable right now.</p>}
          </Panel>

          <Panel id="experience" title="Experience" hint="Roles as ordered in the profile.">
            {site.loading ? <p className="ms-note">Loading portfolio…</p> : site.experiences.length === 0 ? <p className="ms-note">No experience listed yet.</p> : (
              <div className="ms-table-wrap">
                <table className="ms-table">
                  <thead><tr><th scope="col">Role</th><th scope="col">Company</th><th scope="col">Period</th><th scope="col">Details</th></tr></thead>
                  <tbody>
                    {site.experiences.map((item) => (
                      <tr key={item.id}>
                        <th scope="row">{item.jobTitle}</th>
                        <td>{item.company}</td>
                        <td className="ms-nowrap">{formatPeriod(item.startDate, item.endDate, item.isCurrent)}{item.isCurrent && <span className="ms-chip ms-chip-ok">Current</span>}</td>
                        <td className="ms-card-text">{item.description}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Panel>

          <Panel id="projects" title="Projects" hint="Featured work is pinned first.">
            {site.loading ? <p className="ms-note">Loading portfolio…</p> : site.projects.length === 0 ? <p className="ms-note">No projects listed yet.</p> : (
              <>
                {featured.length > 0 && (
                  <div className="ms-card-grid ms-card-grid-pinned" aria-label="Featured projects">
                    {featured.map((project) => <ProjectCard key={`${project.association?.profileId}:${project.id}`} project={project} pinned />)}
                  </div>
                )}
                {others.length > 0 && (
                  <div className="ms-card-grid">
                    {others.map((project) => <ProjectCard key={`${project.association?.profileId ?? 'p'}:${project.id}`} project={project} />)}
                  </div>
                )}
              </>
            )}
          </Panel>

          <Panel id="skills" title="Skills">
            <div className="ms-skill-groups">
              {site.skillGroups.map(([category, items]) => (
                <article key={category} className="ms-card">
                  <h4>{category}</h4>
                  <ul className="ms-chips">{items.map((skill) => <li key={skill.id} className="ms-chip">{skill.name}</li>)}</ul>
                </article>
              ))}
            </div>
            {!site.loading && site.skillGroups.length === 0 && <p className="ms-note">No skills listed yet.</p>}
          </Panel>

          <Panel id="tools" title="Local tools" hint="Services running on this machine.">
            <div className="ms-toolbar">
              <button type="button" className="ms-btn" onClick={site.checkAllTools} disabled={site.toolCount === 0}>Check all</button>
            </div>
            {!site.loading && site.toolCount === 0 && <p className="ms-note">No tools registered yet.</p>}
            {site.toolCount > 0 && (
              <div className="ms-table-wrap">
                <table className="ms-table">
                  <thead><tr><th scope="col">Tool</th><th scope="col">Category</th><th scope="col">Status</th><th scope="col"><span className="ms-sr">Actions</span></th></tr></thead>
                  <tbody>
                    {site.toolGroups.flatMap((group) => group.items.map((tool) => {
                      const status = site.toolStatuses[tool.id]
                      return (
                        <tr key={tool.id}>
                          <th scope="row">{tool.name}<small>{tool.description}</small></th>
                          <td>{group.label}</td>
                          <td><span className={`ms-status-badge tool-${status ?? 'unknown'}`}>{TOOL_STATUS_LABEL[status ?? 'unknown']}</span></td>
                          <td className="ms-row-actions">
                            <a className="ms-btn ms-btn-primary" href={tool.directUrl} target="_blank" rel="noreferrer noopener">Launch</a>
                            <button type="button" className="ms-btn" onClick={() => site.checkTool(tool)} disabled={status === 'checking'} aria-label={`Check whether ${tool.name} is running`}>Check</button>
                            {tool.repositoryUrl && <a className="ms-btn" href={tool.repositoryUrl} target="_blank" rel="noreferrer noopener" aria-label={`${tool.name} repository`}>Repo</a>}
                            {tool.issueUrl && <a className="ms-btn" href={tool.issueUrl} target="_blank" rel="noreferrer noopener" aria-label={`${tool.name} issues`}>Issues</a>}
                          </td>
                        </tr>
                      )
                    }))}
                  </tbody>
                </table>
              </div>
            )}
          </Panel>

          <Panel id="sources" title="Where do I usually find work" hint="The places I look for gigs and roles. Opening one logs today's visit.">
            {!site.loading && site.habits.length === 0 && <p className="ms-note">No sources configured.</p>}
            <ul className="ms-list">
              {site.habits.map(({ platform, days, visits }) => (
                <li key={platform.id} className="ms-list-row">
                  <span className="ms-avatar" aria-hidden="true">{platform.platform.slice(0, 1).toUpperCase()}</span>
                  <div className="ms-list-main"><strong>{platform.platform}</strong><span>{platform.description}</span></div>
                  <HeatStrip days={days.slice(-28)} label={`${platform.platform}: ${plural(visits, 'visit')} in the last 14 weeks`} />
                  <a className="ms-btn" href={platform.url} target="_blank" rel="noreferrer noopener" onClick={() => site.registerVisit(platform)} aria-label={`Open ${platform.platform}`}>Open</a>
                </li>
              ))}
            </ul>
          </Panel>

          <Panel id="cases" title="Work items" hint="Open and recent engineering items.">
            <div className="ms-table-wrap">
              <table className="ms-table">
                <thead><tr><th scope="col">ID</th><th scope="col">Title</th><th scope="col">Status</th><th scope="col">Owner</th></tr></thead>
                <tbody>
                  {site.cases.map((item) => (
                    <tr key={item.id}>
                      <td className="ms-nowrap">#{item.id}</td>
                      <th scope="row">{item.title}<small>{item.summary}</small></th>
                      <td><span className={`ms-status-badge case-${item.state}`}>{item.status}</span></td>
                      <td>{item.meta}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>

          {analytics && <Panel id="usage" title="Usage & savings">{analytics}</Panel>}
        </div>
      </div>
    </div>
  )
}

export default MicrosoftTab
