import { Card, ProgressBar, StatTile } from '../../../shared/components/ui'
import { OrderStatusBadge, ScheduleStatusBadge } from '../../../shared/components/StatusBadges'
import { formatDate } from '../../../shared/lib/date'
import { formatNumber, formatPercent } from '../../../shared/lib/format'
import type { OrderDetailDto } from '../types'

/** Các số liệu suy ra của đơn hàng. Tiến độ = tổng thực tế / số lượng đơn (order detail spec §4.3). */
export function OrderSummary({ order }: { order: OrderDetailDto }) {
  return (
    <Card title="Tổng quan đơn hàng">
      <div className="stats">
        <StatTile label="Tổng số lượng" value={`${formatNumber(order.quantity)} đôi`} />
        <StatTile label="Đã hoàn thành" value={`${formatNumber(order.totalActual)} đôi`} tone="success" />
        <StatTile label="Còn lại" value={`${formatNumber(order.remaining)} đôi`} />
        <StatTile
          label="Hạn hoàn thành"
          value={formatDate(order.dueDate)}
          hint={order.isOverdue ? 'Đã quá hạn' : `Còn ${order.daysRemaining} ngày`}
          tone={order.isOverdue ? 'danger' : 'neutral'}
        />
        <StatTile
          label="Tổng kế hoạch"
          value={`${formatNumber(order.totalPlan)} đôi`}
          hint={
            order.totalPlan > order.quantity
              ? `Gồm ${formatNumber(order.totalPlan - order.totalInitialPlan)} đôi bù thêm`
              : undefined
          }
        />
      </div>

      <div className="summary-progress">
        <div className="summary-progress__head">
          <span>Tiến độ</span>
          <strong>{formatPercent(order.progressPercentage)}</strong>
        </div>
        <ProgressBar
          value={order.progressPercentage}
          tone={order.scheduleStatus === 'Behind' ? 'danger' : order.status === 'Completed' ? 'success' : 'info'}
        />
        <div className="summary-progress__badges">
          <OrderStatusBadge status={order.status} />
          <ScheduleStatusBadge scheduleStatus={order.scheduleStatus} behindQuantity={order.behindQuantity} />
        </div>
      </div>
    </Card>
  )
}
