import { Badge } from '../../../shared/components/ui'
import type { OrderStatus } from '../types'

/**
 * Trạng thái đơn hàng — ba giá trị sau CR-001 (§4.1), cộng "Quá hạn". Không bao giờ do quản lý đặt:
 * `Pending` do bước nhập hàng sinh ra, hai giá trị còn lại suy từ sản lượng thực tế.
 *
 * "Quá hạn" là một trạng thái riêng, thay cho "Đang sản xuất" khi đơn đã qua ngày kết thúc mà chưa
 * hoàn thành. Nó suy ra từ ngày nên không có trong `OrderStatus`; `isOverdue` bắt buộc để không màn
 * hình nào quên truyền và lại hiện "Đang sản xuất" cho một đơn đã trễ hạn.
 */
export function OrderStatusBadge({ status, isOverdue }: { status: OrderStatus; isOverdue: boolean }) {
  if (status === 'Pending') return <Badge tone="warning">Chưa lập tiến độ</Badge>
  if (status === 'Completed') return <Badge tone="success">Hoàn thành</Badge>
  if (isOverdue) return <Badge tone="danger">Quá hạn</Badge>
  return <Badge tone="info">Đang sản xuất</Badge>
}
