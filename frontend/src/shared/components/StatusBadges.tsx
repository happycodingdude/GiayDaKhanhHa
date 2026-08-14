import { Badge } from './ui'
import { formatNumber } from '../lib/format'

/** Trạng thái đơn hàng chỉ có đúng hai giá trị và không bao giờ do quản lý đặt. */
export function OrderStatusBadge({ status }: { status: string }) {
  return status === 'Completed' ? (
    <Badge tone="success">Hoàn thành</Badge>
  ) : (
    <Badge tone="neutral">Chưa hoàn thành</Badge>
  )
}

/**
 * Tình trạng tiến độ, tách riêng khỏi trạng thái đơn hàng: "chậm" không bao giờ là một trạng
 * thái của đơn hàng (order list spec §5).
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

/** Tình trạng sản xuất theo từng ngày, hiển thị trong bảng tiến độ sản xuất. */
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
    // Ngày tương lai đơn giản là chưa diễn ra. Khi ngày đã tới thì phải có sản lượng thực tế,
    // nên việc thiếu số liệu được báo lại — nhẹ nhàng với hôm nay vì cuối ngày mới nhập,
    // và thành cảnh báo với ngày đã trôi qua mà vẫn chưa có số.
    if (dayPosition === 'future') return <Badge tone="neutral">Chờ sản xuất</Badge>
    return <Badge tone={dayPosition === 'today' ? 'neutral' : 'warning'}>Chưa nhập</Badge>
  }

  if (shortageQuantity > 0) return <Badge tone="warning">Thiếu {formatNumber(shortageQuantity)}</Badge>
  if ((difference ?? 0) > 0) return <Badge tone="info">Vượt kế hoạch</Badge>
  return <Badge tone="success">Đạt kế hoạch</Badge>
}
