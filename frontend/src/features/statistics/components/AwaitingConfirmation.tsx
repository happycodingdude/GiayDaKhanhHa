import { Badge, Card } from '../../../shared/components/ui'
import { formatDate, type IsoDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { OrderThumbnail } from '../../orders/components/OrderThumbnail'
import type { DashboardAwaitingConfirmationDto } from '../types'

/**
 * Đơn đã lập tiến độ nhưng chưa chốt. Chúng chưa sản xuất được nên không nằm trong timeline hay
 * số liệu nào khác của dashboard; thiếu khối này thì chúng biến mất khỏi dashboard.
 *
 * Đơn đã qua ngày kết thúc mà vẫn chưa chốt thì không chốt được (ORDER_OVERDUE) cho tới khi sửa
 * tiến độ dời ngày hoặc xoá tiến độ lập lại, nên dòng đó được đánh dấu riêng thay vì "Chờ chốt".
 */
export function AwaitingConfirmation({
  orders,
  today,
  onOpenOrder,
}: {
  orders: DashboardAwaitingConfirmationDto[]
  /** Ngày nghiệp vụ do backend chốt, không phải ngày của trình duyệt. */
  today: IsoDate
  onOpenOrder: (orderId: string) => void
}) {
  // Không có đơn nào chờ chốt thì không có gì phải làm — ẩn hẳn khối thay vì một bảng trống.
  if (orders.length === 0) return null

  return (
    <Card
      title="Chờ chốt tiến độ"
      description={
        <>
          <strong>{formatNumber(orders.length)} đơn</strong> đã lập tiến độ nhưng chưa chốt nên chưa nhập được sản
          lượng. Mở đơn để kiểm tra kế hoạch rồi chốt.
        </>
      }
    >
      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Ảnh</th>
              <th>Mã giày</th>
              <th className="num">Số lượng</th>
              <th>Ngày bắt đầu</th>
              <th>Ngày kết thúc</th>
              <th>Tình trạng</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => (
              <tr
                key={order.orderId}
                className="table__row--clickable"
                onClick={() => onOpenOrder(order.orderId)}
                tabIndex={0}
                onKeyDown={(event) => event.key === 'Enter' && onOpenOrder(order.orderId)}
              >
                <td>
                  <OrderThumbnail imageUrl={order.imageUrl} shoeCode={order.shoeCode} />
                </td>
                <td className="table__strong">{order.shoeCode}</td>
                <td className="num">{formatNumber(order.quantity)}</td>
                <td>{formatDate(order.startDate)}</td>
                <td>{formatDate(order.dueDate)}</td>
                <td>
                  {order.dueDate < today ? (
                    <Badge tone="danger">Quá hạn — không chốt được</Badge>
                  ) : (
                    <Badge tone="warning">Chờ chốt</Badge>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
