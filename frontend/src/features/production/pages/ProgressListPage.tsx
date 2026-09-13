import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { Select } from '../../../shared/components/Select'
import { Badge, Button, Card, ProgressBar } from '../../../shared/components/ui'
import { ScheduleStatusBadge } from '../../../shared/components/StatusBadges'
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/QueryState'
import { formatDate } from '../../../shared/lib/date'
import { formatNumber, formatPercent } from '../../../shared/lib/format'
import { OrderStatusBadge } from '../../orders/components/OrderStatusBadge'
import { OrderThumbnail } from '../../orders/components/OrderThumbnail'
import { ProductionLineTags } from '../../orders/components/ProductionLineTags'
import { useOrders } from '../../orders/hooks/useOrders'
import { useProductionLines } from '../../production-lines/hooks/useProductionLines'

/**
 * `Scheduled` là bộ lọc mặc định: màn này chỉ theo dõi đơn ĐÃ có tiến độ. Đơn `Pending` thuộc màn
 * Nhập hàng (CR-001 §7.7).
 */
const STATUS_FILTERS = [
  { value: 'Scheduled', label: 'Tất cả' },
  { value: 'Incomplete', label: 'Chưa hoàn thành' },
  { value: 'Completed', label: 'Hoàn thành' },
]

const PAGE_SIZES = [10, 20, 50]

/**
 * Tiến độ — danh sách (CR-001 §7.7).
 *
 * Giữ nguyên nguyên tắc đã chốt: "Chậm" KHÔNG phải trạng thái đơn hàng, nó là một cột riêng tách
 * khỏi Chưa hoàn thành / Hoàn thành (order list spec §5).
 */
export function ProgressListPage() {
  const navigate = useNavigate()

  const [status, setStatus] = useState('Scheduled')
  const [productionLineId, setProductionLineId] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)

  const query = useOrders({
    status,
    search,
    productionLineId: productionLineId || undefined,
    page,
    pageSize,
  })
  const linesQuery = useProductionLines()

  const result = query.data
  const hasFilters = search !== '' || status !== 'Scheduled' || productionLineId !== ''
  const totalPages = Math.max(result?.totalPages ?? 1, 1)

  const openOrder = (orderId: string) => navigate({ to: '/progress/$orderId', params: { orderId } })

  return (
    <div className="page page--fill">
      <header className="page__header">
        <div>
          <h1 className="page__title">Tiến độ</h1>
          <p className="page__subtitle">Theo dõi sản xuất theo mã giày và dây chuyền</p>
        </div>
        <Link to="/progress/new">
          <Button variant="primary">+ Lập tiến độ</Button>
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
                onClick={() => {
                  setStatus(filter.value)
                  setPage(1)
                }}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <div className="pagination__size">
            Dây chuyền
            <Select
              value={productionLineId}
              options={[
                { value: '', label: 'Tất cả' },
                ...(linesQuery.data?.items ?? []).map((line) => ({ value: line.id, label: line.code })),
              ]}
              onChange={(next) => {
                setProductionLineId(next)
                setPage(1)
              }}
              aria-label="Lọc theo dây chuyền"
            />
          </div>

          <form
            className="search"
            onSubmit={(event) => {
              event.preventDefault()
              setSearch(searchInput.trim())
              setPage(1)
            }}
          >
            <input
              className="input search__input"
              placeholder="🔍 Tìm mã giày…"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              aria-label="Tìm mã giày"
            />
            <Button type="submit">Tìm</Button>
          </form>
        </div>

        {query.isPending && <LoadingState />}
        {query.isError && (
          <ErrorState
            error={query.error}
            onRetry={() => void query.refetch()}
            title="Không tải được danh sách tiến độ"
          />
        )}

        {result && result.items.length === 0 &&
          (hasFilters ? (
            <EmptyState
              icon="🔍"
              title="Không tìm thấy tiến độ phù hợp"
              description="Thử đổi bộ lọc trạng thái, dây chuyền hoặc từ khoá tìm kiếm."
            />
          ) : (
            <EmptyState
              title="Chưa có tiến độ nào"
              description="Lập tiến độ cho một đơn hàng đã nhập để bắt đầu theo dõi sản xuất."
              action={
                <Link to="/progress/new">
                  <Button variant="primary">+ Lập tiến độ</Button>
                </Link>
              }
            />
          ))}

        {result && result.items.length > 0 && (
          <div className="table-wrapper table-wrapper--fill">
            <table className="table">
              <thead>
                <tr>
                  <th>Ảnh</th>
                  <th>Mã giày</th>
                  <th>Dây chuyền</th>
                  <th className="num">Tổng SL</th>
                  <th className="num">Đã làm</th>
                  <th>Ngày bắt đầu</th>
                  <th>Ngày kết thúc</th>
                  <th>Tiến độ</th>
                  <th>Trạng thái</th>
                  <th>Tình trạng</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((order) => (
                  <tr
                    key={order.id}
                    className="table__row--clickable"
                    onClick={() => openOrder(order.id)}
                    tabIndex={0}
                    onKeyDown={(event) => event.key === 'Enter' && openOrder(order.id)}
                  >
                    <td>
                      <OrderThumbnail imageUrl={order.imageUrl} shoeCode={order.shoeCode} />
                    </td>
                    <td className="table__strong">{order.shoeCode}</td>
                    <td>
                      <ProductionLineTags lines={order.productionLines} />
                    </td>
                    <td className="num">{formatNumber(order.quantity)}</td>
                    <td className="num">{formatNumber(order.totalActual)}</td>
                    <td>{order.startDate ? formatDate(order.startDate) : '—'}</td>
                    <td>
                      {order.dueDate ? formatDate(order.dueDate) : '—'}
                      {order.isOverdue && (
                        <>
                          {' '}
                          <Badge tone="danger">Quá hạn</Badge>
                        </>
                      )}
                      {order.hasUnclosedPastCell && (
                        <span className="table__sub">
                          <Badge tone="warning">Có ngày chưa xuất hàng</Badge>
                        </span>
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
              <div className="pagination__size">
                Số dòng
                <Select
                  value={pageSize}
                  options={PAGE_SIZES.map((size) => ({ value: size, label: String(size) }))}
                  onChange={(next) => {
                    setPageSize(next)
                    setPage(1)
                  }}
                  aria-label="Số dòng mỗi trang"
                />
              </div>

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
