import { Badge } from './ui'
import { formatNumber } from '../lib/format'

/** Order status has exactly two values and is never set by the manager. */
export function OrderStatusBadge({ status }: { status: string }) {
  return status === 'Completed' ? (
    <Badge tone="success">Hoàn thành</Badge>
  ) : (
    <Badge tone="neutral">Chưa hoàn thành</Badge>
  )
}

/**
 * Progress condition, kept separate from the order status: "chậm" is never an order status
 * (order list spec §5).
 */
export function ScheduleStatusBadge({
  scheduleStatus,
  behindQuantity,
}: {
  scheduleStatus: string
  behindQuantity: number
}) {
  if (scheduleStatus === 'Completed') return <Badge tone="success">✓ Hoàn thành</Badge>

  if (scheduleStatus === 'Behind') {
    return <Badge tone="danger">🔴 Chậm {formatNumber(behindQuantity)} đôi</Badge>
  }

  return <Badge tone="success">🟢 Đúng tiến độ</Badge>
}

/** Per-day production condition shown in the production timeline. */
export function DayStatusBadge({
  actualQuantity,
  shortageQuantity,
  difference,
  plannedQuantity,
  dayPosition,
}: {
  actualQuantity: number | null
  shortageQuantity: number
  difference: number | null
  plannedQuantity: number
  dayPosition: 'past' | 'today' | 'future'
}) {
  if (plannedQuantity === 0) return <Badge tone="neutral">Không sản xuất</Badge>

  if (actualQuantity === null) {
    // A future day simply has not happened yet. Once the day has arrived the actual is expected,
    // so the missing entry is reported as such — softly for today, which is entered at its end,
    // and as a warning for a day that has already gone by without a number.
    if (dayPosition === 'future') return <Badge tone="neutral">Chờ sản xuất</Badge>
    return <Badge tone={dayPosition === 'today' ? 'neutral' : 'warning'}>Chưa nhập</Badge>
  }

  if (shortageQuantity > 0) return <Badge tone="warning">Thiếu {formatNumber(shortageQuantity)}</Badge>
  if ((difference ?? 0) > 0) return <Badge tone="info">Vượt kế hoạch</Badge>
  return <Badge tone="success">Đạt kế hoạch</Badge>
}
