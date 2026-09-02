import { Badge } from '../../../shared/components/ui'
import type { DayStatus } from '../types'

const LABELS: Record<DayStatus, { label: string; tone: 'neutral' | 'info' | 'success' | 'warning' }> = {
  NoPlan: { label: 'Không sản xuất', tone: 'neutral' },
  NotStarted: { label: 'Chưa tới', tone: 'neutral' },
  InProduction: { label: 'Đang sản xuất', tone: 'info' },
  Closed: { label: 'Đã xuất hàng', tone: 'success' },
}

/**
 * Trạng thái ngày sản xuất do server suy ra (CR-01 §14.3). Ngày đã qua mà chưa Xuất hàng được đánh
 * dấu riêng: nó vẫn "đang sản xuất" về mặt dữ liệu, nhưng là việc bị treo cần xử lý (CR-01 N-09).
 */
export function DayStatusBadge({
  status,
  isPastDay = false,
}: {
  status: DayStatus
  isPastDay?: boolean
}) {
  if (status === 'InProduction' && isPastDay) {
    return <Badge tone="warning">Chưa xuất hàng</Badge>
  }

  const { label, tone } = LABELS[status]
  return <Badge tone={tone}>{label}</Badge>
}
