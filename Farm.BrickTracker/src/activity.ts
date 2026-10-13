export interface ClickRow {
  platform: string
  day: string
  count: number
}

export interface DayCell {
  date: string
  count: number
}

const DAY_MS = 86400000

export const dayKey = (d: Date) => d.toISOString().slice(0, 10)

export const levelOf = (count: number) => (count === 0 ? 0 : count <= 1 ? 1 : count <= 3 ? 2 : count <= 6 ? 3 : 4)

// Oldest to newest, ending today (UTC, matching how the API buckets days).
export function lastDays(rows: ClickRow[], count: number, platform = 'all'): DayCell[] {
  const byDay = new Map<string, number>()
  for (const r of rows) {
    if (platform !== 'all' && r.platform.toLowerCase() !== platform.toLowerCase()) continue
    byDay.set(r.day, (byDay.get(r.day) ?? 0) + r.count)
  }
  const now = new Date()
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())
  const days: DayCell[] = []
  for (let i = count - 1; i >= 0; i--) {
    const date = dayKey(new Date(today - i * DAY_MS))
    days.push({ date, count: byDay.get(date) ?? 0 })
  }
  return days
}

// A streak survives until the end of today, so an empty today does not break it.
export function currentStreak(days: DayCell[]): number {
  let i = days.length - 1
  if (i >= 0 && days[i].count === 0) i--
  let streak = 0
  for (; i >= 0 && days[i].count > 0; i--) streak++
  return streak
}

export function longestStreak(days: DayCell[]): number {
  let best = 0
  let run = 0
  for (const day of days) {
    run = day.count > 0 ? run + 1 : 0
    best = Math.max(best, run)
  }
  return best
}

export const activeDays = (days: DayCell[]) => days.filter((d) => d.count > 0).length

export const doneToday = (days: DayCell[]) => (days.length > 0 ? days[days.length - 1].count > 0 : false)
