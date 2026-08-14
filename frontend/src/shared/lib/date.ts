/**
 * Ngày nghiệp vụ (ProductionDate, StartDate, DueDate) là chuỗi chỉ có ngày, dạng YYYY-MM-DD.
 * Chúng không bao giờ đi qua Date của JavaScript, vì việc quy đổi múi giờ có thể làm lệch ngày
 * trên lịch (Step 5 §32).
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
  // Tạo theo UTC và đọc theo UTC để thứ trong tuần không lệch theo múi giờ của trình duyệt.
  return names[new Date(Date.UTC(year, month - 1, day)).getUTCDay()]
}

/** Hôm nay dưới dạng chuỗi chỉ có ngày, theo lịch cục bộ của trình duyệt. */
export function today(): IsoDate {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

/** Mọi ngày từ đầu tới cuối, tính cả hai đầu. Thuần tính toán chuỗi theo UTC. */
export function dateRange(start: IsoDate, end: IsoDate): IsoDate[] {
  if (!start || !end || start > end) return []

  const dates: IsoDate[] = []
  const [sy, sm, sd] = start.split('-').map(Number)
  const cursor = new Date(Date.UTC(sy, sm - 1, sd))

  // Chặn trường hợp khoảng ngày khổng lồ do gõ nhầm năm.
  for (let i = 0; i < 400; i++) {
    const value = cursor.toISOString().slice(0, 10)
    if (value > end) break
    dates.push(value)
    cursor.setUTCDate(cursor.getUTCDate() + 1)
  }

  return dates
}

/** Đếm số ngày tính cả hai đầu: 11/08 -> 15/08 là 5 ngày sản xuất. */
export function countDays(start: IsoDate, end: IsoDate): number {
  return dateRange(start, end).length
}

/**
 * Dấu thời gian audit lưu theo UTC và chỉ quy đổi khi hiển thị (Step 5 §33).
 * "2026-08-13T04:49:45+00:00" -> "13/08/2026 11:49"
 */
export function formatTimestamp(timestamp: string): string {
  const date = new Date(timestamp)
  if (Number.isNaN(date.getTime())) return '—'

  const pad = (value: number) => String(value).padStart(2, '0')
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}
