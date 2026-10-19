import { useCallback, useEffect, useMemo, useState } from 'react'
import { activeDays, currentStreak, dayKey, doneToday, lastDays, levelOf, longestStreak, type ClickRow } from './activity'

export type { ClickRow, DayCell } from './activity'
export { levelOf }

export interface Project { id: number; title: string; slug: string; summary: string; description?: string | null; demoUrl?: string | null; sourceUrl?: string | null }
export interface ProfileProject { profileId: number; projectId: number; role?: string | null; organization?: string | null; startDate?: string | null; endDate?: string | null; isFeatured: boolean; displayOrder: number }
export interface DisplayProject extends Project { association?: ProfileProject }
export interface Skill { id: number; name: string; category: string }
export interface Experience { id: number; company: string; jobTitle: string; description?: string | null; startDate: string; endDate?: string | null; isCurrent: boolean; displayOrder: number }
export interface Gig { id: number; title: string; summary: string; category: string; bodyMarkdown?: string | null; problem?: string | null; outcome?: string | null }

/** Full Gig Markdown, or a body rebuilt from the legacy problem/outcome fields. */
export function gigMarkdown(gig: Gig): string {
  return gig.bodyMarkdown ?? [
    gig.problem ? `## Problem\n\n${gig.problem}` : '',
    gig.outcome ? `## Outcome\n\n${gig.outcome}` : '',
  ].filter(Boolean).join('\n\n')
}
export interface PlatformBookmark { id: number; platform: string; url: string; platformId: string; description: string; color: string; displayOrder: number }
export interface LocalhostTool { id: number; name: string; description: string; category: string; directUrl: string; repositoryUrl: string; issueUrl?: string | null; displayOrder: number }
export type ToolStatus = 'checking' | 'online' | 'offline'
export type CaseState = 'solved' | 'open' | 'pending'
export interface CaseFile { id: number; state: CaseState; status: string; title: string; summary: string; meta: string }

export const CASES: CaseFile[] = [
  {
    id: 104,
    state: 'solved',
    status: 'Solved · Salt & Burn',
    title: 'The Poltergeist in the Server Room',
    summary: 'Anomalous network spikes traced to an ungrounded iron pipe and a legacy cron job. Salted the rack, burned the ghost script.',
    meta: 'Lebanon, Kansas · Lead: Dean',
  },
  {
    id: 105,
    state: 'open',
    status: 'Open · Investigation',
    title: 'The Shape-shifter in CI/CD',
    summary: 'Build artifacts changing checksums between runners. Testing silver bullets and verifying git SHA commits.',
    meta: 'Bunker Terminal 4 · Lead: Sam',
  },
  {
    id: 106,
    state: 'pending',
    status: 'Pending · Surveillance',
    title: 'Crossroads Contract at Port 4554',
    summary: 'PostgreSQL instance deployed on custom port 4554 with seed data locked in EF Core. No demon deals found, purely clean vibe coding.',
    meta: 'Internet-facing Farm · Lead: Elder Vibe Coder',
  },
]

const TOOL_CATEGORIES = [
  { key: 'SearchCollect', label: 'Search & Collect', description: 'Discovery, extractors, scanners' },
  { key: 'ConvertTransformCompose', label: 'Convert & Compose', description: 'Translators, converters, composers' },
  { key: 'ObserveWatching', label: 'Observe & Watch', description: 'Telemetry, logs, health monitors' },
  { key: 'CalculatingAnalysing', label: 'Calculate & Analyze', description: 'Metrics, analyzers, scorers' },
  { key: 'WriteExecute', label: 'Write & Execute', description: 'Create or update documents and other content' },
]

export const HISTORY_WEEKS = 14
export const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

export function formatPeriod(start: string | null | undefined, end: string | null | undefined, current: boolean): string | null {
  const fmt = (value: string) => value.slice(0, 7)
  if (!start) return end ? `Through ${fmt(end)}` : null
  return `${fmt(start)} – ${current || !end ? 'present' : fmt(end)}`
}

export interface ApiProbe {
  endpoint: string
  title: string
  rawJson: string
  status: number | null
  statusText: string
  loading: boolean
  error: string | null
  lastUpdated: string | null
  totalRequests: number | null
  uniqueClients: number | null
  rateLimitLimit: number | null
  rateLimitRemaining: number | null
  rateLimitReset: number | null
  refresh: () => void
}

const HIDDEN_UI_KEYS = new Set(['specifiedIssueUrl', 'displayOrder', 'color', 'platformId'])

