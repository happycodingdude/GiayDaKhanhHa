import { Link, useParams } from '@tanstack/react-router'
import { useState } from 'react'
import { Badge, Button } from '../../../shared/components/ui'
import { OrderStatusBadge } from '../../../shared/components/StatusBadges'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate, today } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { AdjustmentHistory } from '../../adjustments/components/AdjustmentHistory'
import { ShortageDialog } from '../../adjustments/components/ShortageDialog'
import { ActualInputDialog } from '../../production/components/ActualInputDialog'
import { ProductionTimeline } from '../../production/components/ProductionTimeline'
import { useProductionPlans } from '../../production/hooks/useProduction'
import type { ProductionDayDto } from '../../production/types'
import { OrderStatisticsPanel } from '../../statistics/components/OrderStatisticsPanel'
import { OrderSummary } from '../components/OrderSummary'
import { useOrder } from '../hooks/useOrders'

/** Màn hình trung tâm của quản lý sản xuất (Step 5 §15). */
export function OrderDetailPage() {
  const { orderId } = useParams({ from: '/authenticated/orders/$orderId' })

  const orderQuery = useOrder(orderId)
  const plansQuery = useProductionPlans(orderId)

  const [actualDay, setActualDay] = useState<ProductionDayDto | null>(null)
  const [shortageDay, setShortageDay] = useState<ProductionDayDto | null>(null)

  if (orderQuery.isPending || plansQuery.isPending) {
    return (
      <div className="page">
        <LoadingState />
      </div>
    )
  }

  if (orderQuery.isError) {
    return (
      <div className="page">
        <ErrorState
          error={orderQuery.error}
          onRetry={() => void orderQuery.refetch()}
          title="Không tải được đơn hàng"
        />
      </div>
    )
  }

  if (plansQuery.isError) {
    return (
      <div className="page">
        <ErrorState
          error={plansQuery.error}
          onRetry={() => void plansQuery.refetch()}
          title="Không tải được kế hoạch sản xuất"
        />
      </div>
    )
  }

  const order = orderQuery.data
  const days = plansQuery.data.items
  const currentDate = today()

  // Đơn hàng đã qua ngày hạn thì bị đóng băng: màn hình chỉ hiển thị trạng thái cuối, không gì
  // khác. Bao gồm cả đơn đã hoàn thành — yếu tố quyết định là lịch, không phải trạng thái.
  // Server cũng áp đúng luật này (ORDER_OVERDUE), phần này chỉ để ẩn các thao tác đã vô nghĩa.
  const readOnly = order.isPastDueDate

  // Ngày hữu ích nhất để mở từ header: hôm nay, nếu không thì ngày đầu tiên còn thiếu sản lượng
  // thực tế. Không bao giờ là ngày tương lai — ngày đó chưa diễn ra nên không nhập được.
  const suggestedDay =
    days.find((day) => day.productionDate === currentDate && day.plannedQuantity > 0) ??
    days.find(
      (day) =>
        day.actualQuantity === null && day.plannedQuantity > 0 && day.productionDate < currentDate,
    ) ??
    null

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <Link to="/orders" className="back-link">
            ← Danh sách đơn hàng
          </Link>
          <h1 className="page__title">
            {order.orderCode} <OrderStatusBadge status={order.status} />
          </h1>
          <p className="page__subtitle">
            {formatDate(order.startDate)} → {formatDate(order.dueDate)}
            {order.scheduleStatus === 'Behind' && (
              <>
                {' · '}
                <Badge tone="danger">Chậm tiến độ: {formatNumber(order.behindQuantity)} đôi</Badge>
              </>
            )}
          </p>
        </div>

        {suggestedDay && order.status !== 'Completed' && !readOnly && (
          <Button variant="primary" onClick={() => setActualDay(suggestedDay)}>
            Nhập sản lượng
          </Button>
        )}
      </header>

      {readOnly && (
        <p className="notice notice--danger">
          🔒 Đơn hàng đã quá hạn hoàn thành ({formatDate(order.dueDate)}) nên chỉ được xem lại. Không
          thể nhập, sửa sản lượng hay bù sản lượng thiếu.
        </p>
      )}

      <OrderSummary order={order} />

      <ProductionTimeline
        days={days}
        orderCompleted={order.status === 'Completed'}
        readOnly={readOnly}
        onEnterActual={setActualDay}
        onHandleShortage={setShortageDay}
      />

      <AdjustmentHistory orderId={orderId} readOnly={readOnly} />

      <OrderStatisticsPanel orderId={orderId} />

      <ActualInputDialog
        open={actualDay !== null}
        day={actualDay}
        orderId={orderId}
        orderCode={order.orderCode}
        orderQuantity={order.quantity}
        totalActual={order.totalActual}
        onClose={() => setActualDay(null)}
        // Xử lý phần thiếu là gợi ý, không bao giờ ép buộc (actual entry spec §7).
        onShortageRecorded={setShortageDay}
      />

      <ShortageDialog
        open={shortageDay !== null}
        orderId={orderId}
        sourceDay={shortageDay}
        allDays={days}
        onClose={() => setShortageDay(null)}
      />
    </div>
  )
}
