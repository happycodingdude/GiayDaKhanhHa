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
