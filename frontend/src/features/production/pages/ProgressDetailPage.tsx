import { Link, useParams } from '@tanstack/react-router'
import { useState } from 'react'
import { Badge, Button, Card, ProgressBar, StatTile } from '../../../shared/components/ui'
import { ScheduleStatusBadge } from '../../../shared/components/StatusBadges'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { formatDate, formatShortDate, today, type IsoDate } from '../../../shared/lib/date'
import { formatNumber, formatPercent } from '../../../shared/lib/format'
import { AdjustmentHistory } from '../../adjustments/components/AdjustmentHistory'
import { ShortageDialog } from '../../adjustments/components/ShortageDialog'
import { OrderStatusBadge } from '../../orders/components/OrderStatusBadge'
import { OrderThumbnail } from '../../orders/components/OrderThumbnail'
import { useOrder } from '../../orders/hooks/useOrders'
import { OrderStatisticsPanel } from '../../statistics/components/OrderStatisticsPanel'
import { CloseDayDialog } from '../components/CloseDayDialog'
import { ProductionCellDialog } from '../components/ProductionCellDialog'
import { ProductionMatrix, type CellRef } from '../components/ProductionMatrix'
import { useProductionMatrix } from '../hooks/useProductionCell'
import type { ProductionMatrixLineDto } from '../types'

