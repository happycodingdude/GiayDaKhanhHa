import { Link, useNavigate } from '@tanstack/react-router'
import { Button, Card, StatTile } from '../../../shared/components/ui'
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate } from '../../../shared/lib/date'
import { formatNumber } from '../../../shared/lib/format'
import { TrackedOrders } from '../components/TrackedOrders'
import { useDashboardStatistics } from '../hooks/useStatistics'

/**
 * Dashboard gồm đúng hai khối: số tổng toàn hệ thống, rồi timeline các đơn hàng đang theo dõi.
 * Mọi thao tác lên một ngày sản xuất đều nằm ở màn hình chi tiết đơn hàng, nên dashboard chỉ để
 * nhìn — không phải nơi bắt đầu một hành động.
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
        <ErrorState
          error={query.error}
          onRetry={() => void query.refetch()}
          title="Không tải được dashboard"
        />
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
          {/* Tổng quan */}
          <div className="stats">
            <StatTile label="Đơn đang chạy" value={formatNumber(data.incompleteOrders)} />
            <StatTile
              label="Đang chậm"
              value={formatNumber(data.behindOrders)}
              tone={data.behindOrders > 0 ? 'danger' : 'neutral'}
            />
            <StatTile label="Hoàn thành" value={formatNumber(data.completedOrders)} tone="success" />
            <StatTile
              label="Đã hoàn thành"
              value={`${formatNumber(data.totalActualQuantity)} đôi`}
              hint="Gồm cả sản lượng tạm tính của ngày chưa xuất hàng"
            />
            <StatTile label="Còn lại" value={`${formatNumber(data.totalRemainingQuantity)} đôi`} />
          </div>

          {/* Timeline đơn hàng */}
          <TrackedOrders orders={data.trackedOrders} today={data.date} onOpenOrder={openOrder} />
        </>
      )}
    </div>
  )
}
