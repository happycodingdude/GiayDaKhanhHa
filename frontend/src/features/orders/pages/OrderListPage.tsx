import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Badge, Button, Card, ProgressBar } from '../../../shared/components/ui'
import { OrderStatusBadge, ScheduleStatusBadge } from '../../../shared/components/StatusBadges'
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate } from '../../../shared/lib/date'
import { formatNumber, formatPercent } from '../../../shared/lib/format'
import { useOrders } from '../hooks/useOrders'

const STATUS_FILTERS = [
  { value: 'All', label: 'Tất cả' },
  { value: 'Incomplete', label: 'Chưa hoàn thành' },
  { value: 'Completed', label: 'Hoàn thành' },
]

const PAGE_SIZES = [10, 20, 50]

export function OrderListPage() {
  const navigate = useNavigate()

  // Filters, search and pagination are local UI state (Step 5 §8).
  const [status, setStatus] = useState('All')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)

  const query = useOrders({ status, search, page, pageSize })

  const applySearch = (value: string) => {
    setSearch(value.trim())
    setPage(1)
  }

  const changeStatus = (value: string) => {
    setStatus(value)
    setPage(1)
  }

  const changePageSize = (value: number) => {
    setPageSize(value)
    setPage(1)
  }

  const result = query.data
  const hasFilters = search !== '' || status !== 'All'
  const totalPages = Math.max(result?.totalPages ?? 1, 1)

  const openOrder = (orderId: string) =>
    navigate({ to: '/orders/$orderId', params: { orderId: String(orderId) } })

  return (
    <div className="page page--fill">
      <header className="page__header">
        <div>
          <h1 className="page__title">Đơn hàng</h1>
          <p className="page__subtitle">Quản lý các đơn hàng sản xuất</p>
        </div>
        <Link to="/orders/new">
          <Button variant="primary">+ Tạo đơn hàng</Button>
        </Link>
      </header>

      <Card>
        <div className="toolbar">
          <div className="segmented" role="group" aria-label="Lọc theo trạng thái">
            {STATUS_FILTERS.map((filter) => (
              <button
                key={filter.value}
                type="button"
                className={`segmented__item ${status === filter.value ? 'segmented__item--active' : ''}`}
                onClick={() => changeStatus(filter.value)}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <form
            className="search"
            onSubmit={(event) => {
              event.preventDefault()
              applySearch(searchInput)
            }}
          >
            <input
              className="input search__input"
              placeholder="🔍 Tìm mã đơn hàng…"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              aria-label="Tìm mã đơn hàng"
            />
            <Button type="submit">Tìm</Button>
            {search && (
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  setSearchInput('')
                  applySearch('')
                }}
              >
                Xoá
              </Button>
            )}
          </form>
        </div>

        {query.isPending && <LoadingState />}
        {query.isError && (
          <ErrorState
            error={query.error}
            onRetry={() => void query.refetch()}
            title="Không tải được danh sách đơn hàng"
          />
        )}

        {result && result.items.length === 0 &&
          (hasFilters ? (
            <EmptyState
              icon="🔍"
              title="Không tìm thấy đơn hàng phù hợp"
              description="Thử đổi bộ lọc trạng thái hoặc từ khoá tìm kiếm."
            />
          ) : (
            <EmptyState
              title="Chưa có đơn hàng"
              description="Tạo đơn hàng đầu tiên để bắt đầu theo dõi sản xuất."
              action={
                <Link to="/orders/new">
                  <Button variant="primary">+ Tạo đơn hàng</Button>
                </Link>
              }
            />
          ))}

        {result && result.items.length > 0 && (
          // The table takes the leftover height and scrolls on its own, so the pagination
          // footer below stays visible without the window ever scrolling.
          <div className="table-wrapper table-wrapper--fill">
            <table className="table">
              <thead>
                <tr>
                  <th>Mã đơn</th>
                  <th className="num">Tổng SL</th>
                  <th className="num">Đã hoàn thành</th>
                  <th className="num">Còn lại</th>
                  <th>Hạn hoàn thành</th>
                  <th>Tiến độ</th>
                  <th>Trạng thái</th>
                  <th>Tình trạng</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((order) => (
                  // The whole row is clickable so opening an order takes one action.
                  <tr
                    key={order.id}
                    className="table__row--clickable"
                    onClick={() => openOrder(order.id)}
                    tabIndex={0}
                    onKeyDown={(event) => event.key === 'Enter' && openOrder(order.id)}
                  >
                    <td className="table__strong">{order.orderCode}</td>
                    <td className="num">{formatNumber(order.quantity)}</td>
                    <td className="num">{formatNumber(order.totalActual)}</td>
                    <td className="num">{formatNumber(order.remaining)}</td>
                    <td>
                      {formatDate(order.dueDate)}
                      {order.isOverdue && (
                        <>
                          {' '}
                          <Badge tone="danger">Quá hạn</Badge>
                        </>
                      )}
                    </td>
                    <td className="table__progress">
                      <span>{formatPercent(order.progressPercentage)}</span>
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
                    </td>
                    <td>
                      <OrderStatusBadge status={order.status} />
                    </td>
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

        {result && result.totalCount > 0 && (
          <div className="pagination">
            <span className="pagination__info">
              {formatNumber(result.totalCount)} đơn hàng · hiển thị{' '}
              {formatNumber((result.page - 1) * result.pageSize + 1)}–
              {formatNumber(Math.min(result.page * result.pageSize, result.totalCount))}
            </span>

            <div className="pagination__controls">
              <label className="pagination__size">
                Số dòng
                <select
                  className="select"
                  value={pageSize}
                  onChange={(event) => changePageSize(Number(event.target.value))}
                  aria-label="Số dòng mỗi trang"
                >
                  {PAGE_SIZES.map((size) => (
                    <option key={size} value={size}>
                      {size}
                    </option>
                  ))}
                </select>
              </label>

              <Button disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                ← Trước
              </Button>
              <span className="pagination__page">
                Trang {result.page} / {totalPages}
              </span>
              <Button disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
                Sau →
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  )
}
