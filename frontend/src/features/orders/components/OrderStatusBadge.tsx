import { Badge } from '../../../shared/components/ui'
import type { OrderStatus } from '../types'

/**
 * Trạng thái đơn hàng — ba giá trị sau CR-001 (§4.1), cộng các trạng thái suy ra. Không bao giờ do
 * quản lý đặt: `Pending` do bước nhập hàng sinh ra, hai giá trị còn lại suy từ sản lượng thực tế.
 *
 * Các trạng thái suy ra tách "Đang sản xuất" ra theo đúng thứ tự vòng đời:
 * - "Chờ chốt": đã lập tiến độ nhưng chưa chốt, nên chưa sản xuất được.
 * - "Chưa sản xuất": đã chốt nhưng chưa tới ngày bắt đầu.
 * - "Quá hạn": đã qua ngày kết thúc mà chưa hoàn thành.
 * Chúng suy ra từ ngày và cờ chốt nên không có trong `OrderStatus`; các prop đều bắt buộc để không
 * màn hình nào quên truyền và lại hiện "Đang sản xuất" cho một đơn chưa chạy hoặc đã trễ hạn.
 */
export function OrderStatusBadge({
  status,
  isOverdue,
  isScheduleConfirmed,
  isBeforeStartDate,
}: {
  status: OrderStatus
  isOverdue: boolean
  isScheduleConfirmed: boolean
  isBeforeStartDate: boolean
}) {
  if (status === 'Pending') return <Badge tone="warning">Chưa lập tiến độ</Badge>
  if (status === 'Completed') return <Badge tone="success">Hoàn thành</Badge>
  if (!isScheduleConfirmed) return <Badge tone="warning">Chờ chốt</Badge>
  if (isOverdue) return <Badge tone="danger">Quá hạn</Badge>
  if (isBeforeStartDate) return <Badge tone="neutral">Chưa sản xuất</Badge>
  return <Badge tone="info">Đang sản xuất</Badge>
}
