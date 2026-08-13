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

/** The central production-management screen (Step 5 §15). */
export function OrderDetailPage() {
  const { orderId: orderIdParam } = useParams({ from: '/authenticated/orders/$orderId' })
  const orderId = Number(orderIdParam)

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

  // The most useful day to open from the header: today, otherwise the first day still missing an actual.
  const suggestedDay =
    days.find((day) => day.productionDate === currentDate && day.plannedQuantity > 0) ??
    days.find((day) => day.actualQuantity === null && day.plannedQuantity > 0) ??
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

        {suggestedDay && order.status !== 'Completed' && (
          <Button variant="primary" onClick={() => setActualDay(suggestedDay)}>
            Nhập sản lượng
          </Button>
        )}
      </header>

      <OrderSummary order={order} />

      <ProductionTimeline
        days={days}
        orderCompleted={order.status === 'Completed'}
        onEnterActual={setActualDay}
        onHandleShortage={setShortageDay}
      />

      <AdjustmentHistory orderId={orderId} />

      <OrderStatisticsPanel orderId={orderId} />

      <ActualInputDialog
        open={actualDay !== null}
        day={actualDay}
        orderId={orderId}
        orderCode={order.orderCode}
        orderQuantity={order.quantity}
        totalActual={order.totalActual}
        onClose={() => setActualDay(null)}
        // Handling a shortage is offered, never forced (actual entry spec §7).
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