const stripHiddenKeys = (value: unknown): unknown => {
  if (Array.isArray(value)) return value.map(stripHiddenKeys)
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>)
        .filter(([key]) => !HIDDEN_UI_KEYS.has(key))
        .map(([key, v]) => [key, stripHiddenKeys(v)])
    )
  }
  return value
}

function getOrCreateClientId(): string {
  try {
    const key = 'farm_client_id'
    let id = localStorage.getItem(key)
    if (!id) {
      id = 'client_' + Math.random().toString(36).substring(2, 10) + Date.now().toString(36)
      localStorage.setItem(key, id)
    }
    return id
  } catch {
    return 'browser-client-' + Math.random().toString(36).substring(2, 10)
  }
}

const intHeader = (response: Response, name: string) => {
  const value = response.headers.get(name)
  return value ? parseInt(value, 10) : null
}

type ProbeState = Omit<ApiProbe, 'endpoint' | 'title' | 'refresh'>

const idleProbe: ProbeState = {
  rawJson: '',
  status: null,
  statusText: '',
  loading: false,
  error: null,
  lastUpdated: null,
  totalRequests: null,
  uniqueClients: null,
  rateLimitLimit: null,
  rateLimitRemaining: null,
  rateLimitReset: null,
}

function useApiProbe(endpoint: string, title: string): ApiProbe {
  const [state, setState] = useState<ProbeState>(idleProbe)

  const refresh = useCallback(async () => {
    setState((prev) => ({ ...prev, loading: true, error: null, statusText: 'Fetching...' }))
    try {
      const response = await fetch(endpoint, {
        headers: { Accept: 'application/json', 'X-Client-Id': getOrCreateClientId() },
      })
      let rawJson: string
      try {
        rawJson = JSON.stringify(stripHiddenKeys(await response.json()), null, 2)
      } catch {
        rawJson = ''
      }
      setState({
        rawJson,
        status: response.status,
        statusText: `${response.status} ${response.statusText || (response.ok ? 'OK' : 'Error')}`,
        loading: false,
        error: response.ok ? null : `HTTP Error ${response.status}`,
        lastUpdated: new Date().toLocaleTimeString(),
        totalRequests: intHeader(response, 'X-Total-Requests'),
        uniqueClients: intHeader(response, 'X-Unique-Clients'),
        rateLimitLimit: intHeader(response, 'X-RateLimit-Limit'),
        rateLimitRemaining: intHeader(response, 'X-RateLimit-Remaining'),
        rateLimitReset: intHeader(response, 'X-RateLimit-Reset'),
      })
    } catch (err: unknown) {
      setState((prev) => ({
        ...prev,
        loading: false,
        status: 0,
        statusText: 'Network Error',
        error: err instanceof Error ? err.message : String(err),
        lastUpdated: new Date().toLocaleTimeString(),
      }))
    }
  }, [endpoint])

  useEffect(() => {
    void refresh()
  }, [refresh])

  return { endpoint, title, refresh: () => void refresh(), ...state }
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error(`${path} returned ${response.status}`)
  return (await response.json()) as T
}

const loadList = <T,>(path: string) => getJson<T[]>(path).catch(() => [] as T[])

interface Loaded {
  projects: DisplayProject[]
  skills: Skill[]
  experiences: Experience[]
  gigs: Gig[]
  platforms: PlatformBookmark[]
  clicks: ClickRow[]
  tools: LocalhostTool[]
  portfolioFailed: boolean
}

async function loadAll(): Promise<Loaded> {
  const [projects, links, skills, experiences, gigs, platforms, clicks, tools] = await Promise.all([
    getJson<Project[]>('/api/portfolio/projects').catch(() => null),
    loadList<ProfileProject>('/api/portfolio/profile-projects'),
    loadList<Skill>('/api/portfolio/skills'),
    loadList<Experience>('/api/portfolio/experiences'),
    loadList<Gig>('/api/portfolio/gigs'),
    loadList<PlatformBookmark>('/api/feeds'),
    loadList<ClickRow>('/api/feeds/clicks'),
    loadList<LocalhostTool>('/api/tools'),
  ])

  const byProject = new Map<number, ProfileProject[]>()
  for (const link of links) byProject.set(link.projectId, [...(byProject.get(link.projectId) ?? []), link])
  const display = (projects ?? []).flatMap<DisplayProject>((project) => {
    const associations = byProject.get(project.id)
    return associations?.length ? associations.map((association) => ({ ...project, association })) : [{ ...project }]
  })
  const max = Number.MAX_SAFE_INTEGER
  display.sort(
    (a, b) =>
      (a.association?.profileId ?? max) - (b.association?.profileId ?? max) ||
      (a.association?.displayOrder ?? max) - (b.association?.displayOrder ?? max) ||
      a.id - b.id
  )

  return {
    projects: display,
    skills,
    experiences: [...experiences].sort((a, b) => a.displayOrder - b.displayOrder),
    gigs,
    platforms,
    clicks,
    tools,
    portfolioFailed: projects === null,
  }
}

