import { Badge } from '../../../shared/components/ui'
import type { OrderStatus } from '../types'

/**
 * Trạng thái đơn hàng — ba giá trị sau CR-001 (§4.1). Không bao giờ do quản lý đặt: `Pending` do
 * bước nhập hàng sinh ra, hai giá trị còn lại suy từ sản lượng thực tế.
 */
export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  if (status === 'Pending') return <Badge tone="warning">Chưa lập tiến độ</Badge>
  if (status === 'Completed') return <Badge tone="success">Hoàn thành</Badge>
  return <Badge tone="info">Đang sản xuất</Badge>
}
