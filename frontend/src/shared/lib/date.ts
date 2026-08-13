/**
 * Business dates (ProductionDate, StartDate, DueDate) are date-only strings in YYYY-MM-DD form.
 * They are never routed through a JavaScript Date, because a timezone conversion could shift the
 * calendar day (Step 5 §32).
 */
export type IsoDate = string

/** "2026-08-13" -> "13/08/2026" */
export function formatDate(date: IsoDate): string {
  const [year, month, day] = date.split('-')
  return `${day}/${month}/${year}`
}

/** "2026-08-13" -> "13/08" */
export function formatShortDate(date: IsoDate): string {
  const [, month, day] = date.split('-')
  return `${day}/${month}`
}

/** "2026-08-13" -> "Thứ Năm" */
export function formatWeekday(date: IsoDate): string {
  const names = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']
  const [year, month, day] = date.split('-').map(Number)
  // Constructed in UTC and read in UTC so the weekday cannot drift with the browser timezone.
  return names[new Date(Date.UTC(year, month - 1, day)).getUTCDay()]
}

/** Today as a date-only string in the browser's local calendar. */
export function today(): IsoDate {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

/** Every date from start to end inclusive. Pure string arithmetic in UTC. */
export function dateRange(start: IsoDate, end: IsoDate): IsoDate[] {
  if (!start || !end || start > end) return []

  const dates: IsoDate[] = []
  const [sy, sm, sd] = start.split('-').map(Number)
  const cursor = new Date(Date.UTC(sy, sm - 1, sd))

  // Guard against an accidental huge range from a mistyped year.
  for (let i = 0; i < 400; i++) {
    const value = cursor.toISOString().slice(0, 10)
    if (value > end) break
    dates.push(value)
    cursor.setUTCDate(cursor.getUTCDate() + 1)
  }

  return dates
}

/** Inclusive day count: 11/08 -> 15/08 is 5 production days. */
export function countDays(start: IsoDate, end: IsoDate): number {
  return dateRange(start, end).length
}

/**
 * Audit timestamps are UTC and are converted only for display (Step 5 §33).
 * "2026-08-13T04:49:45+00:00" -> "13/08/2026 11:49"
 */
export function formatTimestamp(timestamp: string): string {
  const date = new Date(timestamp)
  if (Number.isNaN(date.getTime())) return '—'

  const pad = (value: number) => String(value).padStart(2, '0')
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}
