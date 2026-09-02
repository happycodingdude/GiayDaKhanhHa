import { Link, useParams } from '@tanstack/react-router'
import { useState } from 'react'
import { Badge, Button } from '../../../shared/components/ui'
import { OrderStatusBadge } from '../../../shared/components/StatusBadges'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate, today } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { AdjustmentHistory } from '../../adjustments/components/AdjustmentHistory'
import { ShortageDialog } from '../../adjustments/components/ShortageDialog'
import { CloseDayDialog } from '../../production/components/CloseDayDialog'
import { ProductionDayDialog } from '../../production/components/ProductionDayDialog'
import { ProductionTimeline } from '../../production/components/ProductionTimeline'
import { useProductionPlans } from '../../production/hooks/useProductionPlans'
import type { ProductionDayDto } from '../../production/types'
import { OrderStatisticsPanel } from '../../statistics/components/OrderStatisticsPanel'
import { OrderSummary } from '../components/OrderSummary'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { useOrder } from '../hooks/useOrders'

/** Màn hình trung tâm của quản lý sản xuất (Step 5 §15). */
export function OrderDetailPage() {
  const { orderId } = useParams({ from: '/authenticated/orders/$orderId' })

  const { showToast } = useToast()
  const orderQuery = useOrder(orderId)
  const plansQuery = useProductionPlans(orderId)

  // Ba modal của luồng sản xuất, mở từ đúng một hàng thao tác trên bảng tiến độ.
  const [recordingDay, setRecordingDay] = useState<ProductionDayDto | null>(null)
  const [closingDay, setClosingDay] = useState<ProductionDayDto | null>(null)
  const [viewingDay, setViewingDay] = useState<ProductionDayDto | null>(null)
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

  // Ngày hữu ích nhất để mở từ header: hôm nay nếu còn đang sản xuất, nếu không thì ngày đã qua
  // đầu tiên chưa Xuất hàng. Không bao giờ là ngày tương lai hay ngày đã chốt sổ.
  // Ngày đã qua mà chưa Xuất hàng là việc bị treo: số liệu của chúng vẫn chỉ là tạm tính (CR-01 N-09).
  const unclosedPastDays = days.filter(
    (day) => day.dayStatus === 'InProduction' && day.productionDate < currentDate,
  )

  const suggestedDay =
    days.find((day) => day.productionDate === currentDate && day.dayStatus === 'InProduction') ??
    days.find((day) => day.dayStatus === 'InProduction' && day.productionDate < currentDate) ??
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
          <Button variant="primary" onClick={() => setRecordingDay(suggestedDay)}>
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

      {unclosedPastDays.length > 0 && !readOnly && (
        <p className="notice notice--warning">
          ⚠ {unclosedPastDays.length} ngày đã qua chưa xuất hàng (
          {unclosedPastDays.slice(0, 3).map((day) => formatDate(day.productionDate)).join(', ')}
          {unclosedPastDays.length > 3 && ` và ${unclosedPastDays.length - 3} ngày khác`}). Sản
          lượng của các ngày này chưa được chốt sổ nên vẫn là số tạm tính.
        </p>
      )}

      <OrderSummary order={order} />

      <ProductionTimeline
        days={days}
        orderCompleted={order.status === 'Completed'}
        readOnly={readOnly}
        onRecord={setRecordingDay}
        onCloseDay={setClosingDay}
        onViewDay={setViewingDay}
        onHandleShortage={setShortageDay}
      />

      <AdjustmentHistory orderId={orderId} readOnly={readOnly} />

      <OrderStatisticsPanel orderId={orderId} />

      {/* Nhập sản lượng: modal, không rời khỏi màn hình chi tiết đơn hàng. */}
      {recordingDay && (
        <ProductionDayDialog
          open
          orderId={orderId}
          productionDate={recordingDay.productionDate}
          onClose={() => setRecordingDay(null)}
        />
      )}

      {/* Xem chi tiết ngày đã chốt sổ: chỉ hiển thị thông tin, không kèm lối vào Xử lý thiếu. */}
      {viewingDay && (
        <ProductionDayDialog
          open
          orderId={orderId}
          productionDate={viewingDay.productionDate}
          readOnly
          onClose={() => setViewingDay(null)}
        />
      )}

      {closingDay && (
        <CloseDayDialog
          open
          orderId={orderId}
          productionDate={closingDay.productionDate}
          onClose={() => setClosingDay(null)}
          onClosed={(result) => {
            const closedDate = closingDay.productionDate
            setClosingDay(null)
            showToast(
              `Đã xuất hàng ngày ${formatDate(closedDate)}: ${formatNumber(result.actualQuantity)} đôi.` +
                (result.orderCompleted ? ' Đơn hàng đã hoàn thành.' : ''),
              result.hasShortage ? 'info' : 'success',
            )
          }}
        />
      )}

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
