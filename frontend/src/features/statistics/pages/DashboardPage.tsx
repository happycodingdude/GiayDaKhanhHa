import { Link, useNavigate } from '@tanstack/react-router'
import { Badge, Button, Card, ProgressBar, StatTile } from '../../../shared/components/ui'
import { ScheduleStatusBadge } from '../../../shared/components/StatusBadges'
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate } from '../../../shared/lib/date'
import { formatDifference, formatNumber, formatPercent } from '../../../shared/lib/format'
import { useDashboardStatistics } from '../hooks/useStatistics'

/**
 * "Nhìn → hiểu → hành động": số tổng và số liệu hôm nay trước, rồi tới các đơn hàng đang theo
 * dõi, và khép lại trang là những cảnh báo cần xử lý.
 */
export function DashboardPage() {
  const navigate = useNavigate()
  const query = useDashboardStatistics()

  if (query.isPending) {
    return (
      <div className="page">
        <LoadingState />
      </div>
    )
  }

  if (query.isError) {
    return (
      <div className="page">
        <ErrorState error={query.error} onRetry={() => void query.refetch()} title="Không tải được dashboard" />
      </div>
    )
  }

  const data = query.data
  const openOrder = (orderId: string) =>
    navigate({ to: '/orders/$orderId', params: { orderId: String(orderId) } })

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <h1 className="page__title">Dashboard</h1>
          <p className="page__subtitle">Tình hình sản xuất ngày {formatDate(data.date)}</p>
        </div>
        <Link to="/orders/new">
          <Button variant="primary">+ Tạo đơn hàng</Button>
        </Link>
      </header>

      {data.totalOrders === 0 ? (
        <Card>
          <EmptyState
            title="Chưa có đơn hàng"
            description="Tạo đơn hàng đầu tiên để bắt đầu theo dõi sản xuất."
            action={
              <Link to="/orders/new">
                <Button variant="primary">+ Tạo đơn hàng</Button>
              </Link>
            }
          />
        </Card>
      ) : (
        <>
          <div className="stats">
            <StatTile label="Đơn đang chạy" value={formatNumber(data.incompleteOrders)} />
            <StatTile
              label="Đang chậm"
              value={formatNumber(data.behindOrders)}
              tone={data.behindOrders > 0 ? 'danger' : 'neutral'}
            />
            <StatTile label="Hoàn thành" value={formatNumber(data.completedOrders)} tone="success" />
            <StatTile label="Đã hoàn thành" value={`${formatNumber(data.totalActualQuantity)} đôi`} />
            <StatTile label="Còn lại" value={`${formatNumber(data.totalRemainingQuantity)} đôi`} />
          </div>

          <Card title="Hôm nay">
            {data.today.plannedQuantity === 0 ? (
              <p className="muted">Hôm nay không có kế hoạch sản xuất.</p>
            ) : (
              <>
                <div className="stats">
                  <StatTile label="Cần sản xuất" value={`${formatNumber(data.today.plannedQuantity)} đôi`} />
                  <StatTile
                    label="Đã hoàn thành"
                    value={
                      data.today.hasAnyActualEntered ? `${formatNumber(data.today.actualQuantity)} đôi` : 'Chưa nhập'
                    }
                    tone={data.today.hasAnyActualEntered ? 'success' : 'neutral'}
                  />
                  <StatTile
                    label="Chênh lệch"
                    value={data.today.hasAnyActualEntered ? formatDifference(data.today.difference) : '—'}
                    tone={data.today.hasAnyActualEntered && data.today.difference < 0 ? 'danger' : 'neutral'}
                  />
                </div>

                <div className="summary-progress">
                  <ProgressBar
                    value={data.today.completionPercentage}
                    tone={data.today.difference < 0 ? 'warning' : 'success'}
                  />
                  <p className="summary-progress__caption">
                    {formatPercent(data.today.completionPercentage)} kế hoạch hôm nay
                  </p>
                </div>
              </>
            )}
          </Card>

          <Card title="Đơn hàng đang theo dõi">
            {data.trackedOrders.length === 0 ? (
              <EmptyState icon="✓" title="Tất cả đơn hàng đã hoàn thành" />
            ) : (
              <div className="table-wrapper">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Mã đơn</th>
                      <th>Tiến độ</th>
                      <th className="num">Hôm nay</th>
                      <th className="num">Còn lại</th>
                      <th>Tình trạng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.trackedOrders.map((order) => (
                      <tr
                        key={order.orderId}
                        className="table__row--clickable"
                        onClick={() => openOrder(order.orderId)}
                        tabIndex={0}
                        onKeyDown={(event) => event.key === 'Enter' && openOrder(order.orderId)}
                      >
                        <td className="table__strong">{order.orderCode}</td>
                        <td className="table__progress">
                          <span>{formatPercent(order.progressPercentage)}</span>
                          <ProgressBar
                            value={order.progressPercentage}
                            tone={order.scheduleStatus === 'Behind' ? 'danger' : 'info'}
                          />
                        </td>
                        <td className={`num ${(order.todayDifference ?? 0) < 0 ? 'danger' : ''}`}>
                          {order.todayHasPlan ? formatDifference(order.todayDifference) : <Badge tone="neutral">Không SX</Badge>}
                        </td>
                        <td className="num">{formatNumber(order.remaining)}</td>
                        <td>
                          <ScheduleStatusBadge
                            scheduleStatus={order.scheduleStatus}
                            behindQuantity={order.behindQuantity}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          {data.alerts.length > 0 && (
            <Card title="⚠ Cần xử lý">
              <ul className="alerts">
                {data.alerts.map((alert) => (
                  <li key={alert.orderId} className="alert">
                    <div>
                      <p className="alert__title">🔴 {alert.orderCode}</p>
                      <p className="alert__text">
                        Thiếu {formatNumber(alert.behindQuantity)} đôi so với kế hoạch
                      </p>
                      <p className="alert__meta">
                        {alert.isOverdue
                          ? `Đã quá hạn (${formatDate(alert.dueDate)})`
                          : `Còn ${alert.daysRemaining} ngày đến hạn`}
                      </p>
                    </div>
                    <Button variant="primary" onClick={() => openOrder(alert.orderId)}>
                      Xử lý thiếu
                    </Button>
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </>
      )}
    </div>
  )
}