/** Màn hình trung tâm của quản lý sản xuất, nay theo ma trận ngày × dây chuyền (CR-001 §7.8). */
export function ProgressDetailPage() {
  const { orderId } = useParams({ from: '/authenticated/progress/$orderId' })

  const { showToast } = useToast()
  const orderQuery = useOrder(orderId)
  const matrixQuery = useProductionMatrix(orderId)

  // Bốn dialog của luồng sản xuất, mở từ các nút trên ma trận.
  const [recording, setRecording] = useState<CellRef | null>(null)
  // Chụp lại danh sách dây chuyền lúc bấm: sau khi Xuất hàng, ma trận refetch và ngày đó không còn
  // dây chuyền mở nào, nhưng dialog vẫn phải giữ nguyên nội dung cho tới lúc đóng.
  const [closing, setClosing] = useState<{ date: IsoDate; lines: ProductionMatrixLineDto[] } | null>(null)
  const [viewing, setViewing] = useState<CellRef | null>(null)
  const [shortage, setShortage] = useState<CellRef | null>(null)

  if (orderQuery.isPending || matrixQuery.isPending) {
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

  if (matrixQuery.isError) {
    return (
      <div className="page">
        <ErrorState
          error={matrixQuery.error}
          onRetry={() => void matrixQuery.refetch()}
          title="Không tải được kế hoạch sản xuất"
        />
      </div>
    )
  }

  const order = orderQuery.data
  const matrix = matrixQuery.data
  const currentDate = today()

  // Đơn chưa lập tiến độ chưa có gì để theo dõi ở màn này (CR-001 BR-N04).
  if (order.status === 'Pending') {
    return (
      <div className="page">
        <header className="page__header">
          <div>
            <Link to="/progress" className="back-link">
              ← Danh sách tiến độ
            </Link>
            <h1 className="page__title">
              {order.shoeCode} <OrderStatusBadge status={order.status} isOverdue={order.isOverdue} />
            </h1>
          </div>
        </header>
        <Card>
          <p className="notice notice--warning">
            Đơn hàng này chưa được lập tiến độ nên chưa có kế hoạch sản xuất nào.
          </p>
          <div className="form__actions">
            <Link to="/progress/new" search={{ orderId: order.id }}>
              <Button variant="primary">Lập tiến độ ngay</Button>
            </Link>
          </div>
        </Card>
      </div>
    )
  }

  // Đơn hàng đã qua ngày kết thúc thì bị đóng băng: màn hình chỉ hiển thị trạng thái cuối. Bao gồm
  // cả đơn đã hoàn thành — yếu tố quyết định là lịch, không phải trạng thái. Server cũng áp đúng luật
  // này (ORDER_OVERDUE); phần này chỉ để ẩn các thao tác đã vô nghĩa.
  const readOnly = order.isPastDueDate

  // Ngày đã qua mà chưa Xuất hàng là việc bị treo: số liệu của nó vẫn chỉ là tạm tính (CR-01 N-09).
  // Gom theo ngày vì Xuất hàng chốt sổ cả ngày một lượt.
  const unclosedPastDates = [
    ...new Set(
      matrix.items
        .filter((cell) => cell.dayStatus === 'InProduction' && cell.productionDate < currentDate)
        .map((cell) => cell.productionDate),
    ),
  ].sort()

  return (
    <div className="page">
      <header className="page__header">
        {/* Link quay lại nằm riêng một dòng phía trên, giống trang chi tiết nhập hàng; ảnh chỉ đi
            cùng hàng với mã giày. */}
        <div>
          <Link to="/progress" className="back-link">
            ← Danh sách tiến độ
          </Link>
          <div className="page__heading-with-image">
            <OrderThumbnail imageUrl={order.imageUrl} shoeCode={order.shoeCode} size={64} />
            <h1 className="page__title">
              {order.shoeCode} <OrderStatusBadge status={order.status} isOverdue={order.isOverdue} />
            </h1>
          </div>
        </div>

        <Link to="/goods-receipt/$orderId" params={{ orderId: order.id }}>
          <Button>Thông tin nhập hàng</Button>
        </Link>
      </header>

      {readOnly && (
        <p className="notice notice--danger">
          🔒 Đơn hàng đã qua ngày kết thúc (<strong>{order.dueDate ? formatDate(order.dueDate) : '—'}</strong>) nên
          chỉ được xem lại. Không thể nhập, sửa sản lượng hay bù sản lượng thiếu.
        </p>
      )}

      {unclosedPastDates.length > 0 && !readOnly && (
        <p className="notice notice--warning">
          ⚠ <strong>{unclosedPastDates.length} ngày</strong> đã qua chưa xuất hàng (
          {unclosedPastDates.slice(0, 3).map(formatShortDate).join(', ')}
          {unclosedPastDates.length > 3 && ` và ${unclosedPastDates.length - 3} ngày khác`}). Sản
          lượng của các ngày này chưa được chốt sổ nên vẫn là số tạm tính.
        </p>
      )}

      <Card title="Tổng quan đơn hàng">
        <div className="stats">
          <StatTile label="Tổng số lượng" value={`${formatNumber(order.quantity)} đôi`} />
          <StatTile label="Đã hoàn thành" value={`${formatNumber(order.totalActual)} đôi`} tone="success" />
          <StatTile label="Còn lại" value={`${formatNumber(order.remaining)} đôi`} />
          <StatTile label="Ngày bắt đầu" value={order.startDate ? formatDate(order.startDate) : '—'} />
          <StatTile
            label="Ngày kết thúc"
            value={order.dueDate ? formatDate(order.dueDate) : '—'}
            // Thời hạn còn lại là thứ quản lý cần thấy ngay, nên nổi lên thành badge thay vì chữ phụ.
            hint={
              order.isOverdue ? (
                <Badge tone="danger">Đã quá hạn</Badge>
              ) : (
                <Badge tone="info">
                  Còn <strong>{order.daysRemaining} ngày</strong>
                </Badge>
              )
            }
            tone={order.isOverdue ? 'danger' : 'neutral'}
          />
        </div>

        <div className="summary-progress">
          <div className="summary-progress__head">
            <span>Tiến độ</span>
            <strong>{formatPercent(order.progressPercentage)}</strong>
          </div>
          <ProgressBar
            value={order.progressPercentage}
            tone={
              order.scheduleStatus === 'Behind'
                ? 'danger'
                : order.status === 'Completed'
                  ? 'success'
                  : 'info'
            }
          />
          <div className="summary-progress__badges">
            <OrderStatusBadge status={order.status} isOverdue={order.isOverdue} />
            <ScheduleStatusBadge
              scheduleStatus={order.scheduleStatus}
              behindQuantity={order.behindQuantity}
            />
          </div>
        </div>
      </Card>

      <ProductionMatrix
        matrix={matrix}
        readOnly={readOnly}
        orderCompleted={order.status === 'Completed'}
        onRecord={setRecording}
        onCloseDay={(date, lines) => setClosing({ date, lines })}
        onViewCell={setViewing}
        onHandleShortage={setShortage}
      />

      <AdjustmentHistory orderId={orderId} readOnly={readOnly} />

      <OrderStatisticsPanel orderId={orderId} />

      {/* Nhập sản lượng: modal, không rời khỏi màn hình tiến độ. */}
      {recording && (
        <ProductionCellDialog
          open
          orderId={orderId}
          productionDate={recording.productionDate}
          productionLineId={recording.productionLineId}
          onClose={() => setRecording(null)}
        />
      )}

      {/* Xem chi tiết ô đã chốt sổ: chỉ hiển thị thông tin, không kèm lối vào Xử lý thiếu. */}
      {viewing && (
        <ProductionCellDialog
          open
          orderId={orderId}
          productionDate={viewing.productionDate}
          productionLineId={viewing.productionLineId}
          readOnly
          onClose={() => setViewing(null)}
        />
      )}

      {closing && (
        <CloseDayDialog
          open
          orderId={orderId}
          productionDate={closing.date}
          lines={closing.lines}
          onClose={() => setClosing(null)}
          onClosed={(result) => {
            setClosing(null)
            showToast(
              `Đã xuất hàng ${formatDate(result.productionDate)}: ` +
                result.cells
                  .map((cell) => `${cell.productionLineCode} ${formatNumber(cell.actualQuantity)} đôi`)
                  .join(', ') +
                '.' +
                (result.orderCompleted ? ' Đơn hàng đã hoàn thành.' : ''),
              result.hasShortage ? 'info' : 'success',
            )
          }}
        />
      )}

      <ShortageDialog
        open={shortage !== null}
        orderId={orderId}
        sourceCell={shortage?.cell ?? null}
        allCells={matrix.items}
        lines={matrix.productionLines}
        onClose={() => setShortage(null)}
      />
    </div>
  )
}
