import { Link, useParams } from '@tanstack/react-router'
import { Button, Card } from '../../../shared/components/ui'
import { ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { dateRange } from '../../../shared/lib/date'
import { useOrder } from '../../orders/hooks/useOrders'
import { useProductionMatrix } from '../hooks/useProductionCell'
import { cellKey, splitEvenly } from '../lib/allocation'
import { CreateSchedulePage, type ScheduleEdit } from './CreateSchedulePage'

/**
 * Sửa tiến độ: dùng lại đúng wizard Lập tiến độ, điền sẵn tiến độ hiện có. Chỉ tiến độ chưa chốt mới
 * sửa được; server cũng chặn (SCHEDULE_ALREADY_CONFIRMED).
 */
export function EditSchedulePage() {
  const { orderId } = useParams({ from: '/authenticated/progress/$orderId/edit' })

  const orderQuery = useOrder(orderId)
  const matrixQuery = useProductionMatrix(orderId)

  if (orderQuery.isPending || matrixQuery.isPending) {
    return (
      <div className="page">
        <LoadingState />
      </div>
    )
  }

  if (orderQuery.isError || matrixQuery.isError) {
    return (
      <div className="page">
        <ErrorState
          error={orderQuery.error ?? matrixQuery.error}
          onRetry={() => {
            void orderQuery.refetch()
            void matrixQuery.refetch()
          }}
          title="Không tải được tiến độ để sửa"
        />
      </div>
    )
  }

  const order = orderQuery.data
  const matrix = matrixQuery.data

  if (order.status === 'Pending' || order.isScheduleConfirmed || !order.startDate || !order.dueDate) {
    return (
      <div className="page">
        <header className="page__header">
          <div>
            <Link to="/progress/$orderId" params={{ orderId }} className="back-link">
              ← Chi tiết tiến độ
            </Link>
            <h1 className="page__title">Sửa tiến độ</h1>
          </div>
        </header>
        <Card>
          <p className="notice notice--warning">
            {order.status === 'Pending'
              ? 'Đơn hàng này chưa được lập tiến độ nên chưa có gì để sửa.'
              : 'Tiến độ của đơn hàng này đã được chốt nên không thể sửa nữa.'}
          </p>
          <div className="form__actions">
            <Link to="/progress/$orderId" params={{ orderId }}>
              <Button variant="primary">Về chi tiết tiến độ</Button>
            </Link>
          </div>
        </Card>
      </div>
    )
  }

  // Dây chuyền đã ngừng không chọn lại được (server cũng từ chối, PRODUCTION_LINE_INACTIVE), nên chỉ
  // điền sẵn dây chuyền còn hoạt động; wizard báo dây chuyền bị bỏ để quản lý phân bổ lại.
  const lines = order.productionLines.filter((line) => line.status === 'Active')
  const inactiveLines = order.productionLines.filter((line) => line.status !== 'Active')
  const plannedByCell = new Map(
    matrix.items.map((cell) => [cellKey(cell.productionLineId, cell.productionDate), cell.plannedQuantity]),
  )

  // Ô không có dòng plan là ô bằng 0 (dây chuyền nghỉ ngày đó).
  const planMatrix: Record<string, string> = {}
  for (const line of lines) {
    for (const date of dateRange(order.startDate, order.dueDate)) {
      const key = cellKey(line.id, date)
      planMatrix[key] = String(plannedByCell.get(key) ?? 0)
    }
  }

  // Cách phân bổ không được lưu (BR-N16): phân bổ trùng với chia đều thì coi là chia đều.
  const shares = splitEvenly(order.quantity, lines.length)
  const isEven = lines.every((line, index) => line.allocatedQuantity === shares[index])

  const edit: ScheduleEdit = {
    order: { id: order.id, shoeCode: order.shoeCode, quantity: order.quantity },
    selectedLineIds: lines.map((line) => line.id),
    startDate: order.startDate,
    dueDate: order.dueDate,
    mode: isEven ? 'Even' : 'Manual',
    allocations: Object.fromEntries(lines.map((line) => [line.id, String(line.allocatedQuantity)])),
    matrix: planMatrix,
    inactiveLines: inactiveLines.map((line) => ({ code: line.code, allocatedQuantity: line.allocatedQuantity })),
  }

  return <CreateSchedulePage edit={edit} />
}
