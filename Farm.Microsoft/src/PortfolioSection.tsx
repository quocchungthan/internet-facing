import { useEffect, useState } from 'react'

interface Project { id: number; title: string; slug: string; summary: string; description?: string | null; demoUrl?: string | null; sourceUrl?: string | null }
interface Skill { id: number; name: string; category: string }
interface Experience { id: number; company: string; jobTitle: string; description?: string | null; startDate: string; endDate?: string | null; isCurrent: boolean; displayOrder: number }

interface PortfolioData {
  projects: Project[]
  skills: Skill[]
  experiences: Experience[]
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error(`${path} returned ${response.status}`)
  return (await response.json()) as T
}

function ExternalLink({ href, children }: { href: string; children: React.ReactNode }) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  )
}

function formatPeriod(start: string, end: string | null | undefined, current: boolean): string {
  const fmt = (value: string) => value.slice(0, 7)
  return `${fmt(start)} – ${current || !end ? 'present' : fmt(end)}`
}

export function PortfolioSection() {
  const [data, setData] = useState<PortfolioData | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    Promise.all([
      getJson<Project[]>('/api/portfolio/projects'),
      getJson<Skill[]>('/api/portfolio/skills'),
      getJson<Experience[]>('/api/portfolio/experiences'),
    ])
      .then(([projects, skills, experiences]) => {
        if (cancelled) return
        setData({
          projects,
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
          {experiences.map((item) => (
            <article key={item.id} className="ms-portfolio-item">
              <h4>
                {item.jobTitle} · {item.company}
              </h4>
              <p className="ms-portfolio-muted">{formatPeriod(item.startDate, item.endDate, item.isCurrent)}</p>
              {item.description && <p className="ms-portfolio-text">{item.description}</p>}
            </article>
          ))}
        </section>
      )}

      {projects.length > 0 && (
        <section className="ms-portfolio-section" aria-label="Projects">
          <h3>Projects</h3>
          {projects.map((project) => (
            <article key={project.id} className="ms-portfolio-item">
              <h4>{project.title}</h4>
              <p>{project.summary}</p>
              {project.description && <p className="ms-portfolio-text">{project.description}</p>}
              <p className="ms-portfolio-links">
                {project.sourceUrl && <ExternalLink href={project.sourceUrl}>Source</ExternalLink>}
                {project.demoUrl && <ExternalLink href={project.demoUrl}>Demo</ExternalLink>}
              </p>
            </article>
          ))}
        </section>
      )}

      {skillGroups.length > 0 && (
        <section className="ms-portfolio-section" aria-label="Skills">
          <h3>Skills</h3>
          {skillGroups.map(([category, items]) => (
            <p key={category}>
              <span className="ms-portfolio-muted">{category}: </span>
              {items.map((skill) => skill.name).join(', ')}
            </p>
          ))}
        </section>
      )}
    </div>
  )
}
