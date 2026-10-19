import { useEffect, useState, type ReactNode } from 'react'

export type PortfolioTheme = 'microsoft' | 'brick' | 'winchester'
interface Project { id: number; title: string; slug: string; summary: string; description?: string | null; demoUrl?: string | null; sourceUrl?: string | null }
interface ProfileProject { profileId: number; projectId: number; role?: string | null; organization?: string | null; startDate?: string | null; endDate?: string | null; isFeatured: boolean; displayOrder: number }
interface DisplayProject extends Project { association?: ProfileProject }
interface Skill { id: number; name: string; category: string }
interface Experience { id: number; company: string; jobTitle: string; description?: string | null; startDate: string; endDate?: string | null; isCurrent: boolean; displayOrder: number }

interface PortfolioData {
  projects: DisplayProject[]
  skills: Skill[]
  experiences: Experience[]
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error(`${path} returned ${response.status}`)
  return (await response.json()) as T
}

function ExternalLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  )
}

function formatPeriod(start: string | null | undefined, end: string | null | undefined, current: boolean): string | null {
  const fmt = (value: string) => value.slice(0, 7)
  if (!start) return end ? `Through ${fmt(end)}` : null
  return `${fmt(start)} – ${current || !end ? 'present' : fmt(end)}`
}

export function PortfolioSection({ theme = 'microsoft' }: { theme?: PortfolioTheme }) {
  const [data, setData] = useState<PortfolioData | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    Promise.all([
      getJson<Project[]>('/api/portfolio/projects'),
      getJson<ProfileProject[]>('/api/portfolio/profile-projects'),
      getJson<Skill[]>('/api/portfolio/skills'),
      getJson<Experience[]>('/api/portfolio/experiences'),
    ])
      .then(([projects, profileProjects, skills, experiences]) => {
        if (cancelled) return
        const associations = new Map<number, ProfileProject[]>()
        for (const association of profileProjects) {
          const items = associations.get(association.projectId) ?? []
          items.push(association)
          associations.set(association.projectId, items)
        }
        const displayProjects = projects.flatMap<DisplayProject>((project) => {
          const links = associations.get(project.id)
          return links?.length
            ? links.map((association) => ({ ...project, association }))
            : [{ ...project }]
        })
        displayProjects.sort((a, b) => {
          const profileOrder = (a.association?.profileId ?? Number.MAX_SAFE_INTEGER)
            - (b.association?.profileId ?? Number.MAX_SAFE_INTEGER)
          if (profileOrder !== 0) return profileOrder
          const displayOrder = (a.association?.displayOrder ?? Number.MAX_SAFE_INTEGER)
            - (b.association?.displayOrder ?? Number.MAX_SAFE_INTEGER)
          return displayOrder || a.id - b.id
        })
        setData({
          projects: displayProjects,
          skills,
          experiences: [...experiences].sort((a, b) => a.displayOrder - b.displayOrder),
        })
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err))
      })
    return () => {
      cancelled = true
    }
  }, [])

  if (error) return <p className="ms-portfolio-note">Portfolio is unavailable right now ({error}).</p>
  if (!data) return <p className="ms-portfolio-note">Loading portfolio…</p>

  const { projects, skills, experiences } = data
  const skillGroups = Object.entries(
    skills.reduce<Record<string, Skill[]>>((groups, skill) => {
      ;(groups[skill.category] ??= []).push(skill)
      return groups
    }, {})
  )
  return (
    <div className="ms-portfolio">
      {experiences.length > 0 && (
        <section className="ms-portfolio-section" aria-label="Experience">
          <h3>Experience</h3>
          <div className={`ms-experience-grid ms-experience-grid--${theme}`}>
            {experiences.map((item) => (
              <article key={item.id} className={`ms-experience-card${theme === 'winchester' ? ' case-file' : ''}`}>
                <h4>
                  {item.jobTitle} · {item.company}
                </h4>
                <p className="ms-portfolio-muted">{formatPeriod(item.startDate, item.endDate, item.isCurrent)}</p>
                {item.description && <p className="ms-portfolio-text">{item.description}</p>}
              </article>
            ))}
          </div>
        </section>
      )}

      {projects.length > 0 && (
        <section className="ms-portfolio-section" aria-label="Projects">
          <h3>Projects</h3>
          <div className={`ms-project-grid ms-project-grid--${theme}`}>
            {projects.map((project) => {
              const association = project.association
              const period = formatPeriod(association?.startDate, association?.endDate, false)
              const key = association ? `${association.profileId}:${project.id}` : `project:${project.id}`
              const cardClass = [
                'ms-project-card',
                theme === 'winchester' ? 'case-file' : '',
                association?.isFeatured ? 'is-featured' : '',
              ].filter(Boolean).join(' ')
              return (
                <article key={key} className={cardClass}>
                  {association?.isFeatured && (
                    <span className={`ms-portfolio-featured${theme === 'winchester' ? ' case-status' : ''}`} data-state="featured">
                      Featured
                    </span>
                  )}
                  <h4>{project.title}</h4>
                  {period && <p className="ms-portfolio-period">{period}</p>}
                  {(association?.role || association?.organization) && (
                    <p className="ms-portfolio-meta">
                      {[association.role, association.organization].filter(Boolean).join(' · ')}
                    </p>
                  )}
                  <p>{project.summary}</p>
                  {project.description && <p className="ms-portfolio-text">{project.description}</p>}
                  {(project.sourceUrl || project.demoUrl) && (
                    <footer className="ms-portfolio-links">
                      {project.sourceUrl && <ExternalLink href={project.sourceUrl}>Source</ExternalLink>}
                      {project.demoUrl && <ExternalLink href={project.demoUrl}>Demo</ExternalLink>}
                    </footer>
                  )}
                </article>
              )
            })}
          </div>
        </section>
      )}

      {skillGroups.length > 0 && (
        <section className="ms-portfolio-section" aria-label="Skills">
          <h3>Skills</h3>
          <div className="ms-skill-groups">
            {skillGroups.map(([category, items]) => (
              <article key={category} className="ms-skill-group">
                <h4>{category}</h4>
                <ul>{items.map((skill) => <li key={skill.id}>{skill.name}</li>)}</ul>
              </article>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
