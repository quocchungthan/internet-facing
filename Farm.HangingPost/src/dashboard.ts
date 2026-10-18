export interface TaskUsage {
  id: string
  date: string
  name: string
  model: string
  inputTokens: number
  outputTokens: number
  durationSeconds: number
  inputBytes: number
  outputBytes: number
  costUsd: number
  feeUsd: number
  baselineCostUsd: number
}

export interface AudienceUser {
  subject: string
  username: string
  firstSeen: string
  lastSeen: string
  observedDays: number
}

export interface AudienceData {
  source: 'fences'
  totalUsers: number
  newUsers: number
  returningUsers: number
  growth: { date: string; users: number }[]
  users: AudienceUser[]
  coverage: string
}

export interface DashboardData {
  source: 'demo'
  currency: 'USD'
  days: number
  from: string
  through: string
  isOwner: boolean
  tasks: TaskUsage[]
  audience: { source: 'fences'; url: string | null } | null
}

export const money = (value: number) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(value)

export const count = (value: number) => new Intl.NumberFormat().format(value)

export function bytes(value: number): string {
  if (value < 1024) return `${count(value)} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KiB`
  return `${(value / (1024 * 1024)).toFixed(2)} MiB`
}

export function duration(seconds: number): string {
  const rounded = Math.round(seconds)
  return `${Math.floor(rounded / 60)}m ${rounded % 60}s`
}

export const dateLabel = (date: string) => new Intl.DateTimeFormat(undefined, {
  month: 'short', day: 'numeric', timeZone: 'UTC',
}).format(new Date(`${date}T12:00:00Z`))

export function summarize(tasks: TaskUsage[]) {
  const sum = (field: keyof Pick<TaskUsage,
    'costUsd' | 'feeUsd' | 'baselineCostUsd' | 'inputTokens' | 'outputTokens' |
    'durationSeconds' | 'inputBytes' | 'outputBytes'>) =>
    tasks.reduce((total, task) => total + task[field], 0)
  const cost = sum('costUsd')
  const fees = sum('feeUsd')
  const baseline = sum('baselineCostUsd')
  return {
    cost, fees, baseline,
    savings: baseline - cost - fees,
    inputTokens: sum('inputTokens'),
    outputTokens: sum('outputTokens'),
    inputBytes: sum('inputBytes'),
    outputBytes: sum('outputBytes'),
    totalSeconds: sum('durationSeconds'),
    averageSeconds: tasks.length ? sum('durationSeconds') / tasks.length : 0,
    costPerTask: tasks.length ? (cost + fees) / tasks.length : 0,
  }
}