/** Everything the site shows, loaded once; each style project lays it out in its own way. */
export function useSiteContent() {
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [clickOverrides, setClickOverrides] = useState<ClickRow[]>([])
  const [toolStatuses, setToolStatuses] = useState<Record<number, ToolStatus>>({})
  const metadata = useApiProbe('/api/metadata', 'Metadata')
  const feeds = useApiProbe('/api/feeds', 'Feeds')

  useEffect(() => {
    let cancelled = false
    loadAll().then((result) => {
      if (!cancelled) setLoaded(result)
    })
    return () => {
      cancelled = true
    }
  }, [])

  const clicks = useMemo(() => [...(loaded?.clicks ?? []), ...clickOverrides], [loaded, clickOverrides])

  // Opening a source counts as today's brick for it.
  const registerVisit = useCallback((platform: PlatformBookmark) => {
    setClickOverrides((prev) => [...prev, { platform: platform.platform, day: dayKey(new Date()), count: 1 }])
    fetch(`/api/feeds/${encodeURIComponent(platform.platform)}/click`, { method: 'POST', keepalive: true }).catch(() => {})
  }, [])

  const checkTool = useCallback(async (tool: LocalhostTool) => {
    setToolStatuses((prev) => ({ ...prev, [tool.id]: 'checking' }))
    const controller = new AbortController()
    const timeoutId = setTimeout(() => controller.abort(), 2500)
    try {
      // no-cors lets the page ping a localhost origin without being blocked by CORS
      await fetch(tool.directUrl, { method: 'HEAD', mode: 'no-cors', signal: controller.signal })
      setToolStatuses((prev) => ({ ...prev, [tool.id]: 'online' }))
    } catch {
      setToolStatuses((prev) => ({ ...prev, [tool.id]: 'offline' }))
    } finally {
      clearTimeout(timeoutId)
    }
  }, [])

  const tools = loaded?.tools ?? []
  const checkAllTools = useCallback(() => tools.forEach((tool) => void checkTool(tool)), [tools, checkTool])

  const overall = useMemo(() => lastDays(clicks, HISTORY_WEEKS * 7), [clicks])
  const activity = {
    streak: currentStreak(overall),
    todayDone: doneToday(overall),
    activeDays: activeDays(overall),
    bestStreak: longestStreak(overall),
    recent: overall.slice(-15),
  }

  const platforms = loaded?.platforms ?? []
  const habits = useMemo(
    () => platforms.map((platform) => {
      const days = lastDays(clicks, HISTORY_WEEKS * 7, platform.platform)
      return { platform, days, streak: currentStreak(days), visits: days.reduce((sum, d) => sum + d.count, 0) }
    }),
    [platforms, clicks]
  )

  const toolGroups = useMemo(() => {
    const known = new Set(TOOL_CATEGORIES.map((c) => c.key))
    const groups = TOOL_CATEGORIES.map((c) => ({ ...c, items: tools.filter((t) => t.category === c.key) }))
    const other = tools.filter((t) => !known.has(t.category))
    if (other.length) groups.push({ key: 'Other', label: 'Other', description: 'Uncategorized tools', items: other })
    return groups.filter((g) => g.items.length > 0)
  }, [tools])

  const skillGroups = useMemo(() => {
    const groups: Record<string, Skill[]> = {}
    for (const skill of loaded?.skills ?? []) (groups[skill.category] ??= []).push(skill)
    return Object.entries(groups)
  }, [loaded])

  return {
    loading: loaded === null,
    portfolioFailed: loaded?.portfolioFailed ?? false,
    projects: loaded?.projects ?? [],
    experiences: loaded?.experiences ?? [],
    gigs: loaded?.gigs ?? [],
    skillGroups,
    habits,
    activity,
    toolGroups,
    toolCount: tools.length,
    toolStatuses,
    checkTool: (tool: LocalhostTool) => void checkTool(tool),
    checkAllTools,
    registerVisit,
    probes: [metadata, feeds],
    cases: CASES,
  }
}

export type SiteContent = ReturnType<typeof useSiteContent>
